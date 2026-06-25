/**
 * Agent Factory — Solution Composer
 * Plateforme professionnelle de composition de solutions intelligentes.
 * La conversation n'est que le moyen de recueillir le besoin —
 * la solution se construit en temps réel dans le workspace.
 */

import { renderAuthPremiumLayout } from "./auth-ui.js";

const AUTH_TOKEN_KEY = "agentia_token";

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
  register(data) { return this.json("/auth/register", { method: "POST", body: JSON.stringify(data) }); },
  login(data) { return this.json("/auth/login", { method: "POST", body: JSON.stringify(data) }); },
  getMe() { return this.json("/auth/me"); },
  getOAuthProviders() { return this.json("/auth/oauth/providers"); },
  createConversation(message) {
    return this.json("/conversations", { method: "POST", body: JSON.stringify({ message }) });
  },
  sendMessage(id, message) {
    return this.json(`/conversations/${id}/messages`, { method: "POST", body: JSON.stringify({ message }) });
  },
  getConversation(id) { return this.json(`/conversations/${id}`); },
  getBlueprint(id) { return this.json(`/conversations/${id}/blueprint`); },
  getEstimates(id) { return this.json(`/conversations/${id}/estimates`); },
  deploy(id) { return this.json(`/conversations/${id}/deploy`, { method: "POST" }); },
  confirmDeploy(id, paymentCode) {
    return this.json(`/conversations/${id}/deploy/confirm`, {
      method: "POST",
      body: JSON.stringify({ payment_code: paymentCode }),
    });
  },
  getPlans() { return this.json("/plans"); },
  subscribe(plan) {
    return this.json("/organizations/me/subscribe", { method: "POST", body: JSON.stringify({ plan }) });
  },
  confirmBilling(paymentCode) {
    return this.json("/billing/confirm", { method: "POST", body: JSON.stringify({ payment_code: paymentCode }) });
  },
  pollPaymentStatus(code) {
    return this.json(`/billing/payments/${encodeURIComponent(code)}/status`);
  },
  getBilling(orgId) { return this.json(`/organizations/${orgId}/billing`); },
  listAgents() { return this.json("/agents"); },
  getAgent(id) { return this.json(`/agents/${id}`); },
  updateAgent(id, data) {
    return this.json(`/agents/${id}`, { method: "PATCH", body: JSON.stringify(data) });
  },
  invokeAgent(id, message) {
    return this.json(`/agents/${id}/invoke`, { method: "POST", body: JSON.stringify({ message }) });
  },
  listMarketplaceAgents() { return this.json("/marketplace/agents"); },
  createAgentApiKey(id, label) {
    return this.json(`/agents/${id}/api-keys`, { method: "POST", body: JSON.stringify({ label }) });
  },
};

const STATE = {
  userName: "",
  orgName: "",
  orgId: null,
  planName: "Free",
  conversationId: null,
  blueprint: null,
  estimates: null,
  composerMetadata: null,    // SolutionMetadata from API
  composerMessages: [],      // {role, content} for composer chat
  pendingNeed: "",
  cockpitSelected: null,
  deployments: [],
  billingSummary: null,
  plans: [],
  oauthProviders: [],
  publishedAgents: [],
  agentTestPanel: null,
  marketplaceAgents: [],
};

const PROBLEM_CHIPS = [
  "Je veux réduire de 80% le temps de traitement des factures",
  "Je veux automatiser les réponses à mes clients en moins de 5 min",
  "Je veux un assistant RH pour mes managers",
  "Je veux automatiser le suivi de mes prospects",
  "Je veux analyser automatiquement mes contrats",
];

const MARKETPLACE = [
  { id: "email-crm", category: "CRM", icon: "✉", title: "Gestion emails clients", desc: "Tri, réponse et suivi automatique des demandes entrantes.", need: "Je veux gérer automatiquement mes emails clients" },
  { id: "factures", category: "Comptabilité", icon: "📄", title: "Extraction factures PDF", desc: "Capture et structuration des données de vos factures fournisseurs.", need: "Je veux extraire les données des factures PDF" },
  { id: "prospects", category: "CRM", icon: "🎯", title: "Suivi prospects", desc: "Relances intelligentes et scoring de vos opportunités commerciales.", need: "Je veux suivre mes prospects" },
  { id: "depenses", category: "Finance", icon: "💳", title: "Validation des dépenses", desc: "Circuit d'approbation fluide pour notes de frais et achats.", need: "Je veux automatiser l'approbation des dépenses" },
  { id: "rh", category: "RH", icon: "👥", title: "Assistant RH", desc: "Réponses aux questions employés et gestion des demandes internes.", need: "Je veux un assistant RH" },
  { id: "contrats", category: "Juridique", icon: "⚖", title: "Analyse de contrats", desc: "Extraction des clauses clés et alertes de renouvellement.", need: "Je veux analyser automatiquement mes contrats" },
  { id: "formation", category: "Éducation", icon: "📚", title: "Assistant pédagogique", desc: "Support aux apprenants et suivi des parcours de formation.", need: "Je veux un assistant pour les questions des apprenants" },
  { id: "rapprochement", category: "Finance", icon: "🏦", title: "Rapprochement bancaire", desc: "Conciliation automatique entre relevés et écritures comptables.", need: "Je veux automatiser le rapprochement bancaire" },
];

const CATEGORIES = ["Tous", "Finance", "RH", "CRM", "Comptabilité", "Juridique", "Éducation"];

const COMPONENT_TYPE_LABELS = {
  integration: "Intégration",
  ai: "Intelligence IA",
  workflow: "Workflow",
  agent: "Agent IA",
  api: "API",
  database: "Base de données",
  reporting: "Reporting",
  security: "Sécurité",
};

const COMPONENT_TYPE_ICONS = {
  integration: "⇄",
  ai: "◈",
  workflow: "⊕",
  agent: "◎",
  api: "⟡",
  database: "▤",
  reporting: "▦",
  security: "⬡",
};

function componentTypeLabel(type) {
  return COMPONENT_TYPE_LABELS[type] || "Composant";
}

function componentTypeIcon(type) {
  return COMPONENT_TYPE_ICONS[type] || "◆";
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
  return map[component.name] || component.description
    .replace(/API|LLM|OCR|n8n|Temporal|Azure|Kubernetes|Docker|GPT|Gemini/gi, "")
    .trim() || `Automatise : ${component.name.toLowerCase()}`;
}

function escapeHtml(str) {
  return String(str)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function showToast(msg, type = "info") {
  const el = document.getElementById("toast");
  el.textContent = msg;
  el.className = `toast toast-${type}`;
  el.hidden = false;
  setTimeout(() => { el.hidden = true; }, 4500);
}

function navigate(path, params = {}) {
  if (params.need) STATE.pendingNeed = params.need;
  if (params.conversationId) STATE.conversationId = params.conversationId;
  history.pushState(params, "", path);
  render();
}

function parseRoute() {
  const path = window.location.pathname;
  const composerMatch = path.match(/^\/composer\/([^/]+)/);
  const solutionMatch = path.match(/^\/solution\/([^/]+)/);
  const editorMatch = path.match(/^\/editor\/([^/]+)/);
  if (composerMatch) return { name: "composer", id: composerMatch[1] };
  if (solutionMatch) return { name: "solution", id: solutionMatch[1] };
  if (editorMatch) return { name: "editor", id: editorMatch[1] };
  if (path === "/workspace") return { name: "workspace" };   // legacy redirect
  if (path === "/cockpit") return { name: "cockpit" };
  if (path === "/marketplace") return { name: "marketplace" };
  if (path === "/architect") return { name: "architect" };   // legacy redirect
  if (path === "/inscription") return { name: "inscription" };
  if (path === "/connexion/oauth") return { name: "oauth-callback" };
  if (path === "/connexion") return { name: "connexion" };
  if (path === "/documentation") return { name: "docs" };
  if (path === "/mon-compte") return { name: "account" };
  if (path === "/abonnement") return { name: "subscription" };
  if (path === "/paiement/succes") return { name: "payment-success" };
  if (path === "/paiement/annule") return { name: "payment-cancel" };
  return { name: "home" };
}

function isPublicRoute(routeName) {
  return new Set([
    "inscription", "connexion", "oauth-callback",
    "payment-success", "payment-cancel",
  ]).has(routeName);
}

function navigateAfterAuth() {
  const redirect = sessionStorage.getItem("auth_redirect");
  sessionStorage.removeItem("auth_redirect");
  const safe = redirect &&
    redirect !== "/connexion" &&
    redirect !== "/inscription" &&
    !redirect.startsWith("/connexion/oauth");
  navigate(safe ? redirect : "/");
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
  return `<a href="/connexion" data-nav="/connexion" class="nav-link-login">Connexion</a>`;
}

function updateAuthChrome() {
  const authed = !!getToken();
  document.body.classList.toggle("is-authenticated", authed);
  document.body.classList.toggle("is-guest", !authed);
  const brand = document.querySelector(".brand");
  if (brand) {
    const home = authed ? "/" : "/connexion";
    brand.setAttribute("href", home);
    brand.setAttribute("data-nav", home);
  }
}

function renderPasswordField({ name, placeholder, autocomplete, minlength }) {
  const minAttr = minlength ? ` minlength="${minlength}"` : "";
  return `
    <label class="auth-password-wrap">
      Mot de passe
      <span class="auth-password-input">
        <input name="${name}" type="password" required placeholder="${placeholder}" autocomplete="${autocomplete}"${minAttr} />
        <button type="button" class="auth-password-toggle" aria-label="Afficher le mot de passe" title="Afficher le mot de passe">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
        </button>
      </span>
    </label>`;
}

/* ─── Auth pages ─── */

function renderInscription() {
  const formHtml = `
      <form id="form-register" class="auth-form">
        <label>Nom complet<input name="full_name" required placeholder="Jean Dupont" autocomplete="name" /></label>
        <label>Organisation<input name="organization_name" required placeholder="Mon entreprise" autocomplete="organization" /></label>
        <label>Email<input name="email" type="email" required placeholder="vous@entreprise.com" autocomplete="email" /></label>
        ${renderPasswordField({ name: "password", placeholder: "8 caractères minimum", autocomplete: "new-password", minlength: 8 })}
        <button type="submit" class="btn btn-primary btn-block btn-glow">Commencer gratuitement →</button>
      </form>`;
  const footerLink = `<p class="auth-link">Déjà inscrit ? <a href="/connexion" data-nav="/connexion">Se connecter</a></p>`;
  return renderAuthPremiumLayout({
    cardTitle: "Créer votre compte",
    cardSubtitle: "Accédez à toutes les fonctionnalités après inscription",
    formHtml,
    footerLink,
    oauthProviders: STATE.oauthProviders,
  });
}

function renderConnexion() {
  const formHtml = `
      <form id="form-login" class="auth-form">
        <label>Email<input name="email" type="email" required placeholder="vous@entreprise.com" autocomplete="email" /></label>
        ${renderPasswordField({ name: "password", placeholder: "••••••••", autocomplete: "current-password" })}
        <div class="auth-form-row">
          <label class="auth-remember">
            <input type="checkbox" name="remember" id="auth-remember" />
            <span>Se souvenir de moi</span>
          </label>
          <a href="#" class="auth-forgot" id="auth-forgot-link">Mot de passe oublié ?</a>
        </div>
        <button type="submit" class="btn btn-primary btn-block btn-glow">Se connecter →</button>
      </form>`;
  const footerLink = `<p class="auth-link">Pas encore de compte ? <a href="/inscription" data-nav="/inscription">Créer un compte</a></p>`;
  return renderAuthPremiumLayout({
    cardTitle: "Connexion",
    cardSubtitle: "Accédez à votre espace de composition",
    formHtml,
    footerLink,
    oauthProviders: STATE.oauthProviders,
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
    <div class="plans-grid">
      ${plans.map((p, i) => `
        <article class="plan-card ${i === 1 ? "featured" : ""}">
          <div class="plan-name">${escapeHtml(p.name)}</div>
          <div class="plan-price">${p.monthly_price_eur > 0 ? `${p.monthly_price_eur} €<span>/mois</span>` : "Gratuit"}</div>
          <p style="font-size:.88rem;color:var(--text-muted);margin-bottom:1rem">${escapeHtml(p.description)}</p>
          <ul style="font-size:.875rem;padding-left:1.1rem;color:var(--text-muted);margin-bottom:1.25rem">
            ${p.limits.max_deployments_per_month ? `<li>${p.limits.max_deployments_per_month} déploiements/mois</li>` : "<li>Déploiements illimités</li>"}
          </ul>
          <button class="btn ${i === 1 ? "btn-primary" : "btn-secondary"} btn-block" data-plan="${p.plan}">Choisir ce plan</button>
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

/* ─── HOME SCREEN ─── */

function renderHome() {
  return `
    <div class="home-composer">
      <div class="home-composer-inner">
        <div class="home-eyebrow">Agent Factory</div>
        <h1 class="home-headline">Quel problème souhaitez-vous résoudre ?</h1>
        <p class="home-sub">Décrivez votre défi en une phrase. Agent Factory compose automatiquement la solution — agents IA, workflows, connecteurs, API — sans une ligne de code.</p>
        <div class="home-input-wrap">
          <input
            type="text"
            id="home-need"
            class="home-input"
            placeholder="Ex. : Je veux réduire de 80% le temps de traitement des factures…"
            autocomplete="off"
            value="${escapeHtml(STATE.pendingNeed)}"
          />
          <button type="button" class="home-input-btn" id="btn-compose" aria-label="Composer la solution">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><line x1="5" y1="12" x2="19" y2="12"/><polyline points="12 5 19 12 12 19"/></svg>
          </button>
        </div>
        <div class="home-chips" role="list" aria-label="Exemples de problèmes">
          ${PROBLEM_CHIPS.map((c) => `
            <button type="button" class="home-chip" data-chip="${escapeHtml(c)}">${escapeHtml(c)}</button>
          `).join("")}
        </div>
      </div>
      <div class="home-proof">
        <div class="home-proof-item">
          <span class="home-proof-num">11</span>
          <span class="home-proof-label">agents spécialisés internes</span>
        </div>
        <div class="home-proof-sep"></div>
        <div class="home-proof-item">
          <span class="home-proof-num">∞</span>
          <span class="home-proof-label">types de solutions composables</span>
        </div>
        <div class="home-proof-sep"></div>
        <div class="home-proof-item">
          <span class="home-proof-num">0</span>
          <span class="home-proof-label">ligne de code requise</span>
        </div>
      </div>
    </div>`;
}

/* ─── SOLUTION COMPOSER ─── */

const DISCOVERY_PHASES = ["Contexte", "Processus", "Systèmes", "Contraintes"];

function getPhase(messages) {
  const userCount = messages.filter((m) => m.role === "user").length;
  return Math.min(userCount, DISCOVERY_PHASES.length);
}

function renderPhaseBar(messages) {
  const current = getPhase(messages);
  return `
    <div class="phase-bar">
      ${DISCOVERY_PHASES.map((label, i) => `
        <div class="phase-step ${i < current ? "done" : i === current ? "active" : ""}">
          <div class="phase-dot"></div>
          <span>${label}</span>
        </div>
        ${i < DISCOVERY_PHASES.length - 1 ? `<div class="phase-connector ${i < current ? "done" : ""}"></div>` : ""}
      `).join("")}
    </div>`;
}

function renderInsightCards(metadata) {
  const m = metadata || {};
  const cards = [
    { key: "objectives", label: "Objectifs",    icon: "◎", items: m.objectives || [] },
    { key: "systems",    label: "Systèmes",     icon: "⇄", items: m.systems || [] },
    { key: "documents",  label: "Documents",    icon: "▤", items: m.documents || [] },
    { key: "users",      label: "Utilisateurs", icon: "◈", items: m.users || [] },
    { key: "constraints",label: "Contraintes",  icon: "⬡", items: m.constraints || [] },
    { key: "risks",      label: "Risques",      icon: "⚠", items: m.risks || [] },
  ];

  return `
    <div class="insight-cards-grid">
      ${cards.map((card) => `
        <div class="insight-card ${card.items.length ? "has-data" : "empty"}">
          <div class="insight-card-header">
            <span class="insight-card-icon">${card.icon}</span>
            <span class="insight-card-label">${card.label}</span>
            ${card.items.length ? `<span class="insight-card-count">${card.items.length}</span>` : ""}
          </div>
          <div class="insight-card-body">
            ${card.items.length
              ? card.items.map((item) => `<span class="insight-chip">${escapeHtml(item)}</span>`).join("")
              : `<span class="insight-empty">En attente…</span>`
            }
          </div>
        </div>
      `).join("")}
    </div>`;
}

function renderArchCanvas(blueprint) {
  if (!blueprint) {
    return `
      <div class="arch-canvas empty-canvas">
        <div class="arch-canvas-hint">
          <div class="arch-canvas-hint-icon">⬡</div>
          <p>L'architecture apparaîtra ici au fil de la conversation</p>
        </div>
      </div>`;
  }

  const bp = blueprint.blueprint || blueprint;
  const components = bp.components || [];

  return `
    <div class="arch-canvas">
      <div class="arch-canvas-title">Architecture — ${escapeHtml(bp.title || "Solution composée")}</div>
      <div class="arch-components">
        ${components.map((c, i) => `
          <div class="arch-comp-block" style="animation-delay:${i * 0.06}s">
            <div class="arch-comp-icon">${componentTypeIcon(c.type)}</div>
            <div class="arch-comp-body">
              <div class="arch-comp-name">${escapeHtml(c.name)}</div>
              <div class="arch-comp-type">${componentTypeLabel(c.type)}</div>
            </div>
            ${i < components.length - 1 ? `<div class="arch-comp-arrow">→</div>` : ""}
          </div>
        `).join("")}
      </div>
      ${bp.data_flow && bp.data_flow.length ? `
        <div class="arch-flow-steps">
          ${bp.data_flow.slice(0, 5).map((step) => `<div class="arch-flow-step">${escapeHtml(step)}</div>`).join("")}
        </div>
      ` : ""}
    </div>`;
}

function renderComposerFooter(metadata, estimates, conversationStatus) {
  const completeness = metadata?.completeness || 0;
  const pct = Math.round(completeness * 100);
  const isReady = conversationStatus === "ready_for_blueprint" || conversationStatus === "blueprint_generated" || completeness >= 0.65;
  const cost = estimates ? `~${estimates.monthly_cost_eur} €/mois` : "Calcul en cours…";
  const timeSaved = estimates ? `~${estimates.hours_saved_per_month} h/mois économisées` : "";

  return `
    <div class="composer-footer">
      <div class="composer-footer-left">
        <div class="completeness-bar">
          <div class="completeness-fill" style="width:${pct}%"></div>
        </div>
        <span class="completeness-label">Informations collectées : <strong>${pct}%</strong></span>
      </div>
      <div class="composer-footer-mid">
        ${estimates ? `
          <span class="footer-metric">${cost}</span>
          <span class="footer-metric-sep">·</span>
          <span class="footer-metric">${timeSaved}</span>
        ` : `<span class="footer-metric muted">Continuez la conversation pour obtenir une estimation</span>`}
      </div>
      <div class="composer-footer-right">
        <button
          class="btn btn-primary btn-compose ${isReady ? "" : "disabled"}"
          id="btn-build-solution"
          ${isReady ? "" : "disabled"}
          title="${isReady ? "Générer la solution" : "Continuez la conversation pour débloquer"}"
        >
          ${isReady ? "Construire la solution →" : "Poursuivez la conversation…"}
        </button>
      </div>
    </div>`;
}

function renderComposer(id) {
  const messages = STATE.composerMessages;
  const metadata = STATE.composerMetadata;
  const estimates = STATE.estimates;
  const convStatus = STATE.blueprint ? "blueprint_generated" :
    (metadata?.completeness || 0) >= 0.65 ? "ready_for_blueprint" : "active";

  return `
    <div class="composer-layout">

      <!-- LEFT: Conversation -->
      <aside class="composer-left">
        <div class="composer-left-header">
          <div class="composer-left-title">Agent Creator</div>
          ${renderPhaseBar(messages)}
        </div>
        <div class="composer-messages" id="composer-messages">
          ${messages.length === 0 ? `
            <div class="composer-msg msg-assistant">
              <div class="msg-avatar">AF</div>
              <div class="msg-bubble">Bonjour${STATE.userName ? ` ${escapeHtml(STATE.userName)}` : ""}, je suis votre Agent Creator. Je vais analyser votre besoin et composer une solution sur mesure. Décrivez-moi votre problème.</div>
            </div>` : ""}
          ${messages.map((m) => `
            <div class="composer-msg msg-${m.role}">
              ${m.role === "assistant" ? `<div class="msg-avatar">AF</div>` : ""}
              <div class="msg-bubble">${escapeHtml(m.content)}</div>
              ${m.role === "user" ? `<div class="msg-avatar user-avatar">${STATE.userName ? escapeHtml(STATE.userName[0]) : "U"}</div>` : ""}
            </div>
          `).join("")}
          <div id="composer-typing" class="composer-msg msg-assistant" hidden>
            <div class="msg-avatar">AF</div>
            <div class="msg-bubble typing-indicator"><span></span><span></span><span></span></div>
          </div>
        </div>
        <div class="composer-input-row">
          <input
            type="text"
            id="composer-input"
            class="composer-input"
            placeholder="Répondez à l'Agent Creator…"
            autocomplete="off"
          />
          <button type="button" class="composer-send-btn" id="btn-composer-send" aria-label="Envoyer">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><line x1="22" y1="2" x2="11" y2="13"/><polygon points="22 2 15 22 11 13 2 9 22 2"/></svg>
          </button>
        </div>
      </aside>

      <!-- RIGHT: Solution Workspace -->
      <main class="composer-right">
        <div class="composer-right-header">
          <div class="composer-right-title">Solution en construction</div>
          <div class="composer-right-sub">Les informations se complètent automatiquement au fil de la conversation</div>
        </div>
        <div class="composer-workspace-body">
          ${renderInsightCards(metadata)}
          ${renderArchCanvas(STATE.blueprint)}
        </div>
        ${renderComposerFooter(metadata, estimates, convStatus)}
      </main>

    </div>`;
}

/* ─── SOLUTION page ─── */

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
      <h1>Solution composée</h1>
      <p>${escapeHtml(bp.title)}</p>
    </div>
    <div class="card">
      <h3>Résumé exécutif</h3>
      <p>Votre solution comprend : <strong>${parts.agents}</strong> agent(s) IA, <strong>${parts.workflows}</strong> workflow(s) automatisé(s), <strong>${parts.connectors}</strong> connecteur(s) métier.</p>
      <p style="color:var(--text-muted);margin-top:0.75rem">${escapeHtml(bp.solution_type_rationale)}</p>
    </div>
    <div class="card" style="margin-top:1.25rem">
      <h3>Architecture</h3>
      <div class="arch-blocks" style="margin-top:1rem">
        ${bp.components.map((c) => `
          <div class="arch-block" title="${escapeHtml(c.description)}">
            <div class="arch-block-icon">${componentTypeIcon(c.type)}</div>
            <strong>${escapeHtml(c.name)}</strong>
            <span>${componentTypeLabel(c.type)}</span>
          </div>`).join("")}
      </div>
    </div>
    <div class="card" style="margin-top:1.25rem">
      <div class="tabs">
        <button class="tab active" data-tab="metier">Vue métier</button>
        <button class="tab" data-tab="technique">Vue technique</button>
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
          ${bp.data_flow.map((f) => `<li>${escapeHtml(f)}</li>`).join("")}
        </ul>
      </div>
    </div>
    <div class="action-bar">
      <button class="btn btn-primary" id="btn-deploy">Déployer cette solution</button>
      <button class="btn btn-secondary" id="btn-edit-layout">Modifier la disposition</button>
      <button class="btn btn-ghost" data-nav="/composer/${id}">← Retour au composer</button>
    </div>`;
}

function renderEditor(id) {
  const bp = STATE.blueprint?.blueprint;
  if (!bp) {
    return `<div class="empty-state"><p>Chargement du plan de solution…</p></div>`;
  }
  return `
    <div class="page-header">
      <h1>Éditeur visuel</h1>
      <p>Organisez les composants — glissez-déposez pour repositionner.</p>
    </div>
    <div class="editor-wrap" id="editor-canvas"></div>
    <div class="action-bar">
      <button class="btn btn-secondary" id="btn-save-layout">Enregistrer</button>
      <button class="btn btn-ghost" data-nav="/solution/${id}">← Retour à la solution</button>
    </div>`;
}

/* ─── COCKPIT ─── */

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
  const agents = STATE.publishedAgents || [];
  const testPanel = STATE.agentTestPanel;

  return `
    <div class="page-header">
      <h1>Centre de supervision</h1>
      <p>Suivez vos agents déployés et testez-les directement.</p>
    </div>
    ${testPanel ? `
    <div class="card agent-test-panel" id="agent-test-panel">
      <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:.75rem">
        <h3>Tester : ${escapeHtml(testPanel.title)}</h3>
        <button class="btn btn-ghost btn-sm" id="btn-close-test">✕ Fermer</button>
      </div>
      <div class="agent-chat" id="agent-chat-box">
        <div class="msg msg-assistant">Je suis l'agent <strong>${escapeHtml(testPanel.title)}</strong>. Posez-moi votre question.</div>
      </div>
      <div class="agent-chat-input">
        <input type="text" id="agent-test-input" placeholder="Votre message…" autocomplete="off" />
        <button class="btn btn-primary btn-sm" id="btn-send-agent">Envoyer</button>
      </div>
    </div>` : ""}
    <div class="card">
      <h3>Mes agents</h3>
      ${agents.length ? `
        <table class="data-table" style="margin-top:1rem">
          <thead><tr><th>Agent</th><th>Catégorie</th><th>Visibilité</th><th>Statut</th><th>Actions</th></tr></thead>
          <tbody>
            ${agents.map((a) => `
              <tr>
                <td>${escapeHtml(a.title)}</td>
                <td>${escapeHtml(a.category)}</td>
                <td>
                  <select class="agent-visibility-select" data-agent-id="${escapeHtml(a.id)}">
                    <option value="private" ${a.visibility === "private" ? "selected" : ""}>Privé</option>
                    <option value="organization" ${a.visibility === "organization" ? "selected" : ""}>Organisation</option>
                    <option value="public" ${a.visibility === "public" ? "selected" : ""}>Public</option>
                  </select>
                </td>
                <td><span class="status-pill ${a.status === "active" ? "active" : "paused"}">${a.status === "active" ? "Actif" : "Pause"}</span></td>
                <td><button class="btn btn-secondary btn-sm" data-test-agent="${escapeHtml(a.id)}" data-test-title="${escapeHtml(a.title)}">Tester</button></td>
              </tr>`).join("")}
          </tbody>
        </table>` : `
        <div class="empty-state">
          <p>Aucun agent publié pour le moment.</p>
          <button class="btn btn-primary" data-nav="/">Composer une solution</button>
        </div>`}
    </div>
    <div class="card" style="margin-top:1rem">
      <h3>Mes déploiements</h3>
      ${solutions.length ? `
        <table class="data-table" style="margin-top:1rem">
          <thead><tr><th>Solution</th><th>Statut</th><th>Coût</th><th>Date</th></tr></thead>
          <tbody>
            ${solutions.map((d, i) => `
              <tr data-idx="${i}">
                <td>${escapeHtml(d.title || "Solution")}</td>
                <td><span class="status-pill ${d.status === "deployed" ? "active" : "paused"}">${d.status === "deployed" ? "Actif" : "Pause"}</span></td>
                <td>${d.deployment_cost} ${d.currency}</td>
                <td>${new Date(d.created_at).toLocaleDateString("fr-FR")}</td>
              </tr>`).join("")}
          </tbody>
        </table>` : `
        <div class="empty-state"><p>Aucun déploiement ce mois-ci.</p></div>`}
    </div>`;
}

/* ─── MARKETPLACE ─── */

function renderMarketplace(category = "Tous") {
  const liveAgents = (STATE.marketplaceAgents || []).filter(
    (a) => category === "Tous" || a.category === category
  );
  const staticItems = category === "Tous" ? MARKETPLACE : MARKETPLACE.filter((t) => t.category === category);
  const allCategories = ["Tous", ...new Set([...CATEGORIES.slice(1), ...(STATE.marketplaceAgents || []).map((a) => a.category)])];

  return `
    <div class="marketplace-header">
      <h1>Marketplace de solutions</h1>
      <p>Agents déployés par la communauté et modèles prêts à l'emploi.</p>
    </div>
    <div class="category-tabs">
      ${allCategories.map((c) => `<button class="chip ${c === category ? "active" : ""}" data-category="${escapeHtml(c)}">${escapeHtml(c)}</button>`).join("")}
    </div>
    ${liveAgents.length ? `
    <h3 style="margin:1.5rem 0 .75rem">Agents publiés</h3>
    <div class="template-grid">
      ${liveAgents.map((a) => `
        <article class="template-card">
          <div class="template-thumb">◎</div>
          <div class="template-body">
            <div class="template-tag">${escapeHtml(a.category)}</div>
            <h3>${escapeHtml(a.title)}</h3>
            <p>${escapeHtml(a.description)}</p>
            <button class="btn btn-primary btn-block" data-invoke-agent="${escapeHtml(a.id)}" data-invoke-title="${escapeHtml(a.title)}">Utiliser cet agent</button>
          </div>
        </article>`).join("")}
    </div>
    <h3 style="margin:1.5rem 0 .75rem">Modèles de départ</h3>` : ""}
    <div class="template-grid">
      ${staticItems.map((t) => `
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

/* ─── COMPOSER LOGIC ─── */

function _scrollComposerMessages() {
  const box = document.getElementById("composer-messages");
  if (box) box.scrollTop = box.scrollHeight;
}

function _appendComposerMsg(role, content) {
  STATE.composerMessages.push({ role, content });
  const box = document.getElementById("composer-messages");
  if (!box) return;
  const div = document.createElement("div");
  div.className = `composer-msg msg-${role}`;
  div.innerHTML = role === "assistant"
    ? `<div class="msg-avatar">AF</div><div class="msg-bubble">${escapeHtml(content)}</div>`
    : `<div class="msg-bubble">${escapeHtml(content)}</div><div class="msg-avatar user-avatar">${STATE.userName ? escapeHtml(STATE.userName[0]) : "U"}</div>`;
  box.appendChild(div);
  _scrollComposerMessages();
}

function _showTyping() {
  const el = document.getElementById("composer-typing");
  if (el) el.hidden = false;
  _scrollComposerMessages();
}

function _hideTyping() {
  const el = document.getElementById("composer-typing");
  if (el) el.hidden = true;
}

function _updateWorkspace(metadata, estimates) {
  const cardsEl = document.querySelector(".insight-cards-grid");
  if (cardsEl) cardsEl.outerHTML = renderInsightCards(metadata).trim();
  // Re-render footer
  const footerEl = document.querySelector(".composer-footer");
  const convStatus = STATE.blueprint ? "blueprint_generated" :
    (metadata?.completeness || 0) >= 0.65 ? "ready_for_blueprint" : "active";
  if (footerEl) {
    footerEl.outerHTML = renderComposerFooter(metadata, estimates, convStatus).trim();
    // Rebind the deploy button
    document.getElementById("btn-build-solution")?.addEventListener("click", _handleBuildSolution);
  }
  // Update phase bar
  const phaseEl = document.querySelector(".phase-bar");
  if (phaseEl) phaseEl.outerHTML = renderPhaseBar(STATE.composerMessages).trim();
}

async function _handleBuildSolution() {
  const id = STATE.conversationId;
  if (!id) return;
  const btn = document.getElementById("btn-build-solution");
  if (btn) { btn.disabled = true; btn.textContent = "Composition en cours…"; }
  try {
    STATE.blueprint = await API.getBlueprint(id);
    // Update arch canvas
    const canvasEl = document.querySelector(".arch-canvas, .arch-canvas.empty-canvas");
    if (canvasEl) canvasEl.outerHTML = renderArchCanvas(STATE.blueprint).trim();
    // Also update footer
    const footerEl = document.querySelector(".composer-footer");
    if (footerEl) {
      const convStatus = "blueprint_generated";
      footerEl.outerHTML = renderComposerFooter(STATE.composerMetadata, STATE.estimates, convStatus).trim();
      document.getElementById("btn-build-solution")?.addEventListener("click", () => {
        navigate(`/solution/${id}`);
      });
    }
    // Navigate to solution after a short delay
    showToast("Solution composée — redirection…", "success");
    setTimeout(() => navigate(`/solution/${id}`), 1200);
  } catch (e) {
    showToast(e.message);
    if (btn) { btn.disabled = false; btn.textContent = "Construire la solution →"; }
  }
}

async function startComposerConversation(initialMessage, composerId) {
  _appendComposerMsg("user", initialMessage);
  _showTyping();
  try {
    const res = await API.createConversation(initialMessage);
    STATE.conversationId = res.conversation.id;
    _hideTyping();
    _appendComposerMsg("assistant", res.assistant_message.content);

    if (res.metadata) {
      STATE.composerMetadata = res.metadata;
    }

    // Load estimates in background
    API.getEstimates(STATE.conversationId).then((est) => {
      STATE.estimates = est;
      _updateWorkspace(STATE.composerMetadata, est);
    }).catch(() => {});

    _updateWorkspace(STATE.composerMetadata, STATE.estimates);

    // Update URL to the conversation-specific composer route
    if (!composerId) {
      history.replaceState({}, "", `/composer/${STATE.conversationId}`);
    }
  } catch (e) {
    _hideTyping();
    _appendComposerMsg("assistant", "Désolé, une erreur est survenue. Réessayez dans un instant.");
    showToast(e.message);
  }
}

async function sendComposerMessage(text) {
  if (!STATE.conversationId) {
    await startComposerConversation(text, null);
    return;
  }
  _appendComposerMsg("user", text);
  _showTyping();
  try {
    const res = await API.sendMessage(STATE.conversationId, text);
    _hideTyping();
    _appendComposerMsg("assistant", res.assistant_message.content);

    if (res.metadata) {
      STATE.composerMetadata = res.metadata;
    }

    // Check status
    const status = res.conversation?.status;
    if (status === "ready_for_blueprint" || status === "blueprint_generated") {
      // Load estimates
      API.getEstimates(STATE.conversationId).then((est) => {
        STATE.estimates = est;
        _updateWorkspace(STATE.composerMetadata, est);
      }).catch(() => {});
    }

    _updateWorkspace(STATE.composerMetadata, STATE.estimates);

  } catch (e) {
    _hideTyping();
    showToast(e.message);
  }
}

function bindComposerEvents(id) {
  const input = document.getElementById("composer-input");
  const sendBtn = document.getElementById("btn-composer-send");

  const doSend = () => {
    const text = input?.value?.trim();
    if (!text) return;
    if (input) input.value = "";
    sendComposerMessage(text);
  };

  sendBtn?.addEventListener("click", doSend);
  input?.addEventListener("keydown", (e) => {
    if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); doSend(); }
  });

  document.getElementById("btn-build-solution")?.addEventListener("click", _handleBuildSolution);

  // If arriving at /composer/:id fresh with a pendingNeed, start conversation
  const pending = STATE.pendingNeed;
  if (pending) {
    STATE.pendingNeed = "";
    startComposerConversation(pending, id);
    return;
  }

  // If we have an existing conversation ID but no messages loaded, reload
  if (id && STATE.conversationId === id && STATE.composerMessages.length === 0) {
    _loadExistingComposerConversation(id);
  } else if (id && STATE.conversationId !== id) {
    STATE.conversationId = id;
    STATE.composerMessages = [];
    STATE.composerMetadata = null;
    STATE.blueprint = null;
    STATE.estimates = null;
    _loadExistingComposerConversation(id);
  }
}

async function _loadExistingComposerConversation(id) {
  try {
    const conv = await API.getConversation(id);
    STATE.composerMessages = conv.messages.map((m) => ({ role: m.role, content: m.content }));
    const box = document.getElementById("composer-messages");
    if (box) {
      box.innerHTML = STATE.composerMessages.map((m) => `
        <div class="composer-msg msg-${m.role}">
          ${m.role === "assistant" ? `<div class="msg-avatar">AF</div>` : ""}
          <div class="msg-bubble">${escapeHtml(m.content)}</div>
          ${m.role === "user" ? `<div class="msg-avatar user-avatar">${STATE.userName ? escapeHtml(STATE.userName[0]) : "U"}</div>` : ""}
        </div>
      `).join("");
      _scrollComposerMessages();
    }
    // Load estimates & blueprint if conversation is advanced
    Promise.all([
      API.getEstimates(id).catch(() => null),
      API.getBlueprint(id).catch(() => null),
    ]).then(([est, bp]) => {
      if (est) STATE.estimates = est;
      if (bp) STATE.blueprint = bp;
      _updateWorkspace(STATE.composerMetadata, STATE.estimates);
      if (bp) {
        const canvasEl = document.querySelector(".arch-canvas, .arch-canvas.empty-canvas");
        if (canvasEl) canvasEl.outerHTML = renderArchCanvas(STATE.blueprint).trim();
      }
    });
  } catch { /* ignore */ }
}

/* ─── Solution, Editor, Cockpit events ─── */

async function loadSolution(id) {
  STATE.conversationId = id;
  try {
    STATE.blueprint = await API.getBlueprint(id);
    STATE.estimates = await API.getEstimates(id);
  } catch (e) {
    showToast(e.message);
  }
}

function layoutKey(id) { return `agentia-layout-${id}`; }

function bindSolutionEvents(id) {
  document.querySelectorAll(".tab").forEach((tab) => {
    tab.addEventListener("click", () => {
      document.querySelectorAll(".tab").forEach((t) => t.classList.remove("active"));
      tab.classList.add("active");
      const isMetier = tab.dataset.tab === "metier";
      document.getElementById("tab-metier").hidden = !isMetier;
      document.getElementById("tab-technique").hidden = isMetier;
    });
  });

  document.getElementById("btn-deploy")?.addEventListener("click", async () => {
    try {
      const res = await API.deploy(id);
      if (res.checkout_url && res.payment_code) {
        sessionStorage.setItem("pending_payment_code", res.payment_code);
        sessionStorage.setItem("pending_payment_type", "deploy");
        sessionStorage.setItem("pending_conversation_id", id);
        window.location.href = res.checkout_url;
      } else if (res.payment_pending && res.payment_code) {
        const confirmed = await API.confirmDeploy(id, res.payment_code);
        showToast(confirmed.message || "Déploiement confirmé.", "success");
        try { STATE.publishedAgents = await API.listAgents(); } catch { /* ignore */ }
      } else {
        showToast((res.message || "Déploiement lancé.") + " Retrouvez votre agent dans le Cockpit.", "success");
        try { STATE.publishedAgents = await API.listAgents(); } catch { /* ignore */ }
      }
    } catch (e) { showToast(e.message); }
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
    el.innerHTML = `<h4>${escapeHtml(c.name)}</h4><p>${componentTypeLabel(c.type)}</p>`;
    const pos = positions[c.name] || { x: 40 + (i % 3) * 200, y: 40 + Math.floor(i / 3) * 120 };
    el.style.left = `${pos.x}px`;
    el.style.top = `${pos.y}px`;

    let dragging = false, offsetX = 0, offsetY = 0;
    el.addEventListener("mousedown", (e) => { dragging = true; offsetX = e.clientX - el.offsetLeft; offsetY = e.clientY - el.offsetTop; el.style.zIndex = 10; });
    document.addEventListener("mousemove", (e) => {
      if (!dragging) return;
      const rect = canvas.getBoundingClientRect();
      el.style.left = `${Math.max(0, Math.min(e.clientX - rect.left - offsetX, rect.width - 160))}px`;
      el.style.top = `${Math.max(0, Math.min(e.clientY - rect.top - offsetY, rect.height - 80))}px`;
    });
    document.addEventListener("mouseup", () => { dragging = false; el.style.zIndex = 1; });
    canvas.appendChild(el);
  });

  document.getElementById("btn-save-layout")?.addEventListener("click", () => {
    const layout = {};
    canvas.querySelectorAll(".editor-block").forEach((el, i) => {
      const name = components[i]?.name;
      if (name) layout[name] = { x: parseInt(el.style.left, 10), y: parseInt(el.style.top, 10) };
    });
    localStorage.setItem(layoutKey(id), JSON.stringify(layout));
    showToast("Disposition enregistrée.", "success");
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
  try { STATE.publishedAgents = await API.listAgents(); } catch { STATE.publishedAgents = []; }
}

function bindCockpitEvents() {
  document.querySelectorAll(".data-table tbody tr[data-idx]").forEach((row) => {
    row.addEventListener("click", () => {
      STATE.cockpitSelected = STATE.deployments[parseInt(row.dataset.idx, 10)];
      render();
    });
  });
  document.getElementById("btn-back-cockpit")?.addEventListener("click", () => { STATE.cockpitSelected = null; render(); });
  document.getElementById("btn-close-test")?.addEventListener("click", () => { STATE.agentTestPanel = null; render(); });
  document.querySelectorAll("[data-test-agent]").forEach((btn) => {
    btn.addEventListener("click", () => {
      STATE.agentTestPanel = { id: btn.dataset.testAgent, title: btn.dataset.testTitle };
      render();
      document.getElementById("agent-test-panel")?.scrollIntoView({ behavior: "smooth" });
    });
  });
  document.getElementById("btn-send-agent")?.addEventListener("click", () => _sendAgentTestMessage());
  document.getElementById("agent-test-input")?.addEventListener("keydown", (e) => {
    if (e.key === "Enter") _sendAgentTestMessage();
  });
  document.querySelectorAll(".agent-visibility-select").forEach((sel) => {
    sel.addEventListener("change", async () => {
      try { await API.updateAgent(sel.dataset.agentId, { visibility: sel.value }); showToast("Visibilité mise à jour.", "success"); }
      catch (e) { showToast(e.message); }
    });
  });
}

async function _sendAgentTestMessage() {
  const input = document.getElementById("agent-test-input");
  const box = document.getElementById("agent-chat-box");
  if (!input || !box || !STATE.agentTestPanel) return;
  const msg = input.value.trim();
  if (!msg) return;
  input.value = "";
  box.innerHTML += `<div class="msg msg-user">${escapeHtml(msg)}</div>`;
  box.innerHTML += `<div class="msg msg-assistant" id="agent-typing"><span class="loading"></span></div>`;
  box.scrollTop = box.scrollHeight;
  try {
    const res = await API.invokeAgent(STATE.agentTestPanel.id, msg);
    document.getElementById("agent-typing")?.remove();
    box.innerHTML += `<div class="msg msg-assistant">${escapeHtml(res.reply || "")}</div>`;
  } catch (e) {
    document.getElementById("agent-typing")?.remove();
    box.innerHTML += `<div class="msg msg-assistant" style="color:var(--danger)">Erreur : ${escapeHtml(e.message)}</div>`;
  }
  box.scrollTop = box.scrollHeight;
}

let marketplaceCategory = "Tous";

function bindMarketplaceEvents() {
  document.querySelectorAll("[data-category]").forEach((btn) => {
    btn.addEventListener("click", async () => {
      marketplaceCategory = btn.dataset.category;
      try { STATE.marketplaceAgents = await API.listMarketplaceAgents(); } catch { STATE.marketplaceAgents = []; }
      document.getElementById("view").innerHTML = renderMarketplace(marketplaceCategory);
      bindMarketplaceEvents();
      bindGlobalNav();
    });
  });
  document.querySelectorAll("[data-template]").forEach((btn) => {
    btn.addEventListener("click", () => {
      STATE.pendingNeed = btn.dataset.template;
      STATE.conversationId = null;
      STATE.composerMessages = [];
      STATE.composerMetadata = null;
      STATE.blueprint = null;
      navigate("/composer/new");
    });
  });
  document.querySelectorAll("[data-invoke-agent]").forEach((btn) => {
    btn.addEventListener("click", () => {
      STATE.agentTestPanel = { id: btn.dataset.invokeAgent, title: btn.dataset.invokeTitle };
      navigate("/cockpit");
    });
  });
}

function bindHomeEvents() {
  const input = document.getElementById("home-need");

  document.querySelectorAll("[data-chip]").forEach((chip) => {
    chip.addEventListener("click", () => {
      if (input) input.value = chip.dataset.chip;
      input?.focus();
    });
  });

  const doStart = () => {
    const need = input?.value?.trim();
    if (!need) { showToast("Décrivez votre problème métier."); return; }
    STATE.pendingNeed = need;
    STATE.conversationId = null;
    STATE.composerMessages = [];
    STATE.composerMetadata = null;
    STATE.blueprint = null;
    STATE.estimates = null;
    navigate("/composer/new");
  };

  document.getElementById("btn-compose")?.addEventListener("click", doStart);
  input?.addEventListener("keydown", (e) => {
    if (e.key === "Enter") { e.preventDefault(); doStart(); }
  });
}

/* ─── Auth ─── */

async function loadOAuthProviders() {
  try {
    const data = await API.getOAuthProviders();
    STATE.oauthProviders = data.providers || [];
  } catch { STATE.oauthProviders = []; }
}

async function handleOAuthCallback() {
  const hash = window.location.hash.startsWith("#") ? window.location.hash.slice(1) : "";
  const params = new URLSearchParams(hash);
  const token = params.get("token");
  const error = params.get("error");
  if (error) { showToast(decodeURIComponent(error.replace(/\+/g, " "))); navigate("/connexion"); return; }
  if (!token) { showToast("Connexion OAuth échouée"); navigate("/connexion"); return; }
  setToken(decodeURIComponent(token));
  await loadUserContext();
  showToast("Connexion réussie", "success");
  navigateAfterAuth();
}

function bindAuthEvents() {
  document.getElementById("form-register")?.addEventListener("submit", async (e) => {
    e.preventDefault();
    const fd = new FormData(e.target);
    try {
      const res = await API.register(Object.fromEntries(fd));
      setToken(res.access_token);
      await loadUserContext();
      showToast("Compte créé — bienvenue !", "success");
      navigateAfterAuth();
    } catch (err) { showToast(err.message); }
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
      navigateAfterAuth();
    } catch (err) { showToast(err.message); }
  });
  document.getElementById("btn-logout")?.addEventListener("click", () => { setToken(null); navigate("/"); });
  document.querySelectorAll("[data-oauth]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const provider = btn.dataset.oauth;
      if (provider) window.location.href = apiUrl(`/auth/oauth/${provider}`);
    });
  });
  document.querySelectorAll(".auth-password-toggle").forEach((btn) => {
    btn.addEventListener("click", () => {
      const input = btn.closest(".auth-password-input")?.querySelector("input");
      if (!input) return;
      const show = input.type === "password";
      input.type = show ? "text" : "password";
      btn.setAttribute("aria-label", show ? "Masquer le mot de passe" : "Afficher le mot de passe");
      btn.classList.toggle("is-visible", show);
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
    if (el) el.textContent = `Total facturé : ${billing.total_billed} € — ${billing.deployments_used_this_month}/${billing.deployments_limit ?? "∞"} déploiements ce mois`;
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
          showToast(res.message || "Plan activé.", "success");
          await loadUserContext();
        }
      } catch (e) { showToast(e.message); }
    });
  });
}

async function confirmPaymentWithPolling(paymentCode) {
  for (let i = 0; i < 5; i++) {
    try { return await API.confirmBilling(paymentCode); }
    catch {
      await new Promise((r) => setTimeout(r, 2000));
      try {
        const status = await API.pollPaymentStatus(paymentCode);
        if (status.paid) return await API.confirmBilling(paymentCode);
      } catch { /* retry */ }
    }
  }
  throw new Error("Paiement non confirmé — réessayez depuis Mon compte.");
}

async function bindPaymentSuccessEvents() {
  const params = new URLSearchParams(window.location.search);
  const paymentCode = params.get("payment_code") || sessionStorage.getItem("pending_payment_code");
  const el = document.getElementById("payment-status");
  if (!paymentCode) { if (el) el.textContent = "Code de paiement manquant."; return; }
  try {
    const result = await confirmPaymentWithPolling(paymentCode);
    sessionStorage.removeItem("pending_payment_code");
    sessionStorage.removeItem("pending_payment_type");
    if (el) el.textContent = result.message || "Paiement confirmé.";
    showToast("Paiement confirmé.", "success");
    setTimeout(() => navigate("/cockpit"), 2000);
  } catch (e) { if (el) el.textContent = e.message; }
}

function bindGlobalNav() {
  document.querySelectorAll("[data-nav]").forEach((el) => {
    el.addEventListener("click", (e) => {
      e.preventDefault();
      navigate(el.getAttribute("data-nav") || el.dataset.nav);
    });
  });
}

/* ─── RENDER ─── */

async function render() {
  let route = parseRoute();
  let authGateNotice = false;

  if (!getToken() && !isPublicRoute(route.name)) {
    const returnTo = `${window.location.pathname}${window.location.search}`;
    if (!["/connexion", "/inscription", "/connexion/oauth"].includes(window.location.pathname)) {
      sessionStorage.setItem("auth_redirect", returnTo);
      sessionStorage.setItem("auth_gate_notice", "1");
      history.replaceState({}, "", "/connexion");
      route = parseRoute();
      authGateNotice = true;
    }
  }

  document.body.classList.toggle("page-auth",
    route.name === "connexion" || route.name === "inscription" || route.name === "oauth-callback");
  document.body.classList.toggle("page-composer", route.name === "composer");

  updateAuthChrome();
  document.querySelectorAll(".auth-nav-slot").forEach((el) => { el.innerHTML = renderAuthNav(); });
  setActiveNav(
    route.name === "solution" || route.name === "editor" || route.name === "composer"
      ? "home"
      : route.name
  );

  const view = document.getElementById("view");

  switch (route.name) {
    case "oauth-callback":
      view.innerHTML = `<section class="auth-page"><p><span class="loading"></span> Connexion en cours…</p></section>`;
      await handleOAuthCallback();
      break;
    case "inscription":
      await loadOAuthProviders();
      view.innerHTML = renderInscription();
      break;
    case "connexion":
      await loadOAuthProviders();
      view.innerHTML = renderConnexion();
      if (authGateNotice || sessionStorage.getItem("auth_gate_notice")) {
        sessionStorage.removeItem("auth_gate_notice");
        showToast("Connectez-vous pour accéder à la plateforme");
      }
      break;
    case "docs":
      view.innerHTML = renderDocs();
      break;
    case "account":
      view.innerHTML = renderAccount();
      await bindAccountEvents();
      break;
    case "subscription":
      try { STATE.plans = await API.getPlans(); } catch { STATE.plans = []; }
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
    case "architect":
      // Legacy redirect to new composer
      STATE.pendingNeed = STATE.pendingNeed || "";
      history.replaceState({}, "", "/");
      view.innerHTML = renderHome();
      bindHomeEvents();
      break;
    case "composer": {
      const cid = route.id;
      // Reset state if arriving at a new conversation
      if (cid === "new") {
        if (!STATE.composerMessages.length && !STATE.pendingNeed) {
          STATE.pendingNeed = "";
        }
        view.innerHTML = renderComposer(null);
        bindComposerEvents(null);
      } else {
        if (STATE.conversationId !== cid) {
          STATE.conversationId = cid;
          STATE.composerMessages = [];
          STATE.composerMetadata = null;
          STATE.blueprint = null;
          STATE.estimates = null;
        }
        view.innerHTML = renderComposer(cid);
        bindComposerEvents(cid);
      }
      break;
    }
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
      try { STATE.marketplaceAgents = await API.listMarketplaceAgents(); } catch { STATE.marketplaceAgents = []; }
      view.innerHTML = renderMarketplace(marketplaceCategory);
      bindMarketplaceEvents();
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
