/** Rendu UI premium connexion / inscription — fidèle au mockup Agentia. */

const FEATURE_ICONS = {
  nocode: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><path d="M14 17h7"/></svg>`,
  orchestration: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="12" cy="5" r="2.5"/><circle cx="5" cy="19" r="2.5"/><circle cx="19" cy="19" r="2.5"/><path d="M12 7.5v3M9.5 13l-3 4M14.5 13l3 4"/></svg>`,
  marketplace: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M3 9l2-4h14l2 4"/><path d="M5 9v10a1 1 0 001 1h12a1 1 0 001-1V9"/><path d="M9 14h6"/></svg>`,
  cloud: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M7 18h10a4 4 0 000-8 5.5 5.5 0 00-10.6-1.5A3.5 3.5 0 007 18z"/></svg>`,
};

const TRUST_ICONS = {
  secure: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M12 3l8 4v6c0 5-3.5 8.5-8 9-4.5-.5-8-4-8-9V7l8-4z"/><path d="M9 12l2 2 4-4"/></svg>`,
  europe: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a14 14 0 010 18M12 3a14 14 0 000 18"/></svg>`,
  support: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M4 14a8 8 0 0116 0"/><path d="M12 14v4M8 18h8"/><circle cx="12" cy="8" r="3"/></svg>`,
  updates: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M4 4v5h5"/><path d="M20 20v-5h-5"/><path d="M20 9A8 8 0 006.5 6.5L4 9M4 15a8 8 0 0013.5 2.5L20 15"/></svg>`,
};

const OAUTH_ICONS = {
  google: `<svg viewBox="0 0 24 24"><path fill="#4285F4" d="M22 12c0-.68-.06-1.33-.17-1.95H12v3.69h5.69a4.68 4.68 0 01-2.04 3.07v2.55h3.29c1.93-1.78 3.04-4.4 3.04-7.36z"/><path fill="#34A853" d="M12 22c2.76 0 5.08-.91 6.74-2.47l-3.29-2.55c-.91.61-2.07.97-3.45.97-2.65 0-4.9-1.79-5.7-4.19H3.18v2.63A10 10 0 0012 22z"/><path fill="#FBBC05" d="M6.3 13.76A5.99 5.99 0 016 12c0-.62.11-1.22.3-1.76V7.61H3.18A10 10 0 003 12a10 10 0 002.18 4.39l3.12-2.63z"/><path fill="#EA4335" d="M12 5.38c1.5 0 2.85.52 3.91 1.53l2.93-2.93C17.08 2.55 14.76 1.5 12 1.5 7.7 1.5 3.98 3.98 3.18 7.61l3.12 2.63C7.1 7.17 9.35 5.38 12 5.38z"/></svg>`,
  github: `<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.58 2 12.26c0 4.52 2.87 8.35 6.84 9.7.5.1.68-.22.68-.49 0-.24-.01-.87-.01-1.7-2.78.62-3.37-1.36-3.37-1.36-.45-1.17-1.11-1.48-1.11-1.48-.91-.64.07-.63.07-.63 1 .07 1.53 1.05 1.53 1.05.9 1.56 2.36 1.11 2.94.85.09-.66.35-1.11.63-1.37-2.22-.26-4.56-1.14-4.56-5.07 0-1.12.39-2.03 1.03-2.75-.1-.26-.45-1.3.1-2.7 0 0 .84-.27 2.75 1.05A9.2 9.2 0 0112 6.84c.84 0 1.68.11 2.47.33 1.9-1.32 2.74-1.05 2.74-1.05.55 1.4.2 2.44.1 2.7.64.72 1.03 1.63 1.03 2.75 0 3.94-2.34 4.8-4.57 5.06.36.32.68.94.68 1.9 0 1.37-.01 2.47-.01 2.8 0 .27.18.59.69.49A10.02 10.02 0 0022 12.26C22 6.58 17.52 2 12 2z"/></svg>`,
  microsoft: `<svg viewBox="0 0 24 24"><rect fill="#F25022" x="3" y="3" width="8.5" height="8.5"/><rect fill="#7FBA00" x="12.5" y="3" width="8.5" height="8.5"/><rect fill="#00A4EF" x="3" y="12.5" width="8.5" height="8.5"/><rect fill="#FFB900" x="12.5" y="12.5" width="8.5" height="8.5"/></svg>`,
};

function renderAuthHubDiagram() {
  const agents = [
    { x: 115, y: 42, label: "Agent Recherche", color: "#3dd68c", delay: 1 },
    { x: 405, y: 42, label: "Agent Rédaction", color: "#a78bfa", delay: 2 },
    { x: 55, y: 155, label: "Agent Support", color: "#f5b942", delay: 3 },
    { x: 465, y: 155, label: "Agent Données", color: "#60a5fa", delay: 4 },
    { x: 115, y: 268, label: "Agent Email", color: "#f472b6", delay: 5 },
  ];
  const infra = [
    { x: 220, y: 292, label: "Base de données", type: "db", delay: 6 },
    { x: 310, y: 292, label: "API", type: "pill", color: "#7c6cff", delay: 7 },
    { x: 390, y: 292, label: "Webhook", type: "pill", color: "#8b5cf6", delay: 8 },
  ];
  return `
    <div class="auth-hub" aria-hidden="true">
      <svg class="auth-hub-svg" viewBox="0 0 520 330" xmlns="http://www.w3.org/2000/svg">
        <defs>
          <linearGradient id="hubCore" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stop-color="#a78bfa"/>
            <stop offset="100%" stop-color="#7c3aed"/>
          </linearGradient>
          <linearGradient id="hubLine" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stop-color="#8b5cf6" stop-opacity="0"/>
            <stop offset="50%" stop-color="#8b5cf6" stop-opacity="1"/>
            <stop offset="100%" stop-color="#c4b5fd" stop-opacity="0"/>
          </linearGradient>
          <linearGradient id="hubPlatform" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%" stop-color="rgba(139,92,246,0.35)"/>
            <stop offset="100%" stop-color="rgba(99,102,241,0.08)"/>
          </linearGradient>
          <filter id="hubGlow"><feGaussianBlur stdDeviation="5"/><feMerge><feMergeNode/><feMergeNode in="SourceGraphic"/></feMerge></filter>
        </defs>
        <!-- Plateforme isométrique -->
        <polygon points="260,195 130,250 390,250" fill="url(#hubPlatform)" stroke="rgba(139,92,246,0.35)" stroke-width="1"/>
        <polygon points="130,250 260,280 390,250" fill="rgba(99,102,241,0.12)" stroke="rgba(139,92,246,0.2)" stroke-width="1"/>
        <!-- Lignes de connexion -->
        ${agents.map((n) => `<line x1="260" y1="175" x2="${n.x}" y2="${n.y + 14}" stroke="url(#hubLine)" stroke-width="1.5" class="auth-line-flow auth-node-delay-${n.delay}"/>`).join("")}
        ${infra.map((n) => `<line x1="260" y1="210" x2="${n.x}" y2="${n.y}" stroke="url(#hubLine)" stroke-width="1.5" class="auth-line-flow auth-node-delay-${n.delay}"/>`).join("")}
        <!-- Hub central -->
        <circle cx="260" cy="175" r="36" fill="rgba(124,58,237,0.3)" stroke="url(#hubCore)" stroke-width="2.5" filter="url(#hubGlow)" class="auth-node-pulse"/>
        <circle cx="260" cy="175" r="28" fill="url(#hubCore)" opacity="0.9"/>
        <text x="260" y="182" text-anchor="middle" fill="#fff" font-size="20" font-weight="700" font-family="DM Sans,sans-serif">A</text>
        <!-- Agents -->
        ${agents.map((n) => `
          <g class="auth-node-pulse auth-node-delay-${n.delay}">
            <rect x="${n.x - 54}" y="${n.y}" width="108" height="30" rx="15" fill="rgba(10,10,18,0.92)" stroke="${n.color}" stroke-width="1.2" stroke-opacity="0.75"/>
            <text x="${n.x}" y="${n.y + 19}" text-anchor="middle" fill="#e2e8f0" font-size="9" font-family="DM Sans,sans-serif">${n.label}</text>
          </g>`).join("")}
        <!-- Infra -->
        ${infra.map((n) => {
          if (n.type === "db") {
            return `
              <g class="auth-node-pulse auth-node-delay-${n.delay}">
                <ellipse cx="${n.x}" cy="${n.y + 4}" rx="14" ry="5" fill="rgba(99,102,241,0.3)" stroke="#6366f1" stroke-width="1"/>
                <rect x="${n.x - 14}" y="${n.y - 8}" width="28" height="14" rx="2" fill="rgba(10,10,18,0.92)" stroke="#6366f1" stroke-width="1.2"/>
                <ellipse cx="${n.x}" cy="${n.y - 8}" rx="14" ry="5" fill="rgba(99,102,241,0.2)" stroke="#6366f1" stroke-width="1"/>
                <text x="${n.x}" y="${n.y + 28}" text-anchor="middle" fill="#94a3b8" font-size="8" font-family="DM Sans,sans-serif">${n.label}</text>
              </g>`;
          }
          return `
            <g class="auth-node-pulse auth-node-delay-${n.delay}">
              <rect x="${n.x - 36}" y="${n.y - 4}" width="72" height="26" rx="13" fill="rgba(10,10,18,0.92)" stroke="${n.color}" stroke-width="1.2"/>
              <text x="${n.x}" y="${n.y + 13}" text-anchor="middle" fill="#cbd5e1" font-size="9" font-family="DM Sans,sans-serif">${n.label}</text>
            </g>`;
        }).join("")}
      </svg>
      <div class="auth-hub-glow"></div>
    </div>`;
}

function renderAuthFeatures() {
  const items = [
    { icon: FEATURE_ICONS.nocode, title: "Création No-Code", desc: "Interface visuelle intuitive" },
    { icon: FEATURE_ICONS.orchestration, title: "Orchestration Multi-Agents", desc: "Faites collaborer vos agents" },
    { icon: FEATURE_ICONS.marketplace, title: "Marketplace IA", desc: "Plus de 500 agents prêts à l'emploi" },
    { icon: FEATURE_ICONS.cloud, title: "Déploiement Cloud", desc: "En 1 clic, partout dans le monde" },
  ];
  return `<ul class="auth-features">${items.map((t) => `
    <li>
      <span class="auth-feature-icon">${t.icon}</span>
      <span class="auth-feature-text">
        <strong>${t.title}</strong>
        <small>${t.desc}</small>
      </span>
    </li>`).join("")}</ul>`;
}

function renderAuthStats() {
  const stats = [
    { value: "10 000+", label: "Agents créés" },
    { value: "500+", label: "Templates" },
    { value: "50+", label: "Intégrations" },
    { value: "1 clic", label: "Déploiement" },
  ];
  return `<div class="auth-stats">${stats.map((s) => `
    <div class="auth-stat">
      <span class="auth-stat-value">${s.value}</span>
      <span class="auth-stat-label">${s.label}</span>
    </div>`).join("")}</div>`;
}

function renderAuthTrustLogos() {
  const brands = ["Slack", "Notion", "Microsoft", "OpenAI", "AWS"];
  return `
    <div class="auth-trust-section">
      <p class="auth-trust-caption">Rejoignez les entreprises innovantes qui nous font confiance</p>
      <div class="auth-trust-logos">${brands.map((b) => `<span class="auth-trust-logo">${b}</span>`).join("")}
      </div>
    </div>`;
}

function renderAuthTrustBar() {
  const items = [
    { icon: TRUST_ICONS.secure, title: "Sécurisé & Conforme", desc: "RGPD • ISO 27001 • SOC 2" },
    { icon: TRUST_ICONS.europe, title: "Hébergé en Europe", desc: "Données souveraines" },
    { icon: TRUST_ICONS.support, title: "Support 24/7", desc: "Par des experts IA" },
    { icon: TRUST_ICONS.updates, title: "Mises à jour continues", desc: "Nouvelles fonctionnalités chaque semaine" },
  ];
  return `<div class="auth-trust-bar">${items.map((i) => `
    <div class="auth-trust-item">
      <span class="auth-trust-icon">${i.icon}</span>
      <div class="auth-trust-copy">
        <strong>${i.title}</strong>
        <span>${i.desc}</span>
      </div>
    </div>`).join("")}</div>`;
}

function renderAuthOAuth() {
  const providers = [
    { id: "google", label: "Google", icon: OAUTH_ICONS.google },
    { id: "github", label: "GitHub", icon: OAUTH_ICONS.github },
    { id: "microsoft", label: "Microsoft", icon: OAUTH_ICONS.microsoft },
  ];
  return `
    <div class="auth-oauth">
      <p class="auth-oauth-divider"><span>ou continuer avec</span></p>
      <div class="auth-oauth-buttons auth-oauth-row">
        ${providers.map((p) => `
          <button type="button" class="auth-oauth-btn" data-oauth="${p.id}" title="${p.label}" disabled aria-disabled="true">
            <span class="auth-oauth-icon">${p.icon}</span>
            <span class="auth-oauth-label">${p.label}</span>
          </button>`).join("")}
      </div>
      <p class="auth-oauth-hint">Connexion sociale bientôt disponible</p>
    </div>`;
}

export function renderAuthPremiumLayout({ cardTitle, cardSubtitle, formHtml, footerLink }) {
  return `
    <div class="auth-premium">
      <div class="auth-premium-bg" aria-hidden="true">
        <div class="auth-gradient"></div>
        <div class="auth-nebula"></div>
        <div class="auth-grid-lines"></div>
        <div class="auth-particles">${Array.from({ length: 32 }, (_, i) => `<span style="--i:${i}"></span>`).join("")}</div>
      </div>
      <div class="auth-premium-grid">
        <div class="auth-premium-left">
          <span class="auth-badge">Plateforme Agentic AI</span>
          <h1 class="auth-hero-title">Construisez des agents IA <em>sans coder.</em></h1>
          <p class="auth-hero-sub">Créez, entraînez, déployez et monétisez vos agents intelligents sur une seule plateforme.</p>
          ${renderAuthFeatures()}
          ${renderAuthHubDiagram()}
          ${renderAuthStats()}
          ${renderAuthTrustLogos()}
        </div>
        <div class="auth-premium-right">
          <div class="auth-card">
            <div class="auth-card-glow" aria-hidden="true"></div>
            <div class="auth-card-header">
              <h2>${cardTitle}</h2>
              ${cardSubtitle ? `<p>${cardSubtitle}</p>` : ""}
            </div>
            ${formHtml}
            ${footerLink}
            ${renderAuthOAuth()}
          </div>
        </div>
      </div>
      ${renderAuthTrustBar()}
    </div>`;
}
