using System.Security.Cryptography;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

/// <summary>
/// Remembers the deserialized content of scene files so a workspace snapshot does not re-read and
/// re-deserialize a scene file that has not changed on disk.
/// </summary>
/// <remarks>
/// An entry is keyed by the scene file's fully resolved path and is served without touching the file only
/// while the file's last-write time (UTC) and length are exactly those observed when the entry was read.
/// Any difference forces a re-read; the re-read bytes are then compared by SHA-256 with the cached
/// content, and only byte-identical content keeps the cached scene (a save that rewrites unchanged
/// files therefore costs a read and a hash, never a deserialization). <see cref="Invalidate"/> demotes
/// an entry so its next use always re-reads and re-hashes, which closes the gap of a write landing within
/// the file system's timestamp resolution. A missing file always drops its entry.
///
/// A read that races a write cannot store a stale entry: every invalidation, forget or scope change bumps a
/// per-path generation (or the global epoch), the reader captures both before probing the file, and its result
/// is stored only if neither moved in the meantime. The cache assumes the application is the only writer of the
/// open project; a file rewritten by another process with the same last-write time and length is not detected.
///
/// The cached value is the raw scene exactly as deserialized from the file (after the converted legacy
/// text-override cleanup), i.e. a pure function of the file bytes; project-dependent migration is applied
/// by the caller on every read, so a cached scene never carries a stale project projection.
/// <see cref="ScadaScene"/> and its whole object graph are immutable records exposed through read-only
/// collection interfaces, so one instance may be shared by several snapshots.
///
/// Decisions: DEC-0049.
/// Contracts: docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/ModernProjectSceneCacheTests.cs.
/// </remarks>
public sealed class SceneFileCache
{
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> generations = new(StringComparer.OrdinalIgnoreCase);
    private long epoch;
    private int fileReadCount;
    private int deserializationCount;

    /// <summary>Gets how many scene files were read from disk through this cache since its creation.</summary>
    public int FileReadCount => Volatile.Read(ref fileReadCount);

    /// <summary>Gets how many scene files were deserialized (cache misses on content) since its creation.</summary>
    public int DeserializationCount => Volatile.Read(ref deserializationCount);

    /// <summary>Gets the number of cached scene files.</summary>
    public int Count
    {
        get
        {
            lock (gate) return entries.Count;
        }
    }

    /// <summary>
    /// Returns the scene stored in <paramref name="path"/>, from cache when the file is unchanged, otherwise by
    /// reading the file and calling <paramref name="deserialize"/> on its bytes. Returns <c>null</c> when the
    /// file does not exist (and forgets any entry for it) or when <paramref name="deserialize"/> returns null.
    /// </summary>
    /// <param name="path">Fully resolved scene file path.</param>
    /// <param name="deserialize">Pure function turning the file bytes into a scene; may throw to refuse the content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ScadaScene?> GetOrReadAsync(
        string path,
        Func<byte[], CancellationToken, Task<ScadaScene?>> deserialize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(deserialize);
        path = Path.GetFullPath(path);

        // The generation is captured before the probe and the stamp before the bytes: a write racing this read
        // either makes the stored stamp older than the content (a harmless re-read next time) or bumps the
        // generation through its invalidation, in which case nothing is stored.
        var version = CaptureVersion(path);
        var stamp = Probe(path);
        Entry? existing;
        lock (gate)
        {
            if (stamp is null)
            {
                entries.Remove(path);
                return null;
            }

            entries.TryGetValue(path, out existing);
            if (existing?.Stamp is { } cachedStamp && cachedStamp == stamp.Value)
            {
                return existing.Scene;
            }
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            Forget(path);
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            Forget(path);
            return null;
        }

        Interlocked.Increment(ref fileReadCount);
        var hash = SHA256.HashData(bytes);
        if (existing is not null && CryptographicOperations.FixedTimeEquals(existing.ContentHash, hash))
        {
            lock (gate)
            {
                if (IsCurrent(path, version)) entries[path] = existing with { Stamp = stamp };
            }

            return existing.Scene;
        }

        Interlocked.Increment(ref deserializationCount);
        var scene = await deserialize(bytes, cancellationToken);
        lock (gate)
        {
            if (!IsCurrent(path, version))
            {
                // Invalidated, forgotten or re-scoped while this read was in flight: what was read may already be
                // stale, so it is returned to this caller but never cached.
            }
            else if (scene is null)
            {
                entries.Remove(path);
            }
            else
            {
                entries[path] = new Entry(stamp, hash, scene);
            }
        }

        return scene;
    }

    /// <summary>
    /// Demotes the entry of one scene file so its next use re-reads the file and keeps the cached scene only
    /// for byte-identical content. Call it after any write to that file.
    /// </summary>
    public void Invalidate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = Path.GetFullPath(path);
        lock (gate)
        {
            Bump(path);
            if (entries.TryGetValue(path, out var entry)) entries[path] = entry with { Stamp = null };
        }
    }

    /// <summary>Demotes every entry located under <paramref name="root"/> (see <see cref="Invalidate"/>).</summary>
    public void InvalidateUnder(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        lock (gate)
        {
            // Reads in flight under this root may target files not cached yet: the epoch stops all of them.
            epoch++;
            foreach (var path in entries.Keys.Where(path => IsUnder(path, root)).ToArray())
            {
                entries[path] = entries[path] with { Stamp = null };
            }
        }
    }

    /// <summary>Forgets one scene file entirely, e.g. once the file is deleted.</summary>
    public void Forget(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = Path.GetFullPath(path);
        lock (gate)
        {
            Bump(path);
            entries.Remove(path);
        }
    }

    /// <summary>Forgets every entry located under <paramref name="root"/>, e.g. after a refused project open.</summary>
    public void ForgetUnder(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        lock (gate)
        {
            epoch++;
            foreach (var path in entries.Keys.Where(path => IsUnder(path, root)).ToArray())
            {
                entries.Remove(path);
            }
        }
    }

    /// <summary>
    /// Forgets every entry that does not belong to <paramref name="root"/>; a null root forgets everything.
    /// Used when the active project changes or closes.
    /// </summary>
    public void RetainOnly(string? root)
    {
        lock (gate)
        {
            // A read of the previous project still in flight must not insert a late entry after the switch.
            epoch++;
            if (root is null)
            {
                entries.Clear();
                generations.Clear();
                return;
            }

            foreach (var path in entries.Keys.Where(path => !IsUnder(path, root)).ToArray())
            {
                entries.Remove(path);
            }
        }
    }

    private (long Epoch, long Generation) CaptureVersion(string path)
    {
        lock (gate) return (epoch, generations.GetValueOrDefault(path));
    }

    // Must be called under the gate.
    private bool IsCurrent(string path, (long Epoch, long Generation) version) =>
        epoch == version.Epoch && generations.GetValueOrDefault(path) == version.Generation;

    // Must be called under the gate.
    private void Bump(string path) => generations[path] = generations.GetValueOrDefault(path) + 1;

    private static FileStamp? Probe(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? new FileStamp(info.LastWriteTimeUtc.Ticks, info.Length) : null;
    }

    private static bool IsUnder(string path, string root)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct FileStamp(long LastWriteUtcTicks, long Length);

    private sealed record Entry(FileStamp? Stamp, byte[] ContentHash, ScadaScene Scene);
}
