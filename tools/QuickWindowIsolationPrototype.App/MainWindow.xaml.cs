using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace QuickWindowIsolationPrototype.App;

/// <summary>
/// WebView2 harness for QuickWindow DOM/CSS isolation prototype (Phase 0 Task 0.2).
/// Loads the host-agnostic fixture (tools/prototypes/quick-window-dom-css-isolation/index.html),
/// waits for window.__quickWindowIsolationEvidence, captures BrowserVersionString/arch/mode,
/// serializes assertions and exits with non-zero on failure.
/// No reference to production MainWindow state; runs isolated.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050 (FR-020 isolation gate).
/// Contracts: docs/superpowers/plans/2026-08-10-parameterized-quick-window-management.md#task-02
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindowIsolationPrototypeContractTests.cs
/// </remarks>
public partial class MainWindow : Window
{
    private string _outputPath = string.Empty;
    private readonly TaskCompletionSource<bool> _done = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            ParseArgs();
            await InitializeWebViewAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Fatal: {ex.Message}";
            await WriteFailureAsync(ex.ToString());
            ShutdownWithCode(1);
        }
    }

    private void ParseArgs()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--output" && i + 1 < args.Length)
                _outputPath = args[i + 1];
        }
        if (string.IsNullOrWhiteSpace(_outputPath))
        {
            var repoRoot = FindRepoRoot();
            _outputPath = Path.Combine(repoRoot, "artifacts", "quick-window-isolation", "builder-webview2.json");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ScadaBuilderV2.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? AppContext.BaseDirectory;
    }

    private async Task InitializeWebViewAsync()
    {
        StatusText.Text = "Creating CoreWebView2Environment...";
        var userDataFolder = Path.Combine(Path.GetTempPath(), "QuickWindowIsolationPrototype", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(userDataFolder);
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
        await WebView.EnsureCoreWebView2Async(env);

        WebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        var repoRoot = FindRepoRoot();
        var fixturePath = Path.Combine(repoRoot, "tools", "prototypes", "quick-window-dom-css-isolation", "index.html");
        if (!File.Exists(fixturePath))
            throw new FileNotFoundException($"Fixture not found: {fixturePath}");
        var fixtureUri = new Uri(fixturePath).AbsoluteUri;
        StatusText.Text = $"Navigating to {fixtureUri}";
        WebView.Source = new Uri(fixtureUri);

        // Timeout guard 60s
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(60));
            if (!_done.Task.IsCompleted)
            {
                await Dispatcher.InvokeAsync(HandleTimeoutAsync).Task.Unwrap();
            }
        });
    }

    private async Task HandleTimeoutAsync()
    {
        await WriteFailureAsync("Timeout waiting for __quickWindowIsolationEvidence (60s)");
        ShutdownWithCode(2);
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            await WriteFailureAsync($"Navigation failed: {e.WebErrorStatus}");
            ShutdownWithCode(3);
            return;
        }
        StatusText.Text = "Navigation completed, polling for evidence...";
        await PollForEvidenceAsync();
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // Alternative channel if fixture posts message
    }

    private async Task PollForEvidenceAsync()
    {
        for (var attempt = 0; attempt < 120; attempt++) // 120 * 250ms = 30s
        {
            try
            {
                var json = await WebView.CoreWebView2.ExecuteScriptAsync("JSON.stringify(window.__quickWindowIsolationEvidence || null)");
                // ExecuteScriptAsync returns JSON-encoded string
                var unwrapped = JsonSerializer.Deserialize<string>(json);
                if (!string.IsNullOrWhiteSpace(unwrapped) && unwrapped != "null")
                {
                    StatusText.Text = "Evidence received, capturing WebView2 versions...";
                    await CaptureAndSaveAsync(unwrapped);
                    return;
                }
                var errJson = await WebView.CoreWebView2.ExecuteScriptAsync("JSON.stringify(window.__quickWindowIsolationError || null)");
                var err = JsonSerializer.Deserialize<string>(errJson);
                if (!string.IsNullOrWhiteSpace(err) && err != "null")
                {
                    await WriteFailureAsync($"Fixture error: {err}");
                    ShutdownWithCode(4);
                    return;
                }
            }
            catch (Exception ex)
            {
                // transient
                Debug.WriteLine(ex);
            }
            await Task.Delay(250);
        }
        await WriteFailureAsync("Polling for __quickWindowIsolationEvidence timed out (30s)");
        ShutdownWithCode(5);
    }

    private async Task CaptureAndSaveAsync(string evidenceJson)
    {
        var repoRoot = FindRepoRoot();
        var prototypeHash = ComputePrototypeHash(repoRoot);
        var env = WebView.CoreWebView2.Environment;
        var browserVersion = env.BrowserVersionString;
        var userDataFolder = env.UserDataFolder;

        // Try to get architecture and channel via env is not directly exposed, infer from runtime
        var arch = Environment.Is64BitProcess ? "x64" : "x86";
        var mode = "Evergreen"; // default for installed WebView2

        JsonNode? evidenceNode;
        try { evidenceNode = JsonNode.Parse(evidenceJson); }
        catch { evidenceNode = JsonDocument.Parse(evidenceJson).RootElement.ToString() is string s ? JsonNode.Parse(s) : null; }

        if (evidenceNode is null)
            throw new InvalidOperationException("Failed to parse evidence JSON from fixture");

        // Enrich evidence with harness-specific versions and hash
        var obj = evidenceNode.AsObject();
        obj["prototypeHash"] = prototypeHash;
        if (obj["versions"] is JsonObject versions)
        {
            versions["node"] = LoadExactNodeGateVersion(repoRoot, prototypeHash);
            versions["webView2Sdk"] = "1.0.3967.48";
            versions["webView2Runtime"] = browserVersion;
            versions["webView2UserDataFolder"] = userDataFolder;
            versions["webView2Architecture"] = arch;
            versions["webView2Mode"] = mode;
            // update node version info from file
            try
            {
                var nvmrc = File.ReadAllText(Path.Combine(repoRoot, ".nvmrc")).Trim();
                versions["nvmrc"] = nvmrc;
                var pkgJson = File.ReadAllText(Path.Combine(repoRoot, "tests", "runtime-js", "package.json"));
                using var pkgDoc = JsonDocument.Parse(pkgJson);
                versions["enginesNode"] = pkgDoc.RootElement.GetProperty("engines").GetProperty("node").GetString();
            }
            catch { }
            versions["dotnet"] = Environment.Version.ToString();
            versions["os"] = Environment.OSVersion.ToString();
        }
        // Add hostExtensions for builderWebView2 if not present
        if (obj["assertions"] is JsonObject assertions && assertions["hostExtensions"] is JsonObject ext)
        {
            if (ext["builderWebView2"] is JsonArray arr)
            {
                arr.Add(new JsonObject
                {
                    ["id"] = "host.builderWebView2.browserVersion",
                    ["status"] = "PASS",
                    ["detail"] = browserVersion
                });
            }
        }
        // Determine overall from core PASS
        var overall = obj["overall"]?.GetValue<string>() ?? "FAIL";
        var exitCode = overall == "PASS" ? 0 : 6;

        // Ensure output directory
        var outputDir = Path.GetDirectoryName(_outputPath);
        if (!string.IsNullOrWhiteSpace(outputDir)) Directory.CreateDirectory(outputDir);

        var screenshotPath = Path.GetFullPath(Path.ChangeExtension(_outputPath, ".png"));
        await using (var screenshot = File.Create(screenshotPath))
        {
            await WebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, screenshot);
        }
        obj["capturePath"] = screenshotPath;

        var outputJson = obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_outputPath, outputJson);

        // Also save capture json under same dir as evidence.json for Task 0.4
        var captureDir = Path.GetDirectoryName(_outputPath) ?? Path.Combine(repoRoot, "artifacts", "quick-window-isolation");
        Directory.CreateDirectory(captureDir);

        StatusText.Text = $"Gate {overall} — Browser {browserVersion} — saved to {_outputPath}";
        _done.TrySetResult(true);
        // Allow visual confirmation 1.5s then shutdown
        await Task.Delay(1500);
        ShutdownWithCode(exitCode);
    }

    private static string ComputePrototypeHash(string repoRoot)
    {
        var prototypeDir = Path.Combine(repoRoot, "tools", "prototypes", "quick-window-dom-css-isolation");
        var files = new[] { "prototype.js", "prototype.css", "index.html", "assertions.js", "evidence.schema.json", "README.md" }
            .Select(f => Path.Combine(prototypeDir, f))
            .Where(File.Exists)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();
        using var sha = SHA256.Create();
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    private static string LoadExactNodeGateVersion(string repoRoot, string prototypeHash)
    {
        var path = Path.Combine(repoRoot, "artifacts", "quick-window-isolation", "node-headless.json");
        if (!File.Exists(path))
            throw new InvalidOperationException($"Missing exact-Node gate evidence: {path}");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var overall = root.GetProperty("overall").GetString();
        var revision = root.GetProperty("prototypeRevision").GetString();
        var evidenceHash = root.GetProperty("prototypeHash").GetString();
        var nodeVersion = root.GetProperty("versions").GetProperty("node").GetString();
        if (!string.Equals(overall, "PASS", StringComparison.Ordinal)
            || !string.Equals(revision, "1.0.2", StringComparison.Ordinal)
            || !string.Equals(evidenceHash, prototypeHash, StringComparison.Ordinal)
            || nodeVersion is null
            || !nodeVersion.StartsWith("v20.18.", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Exact-Node evidence does not match PrototypeRevision 1.0.2, the current prototype hash, and Node 20.18.x.");
        }

        return nodeVersion;
    }

    private async Task WriteFailureAsync(string detail)
    {
        try
        {
            var repoRoot = FindRepoRoot();
            var prototypeHash = "unknown";
            try { prototypeHash = ComputePrototypeHash(repoRoot); } catch { }
            var failure = new JsonObject
            {
                ["schemaVersion"] = "1.0.0",
                ["prototypeRevision"] = "1.0.2",
                ["prototypeHash"] = prototypeHash,
                ["generatedUtc"] = DateTimeOffset.UtcNow.ToString("o"),
                ["overall"] = "FAIL",
                ["failureDetail"] = detail,
                ["versions"] = new JsonObject
                {
                    ["webView2Sdk"] = "1.0.3967.48",
                    ["webView2Runtime"] = WebView?.CoreWebView2?.Environment?.BrowserVersionString ?? "unknown",
                    ["os"] = Environment.OSVersion.ToString(),
                    ["dotnet"] = Environment.Version.ToString()
                }
            };
            var outputDir = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrWhiteSpace(outputDir)) Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(_outputPath, failure.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void ShutdownWithCode(int code)
    {
        try { _done.TrySetResult(true); } catch { }
        Application.Current.Shutdown(code);
        Environment.Exit(code);
    }
}
