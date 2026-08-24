using System.Collections.ObjectModel;
using System.ComponentModel;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// Bounded quick-window authoring context of one caller surface: which definitions may be opened, which
/// parent ports may be forwarded and whether the caller lives inside a quick-window content.
/// </summary>
/// <remarks>
/// A page never offers `CloseQuickWindow`; a quick-window content offers it as `Self` without any free
/// target. No Toggle command kind exists: `DEC-0050` removed the popup command kinds entirely.
///
/// Decisions: DEC-0050, FR-011, FR-013, FR-UI-18, FR-UI-19.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.2.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBindingAuthoringTests.cs.
/// </remarks>
public sealed record QuickWindowCommandAuthoringContext(
    IReadOnlyList<QuickWindowDefinition> Definitions,
    IReadOnlyList<QuickWindowInterfaceMember> ParentPorts,
    bool IsQuickWindowContent = false,
    Func<Guid, QuickWindowInvocation?>? FindInvocation = null)
{
    /// <summary>Gets the context of a surface that cannot author any quick-window command.</summary>
    public static QuickWindowCommandAuthoringContext Empty { get; } = new([], []);

    /// <summary>Gets the definitions that may be opened, in project order.</summary>
    public IReadOnlyList<QuickWindowDefinition> EffectiveDefinitions => Definitions ?? [];

    /// <summary>Gets the public ports of the hosting definition that a binding may forward.</summary>
    public IReadOnlyList<QuickWindowInterfaceMember> EffectiveParentPorts =>
        (ParentPorts ?? []).Where(member => member.IsPublic).ToArray();

    /// <summary>Gets whether an `OpenQuickWindow` command may be authored on this surface.</summary>
    public bool CanOpenQuickWindow => EffectiveDefinitions.Count > 0;

    /// <summary>Gets whether a `CloseQuickWindow(Self)` command may be authored on this surface.</summary>
    public bool CanCloseQuickWindow => IsQuickWindowContent;

    /// <summary>Returns the quick-window command kinds offered by this surface, in authoring order.</summary>
    public IReadOnlyList<ScadaCommandKind> AllowedCommandKinds()
    {
        var kinds = new List<ScadaCommandKind>();
        if (CanOpenQuickWindow) kinds.Add(ScadaCommandKind.OpenQuickWindow);
        if (CanCloseQuickWindow) kinds.Add(ScadaCommandKind.CloseQuickWindow);
        return kinds;
    }

    /// <summary>Finds one definition offered by this context.</summary>
    public QuickWindowDefinition? Find(Guid definitionKey) =>
        EffectiveDefinitions.FirstOrDefault(definition => definition.DefinitionKey == definitionKey);

    /// <summary>Resolves the persisted invocation of one caller command, when it already exists.</summary>
    public QuickWindowInvocation? ResolveInvocation(Guid? invocationKey) =>
        invocationKey is { } key && key != Guid.Empty ? FindInvocation?.Invoke(key) : null;

    /// <summary>Resolves the definition targeted by one persisted invocation.</summary>
    public QuickWindowDefinition? ResolveDefinitionOfInvocation(Guid? invocationKey) =>
        ResolveInvocation(invocationKey) is { } invocation ? Find(invocation.DefinitionKey) : null;
}

/// <summary>One prepared invocation authoring request produced by the `Liaisons` tab.</summary>
/// <param name="CommandId">Id of the caller `OpenQuickWindow` command.</param>
/// <param name="DefinitionKey">Target definition of the invocation.</param>
/// <param name="Bindings">Typed bindings authored port by port.</param>
/// <param name="InvocationKey">Existing invocation key, or null to create one.</param>
/// <param name="TitleOverride">Optional per-invocation title override.</param>
public sealed record QuickWindowInvocationAuthoringRequest(
    string CommandId,
    Guid DefinitionKey,
    IReadOnlyList<QuickWindowBinding> Bindings,
    Guid? InvocationKey = null,
    string? TitleOverride = null);

/// <summary>Result of one invocation authoring request applied by the shell.</summary>
/// <param name="Succeeded">Whether the workspace accepted the mutation.</param>
/// <param name="InvocationKey">The resolved invocation key when the mutation succeeded.</param>
/// <param name="Message">Operator-facing message.</param>
/// <param name="Diagnostics">Structured diagnostics carried back to the authoring surface.</param>
public sealed record QuickWindowInvocationAuthoringOutcome(
    bool Succeeded,
    Guid? InvocationKey,
    string Message,
    IReadOnlyList<ScadaBuildValidationIssue> Diagnostics)
{
    /// <summary>Creates a blocked outcome carrying one message.</summary>
    public static QuickWindowInvocationAuthoringOutcome Blocked(string message) =>
        new(false, null, message, []);
}

/// <summary>One outdated invocation as shown by the repair surface.</summary>
/// <param name="Outdated">The outdated invocation and its incompatibility reasons.</param>
/// <param name="PageLabel">Page code of the caller, or a placeholder when it is unattached.</param>
public sealed record QuickWindowRepairRowViewModel(QuickWindowOutdatedInvocation Outdated, string PageLabel)
{
    /// <summary>Gets the invocation key being repaired.</summary>
    public Guid InvocationKey => Outdated.InvocationKey;

    /// <summary>Gets the caller element id.</summary>
    public string OwnerElementId => Outdated.OwnerElementId ?? "(non rattachée)";

    /// <summary>Gets the caller command id.</summary>
    public string OwnerCommandId => Outdated.OwnerCommandId ?? "-";

    /// <summary>Gets the version pair that makes the invocation outdated.</summary>
    public string VersionLabel => $"v{Outdated.InvocationInterfaceVersion} → v{Outdated.DefinitionInterfaceVersion}";

    /// <summary>Gets every incompatibility reason on one line.</summary>
    public string ReasonLabel => string.Join(" ", Outdated.Reasons);
}

/// <summary>One typed source option offered by the `Source` column of the bindings grid (FR-UI-19).</summary>
/// <param name="Label">The French authoring label.</param>
/// <param name="Kind">The typed domain source kind.</param>
public sealed record QuickWindowBindingSourceOption(string Label, QuickWindowBindingSourceKind Kind);

/// <summary>One row of the `Liaisons` grid: one interface port and its typed binding.</summary>
/// <remarks>
/// The row always chooses a source kind first, then edits the contextual value: a catalog tag, a typed
/// literal, an authorized expression or a public port of the parent window. No single free-text field
/// replaces that typed validation.
///
/// Decisions: DEC-0050, FR-009, FR-010, FR-UI-18, FR-UI-19, FR-UI-20, FR-UI-24.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBindingAuthoringTests.cs.
/// </remarks>
public sealed class QuickWindowBindingRowViewModel : INotifyPropertyChanged
{
    /// <summary>Status token of a bound port.</summary>
    public const string BoundToken = "bound";

    /// <summary>Status token of an unbound optional port: muted, never blocking.</summary>
    public const string UnboundOptionalToken = "unbound-optional";

    /// <summary>Status token of an unbound required port: flagged and blocking.</summary>
    public const string UnboundRequiredToken = "unbound-required";

    /// <summary>Status token of a port that no longer exists in the definition interface.</summary>
    public const string OutdatedToken = "outdated";

    private readonly IReadOnlyList<QuickWindowInterfaceMember> parentPorts;
    private QuickWindowInterfaceMember member;
    private QuickWindowBinding binding;
    private bool isOutdated;

    /// <summary>Creates one grid row for a member and its persisted binding.</summary>
    public QuickWindowBindingRowViewModel(
        QuickWindowInterfaceMember member,
        QuickWindowBinding? binding,
        IReadOnlyList<QuickWindowInterfaceMember>? parentPorts = null,
        bool isOrphan = false,
        bool isOutdated = false)
    {
        this.member = member ?? throw new ArgumentNullException(nameof(member));
        this.binding = binding ?? QuickWindowBinding.Absent(member.MemberKey);
        this.parentPorts = (parentPorts ?? []).Where(port => port.IsPublic).ToArray();
        this.isOutdated = isOutdated;
        IsOrphan = isOrphan;
        SourceOptions = BuildSourceOptions(this.parentPorts.Count > 0);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets whether this row describes a binding whose port no longer exists.</summary>
    public bool IsOrphan { get; }

    /// <summary>Gets the durable member key.</summary>
    public Guid MemberKey => member.MemberKey;

    /// <summary>Gets the port name (FR-UI-18).</summary>
    public string Name => member.Name;

    /// <summary>Gets the port family label (FR-UI-18).</summary>
    public string FamilyLabel => QuickWindowInterfaceLabels.For(member.Family);

    /// <summary>Gets the port data type label (FR-UI-18).</summary>
    public string DataTypeLabel => QuickWindowInterfaceLabels.For(member.DataType);

    /// <summary>Gets whether the port must be bound by every invocation.</summary>
    public bool Required => member.Required;

    /// <summary>Gets the typed source options offered by this row.</summary>
    public IReadOnlyList<QuickWindowBindingSourceOption> SourceOptions { get; }

    /// <summary>Gets the public parent ports a binding may forward.</summary>
    public IReadOnlyList<QuickWindowInterfaceMember> ParentPorts => parentPorts;

    /// <summary>Gets or sets the typed source kind; changing it clears the previous typed value.</summary>
    public QuickWindowBindingSourceKind SourceKind
    {
        get => binding.SourceKind;
        set
        {
            if (binding.SourceKind == value) return;
            binding = value switch
            {
                QuickWindowBindingSourceKind.Tag => QuickWindowBinding.FromTag(member.MemberKey, string.Empty),
                QuickWindowBindingSourceKind.Literal => QuickWindowBinding.FromLiteral(member.MemberKey, string.Empty),
                QuickWindowBindingSourceKind.Expression => QuickWindowBinding.FromExpression(member.MemberKey, string.Empty),
                QuickWindowBindingSourceKind.ParentPort => new QuickWindowBinding(member.MemberKey, QuickWindowBindingSourceKind.ParentPort),
                _ => QuickWindowBinding.Absent(member.MemberKey)
            };
            RaiseAll();
        }
    }

    /// <summary>Gets the label of the selected source kind.</summary>
    public string SourceLabel => SourceOptions.FirstOrDefault(option => option.Kind == binding.SourceKind)?.Label ?? "Aucune";

    /// <summary>Gets or sets the contextual value: tag id, literal, expression or parent port name.</summary>
    public string ValueOrReference
    {
        get => binding.SourceKind switch
        {
            QuickWindowBindingSourceKind.Tag => binding.TagId ?? string.Empty,
            QuickWindowBindingSourceKind.Literal => binding.LiteralValue ?? string.Empty,
            QuickWindowBindingSourceKind.Expression => binding.Expression ?? string.Empty,
            QuickWindowBindingSourceKind.ParentPort => SelectedParentPort?.Name ?? string.Empty,
            _ => string.Empty
        };
        set
        {
            var candidate = value ?? string.Empty;
            binding = binding.SourceKind switch
            {
                QuickWindowBindingSourceKind.Tag => binding with { TagId = candidate },
                QuickWindowBindingSourceKind.Literal => binding with { LiteralValue = candidate },
                QuickWindowBindingSourceKind.Expression => binding with { Expression = candidate },
                _ => binding
            };
            RaiseAll();
        }
    }

    /// <summary>Gets or sets the forwarded parent port when the source kind is `Port parent`.</summary>
    public QuickWindowInterfaceMember? SelectedParentPort
    {
        get => binding.ParentMemberKey is { } key ? parentPorts.FirstOrDefault(port => port.MemberKey == key) : null;
        set
        {
            binding = value is null
                ? binding with { ParentMemberKey = null }
                : QuickWindowBinding.FromParentPort(member.MemberKey, value.MemberKey);
            RaiseAll();
        }
    }

    /// <summary>Gets whether the row currently carries a concrete binding.</summary>
    public bool IsBound => binding.SourceKind != QuickWindowBindingSourceKind.None;

    /// <summary>Gets the stable status token driving the muted/flagged presentation (FR-UI-20, FR-UI-24).</summary>
    public string StatusToken => IsOrphan || isOutdated
        ? OutdatedToken
        : IsBound
            ? BoundToken
            : member.Required ? UnboundRequiredToken : UnboundOptionalToken;

    /// <summary>Gets the status label shown in the grid (FR-UI-18).</summary>
    public string StatusLabel => StatusToken switch
    {
        OutdatedToken => IsOrphan ? "Port retiré" : "À réparer",
        BoundToken => "Lié",
        _ => "Non lié"
    };

    /// <summary>Gets whether the row must be flagged in red.</summary>
    public bool IsBlocking => StatusToken is UnboundRequiredToken or OutdatedToken;

    /// <summary>Gets whether the row is an unbound optional port shown muted.</summary>
    public bool IsUnboundOptional => StatusToken == UnboundOptionalToken;

    /// <summary>Gets whether the contextual value editor is a free typed value.</summary>
    public bool UsesTextValue => binding.SourceKind is QuickWindowBindingSourceKind.Literal or QuickWindowBindingSourceKind.Expression;

    /// <summary>Gets whether the contextual value editor is the catalog tag selector.</summary>
    public bool UsesTagSelector => binding.SourceKind == QuickWindowBindingSourceKind.Tag;

    /// <summary>Gets whether the contextual value editor is the parent port selector.</summary>
    public bool UsesParentPortSelector => binding.SourceKind == QuickWindowBindingSourceKind.ParentPort;

    /// <summary>Returns the typed binding described by this row.</summary>
    public QuickWindowBinding ToBinding() => binding;

    /// <summary>Marks this row as belonging to an outdated invocation.</summary>
    public void MarkOutdated(bool value)
    {
        if (isOutdated == value) return;
        isOutdated = value;
        RaiseAll();
    }

    private static IReadOnlyList<QuickWindowBindingSourceOption> BuildSourceOptions(bool allowParentPort)
    {
        var options = new List<QuickWindowBindingSourceOption>
        {
            new("Aucune", QuickWindowBindingSourceKind.None),
            new("Tag", QuickWindowBindingSourceKind.Tag),
            new("Littéral", QuickWindowBindingSourceKind.Literal),
            new("Expression", QuickWindowBindingSourceKind.Expression)
        };
        if (allowParentPort) options.Add(new QuickWindowBindingSourceOption("Port parent", QuickWindowBindingSourceKind.ParentPort));
        return options;
    }

    private void RaiseAll()
    {
        foreach (var name in new[]
                 {
                     nameof(SourceKind), nameof(SourceLabel), nameof(ValueOrReference), nameof(SelectedParentPort),
                     nameof(IsBound), nameof(StatusToken), nameof(StatusLabel), nameof(IsBlocking),
                     nameof(IsUnboundOptional), nameof(UsesTextValue), nameof(UsesTagSelector), nameof(UsesParentPortSelector)
                 })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

/// <summary>
/// The `Liaisons` tab model: one typed grid binding every public port of the target definition, shown only
/// for an `OpenQuickWindow` command.
/// </summary>
/// <remarks>
/// The editor never writes to the workspace: it produces the typed bindings that the shell applies through
/// <c>QuickWindowInvocationService</c> as one undoable transition. An outdated invocation is shown in red and
/// keeps build and export blocked until it is repaired.
///
/// Decisions: DEC-0050, FR-009, FR-010, FR-032, FR-UI-18, FR-UI-19, FR-UI-20, FR-UI-24.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBindingAuthoringTests.cs.
/// </remarks>
public sealed class QuickWindowBindingsEditorViewModel : INotifyPropertyChanged
{
    private QuickWindowDefinition? definition;
    private QuickWindowInvocation? invocation;
    private ScadaTagCatalog? catalog;
    private string titleOverride = string.Empty;
    private string validationMessage = string.Empty;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the rows of the typed bindings grid.</summary>
    public ObservableCollection<QuickWindowBindingRowViewModel> Rows { get; } = [];

    /// <summary>Gets the catalog tags offered by the `Tag` source selector.</summary>
    public ObservableCollection<ScadaTagDefinition> Tags { get; } = [];

    /// <summary>Gets the edited definition key, or null when no invocation is authored.</summary>
    public Guid? DefinitionKey => definition?.DefinitionKey;

    /// <summary>Gets the edited invocation key, or null before the first save.</summary>
    public Guid? InvocationKey => invocation?.InvocationKey;

    /// <summary>Gets the header label of the edited invocation.</summary>
    public string DefinitionLabel => definition is null
        ? string.Empty
        : $"{definition.DisplayName} · interface v{definition.InterfaceVersion}";

    /// <summary>Gets or sets the optional per-invocation title override.</summary>
    public string TitleOverride
    {
        get => titleOverride;
        set { var candidate = value ?? string.Empty; if (titleOverride == candidate) return; titleOverride = candidate; Raise(nameof(TitleOverride)); }
    }

    /// <summary>Gets whether the invocation no longer matches its definition interface version.</summary>
    public bool IsOutdated => definition is not null && invocation is not null &&
        QuickWindowInterfaceCompatibility.StatusOf(definition, invocation) == QuickWindowInvocationStatus.Outdated;

    /// <summary>Gets the repair banner shown in red while the invocation is outdated.</summary>
    public string OutdatedLabel => IsOutdated && definition is not null && invocation is not null
        ? $"Invocation à réparer : interface v{invocation.InterfaceVersion} vs définition v{definition.InterfaceVersion}. Build et export restent bloqués."
        : string.Empty;

    /// <summary>Gets the number of required ports that are still unbound.</summary>
    public int UnboundRequiredCount => Rows.Count(row => row.StatusToken == QuickWindowBindingRowViewModel.UnboundRequiredToken);

    /// <summary>Gets whether the editor currently shows an invocation.</summary>
    public bool HasInvocation => definition is not null;

    /// <summary>Gets the last validation message produced by <see cref="Validate"/>.</summary>
    public string ValidationMessage
    {
        get => validationMessage;
        private set { if (validationMessage == value) return; validationMessage = value; Raise(nameof(ValidationMessage)); }
    }

    /// <summary>Loads the typed grid from one definition, its persisted invocation and the caller context.</summary>
    public void Load(
        QuickWindowDefinition definition,
        QuickWindowInvocation? invocation,
        ScadaTagCatalog? tagCatalog = null,
        IReadOnlyList<QuickWindowInterfaceMember>? parentPorts = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        this.definition = definition;
        this.invocation = invocation;
        catalog = tagCatalog;
        titleOverride = invocation?.EffectiveTitleOverride ?? string.Empty;

        Tags.Clear();
        foreach (var tag in (tagCatalog?.Tags ?? []).Where(tag => tag.Enabled).OrderBy(tag => tag.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            Tags.Add(tag);
        }

        var outdated = invocation is not null &&
            QuickWindowInterfaceCompatibility.StatusOf(definition, invocation) == QuickWindowInvocationStatus.Outdated;
        Rows.Clear();
        foreach (var member in definition.EffectiveInterfaceMembers.Where(member => member.IsPublic))
        {
            Rows.Add(new QuickWindowBindingRowViewModel(
                member,
                invocation?.FindBinding(member.MemberKey),
                parentPorts,
                isOrphan: false,
                isOutdated: false));
        }

        foreach (var orphan in (invocation?.Bindings ?? [])
                     .Where(binding => !definition.EffectiveInterfaceMembers.Any(member => member.MemberKey == binding.MemberKey)))
        {
            Rows.Add(new QuickWindowBindingRowViewModel(
                new QuickWindowInterfaceMember(
                    orphan.MemberKey,
                    $"(port retiré {orphan.MemberKey.ToString("N")[..8]})",
                    QuickWindowInterfaceFamily.PublicParameter,
                    QuickWindowDataType.String,
                    QuickWindowMemberAccess.Read),
                orphan,
                parentPorts,
                isOrphan: true,
                isOutdated: true));
        }

        if (outdated)
        {
            foreach (var row in Rows.Where(row => row.StatusToken == QuickWindowBindingRowViewModel.UnboundRequiredToken).ToArray())
            {
                row.MarkOutdated(true);
            }
        }

        ValidationMessage = string.Empty;
        RaiseAll();
    }

    /// <summary>Clears the editor when the selected command is not an `OpenQuickWindow` command.</summary>
    public void Clear()
    {
        definition = null;
        invocation = null;
        catalog = null;
        titleOverride = string.Empty;
        Rows.Clear();
        Tags.Clear();
        ValidationMessage = string.Empty;
        RaiseAll();
    }

    /// <summary>Returns the typed bindings authored by the grid, dropping neutral absences.</summary>
    public IReadOnlyList<QuickWindowBinding> ToBindings() => Rows
        .Where(row => !row.IsOrphan)
        .Select(row => row.ToBinding())
        .Where(binding => binding.SourceKind != QuickWindowBindingSourceKind.None)
        .ToArray();

    /// <summary>Validates the authored bindings against the domain rules and publishes the first issue.</summary>
    public IReadOnlyList<string> Validate()
    {
        if (definition is null)
        {
            ValidationMessage = string.Empty;
            return [];
        }

        var candidate = new QuickWindowInvocation(
            invocation?.InvocationKey ?? Guid.NewGuid(),
            definition.DefinitionKey,
            ToBindings(),
            string.IsNullOrWhiteSpace(titleOverride) ? null : titleOverride,
            definition.InterfaceVersion,
            invocation?.OwnerPageKey,
            invocation?.OwnerElementId,
            invocation?.OwnerCommandId);
        var issues = QuickWindowBindingValidator.ValidateInvocation(candidate, definition, catalog)
            .Where(result => !result.IsValid)
            .Select(result => result.Message ?? "Liaison invalide.")
            .ToArray();
        ValidationMessage = issues.Length == 0 ? string.Empty : string.Join(Environment.NewLine, issues);
        return issues;
    }

    /// <summary>Builds the authoring request applied by the shell for one caller command.</summary>
    public QuickWindowInvocationAuthoringRequest? ToRequest(string commandId)
    {
        if (definition is null || string.IsNullOrWhiteSpace(commandId)) return null;
        return new QuickWindowInvocationAuthoringRequest(
            commandId,
            definition.DefinitionKey,
            ToBindings(),
            invocation?.InvocationKey,
            string.IsNullOrWhiteSpace(titleOverride) ? null : titleOverride);
    }

    private void RaiseAll()
    {
        foreach (var name in new[]
                 {
                     nameof(DefinitionKey), nameof(InvocationKey), nameof(DefinitionLabel), nameof(TitleOverride),
                     nameof(IsOutdated), nameof(OutdatedLabel), nameof(UnboundRequiredCount), nameof(HasInvocation)
                 })
        {
            Raise(name);
        }
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
