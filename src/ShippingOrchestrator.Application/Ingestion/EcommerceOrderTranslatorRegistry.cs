using ShippingOrchestrator.Modules.Abstractions.Ecommerce;

namespace ShippingOrchestrator.Application.Ingestion;

public interface IEcommerceOrderTranslatorRegistry
{
    IEcommerceOrderTranslator Resolve(string connectorCode);
    bool TryResolve(string connectorCode, out IEcommerceOrderTranslator? translator);
}

internal sealed class EcommerceOrderTranslatorRegistry(IEnumerable<IEcommerceOrderTranslator> translators)
    : IEcommerceOrderTranslatorRegistry
{
    private readonly Dictionary<string, IEcommerceOrderTranslator> _byCode =
        translators.ToDictionary(t => t.ConnectorCode, StringComparer.OrdinalIgnoreCase);

    public IEcommerceOrderTranslator Resolve(string connectorCode) =>
        _byCode.TryGetValue(connectorCode, out var t)
            ? t
            : throw new KeyNotFoundException($"No ecommerce order translator registered for '{connectorCode}'.");

    public bool TryResolve(string connectorCode, out IEcommerceOrderTranslator? translator)
    {
        var ok = _byCode.TryGetValue(connectorCode, out var t);
        translator = t;
        return ok;
    }
}
