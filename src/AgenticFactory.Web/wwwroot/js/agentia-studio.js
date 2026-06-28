(function () {
    'use strict';

    const STORAGE_KEY = 'agentia-studio-draft';
    const SCHEMA_VERSION = 2;
    const TOTAL_STEPS = 10;
    const MAX_CHARS = 2000;

    const AGENTIC_LOOP = ['Observe', 'Understand', 'Decide', 'Act', 'Verify', 'Log', 'Wait'];

    const SECURITY = [
        'Read', 'Read/Write', 'Delete', 'API Keys', 'Secrets', 'Vault',
        'Logging', 'Audit', 'Human validation', 'GDPR'
    ];

    const CATALOG_STEPS = {
        sensors: {
            catalog: () => window.STUDIO_SENSOR_CATALOG,
            getItem: id => getStudioSensor(id),
            getLabel: id => getStudioSensorLabel(id),
            gridId: 'sensorGrid',
            panelId: 'sensorConfigPanel',
            modalPrefix: 'sensor',
            dataAttr: 'data-sensor',
            configureAttr: 'data-configure-sensor'
        },
        skills: {
            catalog: () => window.STUDIO_SKILL_CATALOG,
            getItem: id => getStudioSkill(id),
            getLabel: id => getStudioSkillLabel(id),
            gridId: 'skillGrid',
            panelId: 'skillConfigPanel',
            modalPrefix: 'skill',
            dataAttr: 'data-skill',
            configureAttr: 'data-configure-skill',
            optionalConfig: true
        },
        tools: {
            catalog: () => window.STUDIO_TOOL_CATALOG,
            getItem: id => getStudioTool(id),
            getLabel: id => getStudioToolLabel(id),
            gridId: 'toolGrid',
            panelId: 'toolConfigPanel',
            modalPrefix: 'tool',
            dataAttr: 'data-tool',
            configureAttr: 'data-configure-tool'
        },
        actuators: {
            catalog: () => window.STUDIO_ACTUATOR_CATALOG,
            getItem: id => getStudioActuator(id),
            getLabel: id => getStudioActuatorLabel(id),
            gridId: 'actuatorGrid',
            panelId: 'actuatorConfigPanel',
            modalPrefix: 'actuator',
            dataAttr: 'data-actuator',
            configureAttr: 'data-configure-actuator'
        }
    };

    function defaultState() {
        return {
            schemaVersion: SCHEMA_VERSION,
            step: 1,
            mission: '',
            missionContext: '',
            businessDomain: '',
            sensors: [],
            sensorConfigs: {},
            skills: [],
            skillConfigs: {},
            tools: [],
            toolConfigs: {},
            actuators: [],
            actuatorConfigs: {},
            decision: { engine: 'gpt', config: {} },
            memory: { types: [], config: {} },
            execution: null,
            security: [],
            customName: '',
            freeText: ''
        };
    }

    let state = defaultState();
    let latestEstimate = null;
    let estimateTimer = null;
    let activeConfigModal = null;

    const $ = (sel, ctx) => (ctx || document).querySelector(sel);
    const $$ = (sel, ctx) => [...(ctx || document).querySelectorAll(sel)];
    const UNDEF = '— Non défini —';
    const EXEC = () => window.STUDIO_EXECUTION;

    function init() {
        if (!$('.studio-page')) return;
        try { loadDraft(); } catch (e) { console.error('Agentia Studio — loadDraft', e); }
        renderAgenticLoop();
        renderMissionStep();
        Object.keys(CATALOG_STEPS).forEach(k => renderCatalogStep(k));
        renderDecisionStep();
        renderMemoryStep();
        renderExecution();
        renderSecurity();
        bindMissionInputs();
        bindNavigation();
        bindFormSubmit();
        bindNameEdit();
        bindGenericConfigModal();
        goToStep(state.step, false);
        updateBlueprint();
    }

    function renderAgenticLoop() {
        const el = $('#agenticLoop');
        if (!el) return;
        el.innerHTML = AGENTIC_LOOP.map((step, i) =>
            `<span class="studio-loop-step${state.step === i + 1 || (state.step > 1 && i === 0) ? ' active' : ''}">${step}</span>`
        ).join('<span class="studio-loop-arrow">→</span>');
    }

    function getDomains() {
        return window.STUDIO_DOMAINS?.length ? window.STUDIO_DOMAINS : [];
    }

    function renderMissionStep() {
        const tags = $('#domainTags');
        if (tags) {
            const domains = getDomains();
            tags.innerHTML = domains.slice(0, 12).map(d =>
                `<button type="button" class="studio-chip${state.businessDomain === d.id ? ' selected' : ''}" data-domain-tag="${d.id}">${d.name}</button>`
            ).join('') + `<button type="button" class="studio-chip studio-chip-muted" data-domain-tag="">Aucun / Autre</button>`;
            $$('[data-domain-tag]', tags).forEach(btn => {
                btn.addEventListener('click', () => {
                    state.businessDomain = btn.dataset.domainTag;
                    $$('[data-domain-tag]', tags).forEach(b => b.classList.toggle('selected', b === btn));
                    updateBlueprint();
                });
            });
        }
        const mission = $('#missionText');
        if (mission) mission.value = state.mission;
        const ctx = $('#missionContext');
        if (ctx) ctx.value = state.missionContext;
    }

    function bindMissionInputs() {
        const mission = $('#missionText');
        const ctx = $('#missionContext');
        const free = $('#freeText');
        const counter = $('#charCount');
        mission?.addEventListener('input', () => {
            state.mission = mission.value.slice(0, MAX_CHARS);
            updateBlueprint();
        });
        ctx?.addEventListener('input', () => {
            state.missionContext = ctx.value.slice(0, MAX_CHARS);
            updateBlueprint();
        });
        if (free) {
            free.value = state.freeText;
            const sync = () => {
                state.freeText = free.value.slice(0, MAX_CHARS);
                if (counter) counter.textContent = state.freeText.length;
                updateBlueprint();
            };
            free.addEventListener('input', sync);
            sync();
        }
    }

    function renderCatalogStep(key) {
        const cfg = CATALOG_STEPS[key];
        const grid = $(`#${cfg.gridId}`);
        const catalog = cfg.catalog();
        if (!grid || !catalog) return;

        const selected = state[key] || [];
        const cats = catalog.categories;
        const items = catalog.items;

        grid.innerHTML = cats.map(cat => {
            const catItems = items.filter(s => s.category === cat.id);
            if (!catItems.length) return '';
            const cards = catItems.map(s => {
                const checked = selected.includes(s.id);
                return `
                    <label class="studio-source-card${checked ? ' checked' : ''}" data-catalog="${key}" data-id="${s.id}">
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

        bindCatalogSelection(key, grid);
        renderCatalogConfigs(key);
    }

    function bindCatalogSelection(key, grid) {
        const cfg = CATALOG_STEPS[key];
        const configsKey = `${key.slice(0, -1)}Configs`.replace('sensorConfigs', 'sensorConfigs')
            .replace('skillConfigs', 'skillConfigs');
        const configMap = {
            sensors: 'sensorConfigs', skills: 'skillConfigs',
            tools: 'toolConfigs', actuators: 'actuatorConfigs'
        };
        const configs = configMap[key];

        $$(`.studio-source-card[data-catalog="${key}"]`, grid).forEach(item => {
            item.addEventListener('click', e => {
                e.preventDefault();
                const id = item.dataset.id;
                const arr = state[key];
                const idx = arr.indexOf(id);
                if (idx >= 0) {
                    arr.splice(idx, 1);
                    delete state[configs][id];
                    item.classList.remove('checked');
                    $('input', item).checked = false;
                    if (activeConfigModal?.key === key && $('#catalogConfigModalBackdrop')?.classList.contains('open')) {
                        if (arr.length) openCatalogConfigModal(key, arr[0]);
                        else closeCatalogConfigModal();
                    }
                } else {
                    arr.push(id);
                    if (!state[configs][id]) state[configs][id] = {};
                    item.classList.add('checked');
                    $('input', item).checked = true;
                    const itemDef = cfg.getItem(id);
                    if (itemDef?.fields?.length) openCatalogConfigModal(key, id);
                }
                renderCatalogConfigs(key);
                updateBlueprint();
            });
        });
    }

    function renderCatalogConfigs(key) {
        const cfg = CATALOG_STEPS[key];
        const panel = $(`#${cfg.panelId}`);
        if (!panel) return;
        const configMap = { sensors: 'sensorConfigs', skills: 'skillConfigs', tools: 'toolConfigs', actuators: 'actuatorConfigs' };
        const configs = configMap[key];
        const selected = state[key];

        if (!selected.length) {
            panel.innerHTML = `
                <div class="studio-source-config-empty">
                    <i class="bi bi-sliders"></i>
                    <p>Sélectionnez un ou plusieurs éléments pour configurer leurs paramètres.</p>
                </div>`;
            return;
        }

        panel.innerHTML = `
            <div class="studio-source-config-head">
                <h4><i class="bi bi-gear"></i> Paramètres de connexion</h4>
                <p>Configurez chaque sélection via « Configurer ». Les secrets seront stockés dans <strong>Agentia Vault</strong>.</p>
            </div>
            <div class="studio-config-summary-list">
                ${selected.map(id => renderConfigSummaryRow(key, id, configs)).join('')}
            </div>`;

        $$('[data-configure-item]', panel).forEach(btn => {
            btn.addEventListener('click', e => {
                e.preventDefault();
                openCatalogConfigModal(key, btn.dataset.configureItem);
            });
        });
    }

    function renderConfigSummaryRow(key, itemId, configsKey) {
        const cfg = CATALOG_STEPS[key];
        const item = cfg.getItem(itemId);
        if (!item) return '';
        const c = state[configsKey][itemId] || {};
        const configured = countConfiguredFields(item, c);
        const total = (item.fields || []).length;
        const attr = `data-configure-item="${itemId}" data-configure-${key.slice(0, -1)}="${itemId}"`;
        return `
            <div class="studio-config-summary-row">
                <span class="studio-config-summary-name"><i class="bi ${item.icon}"></i> ${item.label}</span>
                <span class="studio-config-progress">${configured}/${total} renseigné(s)</span>
                <button type="button" class="studio-btn studio-btn-outline studio-btn-sm" ${attr}>
                    <i class="bi bi-gear"></i> Configurer
                </button>
            </div>`;
    }

    function bindGenericConfigModal() {
        $('#catalogConfigModalClose')?.addEventListener('click', closeCatalogConfigModal);
        $('#catalogConfigModalSave')?.addEventListener('click', closeCatalogConfigModal);
        $('#catalogConfigModalBackdrop')?.addEventListener('click', e => {
            if (e.target.id === 'catalogConfigModalBackdrop') closeCatalogConfigModal();
        });
        $('#catalogConfigModalSelector')?.addEventListener('change', e => {
            if (activeConfigModal) populateCatalogConfigModal(activeConfigModal.key, e.target.value);
        });
    }

    function openCatalogConfigModal(key, itemId) {
        const arr = state[key];
        if (!itemId || !arr.includes(itemId)) return;
        activeConfigModal = { key, itemId };
        $('#catalogConfigModalBackdrop')?.classList.add('open');
        document.body.style.overflow = 'hidden';
        populateCatalogConfigModal(key, itemId);
    }

    function closeCatalogConfigModal() {
        $('#catalogConfigModalBackdrop')?.classList.remove('open');
        document.body.style.overflow = '';
        if (activeConfigModal) renderCatalogConfigs(activeConfigModal.key);
        activeConfigModal = null;
        updateBlueprint();
    }

    function populateCatalogConfigModal(key, itemId) {
        const cfg = CATALOG_STEPS[key];
        const item = cfg.getItem(itemId);
        if (!item) return;
        const configMap = { sensors: 'sensorConfigs', skills: 'skillConfigs', tools: 'toolConfigs', actuators: 'actuatorConfigs' };
        const configs = configMap[key];
        const dataAttr = cfg.dataAttr.replace('data-', '');

        $('#catalogConfigModalTitle').textContent = item.label;
        $('#catalogConfigModalIcon').innerHTML = `<i class="bi ${item.icon}"></i>`;

        const selector = $('#catalogConfigModalSelector');
        const arr = state[key];
        if (selector) {
            if (arr.length > 1) {
                selector.classList.remove('d-none');
                selector.innerHTML = arr.map(id => {
                    const s = cfg.getItem(id);
                    return `<option value="${id}"${id === itemId ? ' selected' : ''}>${s?.label || id}</option>`;
                }).join('');
            } else {
                selector.classList.add('d-none');
            }
        }

        const c = state[configs][itemId] || {};
        const body = $('#catalogConfigModalBody');
        if (body) {
            body.innerHTML = `<div class="studio-source-config-fields">${renderConfigFields(item.fields, c, cfg.dataAttr, itemId)}</div>`;
            bindConfigInputs(body, configs, cfg.dataAttr, itemId, key);
        }
        updateCatalogConfigProgress(key, itemId);
    }

    function bindConfigInputs(container, configsKey, dataAttr, itemId, catalogKey) {
        const attrName = dataAttr.replace('data-', '');
        $$(`[${dataAttr}][data-key]`, container).forEach(el => {
            const evt = el.tagName === 'SELECT' ? 'change' : 'input';
            el.addEventListener(evt, () => {
                const id = el.getAttribute(dataAttr) || itemId;
                const k = el.dataset.key;
                if (!state[configsKey][id]) state[configsKey][id] = {};
                state[configsKey][id][k] = el.value;
                updateCatalogConfigProgress(catalogKey, id);
                updateBlueprint();
            });
        });
    }

    function updateCatalogConfigProgress(key, itemId) {
        const cfg = CATALOG_STEPS[key];
        const item = cfg.getItem(itemId);
        if (!item) return;
        const configMap = { sensors: 'sensorConfigs', skills: 'skillConfigs', tools: 'toolConfigs', actuators: 'actuatorConfigs' };
        const c = state[configMap[key]][itemId] || {};
        const configured = countConfiguredFields(item, c);
        const total = (item.fields || []).length;
        const text = `${configured}/${total} renseigné(s)`;
        const modalProgress = $('#catalogConfigModalProgress');
        if (modalProgress) modalProgress.textContent = text;
    }

    function renderConfigFields(fields, cfg, idAttr, idValue) {
        return (fields || []).map(f => {
            const val = cfg[f.key] ?? '';
            if (f.type === 'select') {
                const opts = (f.options || []).map(o =>
                    `<option value="${escapeAttr(o)}"${val === o ? ' selected' : ''}>${o}</option>`
                ).join('');
                return `<div class="studio-config-field"><label>${f.label}${f.secret ? ' <span class="studio-config-secret">🔒 Vault</span>' : ''}</label>
                    <select ${idAttr}="${idValue}" data-key="${f.key}">${opts}</select></div>`;
            }
            if (f.type === 'textarea') {
                return `<div class="studio-config-field"><label>${f.label}${f.secret ? ' <span class="studio-config-secret">🔒 Vault</span>' : ''}</label>
                    <textarea ${idAttr}="${idValue}" data-key="${f.key}" rows="2" placeholder="${escapeAttr(f.placeholder)}">${escapeHtml(val)}</textarea></div>`;
            }
            return `<div class="studio-config-field"><label>${f.label}${f.secret ? ' <span class="studio-config-secret">🔒 Vault</span>' : ''}</label>
                <input type="${f.type === 'password' ? 'password' : f.type === 'number' ? 'number' : 'text'}"
                    ${idAttr}="${idValue}" data-key="${f.key}" value="${escapeAttr(val)}"
                    placeholder="${escapeAttr(f.placeholder)}" autocomplete="off" /></div>`;
        }).join('');
    }

    function countConfiguredFields(item, cfg) {
        return (item.fields || []).filter(f => (cfg[f.key] || '').toString().trim()).length;
    }

    function renderDecisionStep() {
        const grid = $('#decisionGrid');
        if (!grid || !window.STUDIO_DECISION_CATALOG) return;
        const engines = STUDIO_DECISION_CATALOG.engines;
        grid.innerHTML = engines.map(e => `
            <label class="studio-exec-trigger-card studio-decision-card${state.decision.engine === e.id ? ' selected' : ''}" data-decision="${e.id}">
                <input type="radio" name="decisionEngine"${state.decision.engine === e.id ? ' checked' : ''}>
                <span class="studio-exec-trigger-icon"><i class="bi ${e.icon}"></i></span>
                <span class="studio-exec-trigger-label">${e.label}</span>
                <span class="studio-exec-trigger-desc">${e.desc}</span>
            </label>`).join('');

        $$('[data-decision]', grid).forEach(card => {
            card.addEventListener('click', e => {
                e.preventDefault();
                state.decision.engine = card.dataset.decision;
                if (!state.decision.config) state.decision.config = {};
                $$('[data-decision]', grid).forEach(c => c.classList.toggle('selected', c === card));
                renderDecisionConfig();
                updateBlueprint();
            });
        });
        renderDecisionConfig();
    }

    function renderDecisionConfig() {
        const panel = $('#decisionConfigPanel');
        if (!panel) return;
        const fields = getStudioDecisionFields(state.decision.engine);
        if (!fields.length) {
            panel.innerHTML = '';
            return;
        }
        const cfg = state.decision.config || {};
        panel.innerHTML = `
            <div class="studio-exec-config-box">
                <div class="studio-source-config-fields">${renderConfigFields(fields, cfg, 'data-decision-cfg', state.decision.engine)}</div>
            </div>`;
        $$('[data-decision-cfg][data-key]', panel).forEach(el => {
            el.addEventListener('input', () => {
                if (!state.decision.config) state.decision.config = {};
                state.decision.config[el.dataset.key] = el.value;
                updateBlueprint();
            });
            el.addEventListener('change', () => {
                if (!state.decision.config) state.decision.config = {};
                state.decision.config[el.dataset.key] = el.value;
                updateBlueprint();
            });
        });
    }

    function renderMemoryStep() {
        const grid = $('#memoryGrid');
        if (!grid || !window.STUDIO_MEMORY_CATALOG) return;
        grid.innerHTML = STUDIO_MEMORY_CATALOG.types.map(t => {
            const checked = state.memory.types.includes(t.id);
            return `
                <label class="studio-objective-card${checked ? ' checked' : ''}" data-memory="${t.id}">
                    <input type="checkbox" ${checked ? 'checked' : ''}>
                    <span class="studio-objective-icon"><i class="bi ${t.icon}"></i></span>
                    <span class="studio-objective-body">
                        <span class="studio-objective-name">${t.label}</span>
                        <span class="studio-domain-desc">${t.desc}</span>
                    </span>
                </label>`;
        }).join('');

        $$('[data-memory]', grid).forEach(item => {
            item.addEventListener('click', e => {
                e.preventDefault();
                const id = item.dataset.memory;
                const idx = state.memory.types.indexOf(id);
                if (idx >= 0) {
                    state.memory.types.splice(idx, 1);
                    item.classList.remove('checked');
                    $('input', item).checked = false;
                } else {
                    state.memory.types.push(id);
                    item.classList.add('checked');
                    $('input', item).checked = true;
                }
                renderMemoryConfig();
                updateBlueprint();
            });
        });
        renderMemoryConfig();
    }

    function renderMemoryConfig() {
        const panel = $('#memoryConfigPanel');
        if (!panel || !state.memory.types.length) {
            if (panel) panel.innerHTML = '';
            return;
        }
        panel.innerHTML = state.memory.types.map(typeId => {
            const t = getStudioMemoryType(typeId);
            const fields = getStudioMemoryFields(typeId);
            const cfg = state.memory.config[typeId] || {};
            if (!fields.length) return '';
            return `
                <div class="studio-source-config-card">
                    <div class="studio-source-config-card-head"><i class="bi ${t?.icon || 'bi-database'}"></i> ${t?.label || typeId}</div>
                    <div class="studio-source-config-fields">${renderConfigFields(fields, cfg, 'data-memory-cfg', typeId)}</div>
                </div>`;
        }).join('');

        $$('[data-memory-cfg][data-key]', panel).forEach(el => {
            const evt = el.tagName === 'SELECT' ? 'change' : 'input';
            el.addEventListener(evt, () => {
                const typeId = el.getAttribute('data-memory-cfg');
                if (!state.memory.config[typeId]) state.memory.config[typeId] = {};
                state.memory.config[typeId][el.dataset.key] = el.value;
                updateBlueprint();
            });
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
        $$('.studio-check-item[data-id]', grid).forEach(item => {
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

    function bindNavigation() {
        $('#btnPrev')?.addEventListener('click', () => goToStep(state.step - 1));
        $('#btnNext')?.addEventListener('click', () => {
            if (validateStep(state.step)) goToStep(state.step + 1);
        });
        $('#btnDraft')?.addEventListener('click', saveDraft);
    }

    function bindFormSubmit() {
        const form = document.getElementById('studioForm');
        if (!form) return;
        form.addEventListener('submit', e => {
            if (!validateForSubmit()) { e.preventDefault(); return; }
            try {
                populateHiddenFields();
            } catch (err) {
                e.preventDefault();
                console.error('Agentia Studio — submit', err);
                alert('Impossible de préparer le blueprint. Rechargez la page ou effacez le brouillon local.');
            }
        });
    }

    function bindNameEdit() {
        $('#bpEditName')?.addEventListener('click', () => {
            const name = prompt('Nom de l\'agent :', state.customName || suggestAgentName());
            if (name !== null) {
                state.customName = name.trim();
                updateBlueprint();
            }
        });
    }

    function validateStep(step) {
        const msg = $('#validationMsg');
        let ok = true, text = '';
        if (step === 1 && !state.mission.trim() && !state.freeText.trim()) {
            ok = false;
            text = 'Décrivez la mission de votre agent (rôle et objectif).';
        }
        if (msg) {
            msg.textContent = text;
            msg.classList.toggle('show', !ok);
        }
        return ok;
    }

    function validateForSubmit() {
        if (!state.mission.trim() && !state.freeText.trim()) {
            alert('Complétez au minimum la mission ou la description libre.');
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

        $('#btnPrev').style.display = n > 1 ? '' : 'none';
        const onFinal = n === TOTAL_STEPS;
        const next = $('#btnNext');
        const gen = $('#btnGenerate');
        if (next) { next.hidden = onFinal; next.style.display = onFinal ? 'none' : ''; }
        if (gen) { gen.hidden = !onFinal; gen.style.display = onFinal ? '' : 'none'; }

        if (n === TOTAL_STEPS) renderFinalReview();
        if (n === 8) renderExecution();
        renderAgenticLoop();
        updateBlueprint();
    }

    function getBusinessDomainLabel() {
        if (!state.businessDomain) return '';
        const d = getDomains().find(x => x.id === state.businessDomain);
        return d?.name || state.businessDomain;
    }

    function suggestAgentName() {
        if (state.customName) return state.customName;
        const mission = state.mission.trim();
        if (mission) {
            const words = mission.split(/\s+/).slice(0, 3).join(' ');
            return words.length > 40 ? words.slice(0, 40) + ' Agent' : words + ' Agent';
        }
        const domain = getBusinessDomainLabel();
        if (domain) return `${domain.split(' ')[0]} Agent`;
        return 'Runtime Agent';
    }

    function getLabels(key) {
        const cfg = CATALOG_STEPS[key];
        return (state[key] || []).map(id => cfg.getLabel(id));
    }

    function buildCatalogDetails(key) {
        const cfg = CATALOG_STEPS[key];
        const configMap = { sensors: 'sensorConfigs', skills: 'skillConfigs', tools: 'toolConfigs', actuators: 'actuatorConfigs' };
        const configs = configMap[key];
        return (state[key] || []).map(id => {
            const item = cfg.getItem(id);
            const c = state[configs][id] || {};
            const configSummary = {};
            (item?.fields || []).forEach(f => {
                const v = (c[f.key] || '').toString().trim();
                if (v) configSummary[f.key] = f.secret ? '[VAULT]' : v;
            });
            return { id, label: cfg.getLabel(id), config: configSummary };
        });
    }

    function computeComplexity() {
        let score = state.mission.trim() ? 1.5 : 0;
        score += state.sensors.length * 0.25;
        score += state.skills.length * 0.2;
        score += state.tools.length * 0.15;
        score += state.actuators.length * 0.3;
        score += state.memory.types.length * 0.15;
        score += state.decision.engine === 'hybrid' ? 0.5 : 0.2;
        return Math.min(5, Math.max(0, Math.round(score)));
    }

    function computeCost() {
        if (!state.mission.trim() && !state.sensors.length && !state.actuators.length) return 0;
        if (latestEstimate?.estimatedMonthlyCostUsd != null) return latestEstimate.estimatedMonthlyCostUsd;
        const base = 0.5;
        const perSensor = state.sensors.length * 0.18;
        const perSkill = state.skills.length * 0.08;
        const perActuator = state.actuators.length * 0.14;
        const exec = ensureExecution();
        const mult = exec && EXEC() ? EXEC().getCostMultiplier(exec) : 1;
        const decisionMult = { 'business-rules': 0.5, ollama: 0.3, hybrid: 1.4 }[state.decision.engine] || 1;
        return (base + perSensor + perSkill + perActuator) * mult * decisionMult;
    }

    function scheduleEstimate() {
        clearTimeout(estimateTimer);
        estimateTimer = setTimeout(fetchEstimate, 350);
    }

    async function fetchEstimate() {
        if (!state.mission.trim() && !state.sensors.length && !state.actuators.length) {
            latestEstimate = null;
            return;
        }
        const exec = ensureExecution();
        const autonomyMap = { 'business-rules': 0, 'human-validation': 0, workflow: 1, gpt: 2, claude: 2, hybrid: 3 };
        const payload = {
            hasDomain: !!state.mission.trim(),
            objectiveCount: state.skills.length,
            sourceCount: state.sensors.length,
            actionCount: state.actuators.length,
            autonomyLevel: autonomyMap[state.decision.engine] ?? 1,
            triggerId: exec?.trigger || 'manual',
            triggerFrequency: exec?.triggerConfig?.frequency || null,
            runtimeId: exec?.runtime || 'windows-service',
            heartbeatEnabled: !!exec?.supervision?.heartbeat
        };
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        try {
            const res = await fetch('/Agents/EstimateBlueprint', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify(payload)
            });
            if (!res.ok) return;
            latestEstimate = await res.json();
            applyEstimateToPanel();
        } catch (_) { /* fallback */ }
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
        const aiEl = $('#bp-ai-model');
        if (aiEl && latestEstimate.aiModel) aiEl.textContent = latestEstimate.aiModel;
    }

    function estimateCreationTime() {
        const mins = 5 + state.sensors.length * 2 + state.skills.length + state.tools.length * 2
            + state.actuators.length * 2 + (state.decision.engine === 'hybrid' ? 10 : 3);
        if (mins < 60) return `~${mins} min`;
        return `~${Math.round(mins / 60 * 10) / 10} h`;
    }

    function buildPayload() {
        const exec = ensureExecution();
        const decisionLabel = getStudioDecisionLabel(state.decision.engine);
        return {
            schemaVersion: SCHEMA_VERSION,
            mission: state.mission.trim(),
            missionContext: state.missionContext.trim(),
            businessDomain: getBusinessDomainLabel(),
            businessDomainId: state.businessDomain,
            sensors: getLabels('sensors'),
            sensorIds: [...state.sensors],
            sensorDetails: buildCatalogDetails('sensors'),
            skills: getLabels('skills'),
            skillIds: [...state.skills],
            skillDetails: buildCatalogDetails('skills'),
            tools: getLabels('tools'),
            toolIds: [...state.tools],
            toolDetails: buildCatalogDetails('tools'),
            actuators: getLabels('actuators'),
            actuatorIds: [...state.actuators],
            actuatorDetails: buildCatalogDetails('actuators'),
            decision: { engine: state.decision.engine, label: decisionLabel, config: { ...state.decision.config } },
            memory: { types: state.memory.types.map(getStudioMemoryLabel), typeIds: [...state.memory.types], config: { ...state.memory.config } },
            trigger: getTriggerLabel(),
            triggerId: exec?.trigger || 'manual',
            runtime: getRuntimeLabel(),
            runtimeId: exec?.runtime || 'windows-service',
            execution: exec && EXEC() ? EXEC().buildExecutionPayload(exec) : {},
            security: [...state.security],
            freeText: state.freeText.trim(),
            agentName: suggestAgentName(),
            complexity: latestEstimate?.complexity ?? computeComplexity(),
            estimatedCost: computeCost().toFixed(2),
            creationTimeEstimate: estimateCreationTime(),
            aiModel: latestEstimate?.aiModel ?? (state.decision.config?.model || decisionLabel),
            agenticLoop: AGENTIC_LOOP.join(' → ')
        };
    }

    function compileMessage(p) {
        const lines = [
            'Créer un agent IA Runtime Agentic via Agent Factory Studio :',
            `- Mission : ${p.mission || 'À définir'}`,
        ];
        if (p.missionContext) lines.push(`- Contexte métier : ${p.missionContext}`);
        if (p.businessDomain) lines.push(`- Domaine (tag) : ${p.businessDomain}`);
        lines.push(`- Capteurs (Observe) : ${p.sensors.join(', ') || 'Aucun'}`);
        appendDetails(lines, p.sensorDetails);
        lines.push(`- Compétences (Understand) : ${p.skills.join(', ') || 'Aucune'}`);
        appendDetails(lines, p.skillDetails);
        lines.push(`- Outils : ${p.tools.join(', ') || 'Aucun'}`);
        appendDetails(lines, p.toolDetails);
        lines.push(`- Actionneurs (Act) : ${p.actuators.join(', ') || 'Aucun'}`);
        appendDetails(lines, p.actuatorDetails);
        lines.push(`- Moteur de décision : ${p.decision?.label || p.decision?.engine || '—'}`);
        lines.push(`- Mémoire : ${p.memory?.types?.join(', ') || 'Aucune'}`);
        lines.push(`- Exécution : ${p.runtime} — ${p.trigger}`);
        if (p.execution?.resilience) {
            const r = p.execution.resilience;
            lines.push(`  · Résilience : retry=${r.retryOnError ? r.maxAttempts + 'x' : 'non'}`);
        }
        if (p.execution?.logging) {
            lines.push(`  · Logs : ${p.execution.logging.level}, rétention ${p.execution.logging.retentionDays}j`);
        }
        lines.push(`- Sécurité : ${p.security.join(', ') || 'Non définie'}`);
        lines.push(`- Nom : ${p.agentName}`);
        lines.push(`- Boucle agentique : ${p.agenticLoop}`);
        if (p.freeText) lines.push(`Description complémentaire : ${p.freeText}`);
        return lines.join('\n');
    }

    function appendDetails(lines, details) {
        (details || []).forEach(sd => {
            const keys = Object.keys(sd.config || {});
            if (keys.length) lines.push(`  · ${sd.label} : ${keys.map(k => `${k}=${sd.config[k]}`).join(', ')}`);
        });
    }

    function populateHiddenFields() {
        normalizeWizardState();
        const payload = buildPayload();
        $('#hiddenMessage').value = compileMessage(payload);
        $('#hiddenWizardJson').value = JSON.stringify(payload);
    }

    function normalizeWizardState() {
        const d = defaultState();
        if (!Array.isArray(state.sensors)) state.sensors = [];
        if (!Array.isArray(state.skills)) state.skills = [];
        if (!Array.isArray(state.tools)) state.tools = [];
        if (!Array.isArray(state.actuators)) state.actuators = [];
        if (!Array.isArray(state.security)) state.security = [];
        ['sensorConfigs', 'skillConfigs', 'toolConfigs', 'actuatorConfigs'].forEach(k => {
            if (!state[k] || typeof state[k] !== 'object') state[k] = {};
        });
        if (!state.decision) state.decision = d.decision;
        if (!state.memory) state.memory = d.memory;
        if (!state.memory.types) state.memory.types = [];
        if (!state.memory.config) state.memory.config = {};
        if (state.step < 1 || state.step > TOTAL_STEPS) state.step = 1;
    }

    function renderStars(n) {
        return [1, 2, 3, 4, 5].map(i => `<span class="${i <= n ? 'filled' : ''}">★</span>`).join('');
    }

    function setBpText(id, text, undefined) {
        const el = $(`#bp-${id}`);
        if (!el) return;
        el.textContent = text;
        el.classList.toggle('undefined', !!undefined);
    }

    function setBpHtml(id, html, undefined) {
        const el = $(`#bp-${id}`);
        if (!el) return;
        el.innerHTML = html;
        el.classList.toggle('undefined', !!undefined);
    }

    function getTriggerLabel() {
        const exec = ensureExecution();
        return exec && EXEC() ? EXEC().getTriggerSummary(exec) : UNDEF;
    }

    function getRuntimeLabel() {
        const exec = ensureExecution();
        return exec && EXEC() ? EXEC().getRuntimeSummary(exec) : 'Windows Service';
    }

    function updateBlueprint() {
        const p = buildPayload();

        if ($('#bp-name')) $('#bp-name').textContent = suggestAgentName();

        setBpText('mission', p.mission || UNDEF, !p.mission);
        setBpHtml('sensors', p.sensors.length ? p.sensors.join(', ') : UNDEF, !p.sensors.length);
        setBpHtml('skills', p.skills.length ? p.skills.join(', ') : UNDEF, !p.skills.length);
        setBpHtml('tools', p.tools.length ? p.tools.join(', ') : UNDEF, !p.tools.length);
        setBpHtml('actuators', p.actuators.length ? p.actuators.join(', ') : UNDEF, !p.actuators.length);
        setBpHtml('memory', p.memory.types.length ? p.memory.types.join(', ') : UNDEF, !p.memory.types.length);
        setBpText('decision', p.decision.label || UNDEF, !p.decision.engine);

        const trigEl = $('#bp-trigger');
        if (trigEl) {
            const exec = ensureExecution();
            trigEl.textContent = exec ? `${getRuntimeLabel()} — ${getTriggerLabel()}` : UNDEF;
            trigEl.classList.toggle('undefined', !exec);
        }

        const compEl = $('#bp-complexity');
        if (compEl && !latestEstimate) compEl.innerHTML = renderStars(p.complexity);

        const costEl = $('#bp-cost');
        if (costEl && !latestEstimate) {
            costEl.textContent = `$${p.estimatedCost} / mois`;
            costEl.removeAttribute('title');
        }

        const timeEl = $('#bp-creation-time');
        if (timeEl) timeEl.textContent = p.creationTimeEstimate;

        const aiEl = $('#bp-ai-model');
        if (aiEl && !latestEstimate) aiEl.textContent = p.aiModel;

        const loopEl = $('#bp-agentic-loop');
        if (loopEl) loopEl.textContent = p.agenticLoop;

        scheduleEstimate();

        if ($('#bp-runtime')) {
            $('#bp-runtime').innerHTML = `<span class="studio-bp-pill blue">${getRuntimeLabel()}</span>`;
        }

        if (EXEC()) {
            const exec = ensureExecution();
            EXEC().renderRuntimePreviewPanel(exec, suggestAgentName());
        }

        $('#runtimePreviewCard')?.classList.toggle('highlight', state.step === 8);

        const progress = (p.mission ? 1 : 0) + p.sensors.length + p.skills.length + p.actuators.length;
        const empty = $('#bp-empty');
        if (empty) empty.style.display = progress <= 0 ? '' : 'none';
    }

    function renderFinalReview() {
        const p = buildPayload();
        const review = $('#finalReview');
        if (!review) return;
        review.innerHTML = `
            <div class="studio-review-grid">
                <div class="studio-review-row"><span class="studio-review-label">Nom</span>
                    <span class="studio-review-value"><strong>${escapeHtml(p.agentName)}</strong></span></div>
                <div class="studio-review-row"><span class="studio-review-label">Mission</span>
                    <span class="studio-review-value">${escapeHtml(p.mission || '—')}</span></div>
                <div class="studio-review-row"><span class="studio-review-label">Moteur IA</span>
                    <span class="studio-review-value">${escapeHtml(p.decision.label)}</span></div>
                <div class="studio-review-row"><span class="studio-review-label">Runtime</span>
                    <span class="studio-review-value">${escapeHtml(p.runtime)} — ${escapeHtml(p.trigger)}</span></div>
                <div class="studio-review-row"><span class="studio-review-label">Complexité</span>
                    <span class="studio-review-value studio-bp-stars">${renderStars(p.complexity)}</span></div>
                <div class="studio-review-row"><span class="studio-review-label">Coût estimé</span>
                    <span class="studio-review-value">$${p.estimatedCost} / mois</span></div>
                <div class="studio-review-row"><span class="studio-review-label">Temps création</span>
                    <span class="studio-review-value">${p.creationTimeEstimate}</span></div>
                <div class="studio-review-row"><span class="studio-review-label">Sécurité</span>
                    <span class="studio-review-value">${p.security.map(s => `<span class="studio-badge green">${escapeHtml(s)}</span>`).join('') || '—'}</span></div>
            </div>
            <div class="studio-subsection">
                <h4>Boucle agentique</h4>
                <div class="studio-workflow-preview">${p.agenticLoop}</div>
            </div>
            <div class="studio-subsection">
                <h4>Capacités</h4>
                <div class="studio-workflow-preview">
                    Observe: ${p.sensors.join(', ') || '—'}<br>
                    Understand: ${p.skills.join(', ') || '—'}<br>
                    Tools: ${p.tools.join(', ') || '—'}<br>
                    Act: ${p.actuators.join(', ') || '—'}<br>
                    Memory: ${p.memory.types.join(', ') || '—'}
                </div>
            </div>
            ${p.freeText ? `<div class="studio-subsection"><h4>Notes</h4><p>${escapeHtml(p.freeText)}</p></div>` : ''}`;
    }

    function escapeAttr(s) {
        return String(s ?? '').replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');
    }

    function escapeHtml(s) {
        const d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    function saveDraft() {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
            showToast('Brouillon enregistré localement');
        } catch (_) { /* ignore */ }
    }

    function showToast(msg) {
        const toast = $('#draftToast');
        if (!toast) return;
        toast.textContent = msg;
        toast.classList.add('show');
        setTimeout(() => toast.classList.remove('show'), 2500);
    }

    function migrateLegacyDraft(saved) {
        if (saved.schemaVersion >= SCHEMA_VERSION) return saved;
        const migrated = { ...defaultState(), ...saved, schemaVersion: SCHEMA_VERSION };

        if (saved.domain && !saved.businessDomain) migrated.businessDomain = saved.domain;
        if (saved.customDomainLabel) migrated.missionContext = saved.customDomainLabel;
        if (saved.objectives?.length && !saved.mission) {
            migrated.mission = Array.isArray(saved.objectives)
                ? saved.objectives.join(', ')
                : String(saved.objectives);
        }
        if (saved.freeText && !saved.mission) migrated.mission = saved.freeText;

        if (saved.sources?.length && !saved.sensors?.length) {
            migrated.sensors = saved.sources.map(id => migrateSensorId ? migrateSensorId(id) : id);
            migrated.sensorConfigs = saved.sourceConfigs || {};
        }
        if (saved.actions?.length && !saved.actuators?.length) {
            migrated.actuators = saved.actions.map(id => migrateActuatorId ? migrateActuatorId(id) : id);
            migrated.actuatorConfigs = saved.actionConfigs || {};
        }
        if (saved.autonomy != null && saved.decision?.engine == null) {
            const map = ['business-rules', 'workflow', 'gpt', 'hybrid'];
            migrated.decision = { engine: map[saved.autonomy] || 'gpt', config: {} };
        }
        if (saved.security?.length) {
            migrated.security = saved.security.map(s => {
                if (s === 'Lecture seule') return 'Read';
                if (s === 'Lecture / écriture') return 'Read/Write';
                if (s === 'Validation humaine obligatoire') return 'Human validation';
                if (s === 'Journalisation complète') return 'Logging';
                if (s === 'Secrets chiffrés') return 'Secrets';
                return s;
            });
        }
        delete migrated.domain;
        delete migrated.objectives;
        delete migrated.sources;
        delete migrated.actions;
        delete migrated.sourceConfigs;
        delete migrated.actionConfigs;
        delete migrated.autonomy;
        delete migrated.trigger;
        delete migrated.runtime;
        return migrated;
    }

    function loadDraft() {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            if (!raw) return;
            const saved = migrateLegacyDraft(JSON.parse(raw));
            if (!saved.execution && EXEC()) {
                saved.execution = EXEC().migrateExecutionFromLegacy(saved, saved.customName || 'Agent');
            }
            Object.assign(state, saved);
            normalizeWizardState();
        } catch (_) { /* ignore */ }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
