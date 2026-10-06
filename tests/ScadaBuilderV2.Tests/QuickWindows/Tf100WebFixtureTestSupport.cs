using System.Diagnostics;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Shared plumbing for the frozen-fixture tests that also compare their artifact byte-for-byte
/// against a vendored copy in the sibling TF100Web repository
/// (<see cref="QuickWindowRuntimeHandshakeFixtureTests"/>,
/// <see cref="QuickWindowParameterSheetAnimationFixtureTests"/>): finding this repository's root and
/// its <c>tests/conformance</c> directory, and reading TF100Web's vendored copy at a git ref.
/// </summary>
/// <remarks>
/// Fix round 1 of Task 13's review: this was two copies of the same three methods, one per fixture
/// test class, which could silently drift from each other. Extracted to one implementation. Also
/// replaces the fallback root's <c>Path.Combine("F:", "Projet", "Git", "TF100Web")</c> with a plain
/// rooted literal: a drive-only first segment (<c>"F:"</c>, no trailing separator) is exactly the
/// shape .NET's drive-relative-path handling exists to warn about, so spelling the absolute path out
/// removes any doubt about what gets combined, instead of relying on <c>Path.Combine</c> to insert
/// the separator after a bare drive letter.
/// </remarks>
internal static class Tf100WebFixtureTestSupport
{
    /// <summary>Finds this repository's root by walking up from the test assembly to <c>ScadaBuilderV2.sln</c>.</summary>
    internal static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ScadaBuilderV2.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        Assert.Fail("Unable to locate the repository root.");
        return string.Empty;
    }

    /// <summary>This repository's <c>tests/conformance</c> directory, where every frozen fixture lives.</summary>
    internal static string FindConformanceDirectory() =>
        Path.Combine(FindRepositoryRoot(), "tests", "conformance");

    /// <summary>
    /// Reads the vendored copy of <paramref name="fileName"/> from TF100Web's <c>frontend/test_fixtures/</c> at a git
    /// ref, or returns <see langword="null"/> when no TF100Web repository (or no git) exists on this machine.
    /// </summary>
    /// <remarks>
    /// Fix round 2 (2026-10-06): this used to require a checkout or worktree on <c>codex/quick-window-v1</c> and failed
    /// as soon as none existed — but that branch is merged into TF100Web <c>main</c>, so a correct state made both
    /// vendored-fixture tests fail. The bytes are now read with <c>git show &lt;ref&gt;:frontend/test_fixtures/&lt;file&gt;</c>,
    /// independent of whatever branch any TF100Web checkout happens to be on.
    ///
    /// Ref resolution: <c>TF100WEB_FIXTURE_REF</c> when set; otherwise <c>origin/main</c> when it exists, because the
    /// user's local <c>main</c> branch is routinely stale (it was at <c>5c7eea0</c> while <c>origin/main</c> carried the
    /// merged quick-window work at <c>e53c596</c>); otherwise <c>main</c>. No fetch is performed: the test never
    /// touches the network, so <c>origin/main</c> is as fresh as the last fetch. A ref that exists but carries no such
    /// file is a failure (the vendored copy was deleted or renamed), never a skip.
    ///
    /// <c>TF100WEB_ROOT</c> overrides the repository path (default <c>F:\Projet\Git\TF100Web</c>; any worktree of
    /// the same repository works, they share refs).
    /// </remarks>
    internal static byte[]? ReadVendoredFixture(string fileName, out string source)
    {
        var root = Environment.GetEnvironmentVariable("TF100WEB_ROOT") ?? @"F:\Projet\Git\TF100Web";
        source = root;
        if (!Directory.Exists(root)) return null;

        var configuredRef = Environment.GetEnvironmentVariable("TF100WEB_FIXTURE_REF");
        string gitRef;
        if (!string.IsNullOrWhiteSpace(configuredRef))
        {
            gitRef = configuredRef.Trim();
        }
        else
        {
            var originMain = RunGit(root, "rev-parse", "--verify", "--quiet", "origin/main^{commit}");
            if (originMain is null)
            {
                // No git on PATH, or not a repository: nothing to compare against.
                if (RunGit(root, "rev-parse", "--git-dir") is null) return null;
                gitRef = "main";
            }
            else
            {
                gitRef = originMain.ExitCode == 0 ? "origin/main" : "main";
            }
        }

        var path = $"frontend/test_fixtures/{fileName}";
        source = $"{root} @ {gitRef}:{path}";
        var shown = RunGit(root, "show", $"{gitRef}:{path}");
        if (shown is null) return null;
        if (shown.ExitCode != 0)
        {
            Assert.Fail($"TF100Web at {root} has no {path} at ref '{gitRef}' (set TF100WEB_FIXTURE_REF to compare another ref): {shown.Error.Trim()}");
        }

        return shown.Output;
    }

    private sealed record GitResult(int ExitCode, byte[] Output, string Error);

    /// <summary>Runs git and captures stdout as raw bytes (a fixture is binary), or returns null when git cannot start.</summary>
    private static GitResult? RunGit(string root, params string[] arguments)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = "git",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            info.ArgumentList.Add("-C");
            info.ArgumentList.Add(root);
            foreach (var argument in arguments) info.ArgumentList.Add(argument);

            using var process = Process.Start(info);
            if (process is null) return null;
            var errorTask = process.StandardError.ReadToEndAsync();
            using var buffer = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(buffer);
            if (!process.WaitForExit(60_000))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            return new GitResult(process.ExitCode, buffer.ToArray(), errorTask.Result);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
