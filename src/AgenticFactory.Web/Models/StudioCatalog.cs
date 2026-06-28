namespace AgenticFactory.Web.Models;

public record StudioDomainItem(
    string Id,
    string Icon,
    string Bg,
    string Color,
    string Name,
    string Desc,
    string NamePrefix,
    string? DefaultAgent = null);

public static class StudioCatalog
{
    public static IReadOnlyList<StudioDomainItem> Domains { get; } =
    [
        new("comptabilite", "bi-calculator", "#fef3c7", "#d97706", "Comptabilité", "Factures, écritures, rapports financiers", "Finance"),
        new("email", "bi-envelope", "#eef2ff", "#6366f1", "Email", "Courrier, tri, réponses automatiques", "Email", "Email Invoice Agent"),
        new("documents", "bi-file-earmark-text", "#fce7f3", "#db2777", "Documents", "PDF, Word, OCR et archivage", "Document"),
        new("support", "bi-headset", "#dcfce7", "#16a34a", "Support client", "Tickets, FAQ, escalade", "Support"),
        new("rh", "bi-people", "#dbeafe", "#2563eb", "Ressources Humaines", "Recrutement, onboarding, dossiers", "HR"),
        new("marketing", "bi-megaphone", "#f3e8ff", "#9333ea", "Marketing", "Campagnes, contenu, analytics", "Marketing"),
        new("vente", "bi-briefcase", "#ecfdf5", "#059669", "Ventes / CRM", "Prospects, pipeline, relances", "Sales"),
        new("juridique", "bi-file-earmark-ruled", "#f1f5f9", "#475569", "Juridique", "Contrats, conformité, veille", "Legal"),
        new("data", "bi-bar-chart", "#e0e7ff", "#4f46e5", "Analyse de données", "KPI, tableaux de bord, insights", "Analytics"),
        new("devops", "bi-cloud", "#cffafe", "#0891b2", "DevOps / IT", "CI/CD, infra, monitoring", "DevOps"),
        new("cyber", "bi-shield-lock", "#fee2e2", "#dc2626", "Cybersécurité", "Alertes, audit, conformité", "Security"),
        new("ecommerce", "bi-cart3", "#ffedd5", "#ea580c", "E-Commerce", "Commandes, stocks, catalogue", "Commerce"),
        new("industrie", "bi-gear-wide-connected", "#e2e8f0", "#334155", "Industrie", "Production, qualité, maintenance", "Industry"),
        new("sante", "bi-heart-pulse", "#ffe4e6", "#e11d48", "Santé", "Dossiers patients, protocoles", "Health"),
        new("education", "bi-mortarboard", "#ede9fe", "#7c3aed", "Éducation", "Cours, évaluations, parcours", "Education"),
        new("agriculture", "bi-flower1", "#ecfccb", "#65a30d", "Agriculture", "Cultures, météo, traçabilité", "Agri"),
        new("logistique", "bi-truck", "#fef9c3", "#ca8a04", "Logistique", "Expéditions, entrepôts, tracking", "Logistics"),
        new("immobilier", "bi-building", "#fae8ff", "#c026d3", "Immobilier", "Biens, baux, visites", "RealEstate"),
        new("banque", "bi-bank", "#dbeafe", "#1d4ed8", "Banque & Finance", "Crédits, conformité, KYC", "Banking"),
        new("medias", "bi-camera-reels", "#fce7f3", "#be185d", "Médias & Presse", "Contenus, diffusion, veille", "Media"),
        new("qualite", "bi-patch-check", "#d1fae5", "#047857", "Qualité & ISO", "Audits, normes, non-conformités", "Quality"),
        new("projet", "bi-kanban", "#e0f2fe", "#0284c7", "Gestion de projet", "Planning, risques, livrables", "Project"),
        new("productivite", "bi-lightning-charge", "#fef3c7", "#b45309", "Productivité", "Tâches, calendrier, rappels", "Productivity"),
        new("custom", "bi-sliders", "#f8fafc", "#64748b", "Autre domaine", "Domaine personnalisé pour ce projet", "Custom")
    ];
}
