namespace ScadaBuilderV2.Domain.QuickWindows;

/// <summary>
/// Compatibility state of one persisted invocation against its current definition.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-032.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.4.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceVersioningTests.cs.
/// </remarks>
public enum QuickWindowInvocationStatus
{
    /// <summary>The invocation carries the definition's current interface version.</summary>
    Current = 0,

    /// <summary>
    /// The invocation still carries an older interface version because the transition broke it.
    /// Its bindings are preserved, the project stays saveable and build/export is blocked until repair.
    /// </summary>
    Outdated = 1
}

/// <summary>
/// One interface transition classified between two revisions of the same definition.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-032.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceVersioningTests.cs.
/// </remarks>
/// <param name="RequiresVersionIncrement">Whether the public contract changed and needs a higher InterfaceVersion.</param>
/// <param name="Reasons">Stable, human readable reasons describing every contract change.</param>
public sealed record QuickWindowInterfaceTransition(
    bool RequiresVersionIncrement,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Classifies local-interface transitions and evaluates, per invocation, whether the new definition
/// still honours its persisted bindings.
/// </summary>
/// <remarks>
/// The public contract is carried by member keys, never by member names: renaming a member keeps every
/// invocation valid. Private members are invisible to invocations and never affect compatibility.
/// `Outdated` is derived from the interface version pair; no fourth identifier and no persisted status
/// field are introduced (FR-027).
///
/// Decisions: DEC-0050, FR-018, FR-027, FR-032.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.4.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceVersioningTests.cs.
/// </remarks>
public static class QuickWindowInterfaceCompatibility
{
    /// <summary>Classifies the transition between two revisions of one definition.</summary>
    public static QuickWindowInterfaceTransition Classify(QuickWindowDefinition before, QuickWindowDefinition after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var reasons = new List<string>();
        var previous = PublicMembers(before);
        var candidate = PublicMembers(after);

        foreach (var (memberKey, member) in previous)
        {
            if (!candidate.TryGetValue(memberKey, out var updated))
            {
                reasons.Add($"Public member '{member.Name}' was removed.");
                continue;
            }

            if (!SameContract(member, updated))
                reasons.Add($"Public member '{updated.Name}' changed its typed contract.");
        }

        foreach (var (memberKey, member) in candidate)
        {
            if (!previous.ContainsKey(memberKey))
                reasons.Add($"Public member '{member.Name}' was added.");
        }

        return new QuickWindowInterfaceTransition(reasons.Count > 0, reasons);
    }

    /// <summary>
    /// Returns the reasons why one invocation can no longer be carried over to the candidate definition.
    /// An empty list means the invocation may be realigned to the candidate interface version.
    /// </summary>
    public static IReadOnlyList<string> BreakingReasonsFor(QuickWindowDefinition candidate, QuickWindowInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(invocation);

        var reasons = new List<string>();
        var members = PublicMembers(candidate);
        var bindings = invocation.Bindings ?? Array.Empty<QuickWindowBinding>();

        foreach (var binding in bindings.Where(binding => !binding.IsAbsent))
        {
            if (!members.TryGetValue(binding.MemberKey, out var member))
            {
                reasons.Add($"Bound port '{binding.MemberKey}' no longer exists in the definition.");
                continue;
            }

            if (member.Access == QuickWindowMemberAccess.Internal)
                reasons.Add($"Port '{member.Name}' is no longer publicly bindable.");
        }

        foreach (var (memberKey, member) in members.Where(entry => entry.Value.Required))
        {
            var binding = bindings.FirstOrDefault(item => item.MemberKey == memberKey);
            if (binding is null || binding.IsAbsent)
                reasons.Add($"Required port '{member.Name}' is not bound.");
        }

        return reasons;
    }

    /// <summary>
    /// Returns the reasons why a transition breaks one invocation, comparing the previous and candidate
    /// definitions so that an already unbound optional port is not reported as a new failure.
    /// </summary>
    public static IReadOnlyList<string> BreakingReasonsFor(
        QuickWindowDefinition previous,
        QuickWindowDefinition candidate,
        QuickWindowInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(invocation);

        var reasons = new List<string>(BreakingReasonsFor(candidate, invocation));
        var previousMembers = PublicMembers(previous);
        var candidateMembers = PublicMembers(candidate);
        var bindings = invocation.Bindings ?? Array.Empty<QuickWindowBinding>();

        foreach (var binding in bindings.Where(binding => !binding.IsAbsent))
        {
            if (!previousMembers.TryGetValue(binding.MemberKey, out var before) ||
                !candidateMembers.TryGetValue(binding.MemberKey, out var updated))
                continue;

            if (!SameContract(before, updated))
                reasons.Add($"Bound port '{updated.Name}' changed its typed contract.");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Gets the derived status of one invocation against its definition.</summary>
    public static QuickWindowInvocationStatus StatusOf(QuickWindowDefinition definition, QuickWindowInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(invocation);
        return invocation.InterfaceVersion == definition.InterfaceVersion
            ? QuickWindowInvocationStatus.Current
            : QuickWindowInvocationStatus.Outdated;
    }

    /// <summary>Returns the invocation realigned on the definition interface version, bindings untouched.</summary>
    public static QuickWindowInvocation Realign(QuickWindowInvocation invocation, QuickWindowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(definition);
        return invocation.InterfaceVersion == definition.InterfaceVersion
            ? invocation
            : invocation with { InterfaceVersion = definition.InterfaceVersion };
    }

    private static IReadOnlyDictionary<Guid, QuickWindowInterfaceMember> PublicMembers(QuickWindowDefinition definition) =>
        definition.EffectiveInterfaceMembers
            .Where(member => member.IsPublic)
            .GroupBy(member => member.MemberKey)
            .ToDictionary(group => group.Key, group => group.First());

    private static bool SameContract(QuickWindowInterfaceMember before, QuickWindowInterfaceMember after) =>
        before.Family == after.Family &&
        before.DataType == after.DataType &&
        before.Access == after.Access &&
        before.Required == after.Required &&
        string.Equals(before.DefaultValue, after.DefaultValue, StringComparison.Ordinal);
}
