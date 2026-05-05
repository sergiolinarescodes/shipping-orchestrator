namespace ShippingOrchestrator.Modules.Abstractions;

/// <summary>
/// Per-connector source of the customer-facing "what do I need to do to connect this platform"
/// content rendered by the customer SPA when a tenant clicks Connect on
/// <c>ConnectionsPage</c>. Lives next to the connector implementation so adding a new
/// connector ships its own copy and required-input metadata in one place — PublicApi stays
/// connector-agnostic. Resolved via <see cref="ConnectorRegistration.InstallGuideFactory"/>.
/// </summary>
public interface IInstallGuideProvider
{
    InstallGuide GetGuide(InstallGuideContext context);
}

/// <summary>
/// Minimal customer-facing checklist + form fields a tenant needs to complete an install.
/// Steps are short bullets — copy is intentionally terse, the SPA renders them as a small
/// numbered list, not a manual.
/// </summary>
public sealed record InstallGuide(
    string Title,
    IReadOnlyList<string> Steps,
    IReadOnlyList<InstallInputField> Inputs,
    string? HelpUrl = null);

/// <summary>
/// One required (or optional) input the tenant supplies before kicking off OAuth.
/// </summary>
public sealed record InstallInputField(
    string Key,
    string Label,
    string? Placeholder = null,
    bool Required = true,
    string? HelpText = null);

/// <summary>
/// Per-request context handed to a guide provider. <see cref="CallbackBaseUrl"/> is derived
/// from the incoming HTTP request so prod and localhost share one code path.
/// </summary>
public sealed record InstallGuideContext(
    Guid TenantId,
    ConnectorMode Mode,
    string CallbackBaseUrl);
