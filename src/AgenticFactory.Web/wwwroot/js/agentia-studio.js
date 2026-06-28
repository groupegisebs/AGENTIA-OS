(function () {
    'use strict';

    const STORAGE_KEY = 'agentia-studio-draft';
    const TOTAL_STEPS = 8;
    const MAX_CHARS = 2000;

    function getDomains() {
        if (window.STUDIO_DOMAINS?.length) return window.STUDIO_DOMAINS;
        return [
            { id: 'email', icon: 'bi-envelope', bg: '#eef2ff', color: '#6366f1', name: 'Email', desc: 'Courrier, tri, réponses automatiques', namePrefix: 'Email', defaultAgent: 'Email Invoice Agent' }
        ];
    }

    const OBJECTIVES = [
        { id: 'lire-analyser', label: 'Lire et analyser', icon: 'bi-search', domains: ['email', 'documents', 'data', 'medias'] },
        { id: 'lire-emails', label: 'Lire des emails', icon: 'bi-envelope-open', domains: ['email', 'comptabilite', 'support'] },
        { id: 'extraire', label: 'Extraire des données', icon: 'bi-box-arrow-down', domains: ['documents', 'comptabilite', 'email', 'ecommerce'] },
        { id: 'classifier', label: 'Classifier', icon: 'bi-tags', domains: ['email', 'documents', 'support', 'juridique'] },
        { id: 'repondre', label: 'Répondre automatiquement', icon: 'bi-reply', domains: ['email', 'support', 'vente'] },
        { id: 'rapport', label: 'Générer un rapport', icon: 'bi-file-bar-graph', domains: ['comptabilite', 'data', 'marketing', 'qualite'] },
        { id: 'notifier', label: 'Notifier une personne', icon: 'bi-bell', domains: ['support', 'rh', 'devops', 'cyber'] },
        { id: 'tache', label: 'Créer une tâche', icon: 'bi-check2-square', domains: ['rh', 'support', 'vente', 'projet'] },
        { id: 'maj-db', label: 'Mettre à jour une base', icon: 'bi-database', domains: ['comptabilite', 'vente', 'data', 'ecommerce'] },
        { id: 'api', label: 'Appeler une API', icon: 'bi-plug', domains: ['devops', 'data', 'custom', 'ecommerce', 'logistique'] },
        { id: 'surveiller', label: 'Surveiller une situation', icon: 'bi-eye', domains: ['cyber', 'devops', 'email', 'industrie'] },
        { id: 'traduire', label: 'Traduire', icon: 'bi-translate', domains: ['documents', 'support', 'marketing'] },
        { id: 'automatiser', label: 'Automatiser un processus', icon: 'bi-arrow-repeat', domains: ['custom', 'rh', 'comptabilite', 'industrie', 'logistique'] },
        { id: 'resumer', label: 'Résumer un contenu', icon: 'bi-text-paragraph', domains: ['documents', 'email', 'data', 'education'] },
        { id: 'comparer', label: 'Comparer des documents', icon: 'bi-files', domains: ['documents', 'juridique', 'comptabilite'] },
        { id: 'valider', label: 'Valider des documents', icon: 'bi-patch-check', domains: ['documents', 'rh', 'qualite', 'juridique'] },
        { id: 'approuver', label: 'Approuver une demande', icon: 'bi-hand-thumbs-up', domains: ['rh', 'comptabilite', 'juridique'] },
        { id: 'anomalie', label: 'Détecter une anomalie', icon: 'bi-exclamation-triangle', domains: ['cyber', 'comptabilite', 'industrie', 'data'] },
        { id: 'enrichir', label: 'Enrichir des données', icon: 'bi-plus-circle', domains: ['vente', 'data', 'marketing', 'ecommerce'] },
        { id: 'sync', label: 'Synchroniser des systèmes', icon: 'bi-arrow-left-right', domains: ['devops', 'ecommerce', 'logistique', 'vente'] },
        { id: 'archiver', label: 'Archiver automatiquement', icon: 'bi-archive', domains: ['documents', 'email', 'juridique', 'rh'] },
        { id: 'planifier', label: 'Planifier une action', icon: 'bi-calendar-event', domains: ['projet', 'rh', 'productivite', 'marketing'] },
        { id: 'escalader', label: 'Escalader un incident', icon: 'bi-arrow-up-circle', domains: ['support', 'cyber', 'devops'] },
        { id: 'qualifier-lead', label: 'Qualifier un lead', icon: 'bi-funnel', domains: ['vente', 'marketing', 'ecommerce'] },
        { id: 'scorer-risque', label: 'Scorer un risque', icon: 'bi-speedometer2', domains: ['banque', 'cyber', 'juridique', 'data'] },
        { id: 'brouillon', label: 'Générer un brouillon', icon: 'bi-pencil-square', domains: ['marketing', 'juridique', 'support', 'rh'] },
        { id: 'conformite', label: 'Contrôler la conformité', icon: 'bi-shield-check', domains: ['juridique', 'qualite', 'banque', 'cyber'] },
        { id: 'reconcilier', label: 'Réconcilier des comptes', icon: 'bi-calculator', domains: ['comptabilite', 'banque', 'ecommerce'] },
        { id: 'parser', label: 'Parser un formulaire', icon: 'bi-ui-checks', domains: ['documents', 'rh', 'support'] },
        { id: 'ocr', label: 'Traiter des scans (OCR)', icon: 'bi-file-earmark-image', domains: ['documents', 'comptabilite', 'sante'] },
        { id: 'router', label: 'Router vers la bonne équipe', icon: 'bi-signpost-split', domains: ['support', 'email', 'rh'] },
        { id: 'monitorer-kpi', label: 'Monitorer des KPI', icon: 'bi-graph-up-arrow', domains: ['data', 'marketing', 'vente', 'projet'] },
        { id: 'onboard', label: 'Onboarder un collaborateur', icon: 'bi-person-plus', domains: ['rh', 'education'] },
        { id: 'veille', label: 'Assurer une veille', icon: 'bi-rss', domains: ['juridique', 'cyber', 'medias', 'marketing'] },
        { id: 'facturer', label: 'Préparer une facturation', icon: 'bi-receipt', domains: ['comptabilite', 'ecommerce', 'vente'] },
        { id: 'inventorier', label: 'Inventorier des actifs', icon: 'bi-box-seam', domains: ['logistique', 'industrie', 'immobilier'] }
    ];

    const SOURCES = window.STUDIO_SOURCE_CATALOG?.items || [];
    const ACTIONS = window.STUDIO_ACTION_CATALOG?.items || [];
    const EXEC = () => window.STUDIO_EXECUTION;

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
        sourceConfigs: {},
        actions: [],
        actionConfigs: {},
        execution: null,
        autonomy: 0,
        security: [],
        customDomainLabel: '',
        freeText: ''
    };

    let latestEstimate = null;
    let estimateTimer = null;

    const $ = (sel, ctx) => (ctx || document).querySelector(sel);
    const $$ = (sel, ctx) => [...(ctx || document).querySelectorAll(sel)];
    const UNDEF = '— Non défini —';

    function init() {
        if (!$('.studio-page')) return;
        try { loadDraft(); } catch (e) { console.error('Agentia Studio — loadDraft', e); }
        try { renderDomainGrid(); } catch (e) { console.error('Agentia Studio — renderDomainGrid', e); }
        try { renderObjectives(); } catch (e) { console.error('Agentia Studio — renderObjectives', e); }
        try { renderSources(); } catch (e) { console.error('Agentia Studio — renderSources', e); }
        try { renderActions(); } catch (e) { console.error('Agentia Studio — renderActions', e); }
        try { renderExecution(); } catch (e) { console.error('Agentia Studio — renderExecution', e); }
        try { renderSecurity(); } catch (e) { console.error('Agentia Studio — renderSecurity', e); }
        bindAutonomySlider();
        bindFreeText();
        bindNavigation();
        bindFormSubmit();
        bindNameEdit();
        bindDomainRequestModal();
        bindObjectiveRequestModal();
        goToStep(state.step, false);
        updateBlueprint();
    }

    function renderDomainGrid() {
        const grid = $('#domainGrid');
        if (!grid) return;
        const domains = getDomains();

        if (!grid.querySelector('[data-domain]')) {
            if (!domains.length) {
                grid.innerHTML = '<p class="studio-exec-hint">Catalogue de domaines indisponible.</p>';
                return;
            }
            const cards = domains.map(d => `
                <button type="button" class="studio-domain-card${state.domain === d.id ? ' selected' : ''}" data-domain="${d.id}">
                    <span class="studio-domain-check"><i class="bi bi-check"></i></span>
                    <div class="studio-domain-icon-wrap" style="background:${d.bg};color:${d.color}">
                        <i class="bi ${d.icon}"></i>
                    </div>
                    <div class="studio-domain-name">${d.name}</div>
                    <div class="studio-domain-desc">${d.desc}</div>
                </button>
            `).join('');
            const requestCard = `
                <button type="button" class="studio-domain-card studio-domain-request" data-action="request-domain">
                    <div class="studio-domain-request-icon"><i class="bi bi-plus-lg"></i></div>
                    <div class="studio-domain-name">Demander un domaine</div>
                    <div class="studio-domain-desc">Proposez un nouveau domaine métier à l'équipe Agentia</div>
                </button>`;
            grid.innerHTML = cards + requestCard;
        }

        $$('.studio-domain-card[data-domain]', grid).forEach(card => {
            card.classList.toggle('selected', card.dataset.domain === state.domain);
        });

        $$('.studio-domain-card[data-domain]', grid).forEach(card => {
            if (card.dataset.bound === '1') return;
            card.dataset.bound = '1';
            card.addEventListener('click', () => {
                state.domain = card.dataset.domain;
                state.customName = '';
                if (state.domain === 'custom') {
                    const label = prompt('Nom du domaine personnalisé :', state.customDomainLabel || '');
                    if (label === null) return;
                    state.customDomainLabel = label.trim();
                } else {
                    state.customDomainLabel = '';
                }
                $$('.studio-domain-card[data-domain]', grid).forEach(c =>
                    c.classList.toggle('selected', c === card));
                renderObjectives();
                updateBlueprint();
            });
        });

        $$('[data-action="request-domain"]', grid).forEach(btn => {
            if (btn.dataset.bound === '1') return;
            btn.dataset.bound = '1';
            btn.addEventListener('click', e => {
                e.stopPropagation();
                openDomainRequestModal();
            });
        });
    }

    function bindDomainRequestModal() {
        $('#btnRequestDomain')?.addEventListener('click', openDomainRequestModal);
        $('#domainModalClose')?.addEventListener('click', closeDomainRequestModal);
        $('#domainModalCancel')?.addEventListener('click', closeDomainRequestModal);
        $('#domainModalBackdrop')?.addEventListener('click', e => {
            if (e.target.id === 'domainModalBackdrop') closeDomainRequestModal();
        });
        $('#domainRequestForm')?.addEventListener('submit', submitDomainRequest);
    }

    function openDomainRequestModal() {
        const backdrop = $('#domainModalBackdrop');
        const formWrap = $('#domainModalFormWrap');
        const successWrap = $('#domainModalSuccess');
        if (!backdrop) return;
        backdrop.classList.add('open');
        formWrap?.classList.remove('d-none');
        successWrap?.classList.add('d-none');
        $('#domainModalError')?.classList.remove('show');
        document.body.style.overflow = 'hidden';
    }

    function closeDomainRequestModal() {
        $('#domainModalBackdrop')?.classList.remove('open');
        document.body.style.overflow = '';
    }

    async function submitDomainRequest(e) {
        e.preventDefault();
        const errEl = $('#domainModalError');
        const name = $('#reqDomainName')?.value?.trim();
        if (!name) {
            if (errEl) { errEl.textContent = 'Indiquez le nom du domaine souhaité.'; errEl.classList.add('show'); }
            return;
        }
        const payload = {
            domainName: name,
            industry: $('#reqIndustry')?.value?.trim() || null,
            useCase: $('#reqUseCase')?.value?.trim() || null,
            description: $('#reqDescription')?.value?.trim() || null
        };
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        const btn = $('#domainModalSubmit');
        if (btn) { btn.disabled = true; btn.textContent = 'Envoi…'; }
        try {
            const res = await fetch('/Agents/RequestDomain', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(payload)
            });
            const data = await res.json().catch(() => ({}));
            if (!res.ok) throw new Error(data.message || 'Erreur lors de l\'envoi.');
            $('#domainModalFormWrap')?.classList.add('d-none');
            const successWrap = $('#domainModalSuccess');
            successWrap?.classList.remove('d-none');
            if ($('#domainSuccessMsg')) $('#domainSuccessMsg').textContent = data.message;
            $('#domainRequestForm')?.reset();
            showToast(data.message || 'Demande envoyée.', true);
        } catch (err) {
            if (errEl) { errEl.textContent = err.message; errEl.classList.add('show'); }
        } finally {
            if (btn) { btn.disabled = false; btn.innerHTML = '<i class="bi bi-send"></i> Envoyer la demande'; }
        }
    }

    function showToast(msg, success) {
        const toast = $('#draftToast');
        if (!toast) return;
        toast.textContent = msg;
        toast.classList.toggle('studio-toast-success', !!success);
        toast.classList.add('show');
        setTimeout(() => {
            toast.classList.remove('show');
            toast.classList.remove('studio-toast-success');
        }, 3500);
    }

    function renderObjectives() {
        const grid = $('#objectiveGrid');
        if (!grid) return;
        const sorted = [...OBJECTIVES].sort((a, b) => {
            const aRec = state.domain && a.domains.includes(state.domain) ? 0 : 1;
            const bRec = state.domain && b.domains.includes(state.domain) ? 0 : 1;
            return aRec - bRec || a.label.localeCompare(b.label, 'fr');
        });
        const cards = sorted.map(o => {
            const checked = state.objectives.includes(o.id);
            const rec = state.domain && o.domains.includes(state.domain);
            return `
                <label class="studio-objective-card${checked ? ' checked' : ''}" data-id="${o.id}">
                    <input type="checkbox" ${checked ? 'checked' : ''}>
                    <span class="studio-objective-icon"><i class="bi ${o.icon}"></i></span>
                    <span class="studio-objective-body">
                        <span class="studio-objective-name">${o.label}</span>
                        ${rec ? '<span class="studio-badge">Recommandé</span>' : ''}
                    </span>
                </label>`;
        }).join('');
        const requestCard = `
            <button type="button" class="studio-objective-card studio-domain-request" data-action="request-objective">
                <span class="studio-objective-icon"><i class="bi bi-plus-lg"></i></span>
                <span class="studio-objective-body">
                    <span class="studio-objective-name">Demander un objectif</span>
                    <span class="studio-domain-desc" style="margin-top:2px;display:block">Proposer un nouvel objectif au catalogue</span>
                </span>
            </button>`;
        grid.innerHTML = cards + requestCard;
        bindCheckGrid(grid, 'objectives', '.studio-objective-card[data-id]');
        $$('[data-action="request-objective"]', grid).forEach(btn => {
            btn.addEventListener('click', e => {
                e.preventDefault();
                openObjectiveRequestModal();
            });
        });
    }

    function bindObjectiveRequestModal() {
        $('#btnRequestObjective')?.addEventListener('click', openObjectiveRequestModal);
        $('#objectiveModalClose')?.addEventListener('click', closeObjectiveRequestModal);
        $('#objectiveModalCancel')?.addEventListener('click', closeObjectiveRequestModal);
        $('#objectiveModalBackdrop')?.addEventListener('click', e => {
            if (e.target.id === 'objectiveModalBackdrop') closeObjectiveRequestModal();
        });
        $('#objectiveRequestForm')?.addEventListener('submit', submitObjectiveRequest);
    }

    function openObjectiveRequestModal() {
        const backdrop = $('#objectiveModalBackdrop');
        if (!backdrop) return;
        backdrop.classList.add('open');
        $('#objectiveModalFormWrap')?.classList.remove('d-none');
        $('#objectiveModalSuccess')?.classList.add('d-none');
        $('#objectiveModalError')?.classList.remove('show');
        const domain = getDomain();
        const domainField = $('#reqObjectiveDomain');
        if (domainField) domainField.value = domain?.name || '';
        document.body.style.overflow = 'hidden';
    }

    function closeObjectiveRequestModal() {
        $('#objectiveModalBackdrop')?.classList.remove('open');
        document.body.style.overflow = '';
    }

    async function submitObjectiveRequest(e) {
        e.preventDefault();
        const errEl = $('#objectiveModalError');
        const name = $('#reqObjectiveName')?.value?.trim();
        if (!name) {
            if (errEl) { errEl.textContent = 'Indiquez le nom de l\'objectif souhaité.'; errEl.classList.add('show'); }
            return;
        }
        const payload = {
            objectiveName: name,
            relatedDomain: $('#reqObjectiveDomain')?.value?.trim() || null,
            useCase: $('#reqObjectiveUseCase')?.value?.trim() || null,
            description: $('#reqObjectiveDescription')?.value?.trim() || null
        };
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        const btn = $('#objectiveModalSubmit');
        if (btn) { btn.disabled = true; btn.textContent = 'Envoi…'; }
        try {
            const res = await fetch('/Agents/RequestObjective', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify(payload)
            });
            const data = await res.json().catch(() => ({}));
            if (!res.ok) throw new Error(data.message || 'Erreur lors de l\'envoi.');
            $('#objectiveModalFormWrap')?.classList.add('d-none');
            $('#objectiveModalSuccess')?.classList.remove('d-none');
            if ($('#objectiveSuccessMsg')) $('#objectiveSuccessMsg').textContent = data.message;
            $('#objectiveRequestForm')?.reset();
            showToast(data.message || 'Demande envoyée.', true);
        } catch (err) {
            if (errEl) { errEl.textContent = err.message; errEl.classList.add('show'); }
        } finally {
            if (btn) { btn.disabled = false; btn.innerHTML = '<i class="bi bi-send"></i> Envoyer la demande'; }
        }
    }

    function renderSources() {
        const grid = $('#sourceGrid');
        if (!grid || !window.STUDIO_SOURCE_CATALOG) return;

        const cats = STUDIO_SOURCE_CATALOG.categories;
        grid.innerHTML = cats.map(cat => {
            const items = SOURCES.filter(s => s.category === cat.id);
            if (!items.length) return '';
            const cards = items.map(s => {
                const checked = state.sources.includes(s.id);
                return `
                    <label class="studio-source-card${checked ? ' checked' : ''}" data-id="${s.id}">
                        <input type="checkbox" ${checked ? 'checked' : ''}>
                        <span class="studio-source-icon"><i class="bi ${s.icon}"></i></span>
                        <span class="studio-source-name">${s.label}</span>
                    </label>`;
            }).join('');
            return `
                <div class="studio-source-category">
                    <h4 class="studio-source-cat-title"><i class="bi ${cat.icon}"></i> ${cat.label}</h4>
                    <div class="studio-source-grid">${cards}</div>
                </div>`;
        }).join('');

        bindSourceSelection(grid);
        renderSourceConfigs();
    }

    function bindSourceSelection(grid) {
        $$('.studio-source-card[data-id]', grid).forEach(item => {
            item.addEventListener('click', e => {
                e.preventDefault();
                const id = item.dataset.id;
                const idx = state.sources.indexOf(id);
                if (idx >= 0) {
                    state.sources.splice(idx, 1);
                    delete state.sourceConfigs[id];
                    item.classList.remove('checked');
                    $('input', item).checked = false;
                } else {
                    state.sources.push(id);
                    if (!state.sourceConfigs[id]) state.sourceConfigs[id] = {};
                    item.classList.add('checked');
                    $('input', item).checked = true;
                }
                renderSourceConfigs();
                updateBlueprint();
            });
        });
    }

    function renderSourceConfigs() {
        const panel = $('#sourceConfigPanel');
        if (!panel) return;

        if (!state.sources.length) {
            panel.innerHTML = `
                <div class="studio-source-config-empty">
                    <i class="bi bi-shield-lock"></i>
                    <p>Sélectionnez une ou plusieurs sources pour configurer les paramètres de connexion.</p>
                </div>`;
            return;
        }

        panel.innerHTML = `
            <div class="studio-source-config-head">
                <h4><i class="bi bi-key"></i> Paramètres de connexion</h4>
                <p>Les identifiants sensibles seront stockés chiffrés dans <strong>Agentia Vault</strong> — jamais en clair dans le blueprint.</p>
            </div>
            ${state.sources.map(id => renderSourceConfigCard(id)).join('')}`;
        bindSourceConfigInputs(panel);
    }

    function renderSourceConfigCard(sourceId) {
        const src = getStudioSource(sourceId);
        if (!src) return '';
        const cfg = state.sourceConfigs[sourceId] || {};
        const fields = (src.fields || []).map(f => {
            const val = cfg[f.key] ?? '';
            if (f.type === 'select') {
                const opts = (f.options || []).map(o =>
                    `<option value="${escapeAttr(o)}"${val === o ? ' selected' : ''}>${o}</option>`
                ).join('');
                return `
                    <div class="studio-config-field">
                        <label>${f.label}${f.secret ? ' <span class="studio-config-secret">🔒 Vault</span>' : ''}</label>
                        <select data-source="${sourceId}" data-key="${f.key}">${opts}</select>
                    </div>`;
            }
            if (f.type === 'textarea') {
                return `
                    <div class="studio-config-field">
                        <label>${f.label}${f.secret ? ' <span class="studio-config-secret">🔒 Vault</span>' : ''}</label>
                        <textarea data-source="${sourceId}" data-key="${f.key}" rows="2"
                            placeholder="${escapeAttr(f.placeholder)}">${escapeHtml(val)}</textarea>
                    </div>`;
            }
            return `
                <div class="studio-config-field">
                    <label>${f.label}${f.secret ? ' <span class="studio-config-secret">🔒 Vault</span>' : ''}</label>
                    <input type="${f.type === 'password' ? 'password' : f.type === 'number' ? 'number' : 'text'}"
                        data-source="${sourceId}" data-key="${f.key}"
                        value="${escapeAttr(val)}"
                        placeholder="${escapeAttr(f.placeholder)}" autocomplete="off" />
                </div>`;
        }).join('');

        const configured = countConfiguredFields(src, cfg);
        const total = (src.fields || []).length;
        return `
            <div class="studio-source-config-card" data-source-card="${sourceId}">
                <div class="studio-source-config-card-head">
                    <span><i class="bi ${src.icon}"></i> ${src.label}</span>
                    <span class="studio-config-progress">${configured}/${total} renseigné(s)</span>
                </div>
                <div class="studio-source-config-fields">${fields}</div>
            </div>`;
    }

    function countConfiguredFields(src, cfg) {
        return (src.fields || []).filter(f => (cfg[f.key] || '').toString().trim()).length;
    }

    function bindSourceConfigInputs(panel) {
        $$('[data-source][data-key]', panel).forEach(el => {
            const evt = el.tagName === 'SELECT' ? 'change' : 'input';
            el.addEventListener(evt, () => {
                const sid = el.dataset.source;
                const key = el.dataset.key;
                if (!state.sourceConfigs[sid]) state.sourceConfigs[sid] = {};
                state.sourceConfigs[sid][key] = el.value;
                const src = getStudioSource(sid);
                const card = panel.querySelector(`[data-source-card="${sid}"] .studio-config-progress`);
                if (card && src) card.textContent = `${countConfiguredFields(src, state.sourceConfigs[sid])}/${src.fields.length} renseigné(s)`;
                updateBlueprint();
            });
        });
    }

    function escapeAttr(s) {
        return String(s ?? '').replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');
    }

    function getSourceLabels() {
        return state.sources.map(id => getStudioSourceLabel(id));
    }

    function buildSourceDetailsForPayload() {
        return state.sources.map(id => {
            const src = getStudioSource(id);
            const cfg = state.sourceConfigs[id] || {};
            const configSummary = {};
            (src?.fields || []).forEach(f => {
                const v = (cfg[f.key] || '').toString().trim();
                if (!v) return;
                configSummary[f.key] = f.secret ? '[VAULT]' : v;
            });
            return { id, label: src?.label || id, config: configSummary };
        });
    }

    function renderActions() {
        const grid = $('#actionGrid');
        if (!grid || !window.STUDIO_ACTION_CATALOG) return;

        const cats = STUDIO_ACTION_CATALOG.categories;
        grid.innerHTML = cats.map(cat => {
            const items = ACTIONS.filter(a => a.category === cat.id);
            if (!items.length) return '';
            const cards = items.map(a => {
                const checked = state.actions.includes(a.id);
                return `
                    <label class="studio-source-card studio-action-card${checked ? ' checked' : ''}" data-id="${a.id}">
                        <input type="checkbox" ${checked ? 'checked' : ''}>
                        <span class="studio-source-icon"><i class="bi ${a.icon}"></i></span>
                        <span class="studio-source-name">${a.label}</span>
                    </label>`;
            }).join('');
            return `
                <div class="studio-source-category">
                    <h4 class="studio-source-cat-title"><i class="bi ${cat.icon}"></i> ${cat.label}</h4>
                    <div class="studio-source-grid">${cards}</div>
                </div>`;
        }).join('');

        bindActionSelection(grid);
        renderActionConfigs();
    }

    function bindActionSelection(grid) {
        $$('.studio-action-card[data-id]', grid).forEach(item => {
            item.addEventListener('click', e => {
                e.preventDefault();
                const id = item.dataset.id;
                const idx = state.actions.indexOf(id);
                if (idx >= 0) {
                    state.actions.splice(idx, 1);
                    delete state.actionConfigs[id];
                    item.classList.remove('checked');
                    $('input', item).checked = false;
                } else {
                    state.actions.push(id);
                    if (!state.actionConfigs[id]) state.actionConfigs[id] = {};
                    item.classList.add('checked');
                    $('input', item).checked = true;
                }
                renderActionConfigs();
                updateBlueprint();
            });
        });
    }

    function renderActionConfigs() {
        const panel = $('#actionConfigPanel');
        if (!panel) return;

        if (!state.actions.length) {
            panel.innerHTML = `
                <div class="studio-source-config-empty">
                    <i class="bi bi-sliders"></i>
                    <p>Sélectionnez une ou plusieurs actions pour renseigner leurs paramètres (destinataires, templates, règles…).</p>
                </div>`;
            return;
        }

        panel.innerHTML = `
            <div class="studio-source-config-head">
                <h4><i class="bi bi-gear"></i> Paramètres des actions</h4>
                <p>Configurez chaque action sélectionnée. Les secrets seront stockés dans <strong>Agentia Vault</strong>.</p>
            </div>
            ${state.actions.map(id => renderActionConfigCard(id)).join('')}`;
        bindActionConfigInputs(panel);
    }

    function renderActionConfigCard(actionId) {
        const act = getStudioAction(actionId);
        if (!act) return '';
        const cfg = state.actionConfigs[actionId] || {};
        const fields = (act.fields || []).map(f => {
            const val = cfg[f.key] ?? '';
            if (f.type === 'select') {
                const opts = (f.options || []).map(o =>
                    `<option value="${escapeAttr(o)}"${val === o ? ' selected' : ''}>${o}</option>`
                ).join('');
                return `
                    <div class="studio-config-field">
                        <label>${f.label}${f.secret ? ' <span class="studio-config-secret">🔒 Vault</span>' : ''}</label>
                        <select data-action="${actionId}" data-key="${f.key}">${opts}</select>
                    </div>`;
            }
            if (f.type === 'textarea') {
                return `
                    <div class="studio-config-field">
                        <label>${f.label}${f.secret ? ' <span class="studio-config-secret">🔒 Vault</span>' : ''}</label>
                        <textarea data-action="${actionId}" data-key="${f.key}" rows="2"
                            placeholder="${escapeAttr(f.placeholder)}">${escapeHtml(val)}</textarea>
                    </div>`;
            }
            return `
                <div class="studio-config-field">
                    <label>${f.label}${f.secret ? ' <span class="studio-config-secret">🔒 Vault</span>' : ''}</label>
                    <input type="${f.type === 'password' ? 'password' : f.type === 'number' ? 'number' : 'text'}"
                        data-action="${actionId}" data-key="${f.key}"
                        value="${escapeAttr(val)}"
                        placeholder="${escapeAttr(f.placeholder)}" autocomplete="off" />
                </div>`;
        }).join('');

        const configured = countConfiguredFields(act, cfg);
        const total = (act.fields || []).length;
        return `
            <div class="studio-source-config-card" data-action-card="${actionId}">
                <div class="studio-source-config-card-head">
                    <span><i class="bi ${act.icon}"></i> ${act.label}</span>
                    <span class="studio-config-progress">${configured}/${total} renseigné(s)</span>
                </div>
                <div class="studio-source-config-fields">${fields}</div>
            </div>`;
    }

    function bindActionConfigInputs(panel) {
        $$('[data-action][data-key]', panel).forEach(el => {
            const evt = el.tagName === 'SELECT' ? 'change' : 'input';
            el.addEventListener(evt, () => {
                const aid = el.dataset.action;
                const key = el.dataset.key;
                if (!state.actionConfigs[aid]) state.actionConfigs[aid] = {};
                state.actionConfigs[aid][key] = el.value;
                const act = getStudioAction(aid);
                const card = panel.querySelector(`[data-action-card="${aid}"] .studio-config-progress`);
                if (card && act) card.textContent = `${countConfiguredFields(act, state.actionConfigs[aid])}/${act.fields.length} renseigné(s)`;
                updateBlueprint();
            });
        });
    }

    function getActionLabels() {
        return state.actions.map(id => getStudioActionLabel(id));
    }

    function buildActionDetailsForPayload() {
        return state.actions.map(id => {
            const act = getStudioAction(id);
            const cfg = state.actionConfigs[id] || {};
            const configSummary = {};
            (act?.fields || []).forEach(f => {
                const v = (cfg[f.key] || '').toString().trim();
                if (!v) return;
                configSummary[f.key] = f.secret ? '[VAULT]' : v;
            });
            return { id, label: act?.label || id, config: configSummary };
        });
    }

    function ensureExecution() {
        if (!EXEC()) return null;
        if (!state.execution) {
            state.execution = EXEC().migrateExecutionFromLegacy(state, suggestAgentName());
        }
        return state.execution;
    }

    function renderExecution() {
        const exec = ensureExecution();
        if (!exec || !EXEC()) return;
        EXEC().renderExecutionPanel(exec, suggestAgentName(), fullRerender => {
            if (fullRerender) renderExecution();
            else {
                EXEC().renderRuntimePreviewPanel(exec, suggestAgentName());
                updateBlueprint();
            }
        });
        EXEC().renderRuntimePreviewPanel(exec, suggestAgentName());
    }

    function renderTriggers() { renderExecution(); }
    function renderRuntimes() { /* merged into renderExecution */ }

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

    function bindCheckGrid(grid, key, itemSelector) {
        const selector = itemSelector || '.studio-check-item[data-id]';
        $$(selector, grid).forEach(item => {
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
        if (n === 1) renderDomainGrid();
        if (n === 3) renderSourceConfigs();
        if (n === 5) renderExecution();
        updateBlueprint();
    }

    function getDomain() {
        const d = getDomains().find(x => x.id === state.domain);
        if (!d) return null;
        if (state.domain === 'custom' && state.customDomainLabel) {
            return { ...d, name: state.customDomainLabel, namePrefix: state.customDomainLabel.split(' ')[0] };
        }
        return d;
    }

    function getObjectiveLabels() {
        return state.objectives.map(id => OBJECTIVES.find(o => o.id === id)?.label).filter(Boolean);
    }

    function getTriggerLabel() {
        const exec = ensureExecution();
        return exec && EXEC() ? EXEC().getTriggerSummary(exec) : UNDEF;
    }

    function getRuntimeLabel() {
        const exec = ensureExecution();
        return exec && EXEC() ? EXEC().getRuntimeSummary(exec) : 'Windows Service';
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
        if (latestEstimate?.estimatedMonthlyCostUsd != null)
            return latestEstimate.estimatedMonthlyCostUsd;
        const base = 0.5;
        const perSource = state.sources.length * 0.18;
        const perAction = state.actions.length * 0.14;
        const exec = ensureExecution();
        const mult = exec && EXEC() ? EXEC().getCostMultiplier(exec) : 1;
        return (base + perSource + perAction) * mult * (1 + state.autonomy * 0.2);
    }

    function scheduleEstimate() {
        clearTimeout(estimateTimer);
        estimateTimer = setTimeout(fetchEstimate, 350);
    }

    async function fetchEstimate() {
        if (!state.objectives.length && !state.sources.length && !state.actions.length) {
            latestEstimate = null;
            return;
        }
        const exec = ensureExecution();
        const payload = {
            hasDomain: !!state.domain,
            objectiveCount: state.objectives.length,
            sourceCount: state.sources.length,
            actionCount: state.actions.length,
            autonomyLevel: state.autonomy,
            triggerId: exec?.trigger || 'manual',
            triggerFrequency: exec?.triggerConfig?.frequency || null,
            runtimeId: exec?.runtime || 'windows-service',
            heartbeatEnabled: !!exec?.supervision?.heartbeat
        };
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        try {
            const res = await fetch('/Agents/EstimateBlueprint', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(payload)
            });
            if (!res.ok) return;
            latestEstimate = await res.json();
            applyEstimateToPanel();
        } catch (_) { /* fallback to local compute */ }
    }

    function applyEstimateToPanel() {
        if (!latestEstimate) return;
        const compEl = $('#bp-complexity');
        if (compEl) compEl.innerHTML = renderStars(latestEstimate.complexity);
        const costEl = $('#bp-cost');
        if (costEl) {
            costEl.textContent = `$${Number(latestEstimate.estimatedMonthlyCostUsd).toFixed(2)} / mois`;
            if (latestEstimate.costLabel) costEl.title = latestEstimate.costLabel;
        }
    }

    function buildWorkflowText() {
        const parts = [];
        if (state.sources.length) parts.push(`1. Collecter : ${getSourceLabels().join(', ')}`);
        if (state.actions.length) parts.push(`2. Traiter : ${getActionLabels().join(' → ')}`);
        parts.push(`3. Runtime : ${getRuntimeLabel()} — ${getTriggerLabel()}`);
        parts.push(`4. Autonomie : ${AUTONOMY_LEVELS[state.autonomy].label}`);
        return parts.join('\n');
    }

    function buildPayload() {
        const exec = ensureExecution();
        return {
            domain: getDomain()?.name || '',
            domainId: state.domain,
            objectives: getObjectiveLabels(),
            sources: getSourceLabels(),
            sourceIds: [...state.sources],
            sourceDetails: buildSourceDetailsForPayload(),
            actions: getActionLabels(),
            actionIds: [...state.actions],
            actionDetails: buildActionDetailsForPayload(),
            trigger: getTriggerLabel(),
            triggerId: exec?.trigger || 'manual',
            runtime: getRuntimeLabel(),
            runtimeId: exec?.runtime || 'windows-service',
            execution: exec && EXEC() ? EXEC().buildExecutionPayload(exec) : {},
            autonomy: AUTONOMY_LEVELS[state.autonomy].label,
            autonomyLevel: state.autonomy,
            security: [...state.security],
            freeText: state.freeText.trim(),
            agentName: suggestAgentName(),
            complexity: latestEstimate?.complexity ?? computeComplexity(),
            estimatedCost: computeCost().toFixed(2),
            aiModel: latestEstimate?.aiModel ?? 'gpt-4o-mini'
        };
    }

    function compileMessage(p) {
        const lines = [
            'Créer un agent IA via Agent Factory Studio :',
            `- Domaine : ${p.domain}`,
            `- Objectifs : ${p.objectives.join(', ') || 'À définir'}`,
            `- Sources : ${p.sources.join(', ') || 'Aucune'}`,
        ];
        if (p.sourceDetails?.length) {
            p.sourceDetails.forEach(sd => {
                const keys = Object.keys(sd.config || {});
                if (keys.length)
                    lines.push(`  · ${sd.label} : ${keys.map(k => `${k}=${sd.config[k]}`).join(', ')}`);
            });
        }
        lines.push(`- Actions : ${p.actions.join(', ') || 'Aucune'}`);
        if (p.actionDetails?.length) {
            p.actionDetails.forEach(ad => {
                const keys = Object.keys(ad.config || {});
                if (keys.length)
                    lines.push(`  · ${ad.label} : ${keys.map(k => `${k}=${ad.config[k]}`).join(', ')}`);
            });
        }
        lines.push(
            `- Configuration d'exécution : ${p.runtime} — ${p.trigger}`,
        );
        if (p.execution?.resilience) {
            const r = p.execution.resilience;
            lines.push(`  · Résilience : retry=${r.retryOnError ? r.maxAttempts + 'x' : 'non'}, notify=${(r.notify || []).join('/')}`);
        }
        if (p.execution?.logging) {
            lines.push(`  · Logs : ${p.execution.logging.level}, rétention ${p.execution.logging.retentionDays}j`);
        }
        lines.push(
            `- Autonomie : ${p.autonomy}`,
            `- Sécurité : ${p.security.join(', ')}`,
            `- Nom : ${p.agentName}`
        );
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
                const labels = getSourceLabels();
                const configured = state.sources.filter(id => {
                    const src = getStudioSource(id);
                    return src && countConfiguredFields(src, state.sourceConfigs[id] || {}) > 0;
                }).length;
                srcEl.innerHTML = labels.join(', ') +
                    (configured ? ` <span class="studio-badge green">${configured} connectée(s)</span>` : '');
                srcEl.classList.remove('undefined');
            } else {
                srcEl.textContent = UNDEF;
                srcEl.classList.add('undefined');
            }
        }

        const actEl = $('#bp-actions');
        if (actEl) {
            if (state.actions.length) {
                const labels = getActionLabels();
                const configured = state.actions.filter(id => {
                    const act = getStudioAction(id);
                    return act && countConfiguredFields(act, state.actionConfigs[id] || {}) > 0;
                }).length;
                actEl.innerHTML = labels.join(', ') +
                    (configured ? ` <span class="studio-badge green">${configured} configurée(s)</span>` : '');
                actEl.classList.remove('undefined');
            } else {
                actEl.textContent = UNDEF;
                actEl.classList.add('undefined');
            }
        }

        const trigEl = $('#bp-trigger');
        if (trigEl) {
            const exec = ensureExecution();
            const configured = state.step >= 5 && exec;
            trigEl.textContent = configured ? `${getRuntimeLabel()} — ${getTriggerLabel()}` : UNDEF;
            trigEl.classList.toggle('undefined', !configured);
        }

        const autoFill = $('#bp-autonomy-fill');
        const autoLabel = $('#bp-autonomy');
        const level = AUTONOMY_LEVELS[state.autonomy];
        if (autoFill) autoFill.style.width = level.pct + '%';
        if (autoLabel) autoLabel.textContent = level.label;

        const compEl = $('#bp-complexity');
        if (compEl && !latestEstimate) compEl.innerHTML = renderStars(p.complexity);

        const costEl = $('#bp-cost');
        if (costEl && !latestEstimate) {
            costEl.textContent = `$${p.estimatedCost} / mois`;
            costEl.removeAttribute('title');
        }

        scheduleEstimate();

        const rtEl = $('#bp-runtime');
        if (rtEl) rtEl.innerHTML = `<span class="studio-bp-pill blue">${getRuntimeLabel()}</span>`;

        if (EXEC()) {
            const exec = ensureExecution();
            EXEC().renderRuntimePreviewPanel(exec, suggestAgentName());
        }

        const previewWrap = $('#runtimePreviewCard');
        if (previewWrap) previewWrap.classList.toggle('highlight', state.step === 5);

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
            const saved = JSON.parse(raw);
            if (saved.sources?.length && typeof saved.sources[0] === 'string' && window.migrateSourceId) {
                saved.sources = saved.sources.map(migrateSourceId);
            }
            if (!saved.sourceConfigs) saved.sourceConfigs = {};
            if (saved.actions?.length && typeof saved.actions[0] === 'string' && window.migrateActionId) {
                saved.actions = saved.actions.map(migrateActionId);
            }
            if (!saved.actionConfigs) saved.actionConfigs = {};
            if (!saved.execution && EXEC()) {
                saved.execution = EXEC().migrateExecutionFromLegacy(saved, saved.customName || 'Agent');
            }
            delete saved.trigger;
            delete saved.runtime;
            Object.assign(state, saved);
        } catch (_) { /* ignore */ }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
