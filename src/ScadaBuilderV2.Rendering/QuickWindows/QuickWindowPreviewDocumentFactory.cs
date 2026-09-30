using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.Rendering.QuickWindows;

/// <summary>
/// Editor-only test-bench data of one previewed quick-window instance: temporary port values and tag
/// values supplied by the operator.
/// </summary>
/// <remarks>
/// Test-bench data never enters the project model and is never exported: it exists only inside the
/// generated preview document, behind the <see cref="QuickWindowPreviewDocumentFactory.TestValuesAttribute"/>
/// marker that the `.sb2` exporter never emits.
///
/// Decisions: DEC-0050, FR-UI-21.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPreviewTests.cs.
/// </remarks>
/// <param name="TagValues">Temporary tag values injected into the shared TagBridge.</param>
/// <param name="Bindings">Temporary typed bindings applied to the previewed invocation.</param>
/// <param name="TitleOverride">Temporary title override of the previewed instance.</param>
public sealed record QuickWindowTestBenchValues(
    IReadOnlyDictionary<string, string>? TagValues = null,
    IReadOnlyList<QuickWindowBinding>? Bindings = null,
    string? TitleOverride = null)
{
    /// <summary>Gets the effective temporary tag values.</summary>
    public IReadOnlyDictionary<string, string> EffectiveTagValues =>
        TagValues ?? new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the effective temporary bindings.</summary>
    public IReadOnlyList<QuickWindowBinding> EffectiveBindings => Bindings ?? [];

    /// <summary>Gets whether the operator supplied any temporary data.</summary>
    public bool IsEmpty => EffectiveTagValues.Count == 0 && EffectiveBindings.Count == 0 && string.IsNullOrWhiteSpace(TitleOverride);
}

/// <summary>One previewed quick-window instance request.</summary>
/// <param name="Definition">The authored definition.</param>
/// <param name="InvocationKey">The invocation whose bindings are previewed.</param>
/// <param name="TestBench">Editor-only temporary values, or null.</param>
/// <param name="TagCatalog">Catalog used to resolve tag bindings while rendering.</param>
/// <param name="Invocation">
/// A real, fully-bound invocation to register and mount through the shared DEC-0052 runtime
/// (<c>ScadaRuntime.QuickWindow.loadRegistries</c>/<c>open</c>/<c>mountInstance</c>) instead of the legacy
/// <c>QuickWindowHost.open</c> path. Null keeps the legacy path, used by a DEC-0050 definition with no `#N`
/// placeholder member: it mounts exactly as before, with no substitution and no possible refusal.
/// </param>
public sealed record QuickWindowPreviewInput(
    QuickWindowDefinition Definition,
    Guid InvocationKey,
    QuickWindowTestBenchValues? TestBench = null,
    ScadaTagCatalog? TagCatalog = null,
    QuickWindowInvocation? Invocation = null);

/// <summary>Generated editor-only preview document of one quick-window instance.</summary>
/// <param name="InstanceCode">Collision-free code of the preview directory and files.</param>
/// <param name="Namespace">Deterministic DOM/CSS namespace of the definition.</param>
/// <param name="RuntimeInstanceId">Temporary runtime instance id of the previewed context.</param>
/// <param name="Generation">Monotonic generation of the previewed context.</param>
/// <param name="Html">Preview HTML document.</param>
/// <param name="Css">Preview stylesheet.</param>
/// <param name="Script">Preview script bundle: shared runtime plus quick-window host.</param>
/// <param name="Warnings">Non blocking rendering warnings.</param>
public sealed record QuickWindowPreviewDocument(
    string InstanceCode,
    string Namespace,
    Guid RuntimeInstanceId,
    long Generation,
    string Html,
    string Css,
    string Script,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Builds the editor-only preview of one quick-window instance: minimal chrome, shared backdrop, `X`,
/// `Escape`, `Self` close and the definition content rendered by the same Element+ geometry as export.
/// </summary>
/// <remarks>
/// The instance markup is emitted inside a template and mounted by
/// <c>ScadaRuntime.QuickWindowHost</c>, so the host policy — SinglePerDefinition, generations, stale
/// hydration, cascade dispose — has exactly one implementation shared with the frozen Phase 0 prototype.
/// The preview never enters the project model, never becomes a page and is never exported.
///
/// Decisions: DEC-0050, FR-019, FR-020, FR-UI-07, FR-UI-08, FR-UI-21.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §§8.2, 9.3.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPreviewTests.cs.
/// </remarks>
public static class QuickWindowPreviewDocumentFactory
{
    /// <summary>Attribute carrying the editor-only test-bench payload; never emitted by the exporter.</summary>
    public const string TestValuesAttribute = "data-qw-test-values";

    /// <summary>Prefix of every preview instance code so it can never collide with a page code.</summary>
    public const string InstanceCodePrefix = "qw-";

    /// <summary>File name of the preview script bundle materialized next to the document.</summary>
    public const string ScriptFileName = "quick-window-preview.js";

    private static readonly JsonSerializerOptions TestValueJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Options for the DEC-0052 preview registry payload (<c>ScadaRuntime.QuickWindow.loadRegistries</c>):
    /// property names AND enum values keep the exact PascalCase spelling the shared runtime module compares
    /// against (<c>bound.SourceKind === 'Tag'</c>, <c>member.Family !== 'PrivateVariable'</c>, …) — unlike
    /// <c>Ft100SceneExporter.ManifestJsonOptions</c>, which camel-cases enum values for the exported package
    /// manifest. A nested <c>ExpressionAst</c> is re-serialized separately with
    /// <c>Ft100SceneExporter.StateCommandJsonOptions</c> (full camelCase), per spec §3 ruling R3.
    /// </summary>
    private static readonly JsonSerializerOptions RegistryJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Creates the preview document of one quick-window instance.</summary>
    public static QuickWindowPreviewDocument Create(
        QuickWindowPreviewInput input,
        long generation = 1,
        Guid? runtimeInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var definition = input.Definition ?? throw new ArgumentException("A definition is required.", nameof(input));
        var instanceId = runtimeInstanceId ?? Guid.NewGuid();
        var ns = QuickWindowNamespace.ForDefinition(definition.DefinitionKey);
        var instanceCode = $"{InstanceCodePrefix}{definition.EffectiveCode}";
        var scene = definition.EffectiveContent.ToScene(instanceCode, definition.EffectiveTitle, definition.DefinitionKey, instanceCode);
        var warnings = new List<string>();
        var sceneRoot = Ft100SceneExporter.BuildSceneRootHtml(scene, sourceContent: string.Empty, input.TagCatalog, warnings);
        var testBench = input.TestBench ?? new QuickWindowTestBenchValues();
        var title = HtmlEncoder.Default.Encode(
            string.IsNullOrWhiteSpace(testBench.TitleOverride) ? definition.EffectiveTitle : testBench.TitleOverride!);
        var templateId = $"{ns}__template";
        var registry = input.Invocation is { } invocation ? BuildRegistry(definition, invocation, ns) : null;
        var requestJson = HtmlEncoder.Default.Encode(JsonSerializer.Serialize(new
        {
            definitionKey = definition.DefinitionKey,
            invocationKey = input.InvocationKey,
            runtimeInstanceId = instanceId,
            generation,
            templateId,
            registry,
            testValues = new
            {
                tagValues = testBench.EffectiveTagValues,
                bindings = testBench.EffectiveBindings,
                titleOverride = testBench.TitleOverride
            }
        }, TestValueJsonOptions));
        var mountScript = registry is null ? LegacyMountScript : PlaceholderMountScript;

        var html = $$"""
<!doctype html>
<html lang="fr">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{title}}</title>
  <link rel="stylesheet" href="css/{{instanceCode}}.css">
</head>
<body style="margin:0;padding:0;" data-qw-preview="true">
  <div id="qw-preview-host" class="qw-preview-host" data-qw-preview-host="true">
    <p class="qw-preview-hint">Aperçu editor-only d'une instance de fenêtre rapide. Les valeurs du banc d'essai ne sont jamais exportées.</p>
  </div>
  <template id="{{templateId}}">
    <div class="qw-instance">
      <div class="qw-frame" role="dialog" aria-modal="true" aria-label="{{title}}" tabindex="-1">
        <div class="qw-titlebar">
          <span class="qw-title">{{title}}</span>
          <button type="button" class="qw-close" data-qw-close="self" aria-label="Fermer">X</button>
        </div>
        <div class="qw-content">
{{Indent(sceneRoot, 10)}}
        </div>
      </div>
    </div>
  </template>
  <script src="{{ScriptFileName}}" defer></script>
  <script defer>
{{mountScript}}
  </script>
  <div id="qw-preview-request" hidden {{TestValuesAttribute}}="{{requestJson}}"></div>
</body>
</html>
""";

        return new QuickWindowPreviewDocument(
            instanceCode,
            ns,
            instanceId,
            generation,
            html,
            BuildCss(scene, ns),
            BuildScript(),
            warnings);
    }

    /// <summary>Legacy mount script: DEC-0050 hosted-definition preview via <c>QuickWindowHost.open</c>, no `#N` substitution.</summary>
    private const string LegacyMountScript = """
    document.addEventListener('DOMContentLoaded', function () {
      var request = JSON.parse(document.getElementById('qw-preview-request').getAttribute('data-qw-test-values'));
      window.ScadaRuntime.QuickWindowHost.setHostRoot(document.body);
      window.ScadaRuntime.QuickWindowHost.open({
        definitionKey: request.definitionKey,
        invocationKey: request.invocationKey,
        templateId: request.templateId,
        testValues: request.testValues.tagValues
      });
    });
    """;

    /// <summary>
    /// DEC-0052 mount script: registers the previewed definition and invocation with the shared runtime
    /// module, opens the invocation and mounts it through <c>ScadaRuntime.QuickWindow.mountInstance</c> so
    /// the `#N` substitution of Task 7 runs exactly as it will for a real instance. The DOM materialization
    /// and close wiring a real host normally owns are inlined here as a minimal `HostAdapter`, scoped to
    /// this editor-only preview document: the shared runtime module's own open()/close() call
    /// <c>window.ScadaRuntime.HostAdapter.dispatchIntent</c>, which no production host implements yet
    /// (spec §2 — end-to-end host wiring is a later step of this plan, not this one).
    /// </summary>
    private const string PlaceholderMountScript = """
    document.addEventListener('DOMContentLoaded', function () {
      var request = JSON.parse(document.getElementById('qw-preview-request').getAttribute('data-qw-test-values'));
      var hostRoot = document.getElementById('qw-preview-host');
      var showRefusal = function (code) {
        var warning = document.createElement('p');
        warning.className = 'qw-preview-hint';
        warning.textContent = 'Apercu refuse: ' + code;
        hostRoot.appendChild(warning);
      };
      window.ScadaRuntime.QuickWindow.loadRegistries(request.registry);
      window.ScadaRuntime.HostAdapter = {
        dispatchIntent: function (envelope) {
          var intent = envelope.intent;
          if (intent.kind === 'openQuickWindow') {
            var template = document.getElementById(request.templateId);
            var fragment = template.content.cloneNode(true);
            var root = fragment.firstElementChild;
            root.setAttribute('data-qw-def', intent.namespace);
            root.setAttribute('data-qw-inst', intent.runtimeInstanceId);
            hostRoot.appendChild(root);
            var closeButton = root.querySelector('[data-qw-close]');
            if (closeButton) {
              closeButton.addEventListener('click', function () {
                window.ScadaRuntime.QuickWindow.close(intent.runtimeInstanceId);
              });
            }
            var mounted = window.ScadaRuntime.QuickWindow.mountInstance(intent.runtimeInstanceId, root);
            if (!mounted.ok) {
              root.remove();
              showRefusal(mounted.code);
            }
          } else if (intent.kind === 'closeQuickWindow') {
            window.ScadaRuntime.QuickWindow.disposeInstance(intent.runtimeInstanceId);
            var mounted = hostRoot.querySelector('[data-qw-inst="' + intent.runtimeInstanceId + '"]');
            if (mounted) mounted.remove();
          }
        }
      };
      var opened = window.ScadaRuntime.QuickWindow.open(request.invocationKey);
      if (!opened.ok) {
        showRefusal(opened.code);
      }
    });
    """;

    /// <summary>
    /// Builds the minimal DEC-0052 registry payload (<see cref="RegistryJsonOptions"/>) the preview
    /// registers with <c>ScadaRuntime.QuickWindow.loadRegistries</c>: the previewed definition (its
    /// interface members only — visual content is already rendered server-side) and its one invocation.
    /// Carries the same <paramref name="ns"/> namespace <c>Create</c> already computed for the stylesheet
    /// (<c>[data-qw-def="ns"]</c>): <c>quick-window-runtime.js</c>'s <c>open()</c> reads
    /// <c>resolution.definition.Namespace</c> to build both <c>runtimeInstanceId</c> and
    /// <c>intent.namespace</c> — omitting it left the inline adapter setting
    /// <c>data-qw-def="undefined"</c>, so the frame lost every namespaced style rule. A binding's
    /// <c>ExpressionAst</c>, when present, is re-serialized with
    /// <see cref="Ft100SceneExporter.StateCommandJsonOptions"/> per ruling R3.
    /// </summary>
    private static object BuildRegistry(QuickWindowDefinition definition, QuickWindowInvocation invocation, string ns)
    {
        var definitionNode = JsonSerializer.SerializeToNode(new
        {
            definition.DefinitionKey,
            definition.InterfaceVersion,
            InterfaceMembers = definition.EffectiveInterfaceMembers,
            Namespace = ns
        }, RegistryJsonOptions);

        var invocationNode = JsonSerializer.SerializeToNode(invocation, RegistryJsonOptions)!.AsObject();
        var bindingsNode = invocationNode["Bindings"]?.AsArray();
        if (bindingsNode is not null)
        {
            for (var i = 0; i < invocation.Bindings.Count; i++)
            {
                if (invocation.Bindings[i].ExpressionAst is { } ast)
                {
                    bindingsNode[i]!["ExpressionAst"] = JsonSerializer.SerializeToNode(ast, Ft100SceneExporter.StateCommandJsonOptions);
                }
            }
        }

        return new
        {
            QuickWindows = new[] { definitionNode },
            QuickWindowInvocations = new[] { invocationNode }
        };
    }

    /// <summary>Returns the preview script bundle: shared runtime modules plus the quick-window host.</summary>
    /// <remarks>
    /// The host module is intentionally absent from the exported runtime bundle: quick-window runtime
    /// capabilities stay `Blocked` until their own promotion, so no package can carry this behavior yet.
    /// </remarks>
    public static string BuildScript()
    {
        var bundle = new StringBuilder();
        bundle.Append(Ft100SceneExporter.GetRuntimeScript());
        bundle.Append('\n');
        bundle.Append(ReadHostModule());
        return bundle.ToString();
    }

    private static string ReadHostModule()
    {
        var assembly = typeof(QuickWindowPreviewDocumentFactory).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("quick-window-host.js", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "Runtime module 'quick-window-host.js' not found as embedded resource.");
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Runtime module 'quick-window-host.js' could not be read.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string BuildCss(Domain.Scenes.ScadaScene scene, string ns)
    {
        var css = new StringBuilder();
        css.AppendLine("body { margin: 0; padding: 0; background: #eef2f5; font-family: 'Segoe UI', sans-serif; }");
        css.AppendLine(".qw-preview-host { padding: 12px; color: #5b7076; }");
        css.AppendLine(".qw-preview-hint { margin: 0; font-size: 12px; }");
        css.AppendLine(".qw-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,0.35); }");
        css.AppendLine($"[data-qw-def=\"{ns}\"] {{ position: relative; }}");
        css.AppendLine($"[data-qw-def=\"{ns}\"] .qw-frame {{ position: fixed; left: 50%; top: 50%; transform: translate(-50%, -50%); background: #ffffff; border: 1px solid #33454d; box-shadow: 0 10px 40px rgba(0,0,0,0.3); max-width: 96vw; max-height: 92vh; overflow: auto; }}");
        css.AppendLine($"[data-qw-def=\"{ns}\"] .qw-titlebar {{ display: flex; align-items: center; justify-content: space-between; height: 34px; padding: 0 10px; background: #0f3556; color: #ffffff; }}");
        css.AppendLine($"[data-qw-def=\"{ns}\"] .qw-close {{ width: 26px; height: 26px; border: 1px solid #ccc; background: #fff; color: #222; cursor: pointer; }}");
        css.AppendLine($"[data-qw-def=\"{ns}\"] .qw-content {{ padding: 0; }}");
        css.AppendLine($"[data-qw-def=\"{ns}\"] :focus {{ outline: 2px solid #ffb000; }}");
        css.Append(Ft100SceneExporter.BuildDocumentCss(scene));
        return css.ToString();
    }

    private static string Indent(string value, int spaces)
    {
        var prefix = new string(' ', spaces);
        return string.Join(
            Environment.NewLine,
            value.ReplaceLineEndings("\n").Split('\n').Select(line => prefix + line));
    }
}
