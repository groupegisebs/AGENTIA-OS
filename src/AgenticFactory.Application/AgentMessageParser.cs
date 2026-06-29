using System.Text.RegularExpressions;

namespace AgenticFactory.Application;

public sealed record ParsedAgentMetadata(
    string Name,
    string Description,
    string? DomainId,
    string? DomainLabel);

public static class AgentMessageParser
{
    private const int MaxNameLength = 80;
    private const int MaxDescriptionLength = 160;

    private static readonly Dictionary<string, string> DomainLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["comptabilite"] = "Comptabilité",
        ["email"] = "Email",
        ["documents"] = "Documents",
        ["support"] = "Support client",
        ["rh"] = "Ressources Humaines",
        ["marketing"] = "Marketing",
        ["vente"] = "Ventes / CRM",
        ["juridique"] = "Juridique",
        ["data"] = "Analyse de données",
        ["devops"] = "DevOps / IT",
        ["cyber"] = "Cybersécurité",
        ["ecommerce"] = "E-Commerce",
        ["industrie"] = "Industrie",
        ["sante"] = "Santé",
        ["education"] = "Éducation",
        ["agriculture"] = "Agriculture",
        ["logistique"] = "Logistique",
        ["immobilier"] = "Immobilier",
        ["banque"] = "Banque & Finance",
        ["medias"] = "Médias & Presse",
        ["qualite"] = "Qualité & ISO",
        ["projet"] = "Gestion de projet",
        ["productivite"] = "Productivité",
        ["custom"] = "Autre domaine"
    };

    private static readonly Dictionary<string, string> DomainDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["comptabilite"] = "Factures, écritures, rapports financiers",
        ["email"] = "Courrier, tri, réponses automatiques",
        ["documents"] = "PDF, Word, OCR et archivage",
        ["support"] = "Tickets, FAQ, escalade",
        ["rh"] = "Recrutement, onboarding, dossiers",
        ["marketing"] = "Campagnes, contenu, analytics",
        ["vente"] = "Prospects, pipeline, relances",
        ["juridique"] = "Contrats, conformité, veille",
        ["data"] = "KPI, tableaux de bord, insights",
        ["devops"] = "CI/CD, infra, monitoring",
        ["cyber"] = "Alertes, audit, conformité",
        ["ecommerce"] = "Commandes, stocks, catalogue",
        ["industrie"] = "Production, qualité, maintenance",
        ["sante"] = "Dossiers patients, protocoles",
        ["education"] = "Cours, évaluations, parcours",
        ["agriculture"] = "Cultures, météo, traçabilité",
        ["logistique"] = "Expéditions, entrepôts, tracking",
        ["immobilier"] = "Biens, baux, visites",
        ["banque"] = "Crédits, conformité, KYC",
        ["medias"] = "Contenus, diffusion, veille",
        ["qualite"] = "Audits, normes, non-conformités",
        ["projet"] = "Planning, risques, livrables",
        ["productivite"] = "Tâches, calendrier, rappels",
        ["custom"] = "Domaine personnalisé pour ce projet"
    };

    private static readonly Regex TimestampNamePattern = new(
        @"^Agent\s+\d{14}$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ParsedAgentMetadata Parse(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new ParsedAgentMetadata("Agent métier", "Collaborateur IA configuré via Agent Factory Studio.", null, null);

        string? proposedName = null;
        string? mission = null;
        string? domainTag = null;

        foreach (var line in message.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryExtractLabel(line, "Nom proposé", out var name))
                proposedName = name;
            else if (TryExtractLabel(line, "Mission", out var m))
                mission ??= m;
            else if (TryExtractLabel(line, "Domaine (tag)", out var d))
                domainTag ??= d;
            else if (TryExtractLabel(line, "Domaine métier", out var dm))
                domainTag ??= dm;
        }

        var domainLabel = ResolveDomainLabel(domainTag);
        var resolvedName = ResolveName(proposedName, mission, domainLabel);
        var description = ResolveDescription(mission, domainTag, message);

        return new ParsedAgentMetadata(resolvedName, description, NormalizeDomainId(domainTag), domainLabel);
    }

    private static bool TryExtractLabel(string line, string label, out string value)
    {
        value = string.Empty;
        var prefix = $"- {label}";
        if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = line[prefix.Length..].TrimStart();
        if (rest.StartsWith(':'))
            rest = rest[1..].Trim();

        value = rest.Trim().Trim('"');
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? ResolveDomainLabel(string? domainTag)
    {
        if (string.IsNullOrWhiteSpace(domainTag))
            return null;

        var trimmed = domainTag.Trim();
        if (DomainLabels.TryGetValue(trimmed, out var byId))
            return byId;

        var match = DomainLabels.Values.FirstOrDefault(v =>
            v.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        return match ?? trimmed;
    }

    private static string? NormalizeDomainId(string? domainTag)
    {
        if (string.IsNullOrWhiteSpace(domainTag))
            return null;

        var trimmed = domainTag.Trim();
        if (DomainLabels.ContainsKey(trimmed))
            return trimmed.ToLowerInvariant();

        var byLabel = DomainLabels.FirstOrDefault(x =>
            x.Value.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(byLabel.Key) ? trimmed.ToLowerInvariant() : byLabel.Key;
    }

    private static string ResolveName(string? proposedName, string? mission, string? domainLabel)
    {
        if (IsMeaningfulName(proposedName))
            return Truncate(proposedName!.Trim(), MaxNameLength);

        if (!string.IsNullOrWhiteSpace(mission))
        {
            var fromMission = TruncateFirstLine(mission, 60);
            if (IsMeaningfulName(fromMission))
                return fromMission;
        }

        if (!string.IsNullOrWhiteSpace(domainLabel))
            return Truncate($"{domainLabel} Agent", MaxNameLength);

        return "Agent métier";
    }

    private static string ResolveDescription(string? mission, string? domainTag, string message)
    {
        if (!string.IsNullOrWhiteSpace(mission))
            return TruncateFirstLine(mission, MaxDescriptionLength);

        var domainId = NormalizeDomainId(domainTag);
        if (domainId is not null && DomainDescriptions.TryGetValue(domainId, out var domainDesc))
            return domainDesc;

        foreach (var line in message.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryExtractLabel(line, "Description complémentaire", out var extra))
                return TruncateFirstLine(extra, MaxDescriptionLength);
            if (TryExtractLabel(line, "Contexte métier", out var ctx))
                return TruncateFirstLine(ctx, MaxDescriptionLength);
        }

        return "Collaborateur IA configuré via Agent Factory Studio.";
    }

    private static bool IsMeaningfulName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed.Equals("Chat Generated Agent", StringComparison.OrdinalIgnoreCase))
            return false;
        if (TimestampNamePattern.IsMatch(trimmed))
            return false;

        return trimmed.Length >= 3;
    }

    private static string TruncateFirstLine(string text, int maxLength)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? text;
        return Truncate(line.Trim(), maxLength);
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..Math.Max(0, maxLength - 1)].TrimEnd() + "…";
}
