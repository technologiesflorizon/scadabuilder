using System.Collections.ObjectModel;
using System.ComponentModel;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>French authoring labels for the typed local-interface vocabulary.</summary>
/// <remarks>
/// Decisions: DEC-0050, FR-005, FR-UI-15.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceAuthoringTests.cs.
/// </remarks>
public static class QuickWindowInterfaceLabels
{
    /// <summary>Gets the authoring label of one interface family.</summary>
    public static string For(QuickWindowInterfaceFamily family) => family switch
    {
        QuickWindowInterfaceFamily.ReadState => "État lu",
        QuickWindowInterfaceFamily.WriteCommand => "Commande écrite",
        QuickWindowInterfaceFamily.PublicParameter => "Paramètre public",
        QuickWindowInterfaceFamily.PrivateVariable => "Variable privée",
        QuickWindowInterfaceFamily.PrivateConstant => "Constante privée",
        _ => family.ToString()
    };

    /// <summary>Gets the authoring label of one data type.</summary>
    public static string For(QuickWindowDataType dataType) => dataType switch
    {
        QuickWindowDataType.Boolean => "Booléen",
        QuickWindowDataType.Integer => "Entier",
        QuickWindowDataType.Decimal => "Décimal",
        QuickWindowDataType.String => "Texte",
        QuickWindowDataType.Enum => "Énumération",
        _ => dataType.ToString()
    };

    /// <summary>Gets the authoring label of one access mode.</summary>
    public static string For(QuickWindowMemberAccess access) => access switch
    {
        QuickWindowMemberAccess.Read => "Lecture",
        QuickWindowMemberAccess.Write => "Écriture",
        QuickWindowMemberAccess.ReadWrite => "Lecture/Écriture",
        QuickWindowMemberAccess.Internal => "Interne",
        _ => access.ToString()
    };
}

/// <summary>
/// Typed rules that keep a member coherent while it is authored: the family owns the access mode and
/// private families can never be required.
/// </summary>
/// <remarks>
/// The same rules are enforced fail-closed by <see cref="QuickWindowValidation"/>; applying them while
/// editing avoids proposing a member the domain would immediately refuse.
///
/// Decisions: DEC-0050, FR-005, FR-006, FR-UI-16.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceAuthoringTests.cs.
/// </remarks>
public static class QuickWindowInterfaceMemberRules
{
    /// <summary>Gets the every-family list in authoring order: public families first.</summary>
    public static IReadOnlyList<QuickWindowInterfaceFamily> Families { get; } =
    [
        QuickWindowInterfaceFamily.ReadState,
        QuickWindowInterfaceFamily.WriteCommand,
        QuickWindowInterfaceFamily.PublicParameter,
        QuickWindowInterfaceFamily.PrivateVariable,
        QuickWindowInterfaceFamily.PrivateConstant
    ];

    /// <summary>Gets the access mode imposed by one family, or the preserved mode when several are legal.</summary>
    public static QuickWindowMemberAccess AccessFor(QuickWindowInterfaceFamily family, QuickWindowMemberAccess current) => family switch
    {
        QuickWindowInterfaceFamily.ReadState => QuickWindowMemberAccess.Read,
        QuickWindowInterfaceFamily.WriteCommand => QuickWindowMemberAccess.Write,
        QuickWindowInterfaceFamily.PublicParameter => current == QuickWindowMemberAccess.ReadWrite
            ? QuickWindowMemberAccess.ReadWrite
            : QuickWindowMemberAccess.Read,
        _ => QuickWindowMemberAccess.Internal
    };

    /// <summary>Returns the member re-coerced onto one family: access and required flag follow the family.</summary>
    public static QuickWindowInterfaceMember WithFamily(QuickWindowInterfaceMember member, QuickWindowInterfaceFamily family)
    {
        ArgumentNullException.ThrowIfNull(member);
        var isPrivate = family is QuickWindowInterfaceFamily.PrivateVariable or QuickWindowInterfaceFamily.PrivateConstant;
        return member with
        {
            Family = family,
            Access = AccessFor(family, member.Access),
            Required = !isPrivate && member.Required
        };
    }

    /// <summary>Returns the access modes an operator may choose for one family.</summary>
    public static IReadOnlyList<QuickWindowMemberAccess> AllowedAccesses(QuickWindowInterfaceFamily family) => family switch
    {
        QuickWindowInterfaceFamily.ReadState => [QuickWindowMemberAccess.Read],
        QuickWindowInterfaceFamily.WriteCommand => [QuickWindowMemberAccess.Write],
        QuickWindowInterfaceFamily.PublicParameter => [QuickWindowMemberAccess.Read, QuickWindowMemberAccess.ReadWrite],
        _ => [QuickWindowMemberAccess.Internal]
    };
}

/// <summary>One editable member of the local interface, with its usage counter and binding status.</summary>
/// <remarks>
/// Common properties are edited inline in the single grouped table; the advanced ones are edited through the
/// shared member dialog. Binding status is derived, never persisted: an optional port without any binding is
/// muted, a required one is flagged.
///
/// Decisions: DEC-0050, FR-005, FR-UI-15, FR-UI-16, FR-UI-17, FR-UI-20.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.1.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceAuthoringTests.cs.
/// </remarks>
public sealed class QuickWindowInterfaceMemberViewModel : INotifyPropertyChanged
{
    /// <summary>Group label of every publicly bindable member.</summary>
    public const string PublicGroupLabel = "Interface publique";

    /// <summary>Group label of every instance-private member.</summary>
    public const string PrivateGroupLabel = "Données privées";

    /// <summary>Status token of a member bound by at least one invocation.</summary>
    public const string BoundToken = "bound";

    /// <summary>Status token of an unbound optional port: muted, never blocking.</summary>
    public const string UnboundOptionalToken = "unbound-optional";

    /// <summary>Status token of an unbound required port: flagged and blocking.</summary>
    public const string UnboundRequiredToken = "unbound-required";

    /// <summary>Properties edited directly in the table (FR-UI-16).</summary>
    public static IReadOnlyList<string> InlineEditableProperties { get; } =
        ["Name", "Family", "DataType", "Access", "Required"];

    /// <summary>Properties edited through the shared member dialog (FR-UI-16).</summary>
    public static IReadOnlyList<string> AdvancedProperties { get; } =
        ["DefaultValue", "Description"];

    private QuickWindowInterfaceMember member;
    private IReadOnlyList<QuickWindowUsage> usages;
    private bool isSelected;

    /// <summary>Creates the presentation model of one interface member.</summary>
    public QuickWindowInterfaceMemberViewModel(QuickWindowInterfaceMember member, IReadOnlyList<QuickWindowUsage>? usages = null)
    {
        this.member = member ?? throw new ArgumentNullException(nameof(member));
        this.usages = usages ?? Array.Empty<QuickWindowUsage>();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when an inline edit produced a new candidate member.</summary>
    public event EventHandler<QuickWindowInterfaceMemberViewModel>? InlineEdited;

    /// <summary>Gets the current member snapshot.</summary>
    public QuickWindowInterfaceMember Member => member;

    /// <summary>Gets the durable member key.</summary>
    public Guid MemberKey => member.MemberKey;

    /// <summary>Gets or sets the member name; renaming never breaks a persisted binding.</summary>
    public string Name
    {
        get => member.Name;
        set => ApplyInline(member with { Name = value?.Trim() ?? string.Empty });
    }

    /// <summary>Gets or sets the typed family; access and required flag follow it.</summary>
    public QuickWindowInterfaceFamily Family
    {
        get => member.Family;
        set => ApplyInline(QuickWindowInterfaceMemberRules.WithFamily(member, value));
    }

    /// <summary>Gets or sets the data type of the member contract.</summary>
    public QuickWindowDataType DataType
    {
        get => member.DataType;
        set => ApplyInline(member with { DataType = value });
    }

    /// <summary>Gets or sets the access mode, bounded by the family.</summary>
    public QuickWindowMemberAccess Access
    {
        get => member.Access;
        set => ApplyInline(member with { Access = QuickWindowInterfaceMemberRules.AllowedAccesses(member.Family).Contains(value) ? value : member.Access });
    }

    /// <summary>Gets or sets whether an invocation must bind this port.</summary>
    public bool Required
    {
        get => member.Required;
        set => ApplyInline(member with { Required = member.IsPrivate ? false : value });
    }

    /// <summary>Gets the default value, edited through the shared dialog.</summary>
    public string? DefaultValue => member.DefaultValue;

    /// <summary>Gets the description, edited through the shared dialog.</summary>
    public string? Description => member.Description;

    /// <summary>Gets the group of the single table this member belongs to.</summary>
    public string GroupLabel => member.IsPublic ? PublicGroupLabel : PrivateGroupLabel;

    /// <summary>Gets the group sort order: public interface first.</summary>
    public int GroupOrder => member.IsPublic ? 0 : 1;

    /// <summary>Gets the family authoring label.</summary>
    public string FamilyLabel => QuickWindowInterfaceLabels.For(member.Family);

    /// <summary>Gets the data type authoring label.</summary>
    public string DataTypeLabel => QuickWindowInterfaceLabels.For(member.DataType);

    /// <summary>Gets the access authoring label.</summary>
    public string AccessLabel => QuickWindowInterfaceLabels.For(member.Access);

    /// <summary>Gets every invocation binding that currently references this member.</summary>
    public IReadOnlyList<QuickWindowUsage> Usages => usages;

    /// <summary>Gets the number of usages of this member (FR-UI-17).</summary>
    public int UsageCount => usages.Count;

    /// <summary>Gets whether this member has at least one usage to navigate to.</summary>
    public bool HasUsages => usages.Count > 0;

    /// <summary>Gets the usage counter label shown in the table.</summary>
    public string UsageLabel => usages.Count switch
    {
        0 => "Aucune utilisation",
        1 => "1 utilisation",
        _ => $"{usages.Count} utilisations"
    };

    /// <summary>Gets whether at least one invocation binds this member.</summary>
    public bool IsBound => usages.Count > 0;

    /// <summary>Gets the binding status label of this member.</summary>
    public string BindingStatusLabel => IsBound ? "Lié" : "Non lié";

    /// <summary>Gets the stable status token driving the muted/flagged presentation (FR-UI-20).</summary>
    public string BindingStatusToken => IsBound
        ? BoundToken
        : member.Required ? UnboundRequiredToken : UnboundOptionalToken;

    /// <summary>Gets whether this member is an unbound required port and must be flagged.</summary>
    public bool IsUnboundRequired => BindingStatusToken == UnboundRequiredToken;

    /// <summary>Gets whether this member is an unbound optional port and must be muted.</summary>
    public bool IsUnboundOptional => BindingStatusToken == UnboundOptionalToken;

    /// <summary>Gets the searchable text of this member.</summary>
    public string SearchText => $"{member.Name} {FamilyLabel} {DataTypeLabel} {member.Description}";

    /// <summary>Gets or sets whether this member is selected in the table.</summary>
    public bool IsSelected
    {
        get => isSelected;
        set { if (isSelected == value) return; isSelected = value; Raise(nameof(IsSelected)); }
    }

    /// <summary>Replaces the member and its usages after a workspace reload.</summary>
    public void Update(QuickWindowInterfaceMember updated, IReadOnlyList<QuickWindowUsage>? memberUsages = null)
    {
        member = updated ?? throw new ArgumentNullException(nameof(updated));
        usages = memberUsages ?? Array.Empty<QuickWindowUsage>();
        RaiseAll();
    }

    private void ApplyInline(QuickWindowInterfaceMember candidate)
    {
        if (candidate == member) return;
        member = candidate;
        RaiseAll();
        InlineEdited?.Invoke(this, this);
    }

    private void RaiseAll()
    {
        foreach (var name in new[]
                 {
                     nameof(Member), nameof(Name), nameof(Family), nameof(DataType), nameof(Access), nameof(Required),
                     nameof(DefaultValue), nameof(Description), nameof(GroupLabel), nameof(GroupOrder),
                     nameof(FamilyLabel), nameof(DataTypeLabel), nameof(AccessLabel), nameof(Usages),
                     nameof(UsageCount), nameof(HasUsages), nameof(UsageLabel), nameof(IsBound),
                     nameof(BindingStatusLabel), nameof(BindingStatusToken), nameof(IsUnboundRequired),
                     nameof(IsUnboundOptional), nameof(SearchText)
                 })
        {
            Raise(name);
        }
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>One labelled enum option offered by an inline table selector.</summary>
/// <param name="Label">The French authoring label.</param>
/// <param name="Value">The typed domain value bound by the cell.</param>
public sealed record QuickWindowInterfaceOption(string Label, object Value);

/// <summary>Labelled option lists of the inline table selectors.</summary>
/// <remarks>
/// Decisions: DEC-0050, FR-UI-16.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceAuthoringTests.cs.
/// </remarks>
public static class QuickWindowInterfaceOptions
{
    /// <summary>Gets every family option, public families first.</summary>
    public static IReadOnlyList<QuickWindowInterfaceOption> Families { get; } =
        QuickWindowInterfaceMemberRules.Families
            .Select(family => new QuickWindowInterfaceOption(QuickWindowInterfaceLabels.For(family), family))
            .ToArray();

    /// <summary>Gets every data type option.</summary>
    public static IReadOnlyList<QuickWindowInterfaceOption> DataTypes { get; } =
        Enum.GetValues<QuickWindowDataType>()
            .Select(dataType => new QuickWindowInterfaceOption(QuickWindowInterfaceLabels.For(dataType), dataType))
            .ToArray();

    /// <summary>Gets every access option.</summary>
    public static IReadOnlyList<QuickWindowInterfaceOption> Accesses { get; } =
        Enum.GetValues<QuickWindowMemberAccess>()
            .Select(access => new QuickWindowInterfaceOption(QuickWindowInterfaceLabels.For(access), access))
            .ToArray();
}

/// <summary>One entry of the family filter of the local-interface table.</summary>
/// <param name="Label">The displayed label.</param>
/// <param name="Family">The filtered family, or null for every family.</param>
public sealed record QuickWindowInterfaceFamilyFilter(string Label, QuickWindowInterfaceFamily? Family);

/// <summary>
/// The `Interface locale` panel model: one single table grouped in `Interface publique` and `Données privées`,
/// filtered by family and free text, with per-member usage counters.
/// </summary>
/// <remarks>
/// This model replaces the project `Catalogue Tags` while a quick window is the active surface; it never
/// exposes a physical project tag.
///
/// Decisions: DEC-0050, FR-005, FR-UI-15, FR-UI-16, FR-UI-17, FR-UI-20.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.1.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceAuthoringTests.cs.
/// </remarks>
public sealed class QuickWindowInterfacePanelViewModel : INotifyPropertyChanged
{
    /// <summary>Panel title replacing `Catalogue Tags` in a quick-window context.</summary>
    public const string PanelTitle = "Interface locale";

    /// <summary>Label of the unfiltered family option.</summary>
    public const string AllFamiliesLabel = "Toutes les familles";

    private readonly List<QuickWindowInterfaceMemberViewModel> all = [];
    private QuickWindowInterfaceMemberViewModel? selected;
    private QuickWindowInterfaceFamilyFilter familyFilter;
    private string searchText = string.Empty;
    private string definitionLabel = string.Empty;

    /// <summary>Creates an empty panel bound to no definition.</summary>
    public QuickWindowInterfacePanelViewModel()
    {
        FamilyFilters = new[] { new QuickWindowInterfaceFamilyFilter(AllFamiliesLabel, null) }
            .Concat(QuickWindowInterfaceMemberRules.Families.Select(family =>
                new QuickWindowInterfaceFamilyFilter(QuickWindowInterfaceLabels.For(family), family)))
            .ToArray();
        familyFilter = FamilyFilters[0];
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when one inline edit produced a new candidate member to apply.</summary>
    public event EventHandler<QuickWindowInterfaceMemberViewModel>? MemberInlineEdited;

    /// <summary>Gets the visible members, already filtered, in group order.</summary>
    public ObservableCollection<QuickWindowInterfaceMemberViewModel> Members { get; } = [];

    /// <summary>Gets every family filter option, the unfiltered one first.</summary>
    public IReadOnlyList<QuickWindowInterfaceFamilyFilter> FamilyFilters { get; }

    /// <summary>Gets the definition key currently authored, or null.</summary>
    public Guid? DefinitionKey { get; private set; }

    /// <summary>Gets the panel title.</summary>
    public string Title => PanelTitle;

    /// <summary>Gets the label of the authored definition.</summary>
    public string DefinitionLabel
    {
        get => definitionLabel;
        private set { if (definitionLabel == value) return; definitionLabel = value; Raise(nameof(DefinitionLabel)); }
    }

    /// <summary>Gets or sets the free-text filter of the single table.</summary>
    public string SearchText
    {
        get => searchText;
        set
        {
            var candidate = value ?? string.Empty;
            if (searchText == candidate) return;
            searchText = candidate;
            Raise(nameof(SearchText));
            ApplyFilters();
        }
    }

    /// <summary>Gets or sets the selected family filter.</summary>
    public QuickWindowInterfaceFamilyFilter SelectedFamilyFilter
    {
        get => familyFilter;
        set
        {
            var candidate = value ?? FamilyFilters[0];
            if (ReferenceEquals(familyFilter, candidate)) return;
            familyFilter = candidate;
            Raise(nameof(SelectedFamilyFilter));
            ApplyFilters();
        }
    }

    /// <summary>Gets or sets the selected member.</summary>
    public QuickWindowInterfaceMemberViewModel? Selected
    {
        get => selected;
        set
        {
            if (ReferenceEquals(selected, value)) return;
            if (selected is not null) selected.IsSelected = false;
            selected = value;
            if (selected is not null) selected.IsSelected = true;
            Raise(nameof(Selected));
            Raise(nameof(HasSelection));
        }
    }

    /// <summary>Gets whether one member is selected.</summary>
    public bool HasSelection => selected is not null;

    /// <summary>Gets the number of publicly bindable members.</summary>
    public int PublicCount => all.Count(item => item.Member.IsPublic);

    /// <summary>Gets the number of instance-private members.</summary>
    public int PrivateCount => all.Count(item => item.Member.IsPrivate);

    /// <summary>Gets the number of unbound required ports that must be repaired.</summary>
    public int UnboundRequiredCount => all.Count(item => item.IsUnboundRequired);

    /// <summary>Gets the summary shown above the table.</summary>
    public string SummaryLabel => $"{PublicCount} membre(s) public(s) · {PrivateCount} donnée(s) privée(s) · {Members.Count} affiché(s)";

    /// <summary>Gets whether the panel currently shows a definition.</summary>
    public bool HasDefinition => DefinitionKey is not null;

    /// <summary>Reloads the panel from one definition and its per-member usages.</summary>
    public void Load(
        QuickWindowDefinition definition,
        IReadOnlyDictionary<Guid, IReadOnlyList<QuickWindowUsage>>? usagesByMember = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var previousKey = Selected?.MemberKey;
        foreach (var item in all) item.InlineEdited -= OnMemberInlineEdited;
        all.Clear();
        DefinitionKey = definition.DefinitionKey;
        DefinitionLabel = $"{definition.DisplayName} · interface v{definition.InterfaceVersion}";

        foreach (var member in definition.EffectiveInterfaceMembers)
        {
            var usages = usagesByMember is not null && usagesByMember.TryGetValue(member.MemberKey, out var found)
                ? found
                : Array.Empty<QuickWindowUsage>();
            var item = new QuickWindowInterfaceMemberViewModel(member, usages);
            item.InlineEdited += OnMemberInlineEdited;
            all.Add(item);
        }

        ApplyFilters();
        Selected = previousKey is { } key ? Members.FirstOrDefault(item => item.MemberKey == key) : null;
        Raise(nameof(DefinitionKey));
        Raise(nameof(HasDefinition));
    }

    /// <summary>Clears the panel when a page becomes the active surface again.</summary>
    public void Clear()
    {
        foreach (var item in all) item.InlineEdited -= OnMemberInlineEdited;
        all.Clear();
        DefinitionKey = null;
        DefinitionLabel = string.Empty;
        Selected = null;
        ApplyFilters();
        Raise(nameof(DefinitionKey));
        Raise(nameof(HasDefinition));
    }

    private void OnMemberInlineEdited(object? sender, QuickWindowInterfaceMemberViewModel item) =>
        MemberInlineEdited?.Invoke(this, item);

    private void ApplyFilters()
    {
        var family = familyFilter.Family;
        var text = searchText.Trim();
        var visible = all
            .Where(item => family is null || item.Member.Family == family)
            .Where(item => text.Length == 0 || item.SearchText.Contains(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.GroupOrder)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Members.Clear();
        foreach (var item in visible) Members.Add(item);

        Raise(nameof(PublicCount));
        Raise(nameof(PrivateCount));
        Raise(nameof(UnboundRequiredCount));
        Raise(nameof(SummaryLabel));
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Editable draft of one interface member, used by the shared member dialog for both creation and
/// advanced edition. It keeps the typed rules coherent and validates against the domain before returning.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-005, FR-006, FR-UI-16.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceAuthoringTests.cs.
/// </remarks>
public sealed class QuickWindowInterfaceMemberDraft
{
    private QuickWindowInterfaceMember member;

    private QuickWindowInterfaceMemberDraft(QuickWindowInterfaceMember member, bool isNew)
    {
        this.member = member;
        IsNew = isNew;
    }

    /// <summary>Gets whether this draft creates a member instead of editing an existing one.</summary>
    public bool IsNew { get; }

    /// <summary>Gets the durable member key, stable across every edition.</summary>
    public Guid MemberKey => member.MemberKey;

    /// <summary>Gets or sets the member name.</summary>
    public string Name
    {
        get => member.Name;
        set => member = member with { Name = value?.Trim() ?? string.Empty };
    }

    /// <summary>Gets or sets the typed family; access and required flag follow it.</summary>
    public QuickWindowInterfaceFamily Family
    {
        get => member.Family;
        set => member = QuickWindowInterfaceMemberRules.WithFamily(member, value);
    }

    /// <summary>Gets or sets the data type.</summary>
    public QuickWindowDataType DataType
    {
        get => member.DataType;
        set => member = member with { DataType = value };
    }

    /// <summary>Gets or sets the access mode, bounded by the family.</summary>
    public QuickWindowMemberAccess Access
    {
        get => member.Access;
        set => member = member with
        {
            Access = QuickWindowInterfaceMemberRules.AllowedAccesses(member.Family).Contains(value) ? value : member.Access
        };
    }

    /// <summary>Gets or sets whether an invocation must bind this port.</summary>
    public bool Required
    {
        get => member.Required;
        set => member = member with { Required = member.IsPrivate ? false : value };
    }

    /// <summary>Gets or sets the default value.</summary>
    public string? DefaultValue
    {
        get => member.DefaultValue;
        set => member = member with { DefaultValue = string.IsNullOrWhiteSpace(value) ? null : value.Trim() };
    }

    /// <summary>Gets or sets the description.</summary>
    public string? Description
    {
        get => member.Description;
        set => member = member with { Description = string.IsNullOrWhiteSpace(value) ? null : value.Trim() };
    }

    /// <summary>Creates a draft for a new private variable named after the proposed name.</summary>
    public static QuickWindowInterfaceMemberDraft ForNew(string proposedName) =>
        new(
            new QuickWindowInterfaceMember(
                Guid.NewGuid(),
                proposedName ?? string.Empty,
                QuickWindowInterfaceFamily.ReadState,
                QuickWindowDataType.Boolean,
                QuickWindowMemberAccess.Read),
            isNew: true);

    /// <summary>Creates a draft editing one existing member.</summary>
    public static QuickWindowInterfaceMemberDraft From(QuickWindowInterfaceMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return new QuickWindowInterfaceMemberDraft(member, isNew: false);
    }

    /// <summary>Returns the candidate member described by this draft.</summary>
    public QuickWindowInterfaceMember ToMember() => member;

    /// <summary>Validates the draft against the domain rules and its siblings.</summary>
    public IReadOnlyList<string> Validate(IEnumerable<QuickWindowInterfaceMember>? siblings = null) =>
        QuickWindowValidation.ValidateMember(member, siblings);
}
