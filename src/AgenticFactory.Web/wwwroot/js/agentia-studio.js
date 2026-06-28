(function () {
    'use strict';

    const STORAGE_KEY = 'agentia-studio-draft';
    const TOTAL_STEPS = 8;
    const MAX_CHARS = 2000;

    const DOMAINS = [
        { id: 'comptabilite', icon: 'bi-calculator', bg: '#fef3c7', color: '#d97706', name: 'Comptabilité', desc: 'Factures, écritures, rapports financiers', namePrefix: 'Finance' },
        { id: 'email', icon: 'bi-envelope', bg: '#eef2ff', color: '#6366f1', name: 'Email', desc: 'Courrier, tri, réponses automatiques', namePrefix: 'Email', defaultAgent: 'Email Invoice Agent' },
        { id: 'documents', icon: 'bi-file-earmark-text', bg: '#fce7f3', color: '#db2777', name: 'Documents', desc: 'PDF, Word, OCR et archivage', namePrefix: 'Document' },
        { id: 'support', icon: 'bi-headset', bg: '#dcfce7', color: '#16a34a', name: 'Support client', desc: 'Tickets, FAQ, escalade', namePrefix: 'Support' },
        { id: 'rh', icon: 'bi-people', bg: '#dbeafe', color: '#2563eb', name: 'Ressources Humaines', desc: 'Recrutement, onboarding, dossiers', namePrefix: 'HR' },
        { id: 'marketing', icon: 'bi-megaphone', bg: '#f3e8ff', color: '#9333ea', name: 'Marketing', desc: 'Campagnes, contenu, analytics', namePrefix: 'Marketing' },
        { id: 'vente', icon: 'bi-briefcase', bg: '#ecfdf5', color: '#059669', name: 'Ventes / CRM', desc: 'Prospects, pipeline, relances', namePrefix: 'Sales' },
        { id: 'juridique', icon: 'bi-file-earmark-ruled', bg: '#f1f5f9', color: '#475569', name: 'Juridique', desc: 'Contrats, conformité, veille', namePrefix: 'Legal' },
        { id: 'data', icon: 'bi-bar-chart', bg: '#e0e7ff', color: '#4f46e5', name: 'Analyse de données', desc: 'KPI, tableaux de bord, insights', namePrefix: 'Analytics' },
        { id: 'devops', icon: 'bi-cloud', bg: '#cffafe', color: '#0891b2', name: 'DevOps / IT', desc: 'CI/CD, infra, monitoring', namePrefix: 'DevOps' },
        { id: 'cyber', icon: 'bi-shield-lock', bg: '#fee2e2', color: '#dc2626', name: 'Cybersécurité', desc: 'Alertes, audit, conformité', namePrefix: 'Security' },
        { id: 'autre', icon: 'bi-plus-lg', bg: '#f8fafc', color: '#64748b', name: 'Autre domaine', desc: 'Domaine personnalisé', namePrefix: 'Custom' }
    ];

    const OBJECTIVES = [
        { id: 'lire-analyser', label: 'Lire et analyser', domains: ['email', 'documents', 'data'] },
        { id: 'extraire', label: 'Extraire des données', domains: ['documents', 'comptabilite', 'email'] },
        { id: 'classifier', label: 'Classifier', domains: ['email', 'documents', 'support'] },
        { id: 'repondre', label: 'Répondre automatiquement', domains: ['email', 'support'] },
        { id: 'rapport', label: 'Générer un rapport', domains: ['comptabilite', 'data', 'marketing'] },
        { id: 'notifier', label: 'Notifier une personne', domains: ['support', 'rh', 'devops'] },
        { id: 'tache', label: 'Créer une tâche', domains: ['rh', 'support', 'vente'] },
        { id: 'maj-db', label: 'Mettre à jour une base', domains: ['comptabilite', 'vente', 'data'] },
        { id: 'api', label: 'Appeler une API', domains: ['devops', 'data', 'autre'] },
        { id: 'surveiller', label: 'Surveiller une situation', domains: ['cyber', 'devops', 'email'] },
        { id: 'traduire', label: 'Traduire', domains: ['documents', 'support'] },
        { id: 'automatiser', label: 'Automatiser un processus', domains: ['autre', 'rh', 'comptabilite'] }
    ];

    const SOURCES = [
        'Outlook', 'Gmail', 'IMAP', 'SharePoint', 'OneDrive', 'Google Drive',
        'Dossier Windows', 'PDF', 'Word', 'Excel', 'CSV',
        'SQL Server', 'PostgreSQL', 'API REST', 'Webhook', 'Upload manuel'
    ];

    const ACTIONS = [
        'Lire', 'Extraire', 'Résumer', 'Classifier', 'Comparer', 'Valider',
        'Générer', 'Envoyer email', 'Créer ticket', 'Notifier Teams',
        'Archiver', 'Exporter JSON', 'Appeler API', 'OCR', 'Télécharger'
    ];

    const TRIGGERS = [
        { id: 'manual', label: 'Manuel' },
        { id: '5min', label: 'Toutes les 5 minutes' },
        { id: 'hourly', label: 'Toutes les heures' },
        { id: 'daily', label: 'Tous les jours' },
        { id: 'email-in', label: 'À réception d\'un email' },
        { id: 'file-created', label: 'À création d\'un fichier' },
        { id: 'webhook', label: 'Via Webhook' },
        { id: 'api', label: 'Via API' }
    ];

    const RUNTIMES = [
        { id: 'windows-service', label: 'Windows Service' },
        { id: 'docker', label: 'Docker' },
        { id: 'kubernetes', label: 'Kubernetes' },
        { id: 'linux-service', label: 'Linux Service' },
        { id: 'azure', label: 'Azure' },
        { id: 'aws', label: 'AWS' }
    ];

    const AUTONOMY_LEVELS = [
        { id: 0, label: 'Assistant', pct: 25 },
        { id: 1, label: 'Semi-autonome', pct: 50 },
        { id: 2, label: 'Autonome', pct: 75 },
        { id: 3, label: 'Superviseur multi-agent', pct: 100 }
    ];

    const SECURITY = [
        'Lecture seule', 'Lecture / écriture', 'Validation humaine obligatoire',
        'Journalisation complète', 'Accès API protégé', 'Secrets chiffrés', 'Quotas activés'
    ];

    let state = {
        step: 1,
        domain: 'email',
        customName: '',
        objectives: [],
        sources: [],
        actions: [],
        trigger: 'manual',
        runtime: 'windows-service',
        autonomy: 0,
        security: [],
        freeText: ''
    };

    const $ = (sel, ctx) => (ctx || document).querySelector(sel);
    const $$ = (sel, ctx) => [...(ctx || document).querySelectorAll(sel)];
    const UNDEF = '— Non défini —';

    function init() {
        if (!$('.studio-page')) return;
        loadDraft();
        renderDomainGrid();
        renderObjectives();
        renderSources();
        renderActions();
        renderTriggers();
        renderRuntimes();
        renderSecurity();
        bindAutonomySlider();
        bindFreeText();
        bindNavigation();
        bindFormSubmit();
        bindNameEdit();
        goToStep(state.step, false);
        updateBlueprint();
    }

    function renderDomainGrid() {
        const grid = $('#domainGrid');
        if (!grid) return;
        grid.innerHTML = DOMAINS.map(d => `
            <button type="button" class="studio-domain-card${state.domain === d.id ? ' selected' : ''}" data-domain="${d.id}">
                <span class="studio-domain-check"><i class="bi bi-check"></i></span>
                <div class="studio-domain-icon-wrap" style="background:${d.bg};color:${d.color}">
                    <i class="bi ${d.icon}"></i>
                </div>
                <div class="studio-domain-name">${d.name}</div>
                <div class="studio-domain-desc">${d.desc}</div>
            </button>
        `).join('');

        $$('.studio-domain-card', grid).forEach(card => {
            card.addEventListener('click', () => {
                state.domain = card.dataset.domain;
                state.customName = '';
                $$('.studio-domain-card', grid).forEach(c => c.classList.toggle('selected', c === card));
                renderObjectives();
                updateBlueprint();
            });
        });
    }

    function renderObjectives() {
        const grid = $('#objectiveGrid');
        if (!grid) return;
        const sorted = [...OBJECTIVES].sort((a, b) => {
            const aRec = state.domain && a.domains.includes(state.domain) ? 0 : 1;
            const bRec = state.domain && b.domains.includes(state.domain) ? 0 : 1;
            return aRec - bRec;
        });
        grid.innerHTML = sorted.map(o => {
            const checked = state.objectives.includes(o.id);
            const rec = state.domain && o.domains.includes(state.domain);
            return `
                <label class="studio-check-item${checked ? ' checked' : ''}" data-id="${o.id}">
                    <input type="checkbox" ${checked ? 'checked' : ''}>
                    ${o.label}${rec ? ' <span class="studio-badge">Recommandé</span>' : ''}
                </label>`;
        }).join('');
        bindCheckGrid(grid, 'objectives');
    }

    function renderSources() {
        const grid = $('#sourceGrid');
        if (!grid) return;
        grid.innerHTML = SOURCES.map(s => {
            const checked = state.sources.includes(s);
            return `<label class="studio-check-item${checked ? ' checked' : ''}" data-id="${s}">
                <input type="checkbox" ${checked ? 'checked' : ''}> ${s}</label>`;
        }).join('');
        bindCheckGrid(grid, 'sources');
    }

    function renderActions() {
        const grid = $('#actionGrid');
        if (!grid) return;
        grid.innerHTML = ACTIONS.map(a => {
            const sel = state.actions.includes(a);
            return `<button type="button" class="studio-chip${sel ? ' selected' : ''}" data-id="${a}">${a}</button>`;
        }).join('');
        $$('.studio-chip', grid).forEach(chip => {
            chip.addEventListener('click', () => {
                const id = chip.dataset.id;
                if (state.actions.includes(id)) {
                    state.actions = state.actions.filter(x => x !== id);
                    chip.classList.remove('selected');
                } else {
                    state.actions.push(id);
                    chip.classList.add('selected');
                }
                updateBlueprint();
            });
        });
    }

    function renderTriggers() {
        const grid = $('#triggerGrid');
        if (!grid) return;
        grid.innerHTML = TRIGGERS.map(t => `
            <label class="studio-radio-item${state.trigger === t.id ? ' selected' : ''}" data-id="${t.id}">
                <input type="radio" name="trigger" ${state.trigger === t.id ? 'checked' : ''}> ${t.label}
            </label>`).join('');
        bindRadioGrid(grid, 'trigger');
    }

    function renderRuntimes() {
        const grid = $('#runtimeGrid');
        if (!grid) return;
        grid.innerHTML = RUNTIMES.map(r => `
            <label class="studio-radio-item${state.runtime === r.id ? ' selected' : ''}" data-id="${r.id}">
                <input type="radio" name="runtime" ${state.runtime === r.id ? 'checked' : ''}> ${r.label}
            </label>`).join('');
        bindRadioGrid(grid, 'runtime');
    }

    function renderSecurity() {
        const grid = $('#securityGrid');
        if (!grid) return;
        grid.innerHTML = SECURITY.map(s => {
            const checked = state.security.includes(s);
            return `<label class="studio-check-item${checked ? ' checked' : ''}" data-id="${s}">
                <input type="checkbox" ${checked ? 'checked' : ''}> ${s}</label>`;
        }).join('');
        bindCheckGrid(grid, 'security');
    }

    function bindCheckGrid(grid, key) {
        $$('.studio-check-item', grid).forEach(item => {
            item.addEventListener('click', e => {
                e.preventDefault();
                const val = item.dataset.id;
                const arr = state[key];
                const idx = arr.indexOf(val);
                if (idx >= 0) {
                    arr.splice(idx, 1);
                    item.classList.remove('checked');
                    $('input', item).checked = false;
                } else {
                    arr.push(val);
                    item.classList.add('checked');
                    $('input', item).checked = true;
                }
                updateBlueprint();
            });
        });
    }

    function bindRadioGrid(grid, key) {
        $$('.studio-radio-item', grid).forEach(item => {
            item.addEventListener('click', () => {
                state[key] = item.dataset.id;
                $$('.studio-radio-item', grid).forEach(i => i.classList.toggle('selected', i === item));
                updateBlueprint();
            });
        });
    }

    function bindAutonomySlider() {
        const slider = $('#autonomySlider');
        if (!slider) return;
        slider.value = state.autonomy;
        slider.addEventListener('input', () => {
            state.autonomy = parseInt(slider.value, 10);
            updateAutonomyDisplay();
            updateBlueprint();
        });
        updateAutonomyDisplay();
    }

    function updateAutonomyDisplay() {
        const level = AUTONOMY_LEVELS[state.autonomy];
        const label = $('#autonomyLabel');
        const fill = $('#autonomyFill');
        if (label) label.textContent = level.label;
        if (fill) fill.style.width = level.pct + '%';
    }

    function bindFreeText() {
        const ta = $('#freeText');
        const counter = $('#charCount');
        if (!ta) return;
        ta.value = state.freeText;
        const sync = () => {
            state.freeText = ta.value.slice(0, MAX_CHARS);
            if (counter) counter.textContent = state.freeText.length;
            updateBlueprint();
        };
        ta.addEventListener('input', sync);
        sync();
    }

    function bindNameEdit() {
        $('#bpEditName')?.addEventListener('click', () => {
            const current = suggestAgentName();
            const name = prompt('Nom de l\'agent :', state.customName || current);
            if (name !== null) {
                state.customName = name.trim();
                updateBlueprint();
            }
        });
    }

    function bindNavigation() {
        $('#btnPrev')?.addEventListener('click', () => goToStep(state.step - 1));
        $('#btnNext')?.addEventListener('click', () => {
            if (validateStep(state.step)) goToStep(state.step + 1);
        });
        $('#btnDraft')?.addEventListener('click', saveDraft);
    }

    function bindFormSubmit() {
        $('#studioForm')?.addEventListener('submit', e => {
            if (!validateForSubmit()) { e.preventDefault(); return; }
            const payload = buildPayload();
            $('#hiddenMessage').value = compileMessage(payload);
            $('#hiddenWizardJson').value = JSON.stringify(payload);
        });
    }

    function validateStep(step) {
        const msg = $('#validationMsg');
        let ok = true, text = '';
        if (step === 1 && !state.domain) {
            ok = false;
            text = 'Sélectionnez un domaine métier pour continuer.';
        } else if (step === 2 && state.objectives.length === 0) {
            ok = false;
            text = 'Choisissez au moins un objectif.';
        }
        if (msg) {
            msg.textContent = text;
            msg.classList.toggle('show', !ok);
        }
        return ok;
    }

    function validateForSubmit() {
        if (!state.domain && !state.freeText.trim()) {
            alert('Complétez au minimum le domaine ou la description libre.');
            goToStep(1);
            return false;
        }
        return true;
    }

    function goToStep(n, validate) {
        if (n < 1 || n > TOTAL_STEPS) return;
        if (validate !== false && n > state.step && !validateStep(state.step)) return;

        state.step = n;
        $$('.studio-step-panel').forEach(p => p.classList.remove('active'));
        $(`#stepPanel${n}`)?.classList.add('active');

        $$('.studio-stepper-item').forEach(item => {
            const s = parseInt(item.dataset.step, 10);
            item.classList.remove('active', 'done');
            if (s === n) item.classList.add('active');
            else if (s < n) item.classList.add('done');
        });

        const prev = $('#btnPrev');
        if (prev) prev.style.display = n > 1 ? '' : 'none';

        const next = $('#btnNext');
        const gen = $('#btnGenerate');
        if (next) next.style.display = n === TOTAL_STEPS ? 'none' : '';
        if (gen) gen.style.display = n === TOTAL_STEPS ? '' : 'none';

        if (n === TOTAL_STEPS) renderFinalReview();
        updateBlueprint();
    }

    function getDomain() { return DOMAINS.find(d => d.id === state.domain); }

    function getObjectiveLabels() {
        return state.objectives.map(id => OBJECTIVES.find(o => o.id === id)?.label).filter(Boolean);
    }

    function getTriggerLabel() {
        return TRIGGERS.find(t => t.id === state.trigger)?.label || UNDEF;
    }

    function getRuntimeLabel() {
        return RUNTIMES.find(r => r.id === state.runtime)?.label || 'Windows Service';
    }

    function suggestAgentName() {
        if (state.customName) return state.customName;
        const domain = getDomain();
        if (!domain) return 'Nouvel Agent IA';
        if (domain.defaultAgent && !getObjectiveLabels().length) return domain.defaultAgent;
        const objs = getObjectiveLabels();
        const suffix = objs[0] ? objs[0].split(' ').slice(-1)[0] : 'Assistant';
        return `${domain.namePrefix} ${suffix} Agent`;
    }

    function computeComplexity() {
        let score = state.domain ? 2 : 0;
        score += state.objectives.length * 0.5;
        score += state.sources.length * 0.25;
        score += state.actions.length * 0.3;
        score += state.autonomy * 0.35;
        return Math.min(5, Math.max(0, Math.round(score)));
    }

    function computeCost() {
        if (!state.objectives.length && !state.sources.length && !state.actions.length) return 0;
        const base = 0.5;
        const perSource = state.sources.length * 0.18;
        const perAction = state.actions.length * 0.14;
        const mult = { manual: 1, '5min': 2.2, hourly: 1.5, daily: 1.1, 'email-in': 1.4, 'file-created': 1.3, webhook: 1.2, api: 1.25 };
        return (base + perSource + perAction) * (mult[state.trigger] || 1) * (1 + state.autonomy * 0.2);
    }

    function buildWorkflowText() {
        const parts = [];
        if (state.sources.length) parts.push(`1. Collecter : ${state.sources.join(', ')}`);
        if (state.actions.length) parts.push(`2. Traiter : ${state.actions.join(' → ')}`);
        parts.push(`3. Déclenchement : ${getTriggerLabel()}`);
        parts.push(`4. Autonomie : ${AUTONOMY_LEVELS[state.autonomy].label}`);
        return parts.join('\n');
    }

    function buildPayload() {
        return {
            domain: getDomain()?.name || '',
            domainId: state.domain,
            objectives: getObjectiveLabels(),
            sources: [...state.sources],
            actions: [...state.actions],
            trigger: getTriggerLabel(),
            triggerId: state.trigger,
            runtime: getRuntimeLabel(),
            runtimeId: state.runtime,
            autonomy: AUTONOMY_LEVELS[state.autonomy].label,
            autonomyLevel: state.autonomy,
            security: [...state.security],
            freeText: state.freeText.trim(),
            agentName: suggestAgentName(),
            complexity: computeComplexity(),
            estimatedCost: computeCost().toFixed(2),
            aiModel: computeComplexity() >= 3 ? 'GPT-4.1' : 'GPT-4.1 mini'
        };
    }

    function compileMessage(p) {
        const lines = [
            'Créer un agent IA via Agent Factory Studio :',
            `- Domaine : ${p.domain}`,
            `- Objectifs : ${p.objectives.join(', ') || 'À définir'}`,
            `- Sources : ${p.sources.join(', ') || 'Aucune'}`,
            `- Actions : ${p.actions.join(', ') || 'Aucune'}`,
            `- Déclencheur : ${p.trigger}`,
            `- Runtime : ${p.runtime}`,
            `- Autonomie : ${p.autonomy}`,
            `- Sécurité : ${p.security.join(', ')}`,
            `- Nom : ${p.agentName}`
        ];
        if (p.freeText) lines.push(`Description : ${p.freeText}`);
        return lines.join('\n');
    }

    function renderStars(n) {
        return [1, 2, 3, 4, 5].map(i =>
            `<span class="${i <= n ? 'filled' : ''}">★</span>`
        ).join('');
    }

    function setText(id, text, undefined) {
        const el = $(`#bp-${id}`);
        if (!el) return;
        el.textContent = text;
        el.classList.toggle('undefined', !!undefined);
    }

    function updateBlueprint() {
        const p = buildPayload();
        const domain = getDomain();
        const objs = getObjectiveLabels();

        const nameEl = $('#bp-name');
        if (nameEl) nameEl.textContent = suggestAgentName();

        const catEl = $('#bp-category');
        if (catEl && domain) {
            catEl.innerHTML = `<span class="studio-bp-pill">${domain.name}</span>`;
        }

        setText('objective', objs.length ? objs.join(', ') : UNDEF, !objs.length);

        const srcEl = $('#bp-sources');
        if (srcEl) {
            if (state.sources.length) {
                srcEl.textContent = state.sources.join(', ');
                srcEl.classList.remove('undefined');
            } else {
                srcEl.textContent = UNDEF;
                srcEl.classList.add('undefined');
            }
        }

        const actEl = $('#bp-actions');
        if (actEl) {
            if (state.actions.length) {
                actEl.textContent = state.actions.join(', ');
                actEl.classList.remove('undefined');
            } else {
                actEl.textContent = UNDEF;
                actEl.classList.add('undefined');
            }
        }

        const trigEl = $('#bp-trigger');
        if (trigEl) {
            const isDefault = state.trigger === 'manual' && state.step < 5;
            trigEl.textContent = isDefault ? UNDEF : getTriggerLabel();
            trigEl.classList.toggle('undefined', isDefault);
        }

        const autoFill = $('#bp-autonomy-fill');
        const autoLabel = $('#bp-autonomy');
        const level = AUTONOMY_LEVELS[state.autonomy];
        if (autoFill) autoFill.style.width = level.pct + '%';
        if (autoLabel) autoLabel.textContent = level.label;

        const compEl = $('#bp-complexity');
        if (compEl) compEl.innerHTML = renderStars(p.complexity);

        const costEl = $('#bp-cost');
        if (costEl) costEl.textContent = `$${p.estimatedCost} / mois`;

        const rtEl = $('#bp-runtime');
        if (rtEl) rtEl.innerHTML = `<span class="studio-bp-pill blue">${getRuntimeLabel()}</span>`;

        const empty = $('#bp-empty');
        const progress = (state.domain ? 1 : 0) + state.objectives.length + state.sources.length + state.actions.length;
        if (empty) empty.style.display = progress <= 1 ? '' : 'none';
    }

    function renderFinalReview() {
        const p = buildPayload();
        const review = $('#finalReview');
        if (!review) return;
        review.innerHTML = `
            <div class="studio-review-grid">
                <div class="studio-review-row">
                    <span class="studio-review-label">Nom</span>
                    <span class="studio-review-value"><strong>${escapeHtml(p.agentName)}</strong></span>
                </div>
                <div class="studio-review-row">
                    <span class="studio-review-label">Modèle IA</span>
                    <span class="studio-review-value">${p.aiModel}</span>
                </div>
                <div class="studio-review-row">
                    <span class="studio-review-label">Permissions</span>
                    <span class="studio-review-value">${p.security.map(s => `<span class="studio-badge green">${s}</span>`).join('')}</span>
                </div>
            </div>
            <div class="studio-subsection">
                <h4>Workflow généré</h4>
                <div class="studio-workflow-preview">${buildWorkflowText().replace(/\n/g, '<br>')}</div>
            </div>
            ${p.freeText ? `<div class="studio-subsection"><h4>Description</h4><p>${escapeHtml(p.freeText)}</p></div>` : ''}`;
    }

    function escapeHtml(s) {
        const d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    function saveDraft() {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
            $('#draftToast')?.classList.add('show');
            setTimeout(() => $('#draftToast')?.classList.remove('show'), 2500);
        } catch (_) { /* ignore */ }
    }

    function loadDraft() {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            if (!raw) return;
            Object.assign(state, JSON.parse(raw));
        } catch (_) { /* ignore */ }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
