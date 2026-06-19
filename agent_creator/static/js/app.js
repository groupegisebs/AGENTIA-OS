/**
 * Agentia Factory — Interface web
 * Vanilla JS SPA intégrée à l'API REST FastAPI
 */

const API = "";

const state = {
  conversationId: null,
  conversation: null,
  blueprint: null,
  deploymentHint: null,
  organization: null,
  plans: [],
  billing: null,
  sending: false,
  loadingBlueprint: false,
  deploying: false,
};

// ─── Utilitaires ───────────────────────────────────────────────

function $(sel) {
  return document.querySelector(sel);
}

function $$(sel) {
  return document.querySelectorAll(sel);
}

function formatDate(iso) {
  if (!iso) return "—";
  return new Date(iso).toLocaleString("fr-FR", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function formatEur(amount) {
  return new Intl.NumberFormat("fr-FR", {
    style: "currency",
    currency: "EUR",
  }).format(amount ?? 0);
}

function solutionTypeLabel(type) {
  const labels = {
    workflow: "Workflow",
    agent: "Agent IA",
    api: "API",
    microservice: "Microservice",
    hybrid: "Hybride",
  };
  return labels[type] || type;
}

function planLabel(plan) {
  const labels = {
    free: "Gratuit",
    professional: "Professionnel",
    business: "Business",
    enterprise: "Entreprise",
  };
  return labels[plan] || plan;
}

function statusLabel(status) {
  const labels = {
    succeeded: "Réussi",
    pending: "En attente",
    failed: "Échoué",
    deployed: "Déployé",
    active: "Actif",
    payment_pending: "Paiement en attente",
    ready_for_blueprint: "Prêt pour blueprint",
    blueprint_generated: "Blueprint généré",
  };
  return labels[status] || status;
}

function toast(message, type = "info") {
  const container = $("#toast-container");
  const el = document.createElement("div");
  el.className = `toast alert-${type}`;
  el.textContent = message;
  container.appendChild(el);
  setTimeout(() => el.remove(), 4500);
}

function showAlert(containerId, message, type = "info") {
  const el = document.getElementById(containerId);
  if (!el) return;
  if (!message) {
    el.innerHTML = "";
    return;
  }
  el.innerHTML = `<div class="alert alert-${type}">${escapeHtml(message)}</div>`;
}

function escapeHtml(str) {
  const div = document.createElement("div");
  div.textContent = str;
  return div.innerHTML;
}

async function api(path, options = {}) {
  const res = await fetch(`${API}${path}`, {
    headers: { "Content-Type": "application/json", ...options.headers },
    ...options,
  });

  let data = null;
  const ct = res.headers.get("content-type") || "";
  if (ct.includes("application/json")) {
    data = await res.json();
  }

  if (!res.ok) {
    const detail = data?.detail;
    let msg = `Erreur ${res.status}`;
    if (typeof detail === "string") msg = detail;
    else if (detail?.message) msg = detail.message;
    else if (Array.isArray(detail)) msg = detail.map((d) => d.msg).join(", ");
    const err = new Error(msg);
    err.status = res.status;
    err.data = data;
    throw err;
  }
  return data;
}

// ─── Navigation ────────────────────────────────────────────────

function navigate(section) {
  $$(".section").forEach((s) => s.classList.remove("active"));
  $$(".nav-item").forEach((n) => n.classList.remove("active"));

  const sec = document.getElementById(`section-${section}`);
  const nav = document.querySelector(`[data-section="${section}"]`);
  if (sec) sec.classList.add("active");
  if (nav) nav.classList.add("active");

  if (section === "dashboard") loadDashboard();
  if (section === "plans") loadPlans();
  if (section === "billing") loadBilling();
  if (section === "blueprint" && state.conversationId && !state.blueprint) {
    loadBlueprint();
  }
}

function initNavigation() {
  $$(".nav-item").forEach((btn) => {
    btn.addEventListener("click", () => navigate(btn.dataset.section));
  });
  $$("[data-goto]").forEach((btn) => {
    btn.addEventListener("click", () => navigate(btn.dataset.goto));
  });
  $("#btn-new-conversation")?.addEventListener("click", () => {
    resetChat();
    navigate("chat");
  });
  $("#btn-view-plans")?.addEventListener("click", () => navigate("plans"));
}

// ─── Dashboard ─────────────────────────────────────────────────

async function loadDashboard() {
  const container = $("#dashboard-stats");
  showAlert("dashboard-alert");

  try {
    const org = await api("/organizations/me");
    state.organization = org;

    const limitText =
      org.deployments_limit != null
        ? `${org.deployments_used_this_month} / ${org.deployments_limit}`
        : `${org.deployments_used_this_month} / ∞`;

    container.innerHTML = `
      <div class="card">
        <h3>Organisation</h3>
        <div class="value">${escapeHtml(org.name)}</div>
        <div class="sub">ID : ${escapeHtml(org.id)}</div>
      </div>
      <div class="card">
        <h3>Plan actuel</h3>
        <div class="value stat-accent">${escapeHtml(org.plan_name)}</div>
        <div class="sub">${formatEur(org.monthly_subscription_eur)} / mois</div>
      </div>
      <div class="card">
        <h3>Déploiements ce mois</h3>
        <div class="value">${limitText}</div>
        <div class="sub">Quota mensuel</div>
      </div>
    `;
  } catch (err) {
    showAlert("dashboard-alert", err.message, "error");
    container.innerHTML = `<div class="empty-state"><p>Impossible de charger les statistiques.</p></div>`;
  }
}

// ─── Chat ──────────────────────────────────────────────────────

function renderMessages(messages) {
  const container = $("#chat-messages");
  const empty = $("#chat-empty");

  if (!messages || messages.length === 0) {
    empty.style.display = "flex";
    container.querySelectorAll(".message").forEach((m) => m.remove());
    return;
  }

  empty.style.display = "none";
  container.querySelectorAll(".message").forEach((m) => m.remove());

  messages.forEach((msg) => {
    const div = document.createElement("div");
    div.className = `message ${msg.role}`;
    div.innerHTML = `
      <div class="message-bubble">${escapeHtml(msg.content)}</div>
      <div class="message-meta">${msg.role === "user" ? "Vous" : "Assistant"} · ${formatDate(msg.created_at)}</div>
    `;
    container.appendChild(div);
  });

  container.scrollTop = container.scrollHeight;
}

function renderClarifyingQuestions(questions) {
  const container = $("#clarifying-container");
  if (!questions || questions.length === 0) {
    container.innerHTML = "";
    return;
  }
  container.innerHTML = `
    <div class="clarifying-questions">
      <h4>Questions de clarification suggérées</h4>
      <ul>${questions.map((q) => `<li>${escapeHtml(q)}</li>`).join("")}</ul>
    </div>
  `;
}

function updateChatActions() {
  const btnBlueprint = $("#btn-generate-blueprint");
  const btnSend = $("#btn-send");
  const hasConversation = !!state.conversationId;
  const hasMessages = state.conversation?.messages?.length > 0;

  btnBlueprint.disabled = !hasConversation || !hasMessages || state.loadingBlueprint;
  btnSend.disabled = state.sending;
}

function resetChat() {
  state.conversationId = null;
  state.conversation = null;
  state.blueprint = null;
  state.deploymentHint = null;
  renderMessages([]);
  renderClarifyingQuestions([]);
  showAlert("chat-alert");
  $("#chat-input").value = "";
  updateChatActions();
}

async function sendMessage() {
  const input = $("#chat-input");
  const text = input.value.trim();
  if (!text || state.sending) return;

  state.sending = true;
  updateChatActions();
  showAlert("chat-alert");

  try {
    let data;
    if (!state.conversationId) {
      data = await api("/conversations", {
        method: "POST",
        body: JSON.stringify({ message: text }),
      });
      state.conversationId = data.conversation.id;
      toast("Conversation démarrée", "success");
    } else {
      data = await api(`/conversations/${state.conversationId}/messages`, {
        method: "POST",
        body: JSON.stringify({ message: text }),
      });
    }

    state.conversation = data.conversation;
    input.value = "";
    renderMessages(state.conversation.messages);
    renderClarifyingQuestions(state.conversation.clarifying_questions);
    updateChatActions();
  } catch (err) {
    showAlert("chat-alert", err.message, "error");
    toast(err.message, "error");
  } finally {
    state.sending = false;
    updateChatActions();
    input.focus();
  }
}

async function generateBlueprint() {
  if (!state.conversationId || state.loadingBlueprint) return;

  state.loadingBlueprint = true;
  updateChatActions();
  showAlert("blueprint-alert");
  toast("Génération du blueprint en cours…", "info");

  try {
    const data = await api(`/conversations/${state.conversationId}/blueprint`);
    state.blueprint = data.blueprint;
    state.deploymentHint = data.deployment_hint;
    renderBlueprint(data);
    toast("Blueprint généré avec succès", "success");
    navigate("blueprint");
  } catch (err) {
    showAlert("chat-alert", err.message, "error");
    toast(err.message, "error");
  } finally {
    state.loadingBlueprint = false;
    updateChatActions();
  }
}

async function loadBlueprint() {
  if (!state.conversationId) return;

  state.loadingBlueprint = true;
  try {
    const data = await api(`/conversations/${state.conversationId}/blueprint`);
    state.blueprint = data.blueprint;
    state.deploymentHint = data.deployment_hint;
    renderBlueprint(data);
  } catch (err) {
    showAlert("blueprint-alert", err.message, "error");
  } finally {
    state.loadingBlueprint = false;
  }
}

function initChat() {
  $("#btn-send")?.addEventListener("click", sendMessage);
  $("#btn-new-chat")?.addEventListener("click", resetChat);
  $("#btn-generate-blueprint")?.addEventListener("click", generateBlueprint);

  const input = $("#chat-input");
  input?.addEventListener("keydown", (e) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  });
}

// ─── Blueprint ─────────────────────────────────────────────────

function renderReqList(items) {
  if (!items || items.length === 0) return "<li>Aucun élément</li>";
  return items.map((i) => `<li>${escapeHtml(i)}</li>`).join("");
}

function renderBlueprint(data) {
  const bp = data.blueprint;
  const container = $("#blueprint-content");
  const req = bp.requirements || {};

  const componentsHtml = (bp.components || [])
    .map(
      (c) => `
      <div class="component-card">
        <div class="type">${escapeHtml(c.type)}</div>
        <h4>${escapeHtml(c.name)}</h4>
        <p>${escapeHtml(c.description)}</p>
        ${c.technology_hint ? `<p style="margin-top:0.5rem;font-size:0.8rem;color:var(--accent-hover);">💡 ${escapeHtml(c.technology_hint)}</p>` : ""}
      </div>
    `
    )
    .join("");

  const secondaryTypes = (bp.secondary_types || [])
    .map((t) => `<span class="badge">${solutionTypeLabel(t)}</span>`)
    .join("");

  container.innerHTML = `
    <div class="blueprint-header">
      <h3>${escapeHtml(bp.title)}</h3>
      <p style="color:var(--text-muted);font-size:0.9rem;">${escapeHtml(bp.solution_type_rationale || "")}</p>
      <div class="blueprint-meta">
        <span class="badge success">${solutionTypeLabel(bp.solution_type)}</span>
        ${secondaryTypes}
        <span class="badge">Confiance : ${Math.round((bp.confidence || 0) * 100)} %</span>
        <span class="badge warning">Complétude : ${Math.round((req.completeness_score || 0) * 100)} %</span>
      </div>
    </div>

    <div class="grid-2" style="margin-bottom:1.5rem;">
      <div class="card">
        <h3>Objectifs</h3>
        <ul class="req-list">${renderReqList(req.objectives)}</ul>
      </div>
      <div class="card">
        <h3>Contraintes</h3>
        <ul class="req-list">${renderReqList(req.constraints)}</ul>
      </div>
      <div class="card">
        <h3>Sources de données</h3>
        <ul class="req-list">${renderReqList(req.data_sources)}</ul>
      </div>
      <div class="card">
        <h3>Volumes & SLA</h3>
        <ul class="req-list">${renderReqList(req.volumes)}</ul>
      </div>
    </div>

    ${req.summary ? `<div class="card" style="margin-bottom:1.5rem;"><h3>Résumé</h3><p style="font-size:0.92rem;">${escapeHtml(req.summary)}</p></div>` : ""}

    <div class="card" style="margin-bottom:1.5rem;">
      <h3>Composants (${(bp.components || []).length})</h3>
      ${componentsHtml || '<p style="color:var(--text-muted);">Aucun composant défini.</p>'}
    </div>

    ${(bp.data_flow || []).length > 0 ? `
    <div class="card" style="margin-bottom:1.5rem;">
      <h3>Flux de données</h3>
      <ol class="flow-steps">${bp.data_flow.map((s) => `<li>${escapeHtml(s)}</li>`).join("")}</ol>
    </div>` : ""}

    ${(bp.next_steps || []).length > 0 ? `
    <div class="card" style="margin-bottom:1.5rem;">
      <h3>Prochaines étapes</h3>
      <ul class="req-list">${renderReqList(bp.next_steps)}</ul>
    </div>` : ""}

    <div class="deploy-panel">
      <h3 style="font-size:1rem;margin-bottom:0.25rem;">Déployer l'agent</h3>
      <p style="font-size:0.88rem;color:var(--text-muted);">Le déploiement est une action facturée selon votre plan et la complexité du blueprint.</p>
      ${data.deployment_hint ? `<div class="deploy-cost">${escapeHtml(data.deployment_hint)}</div>` : ""}
      <div class="btn-group" style="margin-top:0;">
        <button class="btn btn-success" id="btn-deploy" ${state.deploying ? "disabled" : ""}>
          ${state.deploying ? '<span class="spinner"></span> Déploiement…' : "Déployer maintenant"}
        </button>
      </div>
      <div id="deploy-result"></div>
    </div>
  `;

  $("#btn-deploy")?.addEventListener("click", deployAgent);
}

async function deployAgent() {
  if (!state.conversationId || state.deploying) return;

  state.deploying = true;
  const btn = $("#btn-deploy");
  if (btn) {
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner"></span> Déploiement…';
  }

  const resultEl = $("#deploy-result");
  if (resultEl) resultEl.innerHTML = "";

  try {
    const data = await api(`/conversations/${state.conversationId}/deploy`, {
      method: "POST",
    });

    if (data.payment_pending && data.checkout_url) {
      toast("Redirection vers le paiement…", "warning");
      if (resultEl) {
        resultEl.innerHTML = `
          <div class="alert alert-warning" style="margin-top:1rem;">
            Paiement en attente — vous allez être redirigé vers GiseBsPayGateway.
            <div class="btn-group">
              <a class="btn btn-primary" href="${escapeHtml(data.checkout_url)}" target="_blank" rel="noopener">Ouvrir le checkout</a>
              <button class="btn btn-secondary" id="btn-confirm-payment">Confirmer le paiement</button>
            </div>
          </div>
        `;
        $("#btn-confirm-payment")?.addEventListener("click", () =>
          confirmPayment(data.payment_code)
        );
      }
      setTimeout(() => {
        window.open(data.checkout_url, "_blank");
      }, 800);
    } else {
      toast(data.message || "Déploiement réussi", "success");
      if (resultEl) {
        resultEl.innerHTML = `<div class="alert alert-success" style="margin-top:1rem;">${escapeHtml(data.message)}</div>`;
      }
    }
  } catch (err) {
    toast(err.message, "error");
    if (resultEl) {
      resultEl.innerHTML = `<div class="alert alert-error" style="margin-top:1rem;">${escapeHtml(err.message)}</div>`;
    }
  } finally {
    state.deploying = false;
    if (btn) {
      btn.disabled = false;
      btn.textContent = "Déployer maintenant";
    }
  }
}

async function confirmPayment(paymentCode) {
  if (!state.conversationId || !paymentCode) {
    toast("Code de paiement manquant", "error");
    return;
  }

  try {
    const data = await api(`/conversations/${state.conversationId}/deploy/confirm`, {
      method: "POST",
      body: JSON.stringify({ payment_code: paymentCode }),
    });

    if (data.payment_pending) {
      toast("Paiement toujours en attente", "warning");
    } else {
      toast(data.message || "Déploiement confirmé", "success");
      $("#deploy-result").innerHTML = `<div class="alert alert-success" style="margin-top:1rem;">${escapeHtml(data.message)}</div>`;
    }
  } catch (err) {
    toast(err.message, "error");
  }
}

// ─── Plans ─────────────────────────────────────────────────────

async function loadPlans() {
  const grid = $("#plans-grid");
  showAlert("plans-alert");

  try {
    if (state.plans.length === 0) {
      state.plans = await api("/plans");
    }
    if (!state.organization) {
      state.organization = await api("/organizations/me");
    }

    const currentPlan = state.organization?.plan;

    grid.innerHTML = state.plans
      .map((p) => {
        const isCurrent = p.plan === currentPlan;
        const deployLimit =
          p.limits.max_deployments_per_month != null
            ? `${p.limits.max_deployments_per_month} / mois`
            : "Illimité";

        const features = [];
        if (p.features.blueprint_generation) features.push("Génération de blueprint");
        if (p.features.priority_support) features.push("Support prioritaire");
        if (p.features.multi_department) features.push("Multi-départements");
        if (p.features.sso) features.push("SSO");
        if (p.features.custom_integrations) features.push("Intégrations personnalisées");
        features.push(`Frais déploiement : ${formatEur(p.limits.deployment_base_fee_eur)}`);
        features.push(`Déploiements : ${deployLimit}`);

        return `
          <div class="plan-card ${isCurrent ? "current" : ""}" data-plan="${p.plan}">
            <h3>${escapeHtml(p.name)}</h3>
            <p style="font-size:0.85rem;color:var(--text-muted);">${escapeHtml(p.description)}</p>
            <div class="plan-price">${p.monthly_price_eur === 0 ? "Gratuit" : formatEur(p.monthly_price_eur)}<span>${p.monthly_price_eur > 0 ? " / mois" : ""}</span></div>
            <ul class="plan-features">${features.map((f) => `<li>${escapeHtml(f)}</li>`).join("")}</ul>
            ${
              isCurrent
                ? '<button class="btn btn-secondary" disabled>Plan actuel</button>'
                : `<button class="btn btn-primary btn-subscribe" data-plan="${p.plan}">S'abonner</button>`
            }
          </div>
        `;
      })
      .join("");

    $$(".btn-subscribe").forEach((btn) => {
      btn.addEventListener("click", () => subscribe(btn.dataset.plan, btn));
    });
  } catch (err) {
    showAlert("plans-alert", err.message, "error");
    grid.innerHTML = `<div class="empty-state"><p>Impossible de charger les plans.</p></div>`;
  }
}

async function subscribe(plan, btn) {
  if (!state.organization) {
    try {
      state.organization = await api("/organizations/me");
    } catch (err) {
      toast(err.message, "error");
      return;
    }
  }

  btn.disabled = true;
  btn.innerHTML = '<span class="spinner"></span>';

  try {
    const data = await api(`/organizations/${state.organization.id}/subscribe`, {
      method: "POST",
      body: JSON.stringify({ plan }),
    });

    if (data.checkout_url) {
      toast("Redirection vers le paiement…", "warning");
      setTimeout(() => window.open(data.checkout_url, "_blank"), 800);
    } else {
      toast(data.message || "Abonnement mis à jour", "success");
      state.organization = await api("/organizations/me");
      loadPlans();
    }
  } catch (err) {
    toast(err.message, "error");
    btn.disabled = false;
    btn.textContent = "S'abonner";
  }
}

// ─── Billing ───────────────────────────────────────────────────

async function loadBilling() {
  const summaryEl = $("#billing-summary");
  const eventsEl = $("#billing-events-table");
  const deployEl = $("#deployments-table");
  showAlert("billing-alert");

  try {
    if (!state.organization) {
      state.organization = await api("/organizations/me");
    }

    const data = await api(`/organizations/${state.organization.id}/billing`);
    state.billing = data;

    const limitText =
      data.deployments_limit != null
        ? `${data.deployments_used_this_month} / ${data.deployments_limit}`
        : `${data.deployments_used_this_month} / ∞`;

    summaryEl.innerHTML = `
      <div class="card">
        <h3>Total facturé</h3>
        <div class="value stat-accent">${formatEur(data.total_billed)}</div>
        <div class="sub">Devise : ${escapeHtml(data.currency)}</div>
      </div>
      <div class="card">
        <h3>Plan</h3>
        <div class="value">${escapeHtml(data.plan_name)}</div>
        <div class="sub">${escapeHtml(data.organization.name)}</div>
      </div>
      <div class="card">
        <h3>Déploiements ce mois</h3>
        <div class="value">${limitText}</div>
      </div>
    `;

    if (data.billing_events.length === 0) {
      eventsEl.innerHTML = `<div class="empty-state" style="padding:2rem;"><p>Aucun événement de facturation.</p></div>`;
    } else {
      eventsEl.innerHTML = `
        <table>
          <thead>
            <tr>
              <th>Date</th>
              <th>Description</th>
              <th>Type</th>
              <th>Montant</th>
              <th>Statut</th>
            </tr>
          </thead>
          <tbody>
            ${data.billing_events
              .map(
                (e) => `
              <tr>
                <td>${formatDate(e.created_at)}</td>
                <td>${escapeHtml(e.description)}</td>
                <td>${escapeHtml(e.event_type)}</td>
                <td>${formatEur(e.amount)}</td>
                <td><span class="status-pill ${e.status}">${statusLabel(e.status)}</span></td>
              </tr>
            `
              )
              .join("")}
          </tbody>
        </table>
      `;
    }

    if (data.deployments.length === 0) {
      deployEl.innerHTML = `<div class="empty-state" style="padding:2rem;"><p>Aucun déploiement.</p></div>`;
    } else {
      deployEl.innerHTML = `
        <table>
          <thead>
            <tr>
              <th>Date</th>
              <th>Conversation</th>
              <th>Coût</th>
              <th>Complexité</th>
              <th>Statut</th>
            </tr>
          </thead>
          <tbody>
            ${data.deployments
              .map(
                (d) => `
              <tr>
                <td>${formatDate(d.created_at)}</td>
                <td style="font-family:monospace;font-size:0.8rem;">${escapeHtml(d.conversation_id.slice(0, 8))}…</td>
                <td>${formatEur(d.deployment_cost)}</td>
                <td>${(d.complexity_score || 0).toFixed(2)}</td>
                <td><span class="status-pill ${d.status}">${statusLabel(d.status)}</span></td>
              </tr>
            `
              )
              .join("")}
          </tbody>
        </table>
      `;
    }
  } catch (err) {
    showAlert("billing-alert", err.message, "error");
  }
}

// ─── Init ──────────────────────────────────────────────────────

document.addEventListener("DOMContentLoaded", () => {
  initNavigation();
  initChat();
  loadDashboard();
});
