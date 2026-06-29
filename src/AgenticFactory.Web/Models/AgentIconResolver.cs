namespace AgenticFactory.Web.Models;

public sealed record AgentIconPresentation(
    string Category,
    string IconClass,
    string IconBg,
    string IconColor);

public static class AgentIconResolver
{
    public static AgentIconPresentation Resolve(string name, string description, string? domainId = null)
    {
        var text = $"{name} {description}".ToLowerInvariant();
        var normalizedDomainId = NormalizeDomainId(domainId, text);

        if (!string.IsNullOrWhiteSpace(normalizedDomainId))
        {
            var byId = StudioCatalog.Domains.FirstOrDefault(d =>
                d.Id.Equals(normalizedDomainId, StringComparison.OrdinalIgnoreCase));
            if (byId is not null)
                return new AgentIconPresentation(byId.Name, byId.Icon, byId.Bg, byId.Color);
        }

        foreach (var domain in StudioCatalog.Domains)
        {
            if (text.Contains(domain.Id, StringComparison.OrdinalIgnoreCase)
                || text.Contains(domain.Name.ToLowerInvariant(), StringComparison.Ordinal)
                || text.Contains(domain.NamePrefix.ToLowerInvariant(), StringComparison.Ordinal))
            {
                return new AgentIconPresentation(domain.Name, domain.Icon, domain.Bg, domain.Color);
            }
        }

        if (ContainsAny(text, "email", "mail", "courrier"))
            return FromDomain("email");
        if (ContainsAny(text, "document", "pdf", "ocr", "word"))
            return FromDomain("documents");
        if (ContainsAny(text, "support", "ticket", "client", "faq"))
            return FromDomain("support");
        if (ContainsAny(text, "market", "campagne", "contenu"))
            return FromDomain("marketing");
        if (ContainsAny(text, "vente", "crm", "prospect", "pipeline"))
            return FromDomain("vente");
        if (ContainsAny(text, "comptab", "facture", "finance"))
            return FromDomain("comptabilite");
        if (ContainsAny(text, "rh", "recrut", "onboarding"))
            return FromDomain("rh");
        if (ContainsAny(text, "jurid", "contrat", "conform"))
            return FromDomain("juridique");
        if (ContainsAny(text, "data", "analyt", "kpi", "dashboard"))
            return FromDomain("data");
        if (ContainsAny(text, "devops", "infra", "ci/cd", "monitor"))
            return FromDomain("devops");
        if (ContainsAny(text, "sécurit", "securit", "cyber", "audit"))
            return FromDomain("cyber");
        if (ContainsAny(text, "e-commerce", "ecommerce", "commande", "stock"))
            return FromDomain("ecommerce");
        if (ContainsAny(text, "capteur", "sensor", "observe"))
            return new AgentIconPresentation("Capteurs", "bi-eye", "#e0f2fe", "#0284c7");
        if (ContainsAny(text, "actionneur", "actuator", "workflow"))
            return new AgentIconPresentation("Automation", "bi-lightning-charge", "#fef3c7", "#b45309");

        return new AgentIconPresentation("Automation", "bi-robot", "#eef2ff", "#6366f1");
    }

    private static AgentIconPresentation FromDomain(string domainId)
    {
        var domain = StudioCatalog.Domains.First(d => d.Id == domainId);
        return new AgentIconPresentation(domain.Name, domain.Icon, domain.Bg, domain.Color);
    }

    private static string? NormalizeDomainId(string? domainId, string text)
    {
        if (!string.IsNullOrWhiteSpace(domainId))
            return domainId.Trim().ToLowerInvariant();

        var match = StudioCatalog.Domains.FirstOrDefault(d =>
            text.Contains(d.Id, StringComparison.OrdinalIgnoreCase));
        return match?.Id;
    }

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
}
