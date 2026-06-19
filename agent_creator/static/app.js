/**
 * Agentia — Bureau d'architecture digitale
 * SPA vanilla JS avec routage côté client + authentification SaaS
 */

const AUTH_TOKEN_KEY = "agentia_token";

/** URL API absolue depuis la racine du site (évite les 404 relatifs type /inscription/auth/...). */
function apiUrl(path) {
  if (/^https?:\/\//i.test(path)) return path;
  const base = (document.querySelector('meta[name="agentia-api-base"]')?.content || "")
    .replace(/\/$/, "");
  const normalized = path.startsWith("/") ? path : `/${path}`;
  return `${window.location.origin}${base}${normalized}`;
}

function getToken() {
  return localStorage.getItem(AUTH_TOKEN_KEY);
}

function setToken(token) {
  if (token) localStorage.setItem(AUTH_TOKEN_KEY, token);
  else localStorage.removeItem(AUTH_TOKEN_KEY);
}

const API = {
  async json(path, options = {}) {
    const headers = { "Content-Type": "application/json", ...options.headers };
    const token = getToken();
    if (token) headers.Authorization = `Bearer ${token}`;
    const res = await fetch(apiUrl(path), { ...options, headers });
    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: res.statusText }));
      throw new Error(typeof err.detail === "string" ? err.detail : JSON.stringify(err.detail));
    }
    return res.json();
  },
  register(data) {
    return this.json("/auth/register", { method: "POST", body: JSON.stringify(data) });
  },
  login(data) {
    return this.json("/auth/login", { method: "POST", body: JSON.stringify(data) });
  },
  getMe() {
    return this.json("/auth/me");
  },
  createConversation(message) {
    return this.json("/conversations", { method: "POST", body: JSON.stringify({ message }) });
  },
  sendMessage(id, message) {
    return this.json(`/conversations/${id}/messages`, { method: "POST", body: JSON.stringify({ message }) });
  },
  getBlueprint(id) {
    return this.json(`/conversations/${id}/blueprint`);
  },
  getEstimates(id) {
    return this.json(`/conversations/${id}/estimates`);
  },
  deploy(id) {
    return this.json(`/conversations/${id}/deploy`, { method: "POST" });
  },
  confirmDeploy(id, paymentCode) {
    return this.json(`/conversations/${id}/deploy/confirm`, {
      method: "POST",
      body: JSON.stringify({ payment_code: paymentCode }),
    });
  },
  getPlans() {
    return this.json("/plans");
  },
  subscribe(plan) {
    return this.json("/organizations/me/subscribe", { method: "POST", body: JSON.stringify({ plan }) });
  },
  confirmBilling(paymentCode) {
    return this.json("/billing/confirm", { method: "POST", body: JSON.stringify({ payment_code: paymentCode }) });
  },
  pollPaymentStatus(code) {
    return this.json(`/billing/payments/${encodeURIComponent(code)}/status`);
  },
  getBilling(orgId) {
    return this.json(`/organizations/${orgId}/billing`);
  },
  analyzeArchitect(description) {
    return this.json("/architect/analyze", { method: "POST", body: JSON.stringify({ description }) });
  },
};

const STATE = {
  userName: "",
  orgName: "",
  orgId: null,
  planName: "Free",
  pendingNeed: "",
  conversationId: null,
  blueprint: null,
  estimates: null,
  architectResult: null,
  architectInput: "",
  cockpitSelected: null,
  deployments: [],
  billingSummary: null,
  plans: [],
};

const EXAMPLE_CHIPS = [
  "Je veux gérer automatiquement mes emails clients",
  "Je veux extraire les données des factures PDF",
  "Je veux suivre mes prospects",
  "Je veux automatiser l'approbation des dépenses",
  "Je veux un assistant RH",
];

const MARKETPLACE = [
  { id: "email-crm", category: "CRM", icon: "✉️", title: "Gestion emails clients", desc: "Tri, réponse et suivi automatique des demandes entrantes.", need: "Je veux gérer automatiquement mes emails clients" },
  { id: "factures", category: "Comptabilité", icon: "📄", title: "Extraction factures PDF", desc: "Capture et structuration des données de vos factures fournisseurs.", need: "Je veux extraire les données des factures PDF" },
  { id: "prospects", category: "CRM", icon: "🎯", title: "Suivi prospects", desc: "Relances intelligentes et scoring de vos opportunités commerciales.", need: "Je veux suivre mes prospects" },
  { id: "depenses", category: "Finance", icon: "💳", title: "Validation des dépenses", desc: "Circuit d'approbation fluide pour notes de frais et achats.", need: "Je veux automatiser l'approbation des dépenses" },
  { id: "rh", category: "RH", icon: "👥", title: "Assistant RH", desc: "Réponses aux questions employés et gestion des demandes internes.", need: "Je veux un assistant RH" },
  { id: "contrats", category: "Juridique", icon: "⚖️", title: "Analyse de contrats", desc: "Extraction des clauses clés et alertes de renouvellement.", need: "Je veux analyser automatiquement mes contrats" },
  { id: "formation", category: "Éducation", icon: "📚", title: "Assistant pédagogique", desc: "Support aux apprenants et suivi des parcours de formation.", need: "Je veux un assistant pour les questions des apprenants" },
  { id: "rapprochement", category: "Finance", icon: "🏦", title: "Rapprochement bancaire", desc: "Conciliation automatique entre relevés et écritures comptables.", need: "Je veux automatiser le rapprochement bancaire" },
];

const CATEGORIES = ["Tous", "Finance", "RH", "CRM", "Comptabilité", "Juridique", "Éducation"];

const BUSINESS_LABELS = {
  integration: "Integrations",
  ai: "Intelligence métier",
  workflow: "Orchestration",
  agent: "Assistant intelligent",
  api: "Connecteur de données",
  storage: "Archivage sécurisé",
  notification: "Notifications",
};

function businessLabel(type) {
  return BUSINESS_LABELS[type] || "Composant métier";
}

function businessCapability(component) {
  const map = {
    "Connecteur email": "Lit et traite vos emails entrants",
    "Moteur d'extraction IA": "Extrait les informations clés de vos documents",
    "Workflow d'orchestration": "Enchaîne les étapes de traitement automatiquement",
    "Connecteur comptable": "Synchronise avec votre outil comptable",
    "Agent de validation": "Vérifie et valide les données avant envoi",
    "Tableau de bord": "Visualise l'activité et les indicateurs clés",
  };
  return map[component.name] || component.description.replace(/API|LLM|OCR|n8n|Temporal|Azure|Kubernetes|Docker|RabbitMQ|GPT|Gemini/gi, "").trim() || `Automatise : ${component.name.toLowerCase()}`;
}

function complexityClass(label) {
  if (label === "Faible") return "low";
  if (label === "Moyenne") return "mid";
  return "high";
}

function escapeHtml(str) {
  return String(str)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function showToast(msg) {
  const el = document.getElementById("toast");
  el.textContent = msg;
  el.hidden = false;
  setTimeout(() => { el.hidden = true; }, 4000);
}

function navigate(path, params = {}) {
  if (params.need) STATE.pendingNeed = params.need;
  if (params.conversationId) STATE.conversationId = params.conversationId;
  history.pushState(params, "", path);
  render();
}

function parseRoute() {
  const path = window.location.pathname;
  const solutionMatch = path.match(/^\/solution\/([^/]+)/);
  const editorMatch = path.match(/^\/editor\/([^/]+)/);
  if (solutionMatch) return { name: "solution", id: solutionMatch[1] };
  if (editorMatch) return { name: "editor", id: editorMatch[1] };
  if (path === "/workspace") return { name: "workspace" };
  if (path === "/cockpit") return { name: "cockpit" };
  if (path === "/marketplace") return { name: "marketplace" };
  if (path === "/architect") return { name: "architect" };
  if (path === "/inscription") return { name: "inscription" };
  if (path === "/connexion") return { name: "connexion" };
  if (path === "/documentation") return { name: "docs" };
  if (path === "/mon-compte") return { name: "account" };
  if (path === "/abonnement") return { name: "subscription" };
  if (path === "/paiement/succes") return { name: "payment-success" };
  if (path === "/paiement/annule") return { name: "payment-cancel" };
  return { name: "home" };
}

function requireAuth(routeName) {
  const publicRoutes = new Set(["home", "inscription", "connexion", "docs", "marketplace", "architect", "payment-success", "payment-cancel"]);
  if (!getToken() && !publicRoutes.has(routeName)) {
    navigate("/connexion");
    return false;
  }
  return true;
}

function setActiveNav(routeName) {
  document.querySelectorAll(".topnav a").forEach((a) => {
    const href = a.getAttribute("data-nav");
    a.classList.toggle("active", href === `/${routeName}` || (routeName === "home" && href === "/"));
  });
}

async function loadUserContext() {
  if (!getToken()) return;
  try {
    const me = await API.getMe();
    STATE.userName = me.user.full_name.split(" ")[0] || me.user.full_name;
    STATE.orgName = me.organization.name;
    STATE.orgId = me.organization.id;
    STATE.planName = me.plan_name;
  } catch {
    setToken(null);
  }
}

function renderAuthNav() {
  if (getToken()) {
    return `<a href="/mon-compte" data-nav="/mon-compte">Mon compte</a>
      <button type="button" class="btn btn-ghost btn-sm" id="btn-logout">Déconnexion</button>`;
  }
  return `<a href="/connexion" data-nav="/connexion" class="nav-link-login">Connexion</a>
    <a href="/inscription" data-nav="/inscription" class="btn btn-primary btn-sm btn-glow">Commencer gratuitement</a>`;
}

function renderAuthIllustration() {
  return `
    <div class="auth-visual" aria-hidden="true">
      <svg class="auth-visual-svg" viewBox="0 0 520 380" xmlns="http://www.w3.org/2000/svg">
        <defs>
          <linearGradient id="authGrad1" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stop-color="#7c6cff" stop-opacity="0.9"/>
            <stop offset="100%" stop-color="#3dd68c" stop-opacity="0.6"/>
          </linearGradient>
          <linearGradient id="authGradLine" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stop-color="#7c6cff" stop-opacity="0"/>
            <stop offset="50%" stop-color="#7c6cff" stop-opacity="0.8"/>
            <stop offset="100%" stop-color="#a78bfa" stop-opacity="0"/>
          </linearGradient>
          <filter id="authGlow"><feGaussianBlur stdDeviation="3" result="b"/><feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
        </defs>
        <rect x="60" y="40" width="400" height="240" rx="16" fill="rgba(124,108,255,0.06)" stroke="rgba(124,108,255,0.25)" stroke-width="1"/>
        <line x1="260" y1="60" x2="260" y2="260" stroke="rgba(124,108,255,0.15)" stroke-width="1" stroke-dasharray="4 6"/>
        <line x1="120" y1="160" x2="400" y2="160" stroke="rgba(124,108,255,0.12)" stroke-width="1" stroke-dasharray="4 6"/>
        <circle cx="260" cy="160" r="36" fill="rgba(124,108,255,0.2)" stroke="url(#authGrad1)" stroke-width="2" filter="url(#authGlow)" class="auth-node-pulse"/>
        <text x="260" y="165" text-anchor="middle" fill="#eef0f6" font-size="11" font-family="DM Sans,sans-serif">Architecte IA</text>
        <circle cx="120" cy="100" r="22" fill="rgba(61,214,140,0.15)" stroke="#3dd68c" stroke-width="1.5" class="auth-node-pulse auth-node-delay-1"/>
        <text x="120" y="104" text-anchor="middle" fill="#9aa3b8" font-size="9">Agent CRM</text>
        <circle cx="400" cy="100" r="22" fill="rgba(167,139,250,0.15)" stroke="#a78bfa" stroke-width="1.5" class="auth-node-pulse auth-node-delay-2"/>
        <text x="400" y="104" text-anchor="middle" fill="#9aa3b8" font-size="9">Agent RH</text>
        <circle cx="120" cy="220" r="22" fill="rgba(245,185,66,0.12)" stroke="#f5b942" stroke-width="1.5" class="auth-node-pulse auth-node-delay-3"/>
        <text x="120" y="224" text-anchor="middle" fill="#9aa3b8" font-size="9">Workflow</text>
        <circle cx="400" cy="220" r="22" fill="rgba(124,108,255,0.15)" stroke="#7c6cff" stroke-width="1.5" class="auth-node-pulse auth-node-delay-4"/>
        <text x="400" y="224" text-anchor="middle" fill="#9aa3b8" font-size="9">API / DB</text>
        <line x1="142" y1="115" x2="230" y2="145" stroke="url(#authGradLine)" stroke-width="1.5" class="auth-line-flow"/>
        <line x1="378" y1="115" x2="290" y2="145" stroke="url(#authGradLine)" stroke-width="1.5" class="auth-line-flow auth-line-delay"/>
        <line x1="142" y1="205" x2="230" y2="175" stroke="url(#authGradLine)" stroke-width="1.5" class="auth-line-flow auth-line-delay-2"/>
        <line x1="378" y1="205" x2="290" y2="175" stroke="url(#authGradLine)" stroke-width="1.5" class="auth-line-flow auth-line-delay-3"/>
        <rect x="180" y="300" width="160" height="28" rx="14" fill="rgba(124,108,255,0.12)" stroke="rgba(124,108,255,0.3)"/>
        <text x="260" y="318" text-anchor="middle" fill="#c4b5fd" font-size="10" font-family="DM Sans,sans-serif">Déploiement 1 clic → Production</text>
      </svg>
      <div class="auth-visual-glow"></div>
    </div>`;
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

function renderAuthFeatures() {
  const items = [
    "Création No-Code",
    "Orchestration Multi-Agents",
    "Marketplace IA",
    "Déploiement Cloud",
  ];
  return `<ul class="auth-features">${items.map((t) => `<li><span class="auth-check">✓</span>${t}</li>`).join("")}</ul>`;
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
      <div class="auth-oauth-buttons">
        ${providers.map((p) => `
          <button type="button" class="auth-oauth-btn" data-oauth="${p.id}" title="${p.label}">
            <span class="auth-oauth-icon">${p.icon}</span>
            <span class="auth-oauth-label">${p.label}</span>
          </button>`).join("")}
      </div>
    </div>`;
}

function renderAuthPremiumLayout({ cardTitle, cardSubtitle, formHtml, footerLink }) {
  return `
    <div class="auth-premium">
      <div class="auth-premium-bg" aria-hidden="true">
        <div class="auth-gradient"></div>
        <div class="auth-grid-lines"></div>
        <div class="auth-particles">${Array.from({ length: 24 }, (_, i) => `<span style="--i:${i}"></span>`).join("")}</div>
        <div class="auth-neural-ring auth-neural-ring-1"></div>
        <div class="auth-neural-ring auth-neural-ring-2"></div>
      </div>
      <div class="auth-premium-grid">
        <div class="auth-premium-left">
          ${renderAuthIllustration()}
          <h1 class="auth-hero-title">Construisez vos agents IA sans coder</h1>
          <p class="auth-hero-sub">Créez, entraînez, déployez et monétisez vos agents intelligents sur une seule plateforme.</p>
          ${renderAuthFeatures()}
          ${renderAuthStats()}
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
      <p class="auth-banner">Plus de <strong>500 agents</strong> disponibles dans la marketplace Agentia</p>
    </div>`;
}

function renderInscription() {
  const formHtml = `
      <form id="form-register" class="auth-form">
        <label>Nom complet<input name="full_name" required placeholder="Jean Dupont" autocomplete="name" /></label>
        <label>Organisation<input name="organization_name" required placeholder="Mon entreprise" autocomplete="organization" /></label>
        <label>Email<input name="email" type="email" required placeholder="vous@entreprise.com" autocomplete="email" /></label>
        <label>Mot de passe<input name="password" type="password" minlength="8" required placeholder="8 caractères minimum" autocomplete="new-password" /></label>
        <button type="submit" class="btn btn-primary btn-block btn-glow">Commencer gratuitement</button>
      </form>`;
  const footerLink = `<p class="auth-link">Déjà inscrit ? <a href="/connexion" data-nav="/connexion">Se connecter</a></p>`;
  return renderAuthPremiumLayout({
    cardTitle: "Créer votre compte",
    cardSubtitle: "Plan Gratuit inclus — sans carte bancaire",
    formHtml,
    footerLink,
  });
}

function renderConnexion() {
  const formHtml = `
      <form id="form-login" class="auth-form">
        <label>Email<input name="email" type="email" required placeholder="vous@entreprise.com" autocomplete="email" /></label>
        <label>Mot de passe<input name="password" type="password" required placeholder="••••••••" autocomplete="current-password" /></label>
        <label class="auth-remember">
          <input type="checkbox" name="remember" id="auth-remember" />
          <span>Se souvenir de moi</span>
        </label>
        <button type="submit" class="btn btn-primary btn-block btn-glow">Se connecter</button>
      </form>
      <p class="auth-forgot"><a href="#" id="auth-forgot-link">Mot de passe oublié ?</a></p>`;
  const footerLink = `<p class="auth-link">Pas encore de compte ? <a href="/inscription" data-nav="/inscription">Créer un compte</a></p>`;
  return renderAuthPremiumLayout({
    cardTitle: "Connexion",
    cardSubtitle: "Accédez à votre espace architecte",
    formHtml,
    footerLink,
  });
}

function renderDocs() {
  return `
    <section class="page-header">
      <h1>Documentation</h1>
      <p>Guides API, déploiement et intégrations — bientôt disponible.</p>
      <a href="/" data-nav="/" class="btn btn-secondary">Retour à l'accueil</a>
    </section>`;
}

function renderAccount() {
  return `
    <section class="page-header"><h1>Mon compte</h1><p>${escapeHtml(STATE.orgName)} — Plan ${escapeHtml(STATE.planName)}</p></section>
    <div class="card">
      <h3>Abonnement actuel</h3>
      <p>Plan <strong>${escapeHtml(STATE.planName)}</strong></p>
      <button class="btn btn-primary" data-nav="/abonnement">Choisir un plan</button>
    </div>
    <div class="card" style="margin-top:1rem">
      <h3>Facturation</h3>
      <p id="account-billing-summary">Chargement…</p>
    </div>`;
}

function renderSubscription() {
  const plans = STATE.plans || [];
  return `
    <section class="page-header"><h1>Choisir un plan</h1><p>Upgradez pour déployer plus de solutions.</p></section>
    <div class="plan-grid">
      ${plans.map((p) => `
        <article class="plan-card ${p.plan === 'free' ? 'plan-free' : ''}">
          <h3>${escapeHtml(p.name)}</h3>
          <div class="plan-price">${p.monthly_price_eur > 0 ? `${p.monthly_price_eur} €/mois` : "Gratuit"}</div>
          <p>${escapeHtml(p.description)}</p>
          <ul>${p.limits.max_deployments_per_month ? `<li>${p.limits.max_deployments_per_month} déploiements/mois</li>` : "<li>Déploiements illimités</li>"}</ul>
          <button class="btn btn-secondary btn-block" data-plan="${p.plan}">Choisir</button>
        </article>`).join("")}
    </div>`;
}

function renderPaymentSuccess() {
  return `<section class="auth-page"><h1>Paiement en cours de confirmation…</h1><p id="payment-status"><span class="loading"></span></p></section>`;
}

function renderPaymentCancel() {
  return `<section class="auth-page"><h1>Paiement annulé</h1><p>Vous pouvez réessayer depuis votre compte ou la page solution.</p>
    <button class="btn btn-primary" data-nav="/mon-compte">Mon compte</button></section>`;
}

async function confirmPaymentWithPolling(paymentCode) {
  for (let i = 0; i < 5; i++) {
    try {
      const result = await API.confirmBilling(paymentCode);
      return result;
    } catch {
      await new Promise((r) => setTimeout(r, 2000));
      try {
        const status = await API.pollPaymentStatus(paymentCode);
        if (status.paid) return await API.confirmBilling(paymentCode);
      } catch { /* retry */ }
    }
  }
  throw new Error("Paiement non confirmé — réessayez depuis Mon compte.");
}

/* ─── Views ─── */

function renderHome() {
  return `
    <section class="hero-page">
      <h1 class="hero-title">Que souhaitez-vous accomplir aujourd'hui ?</h1>
      <p class="hero-sub">Décrivez votre besoin métier. Notre architecte digital conçoit une solution sur mesure — sans jargon technique.</p>
      <div class="hero-form">
        <textarea id="home-need" placeholder="Décrivez votre besoin métier..." aria-label="Besoin métier">${escapeHtml(STATE.pendingNeed)}</textarea>
        <div class="chips" role="list">
          ${EXAMPLE_CHIPS.map((c) => `<button type="button" class="chip" data-chip="${escapeHtml(c)}">${escapeHtml(c)}</button>`).join("")}
        </div>
        <button type="button" class="btn btn-primary btn-block" id="btn-design">Concevoir ma solution</button>
      </div>
    </section>`;
}

function defaultFlowNodes() {
  return ["Réception", "Analyse", "Classification", "Action", "Suivi"];
}

function renderFlowDiagram(nodes) {
  if (!nodes.length) {
    return `<div class="arch-placeholder"><div class="icon">🏗️</div><p>Décrivez votre besoin pour voir l'architecture prendre forme.</p></div>`;
  }
  return `<div class="flow-diagram">${nodes.map((n, i) => {
    const delay = i * 0.12;
    const arrow = i < nodes.length - 1 ? `<span class="flow-arrow" style="animation-delay:${delay + 0.06}s">→</span>` : "";
    return `<div class="flow-node" style="animation-delay:${delay}s">${escapeHtml(n)}</div>${arrow}`;
  }).join("")}</div>`;
}

function renderEstimatesPanel(est) {
  if (!est) {
    return `<p class="empty-state">Estimation en cours de calcul…</p>`;
  }
  const badge = complexityClass(est.complexity);
  return `
    <table class="est-table">
      <tr><td>Complexité</td><td><span class="est-badge ${badge}">${escapeHtml(est.complexity)}</span></td></tr>
      <tr><td>Temps de construction</td><td>~${est.build_time_min} min</td></tr>
      <tr><td>Coût estimé</td><td>~${est.monthly_cost_eur} €/mois</td></tr>
      <tr><td>Économie estimée</td><td class="est-highlight">~${est.hours_saved_per_month} h/mois</td></tr>
      <tr><td>ROI</td><td class="est-highlight">~${est.roi_percent}%</td></tr>
    </table>`;
}

function renderWorkspace() {
  const flowNodes = STATE.blueprint?.blueprint?.data_flow?.length
    ? STATE.blueprint.blueprint.data_flow.map((s) => s.split("→").pop()?.trim() || s)
    : STATE.conversationId
      ? defaultFlowNodes().slice(0, 2 + Math.min(3, (STATE.estimates?.build_time_min || 12) / 8 | 0))
      : [];

  return `
    <div class="workspace">
      <section class="panel">
        <div class="panel-header">Conversation</div>
        <div class="panel-body">
          <div id="chat-messages" class="chat-messages">
            <div class="msg msg-assistant">Bonjour ${escapeHtml(STATE.userName)}, je suis votre architecte digital. Décrivez ce que vous souhaitez accomplir pour votre activité — je construirai une solution adaptée, étape par étape.</div>
          </div>
        </div>
        <div class="chat-input-row">
          <input type="text" id="chat-input" placeholder="Votre message…" autocomplete="off" />
          <button type="button" class="btn btn-primary" id="btn-send">Envoyer</button>
        </div>
      </section>
      <section class="panel">
        <div class="panel-header">Architecture en construction</div>
        <div class="panel-body arch-viz" id="arch-viz">${renderFlowDiagram(flowNodes)}</div>
      </section>
      <section class="panel">
        <div class="panel-header"><span class="pulse-dot"></span>Estimation temps réel</div>
        <div class="panel-body" id="estimates-panel">${renderEstimatesPanel(STATE.estimates)}</div>
      </section>
    </div>`;
}

function countSolutionParts(bp) {
  const components = bp.components || [];
  const agents = components.filter((c) => c.type === "agent" || c.type === "ai").length;
  const workflows = components.filter((c) => c.type === "workflow").length;
  const connectors = components.filter((c) => c.type === "integration" || c.type === "api").length;
  return { agents: agents || 1, workflows: workflows || 1, connectors: connectors || 1 };
}

function renderSolution(id) {
  const bp = STATE.blueprint?.blueprint;
  if (!bp) {
    return `<div class="empty-state"><p>Chargement de votre solution…</p><span class="loading"></span></div>`;
  }
  const parts = countSolutionParts(bp);
  const capabilities = bp.components.map((c) => businessCapability(c));

  return `
    <div class="page-header">
      <h1>Solution proposée</h1>
      <p>Résumé exécutif — ${escapeHtml(bp.title)}</p>
    </div>
    <div class="card">
      <h3>Résumé exécutif</h3>
      <p>Votre besoin sera couvert par : <strong>${parts.agents}</strong> assistant(s) intelligent(s), <strong>${parts.workflows}</strong> processus automatisé(s), <strong>${parts.connectors}</strong> connecteur(s) métier.</p>
      <p style="color:var(--text-muted);margin-top:0.75rem">${escapeHtml(bp.solution_type_rationale)}</p>
    </div>
    <div class="card" style="margin-top:1.25rem">
      <h3>Architecture</h3>
      <div class="arch-blocks" style="margin-top:1rem">
        ${bp.components.map((c) => `
          <div class="arch-block" title="${escapeHtml(c.description)}">
            <strong>${escapeHtml(c.name)}</strong>
            <span>${escapeHtml(businessLabel(c.type))}</span>
          </div>`).join("")}
      </div>
    </div>
    <div class="card" style="margin-top:1.25rem">
      <div class="tabs">
        <button class="tab active" data-tab="metier">Vue métier</button>
        <button class="tab" data-tab="technique">Vue technique avancée</button>
      </div>
      <div id="tab-metier">
        <ul class="checklist">
          ${capabilities.map((c) => `<li>${escapeHtml(c)}</li>`).join("")}
          ${bp.requirements.objectives.map((o) => `<li>${escapeHtml(o)}</li>`).join("")}
        </ul>
      </div>
      <div id="tab-technique" hidden>
        <ul class="checklist">
          ${bp.components.map((c) => `<li><strong>${escapeHtml(c.name)}</strong> (${escapeHtml(c.type)}) — ${escapeHtml(c.description)}${c.technology_hint ? ` · ${escapeHtml(c.technology_hint)}` : ""}</li>`).join("")}
          ${bp.data_flow.map((f) => `<li>Flux : ${escapeHtml(f)}</li>`).join("")}
        </ul>
      </div>
    </div>
    <div class="action-bar">
      <button class="btn btn-primary" id="btn-deploy">Déployer ma solution</button>
      <button class="btn btn-secondary" id="btn-edit-layout">Modifier la disposition</button>
      <button class="btn btn-ghost" data-nav="/workspace">Retour au workspace</button>
    </div>`;
}

function layoutKey(id) {
  return `agentia-layout-${id}`;
}

function renderEditor(id) {
  const bp = STATE.blueprint?.blueprint;
  if (!bp) {
    return `<div class="empty-state"><p>Chargement du plan de solution…</p></div>`;
  }
  return `
    <div class="page-header">
      <h1>Générateur visuel</h1>
      <p>Organisez les composants de votre solution — glissez-déposez pour repositionner.</p>
    </div>
    <div class="editor-wrap" id="editor-canvas"></div>
    <div class="action-bar">
      <button class="btn btn-secondary" id="btn-save-layout">Enregistrer la disposition</button>
      <button class="btn btn-ghost" data-nav="/solution/${id}">Retour à la solution</button>
    </div>`;
}

function cockpitMetrics(deployment, billing) {
  const failed = (billing?.deployments || []).filter((d) => d.status === "failed").length;
  return {
    executions: (billing?.billing_events || []).filter((e) => e.status === "succeeded").length,
    cost: deployment?.deployment_cost?.toFixed(2) || "0.00",
    timeSaved: Math.round((deployment?.complexity_score || 1) * 14),
    errors: failed,
    savings: billing?.total_billed ? Math.round(billing.total_billed * 6) : 0,
  };
}

function renderCockpit() {
  const selected = STATE.cockpitSelected;
  if (selected) {
    const m = cockpitMetrics(selected, STATE.billingSummary);
    return `
      <div class="page-header">
        <button class="btn btn-ghost" id="btn-back-cockpit">← Mes solutions</button>
        <h1>${escapeHtml(selected.title || "Solution déployée")}</h1>
        <p>Tableau de bord de supervision</p>
      </div>
      <div class="stats-row">
        <div class="stat-card"><div class="label">Exécutions ce mois</div><div class="value">${m.executions}</div></div>
        <div class="stat-card"><div class="label">Coût</div><div class="value">${m.cost} €</div></div>
        <div class="stat-card"><div class="label">Temps gagné</div><div class="value">${m.timeSaved} h</div></div>
        <div class="stat-card"><div class="label">Erreurs</div><div class="value">${m.errors}</div></div>
        <div class="stat-card"><div class="label">Économies</div><div class="value">${m.savings} €</div></div>
      </div>`;
  }

  const solutions = STATE.deployments || [];
  return `
    <div class="page-header">
      <h1>Centre de supervision</h1>
      <p>Suivez vos solutions déployées et leurs performances.</p>
    </div>
    <div class="card">
      <h3>Mes solutions</h3>
      ${solutions.length ? `
        <table class="data-table" style="margin-top:1rem">
          <thead><tr><th>Solution</th><th>Statut</th><th>Coût</th><th>Date</th></tr></thead>
          <tbody>
            ${solutions.map((d, i) => `
              <tr data-idx="${i}">
                <td>Solution ${escapeHtml(d.conversation_id.slice(0, 8))}</td>
                <td><span class="status-pill ${d.status === "deployed" ? "active" : "paused"}">${d.status === "deployed" ? "Actif" : "Pause"}</span></td>
                <td>${d.deployment_cost} ${d.currency}</td>
                <td>${new Date(d.created_at).toLocaleDateString("fr-FR")}</td>
              </tr>`).join("")}
          </tbody>
        </table>` : `
        <div class="empty-state">
          <p>Aucune solution déployée pour le moment.</p>
          <button class="btn btn-primary" data-nav="/">Concevoir ma première solution</button>
        </div>`}
    </div>`;
}

function renderMarketplace(category = "Tous") {
  const items = category === "Tous" ? MARKETPLACE : MARKETPLACE.filter((t) => t.category === category);
  return `
    <div class="marketplace-header">
      <h1>Modèles de solutions</h1>
      <p>Démarrez rapidement avec un modèle éprouvé, adapté à votre secteur.</p>
    </div>
    <div class="category-tabs">
      ${CATEGORIES.map((c) => `<button class="chip ${c === category ? "active" : ""}" data-category="${escapeHtml(c)}" style="${c === category ? "border-color:var(--accent);color:var(--text)" : ""}">${escapeHtml(c)}</button>`).join("")}
    </div>
    <div class="template-grid">
      ${items.map((t) => `
        <article class="template-card">
          <div class="template-thumb">${t.icon}</div>
          <div class="template-body">
            <div class="template-tag">${escapeHtml(t.category)}</div>
            <h3>${escapeHtml(t.title)}</h3>
            <p>${escapeHtml(t.desc)}</p>
            <button class="btn btn-secondary btn-block" data-template="${escapeHtml(t.need)}">Utiliser ce modèle</button>
          </div>
        </article>`).join("")}
    </div>`;
}

function renderArchitect() {
  const r = STATE.architectResult;
  return `
    <div class="architect-page">
      <div class="page-header" style="text-align:center">
        <h1>Architecte de solutions IA</h1>
        <p>Décrivez votre activité — nous identifions les automatisations à plus fort impact.</p>
      </div>
      <div class="architect-input">
        <textarea id="architect-desc" placeholder="Ex. : Je suis un cabinet comptable de 10 employés…">${escapeHtml(STATE.architectInput || "")}</textarea>
        <button class="btn btn-primary btn-block" id="btn-analyze" style="margin-top:1rem">Analyser mon activité</button>
      </div>
      ${r ? `
        <div class="analysis-results">
          <p style="font-size:1.1rem">${escapeHtml(r.summary)}</p>
          <div class="analysis-metrics">
            <div class="metric-card"><div class="num">${r.processes_count}</div><div class="lbl">Processus automatisables</div></div>
            <div class="metric-card"><div class="num">${r.hours_saved_per_year.toLocaleString("fr-FR")} h</div><div class="lbl">Économie potentielle / an</div></div>
            <div class="metric-card"><div class="num">${r.monthly_cost_eur} €</div><div class="lbl">Coût estimé / mois</div></div>
            <div class="metric-card"><div class="num">${r.roi_percent}%</div><div class="lbl">ROI estimé</div></div>
          </div>
          <h3>Solutions proposées</h3>
          <div class="proposal-cards" style="margin-top:1rem">
            ${r.proposals.map((p) => `
              <div class="proposal-card" data-need="${escapeHtml(p.need)}">
                <h4>${escapeHtml(p.title)}</h4>
                <p>${escapeHtml(p.description)}</p>
              </div>`).join("")}
          </div>
        </div>` : ""}
    </div>`;
}

/* ─── Workspace logic ─── */

let estimatePollTimer = null;

async function pollEstimates(id) {
  try {
    STATE.estimates = await API.getEstimates(id);
    const panel = document.getElementById("estimates-panel");
    if (panel) panel.innerHTML = renderEstimatesPanel(STATE.estimates);

    if (STATE.estimates.ready && !STATE.blueprint) {
      try {
        STATE.blueprint = await API.getBlueprint(id);
        updateArchViz();
        if (STATE.estimates.ready && STATE.conversationId) {
          clearInterval(estimatePollTimer);
        }
      } catch { /* blueprint not ready yet */ }
    }
  } catch { /* ignore */ }
}

function updateArchViz() {
  const viz = document.getElementById("arch-viz");
  if (!viz) return;
  const nodes = STATE.blueprint?.blueprint?.data_flow?.length
    ? STATE.blueprint.blueprint.data_flow.map((s) => {
        const parts = s.split("→");
        return parts[parts.length - 1].trim();
      })
    : defaultFlowNodes().slice(0, 3);
  viz.innerHTML = renderFlowDiagram(nodes);
}

function appendChatMessage(role, content) {
  const box = document.getElementById("chat-messages");
  if (!box) return;
  const div = document.createElement("div");
  div.className = `msg msg-${role}`;
  div.textContent = content;
  box.appendChild(div);
  box.scrollTop = box.scrollHeight;
}

async function startWorkspaceConversation(initialMessage) {
  appendChatMessage("user", initialMessage);
  try {
    const res = await API.createConversation(initialMessage);
    STATE.conversationId = res.conversation.id;
    appendChatMessage("assistant", res.assistant_message.content);
    pollEstimates(STATE.conversationId);
    estimatePollTimer = setInterval(() => pollEstimates(STATE.conversationId), 3000);
  } catch (e) {
    appendChatMessage("assistant", "Désolé, une erreur est survenue. Réessayez dans un instant.");
    showToast(e.message);
  }
}

async function sendChatMessage(text) {
  if (!STATE.conversationId) {
    await startWorkspaceConversation(text);
    return;
  }
  appendChatMessage("user", text);
  try {
    const res = await API.sendMessage(STATE.conversationId, text);
    appendChatMessage("assistant", res.assistant_message.content);
    pollEstimates(STATE.conversationId);
    if (res.conversation.status === "ready_for_blueprint" || res.conversation.status === "blueprint_generated") {
      try {
        STATE.blueprint = await API.getBlueprint(STATE.conversationId);
        updateArchViz();
        showToast("Votre solution est prête à être consultée.");
        setTimeout(() => navigate(`/solution/${STATE.conversationId}`), 1500);
      } catch { /* wait for more info */ }
    }
  } catch (e) {
    showToast(e.message);
  }
}

function bindWorkspaceEvents() {
  const pending = STATE.pendingNeed;
  STATE.pendingNeed = "";
  if (pending) startWorkspaceConversation(pending);

  document.getElementById("btn-send")?.addEventListener("click", () => {
    const input = document.getElementById("chat-input");
    const text = input.value.trim();
    if (!text) return;
    input.value = "";
    sendChatMessage(text);
  });

  document.getElementById("chat-input")?.addEventListener("keydown", (e) => {
    if (e.key === "Enter") {
      e.preventDefault();
      document.getElementById("btn-send")?.click();
    }
  });

  if (STATE.conversationId && !pending) {
    pollEstimates(STATE.conversationId);
    estimatePollTimer = setInterval(() => pollEstimates(STATE.conversationId), 3000);
    API.json(`/conversations/${STATE.conversationId}`).then((conv) => {
      const box = document.getElementById("chat-messages");
      if (!box) return;
      box.innerHTML = `<div class="msg msg-assistant">Bonjour ${escapeHtml(STATE.userName)}, je suis votre architecte digital. Décrivez ce que vous souhaitez accomplir pour votre activité.</div>`;
      conv.messages.forEach((m) => appendChatMessage(m.role, m.content));
    }).catch(() => {});
  }
}

async function loadSolution(id) {
  STATE.conversationId = id;
  try {
    STATE.blueprint = await API.getBlueprint(id);
    STATE.estimates = await API.getEstimates(id);
  } catch (e) {
    showToast(e.message);
  }
}

function bindSolutionEvents(id) {
  document.querySelectorAll(".tab").forEach((tab) => {
    tab.addEventListener("click", () => {
      document.querySelectorAll(".tab").forEach((t) => t.classList.remove("active"));
      tab.classList.add("active");
      const metier = document.getElementById("tab-metier");
      const tech = document.getElementById("tab-technique");
      const isMetier = tab.dataset.tab === "metier";
      metier.hidden = !isMetier;
      tech.hidden = isMetier;
    });
  });

  document.getElementById("btn-deploy")?.addEventListener("click", async () => {
    try {
      const res = await API.deploy(id);
      if (res.checkout_url && res.payment_code) {
        showToast("Redirection vers le paiement…");
        sessionStorage.setItem("pending_payment_code", res.payment_code);
        sessionStorage.setItem("pending_payment_type", "deploy");
        sessionStorage.setItem("pending_conversation_id", id);
        window.location.href = res.checkout_url;
      } else if (res.payment_pending && res.payment_code) {
        const confirmed = await API.confirmDeploy(id, res.payment_code);
        showToast(confirmed.message || "Déploiement confirmé.");
      } else {
        showToast(res.message || "Déploiement lancé.");
      }
    } catch (e) {
      showToast(e.message);
    }
  });

  document.getElementById("btn-edit-layout")?.addEventListener("click", () => {
    navigate(`/editor/${id}`);
  });
}

function initEditorCanvas(id) {
  const canvas = document.getElementById("editor-canvas");
  if (!canvas || !STATE.blueprint?.blueprint) return;

  const saved = localStorage.getItem(layoutKey(id));
  const positions = saved ? JSON.parse(saved) : {};
  const components = STATE.blueprint.blueprint.components;

  components.forEach((c, i) => {
    const el = document.createElement("div");
    el.className = "editor-block";
    el.innerHTML = `<h4>${escapeHtml(c.name)}</h4><p>${escapeHtml(businessLabel(c.type))}</p>`;
    const pos = positions[c.name] || { x: 40 + (i % 3) * 200, y: 40 + Math.floor(i / 3) * 120 };
    el.style.left = `${pos.x}px`;
    el.style.top = `${pos.y}px`;

    let dragging = false;
    let offsetX = 0;
    let offsetY = 0;

    el.addEventListener("mousedown", (e) => {
      dragging = true;
      offsetX = e.clientX - el.offsetLeft;
      offsetY = e.clientY - el.offsetTop;
      el.style.zIndex = 10;
    });

    document.addEventListener("mousemove", (e) => {
      if (!dragging) return;
      const rect = canvas.getBoundingClientRect();
      el.style.left = `${Math.max(0, Math.min(e.clientX - rect.left - offsetX, rect.width - 160))}px`;
      el.style.top = `${Math.max(0, Math.min(e.clientY - rect.top - offsetY, rect.height - 80))}px`;
    });

    document.addEventListener("mouseup", () => {
      dragging = false;
      el.style.zIndex = 1;
    });

    canvas.appendChild(el);
  });

  document.getElementById("btn-save-layout")?.addEventListener("click", () => {
    const layout = {};
    canvas.querySelectorAll(".editor-block").forEach((el, i) => {
      const name = components[i]?.name;
      if (name) layout[name] = { x: parseInt(el.style.left, 10), y: parseInt(el.style.top, 10) };
    });
    localStorage.setItem(layoutKey(id), JSON.stringify(layout));
    showToast("Disposition enregistrée.");
  });
}

async function loadCockpitData() {
  if (!STATE.orgId) return;
  try {
    STATE.billingSummary = await API.getBilling(STATE.orgId);
    STATE.deployments = STATE.billingSummary.deployments.map((d) => ({
      ...d,
      title: `Solution ${d.conversation_id.slice(0, 8)}`,
    }));
  } catch {
    STATE.deployments = [];
    STATE.billingSummary = null;
  }
}

function bindCockpitEvents() {
  document.querySelectorAll(".data-table tbody tr").forEach((row) => {
    row.addEventListener("click", () => {
      const idx = parseInt(row.dataset.idx, 10);
      STATE.cockpitSelected = STATE.deployments[idx];
      render();
    });
  });
  document.getElementById("btn-back-cockpit")?.addEventListener("click", () => {
    STATE.cockpitSelected = null;
    render();
  });
}

let marketplaceCategory = "Tous";

function bindMarketplaceEvents() {
  document.querySelectorAll("[data-category]").forEach((btn) => {
    btn.addEventListener("click", () => {
      marketplaceCategory = btn.dataset.category;
      document.getElementById("view").innerHTML = renderMarketplace(marketplaceCategory);
      bindMarketplaceEvents();
      bindGlobalNav();
    });
  });
  document.querySelectorAll("[data-template]").forEach((btn) => {
    btn.addEventListener("click", () => {
      navigate("/workspace", { need: btn.dataset.template });
    });
  });
}

function bindArchitectEvents() {
  document.getElementById("btn-analyze")?.addEventListener("click", async () => {
    const desc = document.getElementById("architect-desc").value.trim();
    if (desc.length < 10) {
      showToast("Décrivez votre activité en quelques mots.");
      return;
    }
    STATE.architectInput = desc;
    const btn = document.getElementById("btn-analyze");
    btn.disabled = true;
    btn.innerHTML = '<span class="loading"></span> Analyse en cours…';
    try {
      STATE.architectResult = await API.analyzeArchitect(desc);
      render();
    } catch (e) {
      showToast(e.message);
      btn.disabled = false;
      btn.textContent = "Analyser mon activité";
    }
  });

  document.querySelectorAll(".proposal-card").forEach((card) => {
    card.addEventListener("click", () => {
      navigate("/workspace", { need: card.dataset.need });
    });
  });
}

function bindHomeEvents() {
  document.querySelectorAll("[data-chip]").forEach((chip) => {
    chip.addEventListener("click", () => {
      document.getElementById("home-need").value = chip.dataset.chip;
    });
  });
  document.getElementById("btn-design")?.addEventListener("click", () => {
    const need = document.getElementById("home-need").value.trim();
    if (!need) {
      showToast("Décrivez d'abord votre besoin métier.");
      return;
    }
    navigate("/workspace", { need });
  });
}

function bindAuthEvents() {
  document.getElementById("form-register")?.addEventListener("submit", async (e) => {
    e.preventDefault();
    const fd = new FormData(e.target);
    try {
      const res = await API.register(Object.fromEntries(fd));
      setToken(res.access_token);
      await loadUserContext();
      showToast("Compte créé — bienvenue !");
      navigate("/");
    } catch (err) {
      showToast(err.message);
    }
  });
  document.getElementById("form-login")?.addEventListener("submit", async (e) => {
    e.preventDefault();
    const fd = new FormData(e.target);
    const data = Object.fromEntries(fd);
    delete data.remember;
    try {
      const res = await API.login(data);
      setToken(res.access_token);
      await loadUserContext();
      navigate("/");
    } catch (err) {
      showToast(err.message);
    }
  });
  document.getElementById("btn-logout")?.addEventListener("click", () => {
    setToken(null);
    navigate("/");
  });
  document.querySelectorAll("[data-oauth]").forEach((btn) => {
    btn.addEventListener("click", () => {
      showToast("Connexion OAuth bientôt disponible.");
    });
  });
  document.getElementById("auth-forgot-link")?.addEventListener("click", (e) => {
    e.preventDefault();
    showToast("Contactez le support pour réinitialiser votre mot de passe.");
  });
  const remember = document.getElementById("auth-remember");
  if (remember) {
    remember.checked = localStorage.getItem("agentia_remember") === "1";
    remember.addEventListener("change", () => {
      localStorage.setItem("agentia_remember", remember.checked ? "1" : "0");
    });
  }
}

async function bindAccountEvents() {
  if (!STATE.orgId) return;
  try {
    const billing = await API.getBilling(STATE.orgId);
    const el = document.getElementById("account-billing-summary");
    if (el) {
      el.textContent = `Total facturé : ${billing.total_billed} € — ${billing.deployments_used_this_month}/${billing.deployments_limit ?? "∞"} déploiements ce mois`;
    }
  } catch { /* ignore */ }
}

function bindSubscriptionEvents() {
  document.querySelectorAll("[data-plan]").forEach((btn) => {
    btn.addEventListener("click", async () => {
      const plan = btn.dataset.plan;
      try {
        const res = await API.subscribe(plan);
        if (res.checkout_url && res.payment_code) {
          sessionStorage.setItem("pending_payment_code", res.payment_code);
          sessionStorage.setItem("pending_payment_type", "subscribe");
          window.location.href = res.checkout_url;
        } else {
          showToast(res.message || "Plan activé.");
          await loadUserContext();
        }
      } catch (e) {
        showToast(e.message);
      }
    });
  });
}

async function bindPaymentSuccessEvents() {
  const params = new URLSearchParams(window.location.search);
  const paymentCode = params.get("payment_code") || sessionStorage.getItem("pending_payment_code");
  const el = document.getElementById("payment-status");
  if (!paymentCode) {
    if (el) el.textContent = "Code de paiement manquant.";
    return;
  }
  try {
    const result = await confirmPaymentWithPolling(paymentCode);
    sessionStorage.removeItem("pending_payment_code");
    sessionStorage.removeItem("pending_payment_type");
    if (el) el.textContent = result.message || "Paiement confirmé.";
    showToast("Paiement confirmé.");
    setTimeout(() => navigate("/cockpit"), 2000);
  } catch (e) {
    if (el) el.textContent = e.message;
  }
}

function bindGlobalNav() {
  document.querySelectorAll("[data-nav]").forEach((el) => {
    el.addEventListener("click", (e) => {
      e.preventDefault();
      navigate(el.getAttribute("data-nav") || el.dataset.nav);
    });
  });
}

async function render() {
  clearInterval(estimatePollTimer);
  const route = parseRoute();
  if (!requireAuth(route.name)) return;

  document.body.classList.toggle("page-auth", route.name === "connexion" || route.name === "inscription");

  document.querySelectorAll(".auth-nav-slot").forEach((el) => {
    el.innerHTML = renderAuthNav();
  });

  setActiveNav(route.name === "solution" || route.name === "editor" ? "home" : route.name);
  const view = document.getElementById("view");

  switch (route.name) {
    case "inscription":
      view.innerHTML = renderInscription();
      bindAuthEvents();
      break;
    case "connexion":
      view.innerHTML = renderConnexion();
      bindAuthEvents();
      break;
    case "docs":
      view.innerHTML = renderDocs();
      break;
    case "account":
      view.innerHTML = renderAccount();
      await bindAccountEvents();
      break;
    case "subscription":
      try {
        STATE.plans = await API.getPlans();
      } catch {
        STATE.plans = [];
      }
      view.innerHTML = renderSubscription();
      bindSubscriptionEvents();
      break;
    case "payment-success":
      view.innerHTML = renderPaymentSuccess();
      await bindPaymentSuccessEvents();
      break;
    case "payment-cancel":
      view.innerHTML = renderPaymentCancel();
      break;
    case "home":
      view.innerHTML = renderHome();
      bindHomeEvents();
      break;
    case "workspace":
      view.innerHTML = renderWorkspace();
      bindWorkspaceEvents();
      break;
    case "solution":
      view.innerHTML = `<div class="empty-state"><span class="loading"></span></div>`;
      await loadSolution(route.id);
      view.innerHTML = renderSolution(route.id);
      bindSolutionEvents(route.id);
      break;
    case "editor":
      view.innerHTML = `<div class="empty-state"><span class="loading"></span></div>`;
      await loadSolution(route.id);
      view.innerHTML = renderEditor(route.id);
      initEditorCanvas(route.id);
      break;
    case "cockpit":
      await loadCockpitData();
      view.innerHTML = renderCockpit();
      bindCockpitEvents();
      break;
    case "marketplace":
      view.innerHTML = renderMarketplace(marketplaceCategory);
      bindMarketplaceEvents();
      break;
    case "architect":
      view.innerHTML = renderArchitect();
      bindArchitectEvents();
      break;
    default:
      view.innerHTML = renderHome();
      bindHomeEvents();
  }

  bindGlobalNav();
  bindAuthEvents();
}

window.addEventListener("popstate", render);

document.addEventListener("DOMContentLoaded", async () => {
  await loadUserContext();
  const params = new URLSearchParams(window.location.search);
  if (params.get("need")) STATE.pendingNeed = params.get("need");
  render();
});

export {};
