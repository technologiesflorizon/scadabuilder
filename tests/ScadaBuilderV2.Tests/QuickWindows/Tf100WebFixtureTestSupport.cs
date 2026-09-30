using System.Diagnostics;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Shared plumbing for the frozen-fixture tests that also compare their artifact byte-for-byte
/// against a vendored copy in the sibling TF100Web repository
/// (<see cref="QuickWindowRuntimeHandshakeFixtureTests"/>,
/// <see cref="QuickWindowParameterSheetAnimationFixtureTests"/>): finding this repository's root and
/// its <c>tests/conformance</c> directory, and resolving the TF100Web checkout or worktree that
/// actually carries the quick-window branch.
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
    /// <summary>The TF100Web branch that carries the quick-window work, and therefore the vendored copy.</summary>
    internal const string ExpectedTf100Branch = "codex/quick-window-v1";

    private static readonly char[] LineSeparators = ['\r', '\n'];

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
    /// Resolves the TF100Web checkout or worktree that actually carries the quick-window branch.
    /// </summary>
    /// <remarks>
    /// The main checkout is routinely on another site, and it was on 2026-09-02: comparing against it made
    /// this test permanently inconclusive, so the one assertion that keeps both repositories on the same
    /// bytes never ran. `git worktree list` answers the real question — where is this branch checked out —
    /// and returning null only when git or the repository is absent keeps the skip for a machine that has
    /// no TF100Web at all, not for a machine whose checkout merely wandered off.
    ///
    /// Mirrors Resolve-WorktreeForBranch in tools/quick-window/record-checkpoint.ps1.
    /// </remarks>
    internal static string? ResolveTf100WebWorktree()
    {
        var root = Environment.GetEnvironmentVariable("TF100WEB_ROOT") ?? @"F:\Projet\Git\TF100Web";
        if (!Directory.Exists(root)) return null;

        string output;
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                ArgumentList = { "-C", root, "worktree", "list", "--porcelain" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (process is null) return null;
            output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30_000);
            if (process.ExitCode != 0) return null;
        }
        catch (Exception)
        {
            // No git on PATH, or no repository here. Either way there is nothing to compare against.
            return null;
        }

        string? candidate = null;
        foreach (var line in output.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("worktree ", StringComparison.Ordinal))
            {
                candidate = trimmed[9..].Trim();
            }
            else if (trimmed.StartsWith("branch ", StringComparison.Ordinal) && candidate is not null)
            {
                var branch = trimmed[7..].Trim();
                if (branch.StartsWith("refs/heads/", StringComparison.Ordinal)) branch = branch[11..];
                if (string.Equals(branch, ExpectedTf100Branch, StringComparison.Ordinal)) return candidate;
            }
        }

        Assert.Fail($"TF100Web exists at {root} but no checkout or worktree carries {ExpectedTf100Branch}; "
            + "the vendored fixture cannot be compared against the wrong branch.");
        return null;
    }
}
