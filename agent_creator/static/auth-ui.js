/** Rendu UI premium connexion / inscription (mockup Agentia). */

function renderAuthHubDiagram() {
  const nodes = [
    { x: 130, y: 55, label: "Agent Recherche", color: "#3dd68c", delay: 1 },
    { x: 390, y: 55, label: "Agent Rédaction", color: "#a78bfa", delay: 2 },
    { x: 60, y: 175, label: "Agent Support", color: "#f5b942", delay: 3 },
    { x: 460, y: 175, label: "Agent Données", color: "#60a5fa", delay: 4 },
    { x: 130, y: 295, label: "Agent Email", color: "#f472b6", delay: 5 },
    { x: 320, y: 310, label: "API", color: "#7c6cff", delay: 6 },
    { x: 400, y: 310, label: "Webhook", color: "#8b5cf6", delay: 7 },
    { x: 240, y: 310, label: "Base de données", color: "#6366f1", delay: 8 },
  ];
  return `
    <div class="auth-hub" aria-hidden="true">
      <svg class="auth-hub-svg" viewBox="0 0 520 360" xmlns="http://www.w3.org/2000/svg">
        <defs>
          <linearGradient id="hubCore" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stop-color="#8b5cf6"/>
            <stop offset="100%" stop-color="#6366f1"/>
          </linearGradient>
          <linearGradient id="hubLine" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stop-color="#8b5cf6" stop-opacity="0"/>
            <stop offset="50%" stop-color="#8b5cf6" stop-opacity="0.9"/>
            <stop offset="100%" stop-color="#a78bfa" stop-opacity="0"/>
          </linearGradient>
          <filter id="hubGlow"><feGaussianBlur stdDeviation="4"/><feMerge><feMergeNode/><feMergeNode in="SourceGraphic"/></feMerge></filter>
        </defs>
        <ellipse cx="260" cy="180" rx="200" ry="120" fill="rgba(139,92,246,0.04)" stroke="rgba(139,92,246,0.12)"/>
        ${nodes.map((n) => `<line x1="260" y1="175" x2="${n.x}" y2="${n.y + 8}" stroke="url(#hubLine)" stroke-width="1.5" class="auth-line-flow auth-node-delay-${n.delay}"/>`).join("")}
        <circle cx="260" cy="175" r="42" fill="rgba(139,92,246,0.25)" stroke="url(#hubCore)" stroke-width="2.5" filter="url(#hubGlow)" class="auth-node-pulse"/>
        <text x="260" y="172" text-anchor="middle" fill="#fff" font-size="22" font-weight="700" font-family="DM Sans,sans-serif">A</text>
        <text x="260" y="192" text-anchor="middle" fill="#c4b5fd" font-size="9" font-family="DM Sans,sans-serif">Hub Agentia</text>
        ${nodes.map((n) => `
          <g class="auth-node-pulse auth-node-delay-${n.delay}">
            <rect x="${n.x - 52}" y="${n.y}" width="104" height="28" rx="14" fill="rgba(15,15,25,0.85)" stroke="${n.color}" stroke-width="1.2" stroke-opacity="0.7"/>
            <text x="${n.x}" y="${n.y + 18}" text-anchor="middle" fill="#cbd5e1" font-size="8.5" font-family="DM Sans,sans-serif">${n.label}</text>
          </g>`).join("")}
      </svg>
      <div class="auth-hub-glow"></div>
    </div>`;
}

function renderAuthFeatures() {
  const items = [
    { icon: "◆", title: "Création No-Code", desc: "Interface visuelle intuitive" },
    { icon: "⬡", title: "Orchestration Multi-Agents", desc: "Faites collaborer vos agents" },
    { icon: "▣", title: "Marketplace IA", desc: "Plus de 500 agents prêts à l'emploi" },
    { icon: "☁", title: "Déploiement Cloud", desc: "En 1 clic, partout dans le monde" },
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
  return `<div class="auth-trust-logos">${["Slack", "Notion", "Microsoft", "OpenAI", "AWS"].map((b) => `<span class="auth-trust-logo">${b}</span>`).join("")}</div>`;
}

function renderAuthTrustBar() {
  const items = [
    { title: "Sécurisé & Conforme", desc: "RGPD • ISO 27001 • SOC 2" },
    { title: "Hébergé en Europe", desc: "Données souveraines" },
    { title: "Support 24/7", desc: "Par des experts IA" },
    { title: "Mises à jour continues", desc: "Nouvelles fonctionnalités chaque semaine" },
  ];
  return `<div class="auth-trust-bar">${items.map((i) => `
    <div class="auth-trust-item">
      <strong>${i.title}</strong>
      <span>${i.desc}</span>
    </div>`).join("")}</div>`;
}

function renderAuthOAuth() {
  const providers = [
    { id: "google", label: "Google", icon: "G" },
    { id: "github", label: "GitHub", icon: "⌘" },
    { id: "microsoft", label: "Microsoft", icon: "⊞" },
  ];
  return `
    <div class="auth-oauth">
      <p class="auth-oauth-divider"><span>ou continuer avec</span></p>
      <div class="auth-oauth-buttons auth-oauth-row">
        ${providers.map((p) => `
          <button type="button" class="auth-oauth-btn" data-oauth="${p.id}" title="${p.label}">
            <span class="auth-oauth-icon">${p.icon}</span>
            <span class="auth-oauth-label">${p.label}</span>
          </button>`).join("")}
      </div>
    </div>`;
}

export function renderAuthPremiumLayout({ cardTitle, cardSubtitle, formHtml, footerLink }) {
  return `
    <div class="auth-premium">
      <div class="auth-premium-bg" aria-hidden="true">
        <div class="auth-gradient"></div>
        <div class="auth-nebula"></div>
        <div class="auth-grid-lines"></div>
        <div class="auth-particles">${Array.from({ length: 28 }, (_, i) => `<span style="--i:${i}"></span>`).join("")}</div>
      </div>
      <div class="auth-premium-grid">
        <div class="auth-premium-left">
          <h1 class="auth-hero-title">Construisez des agents IA <em>sans coder.</em></h1>
          <p class="auth-hero-sub">Créez, entraînez, déployez et monétisez vos agents intelligents sur une seule plateforme.</p>
          ${renderAuthFeatures()}
          ${renderAuthHubDiagram()}
          ${renderAuthStats()}
          ${renderAuthTrustLogos()}
        </div>
        <div class="auth-premium-right">
          <div class="auth-card">
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
