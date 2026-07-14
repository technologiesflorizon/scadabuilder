namespace ScadaBuilderV2.Application.Commands;

/// <summary>Provides the authorization seam used before editor command execution.</summary>
/// <remarks>
/// This tranche deliberately supplies no role model; future access control can replace the
/// policy without changing command implementations or UI surfaces.
/// </remarks>
public interface ICommandAuthorizationPolicy
{
    /// <summary>Returns whether the command is authorized in the current context.</summary>
    bool IsAuthorized(IApplicationCommand command, ApplicationContext context);
}

/// <summary>Default policy that preserves current behavior by authorizing every command.</summary>
public sealed class AllowAllCommandAuthorizationPolicy : ICommandAuthorizationPolicy
{
    private AllowAllCommandAuthorizationPolicy()
    {
    }

    /// <summary>Gets the shared permissive policy instance.</summary>
    public static AllowAllCommandAuthorizationPolicy Instance { get; } = new();

    /// <inheritdoc />
    public bool IsAuthorized(IApplicationCommand command, ApplicationContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        return true;
    }
}
