using System.Text.Json;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// Validates the WebView2 harness output for FR-020 isolation gate (Phase 0 Task 0.2).
/// Reads artifacts/quick-window-isolation/builder-webview2.json produced by
/// tools/QuickWindowIsolationPrototype.App and asserts PASS, 100% core assertions,
/// single poller, zero counters after dispose, no id/CSS leaks.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050 (FR-020).
/// Contracts: docs/superpowers/plans/2026-08-10-parameterized-quick-window-management.md#task-02
/// </remarks>
[TestClass]
public sealed class QuickWindowIsolationPrototypeContractTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ScadaBuilderV2.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("ScadaBuilderV2.sln not found");
    }

    private static JsonDocument LoadEvidence()
    {
        var repoRoot = FindRepoRoot();
        var evidencePath = Path.Combine(repoRoot, "artifacts", "quick-window-isolation", "builder-webview2.json");
        if (!File.Exists(evidencePath))
            Assert.Fail($"Missing WebView2 evidence at {evidencePath}. Run: dotnet run --project tools/QuickWindowIsolationPrototype.App --configuration Release -- --output artifacts/quick-window-isolation/builder-webview2.json");
        var json = File.ReadAllText(evidencePath);
        return JsonDocument.Parse(json);
    }

    [TestMethod]
    public void WebView2EvidenceExistsAndIsPass()
    {
        using var doc = LoadEvidence();
        var root = doc.RootElement;
        Assert.AreEqual("1.0.0", root.GetProperty("schemaVersion").GetString());
        Assert.AreEqual("1.0.2", root.GetProperty("prototypeRevision").GetString());
        var hash = root.GetProperty("prototypeHash").GetString();
        Assert.IsNotNull(hash);
        Assert.AreEqual(64, hash!.Length, "prototypeHash must be SHA-256 hex");
        Assert.IsTrue(hash.All(c => "0123456789abcdef".Contains(c)), "hash must be lower hex");

        var overall = root.GetProperty("overall").GetString();
        Assert.AreEqual("PASS", overall, $"Gate must be PASS, got {overall}. Check assertions for FAIL details.");
    }

    [TestMethod]
    public void WebView2CoreAssertionsAre100PercentPass()
    {
        using var doc = LoadEvidence();
        var assertions = doc.RootElement.GetProperty("assertions").GetProperty("core").EnumerateArray().ToArray();
        Assert.IsTrue(assertions.Length >= 20, $"Expected >=20 core assertions, got {assertions.Length}");
        var failures = assertions.Where(a => a.GetProperty("status").GetString() != "PASS").Select(a => a.GetProperty("id").GetString()).ToArray();
        Assert.AreEqual(0, failures.Length, $"All core assertions must PASS, failed: {string.Join(", ", failures)}");
    }

    [TestMethod]
    public void WebView2NoDomOrCssLeak()
    {
        using var doc = LoadEvidence();
        var ids = doc.RootElement.GetProperty("assertions").GetProperty("core").EnumerateArray()
            .Where(a => a.GetProperty("id").GetString()!.Contains("ids.") || a.GetProperty("id").GetString()!.Contains("classes.") || a.GetProperty("id").GetString()!.Contains("keyframes") || a.GetProperty("id").GetString()!.Contains("selectors"))
            .ToArray();
        Assert.IsTrue(ids.All(a => a.GetProperty("status").GetString() == "PASS"), "DOM/CSS sentinels must be isolated");
    }

    [TestMethod]
    public void WebView2SinglePollerAndCountersZeroAfterDispose()
    {
        using var doc = LoadEvidence();
        var metrics = doc.RootElement.GetProperty("metrics");
        Assert.AreEqual(1, metrics.GetProperty("pollerCount").GetInt32(), "Exactly one poller/cache/bridge");
        // core lifecycle counters zero is checked via assertions, but also verify metrics
        var core = doc.RootElement.GetProperty("assertions").GetProperty("core").EnumerateArray();
        var poller = core.FirstOrDefault(a => a.GetProperty("id").GetString() == "core.lifecycle.poller-unique");
        Assert.AreEqual("PASS", poller.GetProperty("status").GetString());
        var counters = core.FirstOrDefault(a => a.GetProperty("id").GetString() == "core.lifecycle.counters-zero-after-dispose");
        Assert.AreEqual("PASS", counters.GetProperty("status").GetString());
    }

    [TestMethod]
    public void WebView2VersionsAreCaptured()
    {
        using var doc = LoadEvidence();
        var versions = doc.RootElement.GetProperty("versions");
        Assert.AreEqual("1.0.3967.48", versions.GetProperty("webView2Sdk").GetString(), "SDK version must be pinned 1.0.3967.48");
        var runtime = versions.GetProperty("webView2Runtime").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(runtime), "BrowserVersionString must be captured");
        Assert.IsFalse(runtime!.Contains("simulated", StringComparison.OrdinalIgnoreCase), "Phase 0 requires the actual CoreWebView2 BrowserVersionString.");

        var capturePath = doc.RootElement.GetProperty("capturePath").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(capturePath), "The real WebView2 run must capture a PNG.");
        Assert.IsTrue(File.Exists(capturePath), $"Missing WebView2 capture: {capturePath}");
    }

    [TestMethod]
    public void WebView2PrototypeHashMatchesCurrentFiles()
    {
        using var doc = LoadEvidence();
        var reportedHash = doc.RootElement.GetProperty("prototypeHash").GetString()!;
        var repoRoot = FindRepoRoot();
        var prototypeDir = Path.Combine(repoRoot, "tools", "prototypes", "quick-window-dom-css-isolation");
        var files = new[] { "prototype.js", "prototype.css", "index.html", "assertions.js", "evidence.schema.json", "README.md" }
            .Select(f => Path.Combine(prototypeDir, f))
            .Where(File.Exists)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();
        using var sha = System.Security.Cryptography.SHA256.Create();
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var computed = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        Assert.AreEqual(computed, reportedHash, "Evidence prototypeHash must match current prototype files; if you modified the prototype, increment PrototypeRevision and re-run harness");
    }

    [TestMethod]
    public void WebView2PerformanceSlaMet()
    {
        using var doc = LoadEvidence();
        var metrics = doc.RootElement.GetProperty("metrics");
        var p95Hot = metrics.GetProperty("p95HotMs").GetDouble();
        var p95Cold = metrics.GetProperty("p95ColdMs").GetDouble();
        Assert.IsTrue(p95Hot <= 500, $"p95 hot {p95Hot} must be <=500ms");
        Assert.IsTrue(p95Cold <= 1500, $"p95 cold {p95Cold} must be <=1500ms");
    }
}
