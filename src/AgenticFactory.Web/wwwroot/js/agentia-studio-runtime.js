/* Agent Factory Studio — configuration d'exécution (Execution Runtime) */
(function (global) {
    'use strict';

    const TRIGGERS = [
        { id: 'manual', label: 'Déclenchement manuel', icon: 'bi-hand-index', desc: 'Exécution à la demande depuis l\'interface' },
        { id: 'scheduled', label: 'Planifié (Cron)', icon: 'bi-clock-history', desc: 'Fréquence fixe ou expression Cron personnalisée' },
        { id: 'email-in', label: 'Email entrant', icon: 'bi-envelope-at', desc: 'À réception d\'un message dans une boîte mail' },
        { id: 'file-created', label: 'Fichier créé', icon: 'bi-file-earmark-plus', desc: 'Surveillance d\'un dossier ou partage' },
        { id: 'webhook', label: 'Webhook', icon: 'bi-link-45deg', desc: 'Appel HTTP entrant vers l\'agent runtime' },
        { id: 'api', label: 'API', icon: 'bi-braces', desc: 'Endpoint REST exposé pour lancer l\'agent' }
    ];

    const FREQUENCY_PRESETS = [
        { id: '5min', label: '5 minutes', cron: '*/5 * * * *' },
        { id: '10min', label: '10 minutes', cron: '*/10 * * * *' },
        { id: '15min', label: '15 minutes', cron: '*/15 * * * *' },
        { id: '30min', label: '30 minutes', cron: '*/30 * * * *' },
        { id: 'hourly', label: '1 heure', cron: '0 * * * *' },
        { id: 'daily', label: 'Tous les jours (8h)', cron: '0 8 * * *' },
        { id: 'custom', label: 'Personnalisé', cron: '' }
    ];

    const TIMEZONES = [
        'UTC', 'Europe/Paris', 'Europe/London', 'Canada/Eastern', 'Canada/Central',
        'America/New_York', 'America/Los_Angeles', 'Asia/Tokyo', 'Asia/Singapore'
    ];

    const RUNTIMES = [
        { id: 'windows-service', label: 'Windows Service', icon: 'bi-windows', desc: 'Service Windows natif — idéal on-premise' },
        { id: 'docker', label: 'Docker', icon: 'bi-box', desc: 'Conteneur Docker isolé' },
        { id: 'kubernetes', label: 'Kubernetes', icon: 'bi-diagram-3', desc: 'Orchestration K8s / AKS / EKS' },
        { id: 'linux-service', label: 'Linux Service', icon: 'bi-terminal', desc: 'systemd / service Linux' },
        { id: 'azure', label: 'Azure Functions', icon: 'bi-cloud', desc: 'Serverless Azure' },
        { id: 'aws', label: 'AWS Lambda', icon: 'bi-cloud-arrow-up', desc: 'Serverless AWS' },
        { id: 'ovh', label: 'OVH Cloud', icon: 'bi-cloud-haze2', desc: 'Instances / containers OVH — hébergement EU' }
    ];

    const LOG_LEVELS = ['Debug', 'Info', 'Warning', 'Erreur', 'Critique'];
    const LOG_EXPORTS = ['Grafana', 'Elastic', 'Seq', 'Fichier', 'Base SQL'];
    const NOTIFY_CHANNELS = ['Email', 'Teams', 'Slack', 'Créer Incident'];
    const MONITORING_TOOLS = ['Grafana', 'Prometheus', 'OpenTelemetry'];
    const ALERT_TYPES = ['CPU', 'Mémoire', 'Erreur', 'Temps d\'exécution', 'Coût IA'];
    const PRIORITIES = ['Basse', 'Normale', 'Haute'];
    const MAILBOX_TYPES = ['Outlook', 'Exchange', 'Gmail', 'IMAP', 'Autre'];

    function defaultTriggerConfig(triggerId, agentSlug) {
        const slug = agentSlug || 'agent';
        const map = {
            manual: {},
            scheduled: { frequency: '5min', cronExpression: '*/5 * * * *', timezone: 'Europe/Paris' },
            'email-in': {
                mailbox: 'Outlook', address: '', folder: 'Inbox', subfolder: '',
                subjectContains: '', sender: '', attachmentsOnly: true,
                moveAfter: true, moveTo: 'Traités'
            },
            'file-created': { watchPath: 'C:\\Agentia\\Inbox', pattern: '*.*', recursive: true },
            webhook: {
                url: `https://runtime.gisebs.com/webhooks/${slug}`,
                authType: 'API Key', secret: '', method: 'POST',
                formats: ['JSON']
            },
            api: {
                endpoint: '/api/agent/run', method: 'POST',
                authType: 'Bearer Token', quotaPerHour: 1000
            }
        };
        return { ...(map[triggerId] || {}) };
    }

    function defaultRuntimeConfig(runtimeId, agentName) {
        const name = (agentName || 'Agent').replace(/\s+/g, '');
        const map = {
            'windows-service': {
                serviceName: `${name}Runtime`, mode: 'Automatique', windowsUser: 'LocalService',
                autoStart: true, autoRestart: true, maxInstances: 2, timeoutSec: 300,
                maxMemoryGb: 2, maxCpuPercent: 40, priority: 'Normale', deployMode: 'Production'
            },
            docker: {
                image: `agentia/${name.toLowerCase()}:latest`, containerName: `${name.toLowerCase()}-rt`,
                restartPolicy: 'unless-stopped', cpuLimit: '0.4', memoryLimit: '2g', network: 'agentia-net'
            },
            kubernetes: {
                namespace: 'agentia', deployment: `${name.toLowerCase()}-deploy`, replicas: 2,
                cpuRequest: '200m', memoryRequest: '512Mi', cpuLimit: '500m', memoryLimit: '2Gi'
            },
            'linux-service': {
                unitName: `${name.toLowerCase()}.service`, linuxUser: 'agentia',
                autoStart: true, autoRestart: true, timeoutSec: 300
            },
            azure: {
                functionApp: `${name.toLowerCase()}-func`, plan: 'Consumption', region: 'West Europe',
                timeoutSec: 300, maxInstances: 10
            },
            aws: {
                functionName: `${name}-lambda`, memoryMb: 2048, timeoutSec: 300, region: 'eu-west-1'
            },
            ovh: {
                projectId: 'ovh-project-id', region: 'GRA', instanceType: 'b2-7',
                containerName: `${name.toLowerCase()}-ovh`, replicas: 1,
                timeoutSec: 300, maxMemoryGb: 2, maxCpuPercent: 50
            }
        };
        return { ...(map[runtimeId] || {}) };
    }

    function createDefaultExecution(agentName) {
        const slug = (agentName || 'agent').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'agent';
        return {
            mode: 'simple',
            trigger: 'scheduled',
            triggerConfig: defaultTriggerConfig('scheduled', slug),
            runtime: 'windows-service',
            runtimeConfig: defaultRuntimeConfig('windows-service', agentName || 'Agent'),
            resilience: {
                retryOnError: true, maxAttempts: 5, waitSeconds: 30,
                notify: ['Email'], stopOnFail: false
            },
            logging: {
                level: 'Info', retentionDays: 30, exports: ['Fichier']
            },
            supervision: {
                healthIntervalSec: 60, heartbeat: true,
                monitoring: ['Grafana', 'Prometheus'], alerts: ['CPU', 'Mémoire', 'Erreur']
            },
            advanced: {
                cronExpression: '', threads: 4, parallelism: 2, cacheEnabled: false,
                cacheTtlMin: 15, envVars: '', secretsRef: '', cpuAffinity: '',
                aiQuotaPerHour: 500, errorHandling: 'retry-then-notify'
            }
        };
    }

    const LEGACY_TRIGGER_MAP = {
        manual: 'manual',
        '5min': 'scheduled', '10min': 'scheduled', '15min': 'scheduled',
        '30min': 'scheduled', hourly: 'scheduled', daily: 'scheduled',
        'email-in': 'email-in', 'file-created': 'file-created',
        webhook: 'webhook', api: 'api'
    };

    const LEGACY_FREQ_MAP = {
        '5min': '5min', hourly: 'hourly', daily: 'daily'
    };

    function migrateExecutionFromLegacy(state, agentName) {
        if (state.execution?.trigger) return state.execution;
        const exec = createDefaultExecution(agentName);
        if (state.trigger) {
            const newTrigger = LEGACY_TRIGGER_MAP[state.trigger] || 'manual';
            exec.trigger = newTrigger;
            exec.triggerConfig = defaultTriggerConfig(newTrigger, agentName);
            const freq = LEGACY_FREQ_MAP[state.trigger];
            if (freq) {
                exec.triggerConfig.frequency = freq;
                const preset = FREQUENCY_PRESETS.find(p => p.id === freq);
                if (preset) exec.triggerConfig.cronExpression = preset.cron;
            }
        }
        if (state.runtime) {
            exec.runtime = state.runtime;
            exec.runtimeConfig = defaultRuntimeConfig(state.runtime, agentName);
        }
        return exec;
    }

    function getFrequencyLabel(freqId) {
        return FREQUENCY_PRESETS.find(f => f.id === freqId)?.label || freqId;
    }

    function getTriggerSummary(exec) {
        if (!exec) return '—';
        const t = TRIGGERS.find(x => x.id === exec.trigger);
        if (!t) return exec.trigger;
        if (exec.trigger === 'manual') return t.label;
        if (exec.trigger === 'scheduled') {
            const cfg = exec.triggerConfig || {};
            if (cfg.frequency === 'custom') return `Cron : ${cfg.cronExpression || '—'}`;
            return `Toutes les ${getFrequencyLabel(cfg.frequency || '5min')}`;
        }
        if (exec.trigger === 'email-in') {
            const c = exec.triggerConfig || {};
            return `${t.label} — ${c.mailbox || 'Outlook'}${c.address ? ` (${c.address})` : ''}`;
        }
        if (exec.trigger === 'file-created') {
            return `${t.label} — ${exec.triggerConfig?.watchPath || '—'}`;
        }
        if (exec.trigger === 'webhook') return `${t.label} — ${exec.triggerConfig?.method || 'POST'}`;
        if (exec.trigger === 'api') return `${t.label} — ${exec.triggerConfig?.endpoint || '/api/agent/run'}`;
        return t.label;
    }

    function getRuntimeSummary(exec) {
        if (!exec) return 'Windows Service';
        return RUNTIMES.find(r => r.id === exec.runtime)?.label || exec.runtime;
    }

    function getCostMultiplier(exec) {
        if (!exec) return 1;
        const m = { manual: 1, scheduled: 1.5, 'email-in': 1.4, 'file-created': 1.3, webhook: 1.2, api: 1.25 };
        let mult = m[exec.trigger] || 1;
        if (exec.trigger === 'scheduled') {
            const f = exec.triggerConfig?.frequency;
            if (f === '5min') mult = 2.2;
            else if (f === '10min' || f === '15min') mult = 1.8;
            else if (f === 'hourly') mult = 1.5;
            else if (f === 'daily') mult = 1.1;
        }
        return mult;
    }

    function buildExecutionPayload(exec) {
        if (!exec) return {};
        const tc = { ...exec.triggerConfig };
        if (tc.secret) tc.secret = '[VAULT]';
        const summary = {
            mode: exec.mode,
            trigger: exec.trigger,
            triggerLabel: getTriggerSummary(exec),
            triggerConfig: tc,
            runtime: exec.runtime,
            runtimeLabel: getRuntimeSummary(exec),
            runtimeConfig: { ...exec.runtimeConfig },
            resilience: { ...exec.resilience },
            logging: { ...exec.logging },
            supervision: { ...exec.supervision }
        };
        if (exec.mode === 'advanced') {
            summary.advanced = { ...exec.advanced };
            if (summary.advanced.secretsRef) summary.advanced.secretsRef = '[VAULT]';
        }
        return summary;
    }

    function getRuntimePreview(exec, agentName) {
        const rt = exec?.runtimeConfig || {};
        const trig = getTriggerSummary(exec);
        return {
            name: agentName || 'Agent',
            type: getRuntimeSummary(exec),
            trigger: trig,
            maxTime: `${rt.timeoutSec || 300} sec`,
            memory: rt.maxMemoryGb ? `${rt.maxMemoryGb} Go` : (rt.memoryLimit || rt.memoryMb ? `${rt.memoryLimit || rt.memoryMb + ' Mo'}` : '2 Go'),
            cpu: rt.maxCpuPercent ? `${rt.maxCpuPercent} %` : (rt.cpuLimit ? rt.cpuLimit : '40 %'),
            mode: rt.deployMode || 'Production',
            health: exec?.supervision?.heartbeat ? 'Heartbeat activé' : 'Non configuré',
            healthBadge: exec?.supervision?.heartbeat ? 'config' : 'none',
            monitoring: (exec?.supervision?.monitoring?.length || 0) > 0 ? 'Activé' : 'Désactivé',
            logs: exec?.logging?.level ? 'Activés' : '—',
            restart: rt.autoRestart !== false ? 'Automatique' : 'Manuel'
        };
    }

    function esc(s) {
        return String(s ?? '').replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');
    }

    function escHtml(s) {
        const d = document.createElement('div');
        d.textContent = s ?? '';
        return d.innerHTML;
    }

    function fieldInput(label, path, value, type, placeholder, extra) {
        const t = type || 'text';
        const ph = placeholder ? ` placeholder="${esc(placeholder)}"` : '';
        const secret = extra?.secret ? ' <span class="studio-config-secret">🔒 Vault</span>' : '';
        if (t === 'select') {
            const opts = (extra.options || []).map(o =>
                `<option value="${esc(o)}"${value === o ? ' selected' : ''}>${escHtml(o)}</option>`
            ).join('');
            return `<div class="studio-config-field"><label>${label}${secret}</label>
                <select data-exec-path="${path}">${opts}</select></div>`;
        }
        if (t === 'textarea') {
            return `<div class="studio-config-field"><label>${label}${secret}</label>
                <textarea data-exec-path="${path}" rows="2"${ph}>${escHtml(value)}</textarea></div>`;
        }
        if (t === 'checkbox') {
            const checked = value ? ' checked' : '';
            return `<label class="studio-exec-check-inline">
                <input type="checkbox" data-exec-path="${path}" data-exec-bool="1"${checked}> ${label}</label>`;
        }
        return `<div class="studio-config-field"><label>${label}${secret}</label>
            <input type="${t === 'password' ? 'password' : t === 'number' ? 'number' : 'text'}"
                data-exec-path="${path}" value="${esc(value)}"${ph} autocomplete="off" /></div>`;
    }

    function checkGrid(label, arrayPath, items, selected) {
        const sel = selected || [];
        const chips = items.map(item => {
            const on = sel.includes(item);
            return `<label class="studio-check-item studio-exec-check${on ? ' checked' : ''}" data-exec-array="${arrayPath}" data-exec-value="${esc(item)}">
                <input type="checkbox"${on ? ' checked' : ''}> ${escHtml(item)}</label>`;
        }).join('');
        return `<div class="studio-config-field studio-config-field-full">
            <label>${label}</label><div class="studio-check-grid cols-3">${chips}</div></div>`;
    }

    function freqRadios(cfg) {
        const freq = cfg.frequency || '5min';
        return `<div class="studio-config-field studio-config-field-full">
            <label>Toutes les</label>
            <div class="studio-exec-freq-grid">${FREQUENCY_PRESETS.map(p => `
                <label class="studio-radio-item studio-exec-freq${freq === p.id ? ' selected' : ''}" data-exec-freq="${p.id}">
                    <input type="radio" name="execFreq"${freq === p.id ? ' checked' : ''}> ${p.label}
                </label>`).join('')}
            </div></div>`;
    }

    function renderTriggerConfigSimple(exec) {
        const c = exec.triggerConfig || {};
        switch (exec.trigger) {
            case 'manual':
                return `<p class="studio-exec-hint"><i class="bi bi-info-circle"></i> Lancement manuel depuis l'interface Agentia.</p>`;
            case 'scheduled':
                return freqRadios(c);
            case 'email-in':
                return fieldInput('Adresse email', 'triggerConfig.address', c.address, 'text', 'comptabilite@entreprise.com') +
                    fieldInput('Sujet contient', 'triggerConfig.subjectContains', c.subjectContains, 'text', 'Facture');
            default:
                return `<p class="studio-exec-hint"><i class="bi bi-info-circle"></i> Configuration par défaut appliquée — personnalisez via le mode avancé.</p>`;
        }
    }

    function renderRuntimeConfigSimple(exec) {
        const c = exec.runtimeConfig || {};
        if (exec.runtime === 'windows-service') {
            return fieldInput('Nom du service', 'runtimeConfig.serviceName', c.serviceName, 'text', 'MonAgentRuntime');
        }
        return `<p class="studio-exec-hint"><i class="bi bi-info-circle"></i> ${getRuntimeSummary(exec)} avec paramètres par défaut (timeout 300s, 2 Go RAM).</p>`;
    }

    function renderTriggerConfig(exec) {
        const c = exec.triggerConfig || {};
        const adv = exec.mode === 'advanced';
        switch (exec.trigger) {
            case 'manual':
                return `<p class="studio-exec-hint"><i class="bi bi-info-circle"></i> Aucun paramètre — l'agent sera lancé manuellement depuis Agentia.</p>`;
            case 'scheduled':
                return freqRadios(c) +
                    (c.frequency === 'custom' || adv ? fieldInput('Expression Cron', 'triggerConfig.cronExpression', c.cronExpression, 'text', '0 8 * * 1-5') : '') +
                    fieldInput('Fuseau horaire', 'triggerConfig.timezone', c.timezone, 'select', '', { options: TIMEZONES });
            case 'email-in':
                return fieldInput('Boîte mail', 'triggerConfig.mailbox', c.mailbox, 'select', '', { options: MAILBOX_TYPES }) +
                    fieldInput('Adresse', 'triggerConfig.address', c.address, 'text', 'comptabilite@entreprise.com') +
                    fieldInput('Dossier', 'triggerConfig.folder', c.folder, 'text', 'Inbox') +
                    (adv ? fieldInput('Sous-dossier', 'triggerConfig.subfolder', c.subfolder, 'text', 'Factures') : '') +
                    fieldInput('Sujet contient', 'triggerConfig.subjectContains', c.subjectContains, 'text', 'Facture') +
                    (adv ? fieldInput('Expéditeur', 'triggerConfig.sender', c.sender, 'text', '') : '') +
                    fieldInput('Pièces jointes uniquement', 'triggerConfig.attachmentsOnly', c.attachmentsOnly, 'checkbox') +
                    fieldInput('Déplacer après traitement', 'triggerConfig.moveAfter', c.moveAfter, 'checkbox') +
                    fieldInput('Vers', 'triggerConfig.moveTo', c.moveTo, 'text', 'Traités');
            case 'file-created':
                return fieldInput('Dossier surveillé', 'triggerConfig.watchPath', c.watchPath, 'text', 'C:\\Agentia\\Inbox') +
                    fieldInput('Pattern fichiers', 'triggerConfig.pattern', c.pattern, 'text', '*.pdf') +
                    fieldInput('Sous-dossiers récursifs', 'triggerConfig.recursive', c.recursive, 'checkbox');
            case 'webhook':
                return fieldInput('URL générée', 'triggerConfig.url', c.url, 'text', '') +
                    fieldInput('Authentification', 'triggerConfig.authType', c.authType, 'select', '', { options: ['Aucune', 'API Key', 'Bearer Token', 'Basic'] }) +
                    fieldInput('Secret', 'triggerConfig.secret', c.secret, 'password', '', { secret: true }) +
                    fieldInput('Méthode', 'triggerConfig.method', c.method, 'select', '', { options: ['POST', 'PUT', 'PATCH'] }) +
                    checkGrid('Formats acceptés', 'triggerConfig.formats', ['JSON', 'XML', 'Multipart'], c.formats || ['JSON']);
            case 'api':
                return fieldInput('Endpoint', 'triggerConfig.endpoint', c.endpoint, 'text', '/api/agent/run') +
                    fieldInput('Méthode', 'triggerConfig.method', c.method, 'select', '', { options: ['POST', 'GET'] }) +
                    fieldInput('Authentification', 'triggerConfig.authType', c.authType, 'select', '', { options: ['Bearer Token', 'API Key', 'OAuth2', 'Aucune'] }) +
                    fieldInput('Quota', 'triggerConfig.quotaPerHour', c.quotaPerHour, 'number', '1000') + ' appels / heure';
            default:
                return '';
        }
    }

    function renderRuntimeConfig(exec) {
        const c = exec.runtimeConfig || {};
        const adv = exec.mode === 'advanced';
        switch (exec.runtime) {
            case 'windows-service':
                return fieldInput('Nom du service', 'runtimeConfig.serviceName', c.serviceName, 'text', 'InvoiceAgentRuntime') +
                    fieldInput('Mode', 'runtimeConfig.mode', c.mode, 'select', '', { options: ['Automatique', 'Manuel', 'Désactivé'] }) +
                    (adv ? fieldInput('Utilisateur Windows', 'runtimeConfig.windowsUser', c.windowsUser, 'select', '', { options: ['LocalService', 'NetworkService', 'LocalSystem', 'Compte dédié'] }) : '') +
                    fieldInput('Démarrage automatique', 'runtimeConfig.autoStart', c.autoStart, 'checkbox') +
                    fieldInput('Redémarrage automatique', 'runtimeConfig.autoRestart', c.autoRestart, 'checkbox') +
                    (adv ? fieldInput('Instances max', 'runtimeConfig.maxInstances', c.maxInstances, 'number', '2') : '') +
                    fieldInput('Timeout', 'runtimeConfig.timeoutSec', c.timeoutSec, 'number', '300') + ' secondes' +
                    fieldInput('Mémoire maximale', 'runtimeConfig.maxMemoryGb', c.maxMemoryGb, 'number', '2') + ' Go' +
                    fieldInput('CPU maximal', 'runtimeConfig.maxCpuPercent', c.maxCpuPercent, 'number', '40') + ' %' +
                    fieldInput('Priorité', 'runtimeConfig.priority', c.priority, 'select', '', { options: PRIORITIES });
            case 'docker':
                return fieldInput('Image', 'runtimeConfig.image', c.image, 'text', 'agentia/agent:latest') +
                    fieldInput('Conteneur', 'runtimeConfig.containerName', c.containerName, 'text', '') +
                    fieldInput('Politique restart', 'runtimeConfig.restartPolicy', c.restartPolicy, 'select', '', { options: ['no', 'always', 'unless-stopped', 'on-failure'] }) +
                    fieldInput('Limite CPU', 'runtimeConfig.cpuLimit', c.cpuLimit, 'text', '0.4') +
                    fieldInput('Limite mémoire', 'runtimeConfig.memoryLimit', c.memoryLimit, 'text', '2g') +
                    (adv ? fieldInput('Réseau', 'runtimeConfig.network', c.network, 'text', 'agentia-net') : '');
            case 'kubernetes':
                return fieldInput('Namespace', 'runtimeConfig.namespace', c.namespace, 'text', 'agentia') +
                    fieldInput('Deployment', 'runtimeConfig.deployment', c.deployment, 'text', '') +
                    fieldInput('Replicas', 'runtimeConfig.replicas', c.replicas, 'number', '2') +
                    fieldInput('CPU request', 'runtimeConfig.cpuRequest', c.cpuRequest, 'text', '200m') +
                    fieldInput('Mémoire request', 'runtimeConfig.memoryRequest', c.memoryRequest, 'text', '512Mi') +
                    (adv ? fieldInput('CPU limit', 'runtimeConfig.cpuLimit', c.cpuLimit, 'text', '500m') +
                        fieldInput('Mémoire limit', 'runtimeConfig.memoryLimit', c.memoryLimit, 'text', '2Gi') : '');
            case 'linux-service':
                return fieldInput('Unit systemd', 'runtimeConfig.unitName', c.unitName, 'text', 'agent.service') +
                    fieldInput('Utilisateur', 'runtimeConfig.linuxUser', c.linuxUser, 'text', 'agentia') +
                    fieldInput('Démarrage auto', 'runtimeConfig.autoStart', c.autoStart, 'checkbox') +
                    fieldInput('Redémarrage auto', 'runtimeConfig.autoRestart', c.autoRestart, 'checkbox') +
                    fieldInput('Timeout', 'runtimeConfig.timeoutSec', c.timeoutSec, 'number', '300');
            case 'azure':
                return fieldInput('Function App', 'runtimeConfig.functionApp', c.functionApp, 'text', '') +
                    fieldInput('Plan', 'runtimeConfig.plan', c.plan, 'select', '', { options: ['Consumption', 'Premium', 'Dedicated'] }) +
                    fieldInput('Région', 'runtimeConfig.region', c.region, 'text', 'West Europe') +
                    fieldInput('Timeout', 'runtimeConfig.timeoutSec', c.timeoutSec, 'number', '300') +
                    fieldInput('Instances max', 'runtimeConfig.maxInstances', c.maxInstances, 'number', '10');
            case 'aws':
                return fieldInput('Fonction Lambda', 'runtimeConfig.functionName', c.functionName, 'text', '') +
                    fieldInput('Mémoire (Mo)', 'runtimeConfig.memoryMb', c.memoryMb, 'number', '2048') +
                    fieldInput('Timeout', 'runtimeConfig.timeoutSec', c.timeoutSec, 'number', '300') +
                    fieldInput('Région', 'runtimeConfig.region', c.region, 'text', 'eu-west-1');
            case 'ovh':
                return fieldInput('Project ID', 'runtimeConfig.projectId', c.projectId, 'text', 'ovh-project-id') +
                    fieldInput('Région', 'runtimeConfig.region', c.region, 'text', 'GRA') +
                    fieldInput('Type instance', 'runtimeConfig.instanceType', c.instanceType, 'text', 'b2-7') +
                    fieldInput('Conteneur', 'runtimeConfig.containerName', c.containerName, 'text', '') +
                    fieldInput('Replicas', 'runtimeConfig.replicas', c.replicas, 'number', '1') +
                    fieldInput('Timeout', 'runtimeConfig.timeoutSec', c.timeoutSec, 'number', '300') +
                    fieldInput('Mémoire max (Go)', 'runtimeConfig.maxMemoryGb', c.maxMemoryGb, 'number', '2') +
                    fieldInput('CPU max (%)', 'runtimeConfig.maxCpuPercent', c.maxCpuPercent, 'number', '50');
            default:
                return '';
        }
    }

    function renderExecutionPanel(exec, agentName, onUpdate) {
        const panel = document.getElementById('executionPanel');
        if (!panel || !exec) return;
        const isAdv = exec.mode === 'advanced';

        const triggerCards = TRIGGERS.map(t => `
            <label class="studio-exec-trigger-card${exec.trigger === t.id ? ' selected' : ''}" data-exec-trigger="${t.id}">
                <input type="radio" name="execTrigger"${exec.trigger === t.id ? ' checked' : ''}>
                <span class="studio-exec-trigger-icon"><i class="bi ${t.icon}"></i></span>
                <span class="studio-exec-trigger-label">${t.label}</span>
                <span class="studio-exec-trigger-desc">${t.desc}</span>
            </label>`).join('');

        const runtimeCards = RUNTIMES.map(r => `
            <label class="studio-exec-runtime-card${exec.runtime === r.id ? ' selected' : ''}" data-exec-runtime="${r.id}">
                <input type="radio" name="execRuntime"${exec.runtime === r.id ? ' checked' : ''}>
                <span class="studio-exec-trigger-icon"><i class="bi ${r.icon}"></i></span>
                <span class="studio-exec-trigger-label">${r.label}</span>
                <span class="studio-exec-trigger-desc">${r.desc}</span>
            </label>`).join('');

        const res = exec.resilience || {};
        const log = exec.logging || {};
        const sup = exec.supervision || {};
        const adv = exec.advanced || {};

        panel.innerHTML = `
            <div class="studio-exec-mode-bar">
                <div class="studio-exec-mode-label">
                    <i class="bi bi-${isAdv ? 'gear-wide-connected' : 'lightning-charge'}"></i>
                    Mode ${isAdv ? 'Avancé' : 'Simple'}
                </div>
                <button type="button" class="studio-btn studio-btn-outline studio-exec-mode-btn" id="execModeToggle">
                    <i class="bi bi-${isAdv ? 'chevron-up' : 'sliders'}"></i>
                    ${isAdv ? 'Revenir au mode simple' : 'Afficher les paramètres avancés'}
                </button>
            </div>

            <section class="studio-exec-section">
                <h4 class="studio-exec-section-title"><span class="studio-exec-num">1</span> Déclencheur principal</h4>
                <div class="studio-exec-trigger-grid">${triggerCards}</div>
                <div class="studio-exec-config-box">
                    <div class="studio-source-config-fields">${isAdv ? renderTriggerConfig(exec) : renderTriggerConfigSimple(exec)}</div>
                </div>
            </section>

            <section class="studio-exec-section">
                <h4 class="studio-exec-section-title"><span class="studio-exec-num">2</span> Runtime</h4>
                <div class="studio-exec-runtime-grid">${runtimeCards}</div>
                <div class="studio-exec-config-box">
                    <div class="studio-source-config-fields">${isAdv ? renderRuntimeConfig(exec) : renderRuntimeConfigSimple(exec)}</div>
                </div>
            </section>

            <div class="studio-exec-advanced-block${isAdv ? ' show' : ''}">
                <section class="studio-exec-section">
                    <h4 class="studio-exec-section-title"><span class="studio-exec-num">3</span> Résilience</h4>
                    <div class="studio-exec-config-box">
                        <div class="studio-source-config-fields">
                            ${fieldInput('Si erreur — réessayer', 'resilience.retryOnError', res.retryOnError, 'checkbox')}
                            ${fieldInput('Nombre de tentatives', 'resilience.maxAttempts', res.maxAttempts, 'number', '5')}
                            ${fieldInput('Attendre', 'resilience.waitSeconds', res.waitSeconds, 'number', '30')} secondes
                            ${checkGrid('Notifier', 'resilience.notify', NOTIFY_CHANNELS, res.notify)}
                            ${fieldInput('Arrêter définitivement', 'resilience.stopOnFail', res.stopOnFail, 'checkbox')}
                        </div>
                    </div>
                </section>

                <section class="studio-exec-section">
                    <h4 class="studio-exec-section-title"><span class="studio-exec-num">4</span> Journalisation</h4>
                    <div class="studio-exec-config-box">
                        <div class="studio-source-config-fields">
                            ${fieldInput('Niveau de logs', 'logging.level', log.level, 'select', '', { options: LOG_LEVELS })}
                            ${fieldInput('Conserver', 'logging.retentionDays', log.retentionDays, 'number', '30')} jours
                            ${checkGrid('Exporter vers', 'logging.exports', LOG_EXPORTS, log.exports)}
                        </div>
                    </div>
                </section>

                <section class="studio-exec-section">
                    <h4 class="studio-exec-section-title"><span class="studio-exec-num">5</span> Supervision</h4>
                    <div class="studio-exec-config-box">
                        <div class="studio-source-config-fields">
                            ${fieldInput('Health Check', 'supervision.healthIntervalSec', sup.healthIntervalSec, 'number', '60')} secondes
                            ${fieldInput('Heartbeat activé', 'supervision.heartbeat', sup.heartbeat, 'checkbox')}
                            ${checkGrid('Monitoring', 'supervision.monitoring', MONITORING_TOOLS, sup.monitoring)}
                            ${checkGrid('Alertes', 'supervision.alerts', ALERT_TYPES, sup.alerts)}
                        </div>
                    </div>
                </section>

                <section class="studio-exec-section">
                    <h4 class="studio-exec-section-title"><span class="studio-exec-num">+</span> Paramètres moteur avancés</h4>
                    <div class="studio-exec-config-box">
                        <div class="studio-source-config-fields">
                            ${fieldInput('Expression Cron (override)', 'advanced.cronExpression', adv.cronExpression, 'text', '0 8 * * 1-5')}
                            ${fieldInput('Threads', 'advanced.threads', adv.threads, 'number', '4')}
                            ${fieldInput('Parallélisme', 'advanced.parallelism', adv.parallelism, 'number', '2')}
                            ${fieldInput('Cache activé', 'advanced.cacheEnabled', adv.cacheEnabled, 'checkbox')}
                            ${fieldInput('Cache TTL (min)', 'advanced.cacheTtlMin', adv.cacheTtlMin, 'number', '15')}
                            ${fieldInput('Variables d\'environnement', 'advanced.envVars', adv.envVars, 'textarea', 'KEY=value\\nAPI_URL=...')}
                            ${fieldInput('Secrets (Vault ref)', 'advanced.secretsRef', adv.secretsRef, 'password', '', { secret: true })}
                            ${fieldInput('Affinité CPU', 'advanced.cpuAffinity', adv.cpuAffinity, 'text', '0-3')}
                            ${fieldInput('Quota IA / heure', 'advanced.aiQuotaPerHour', adv.aiQuotaPerHour, 'number', '500')}
                            ${fieldInput('Gestion erreurs', 'advanced.errorHandling', adv.errorHandling, 'select', '', { options: ['retry-then-notify', 'fail-fast', 'quarantine', 'ignore'] })}
                        </div>
                    </div>
                </section>
            </div>`;

        bindExecutionPanel(exec, onUpdate);
    }

    function setExecPath(exec, path, value) {
        const parts = path.split('.');
        let obj = exec;
        for (let i = 0; i < parts.length - 1; i++) {
            if (!obj[parts[i]]) obj[parts[i]] = {};
            obj = obj[parts[i]];
        }
        obj[parts[parts.length - 1]] = value;
    }

    function getExecPath(exec, path) {
        return path.split('.').reduce((o, k) => o?.[k], exec);
    }

    function bindExecutionPanel(exec, onUpdate) {
        const panel = document.getElementById('executionPanel');
        if (!panel) return;
        const q = (sel, ctx) => (ctx || panel).querySelector(sel);
        const qq = (sel, ctx) => [...(ctx || panel).querySelectorAll(sel)];

        q('#execModeToggle')?.addEventListener('click', () => {
            exec.mode = exec.mode === 'advanced' ? 'simple' : 'advanced';
            onUpdate(true);
        });

        qq('[data-exec-trigger]', panel).forEach(card => {
            card.addEventListener('click', e => {
                e.preventDefault();
                const id = card.dataset.execTrigger;
                if (exec.trigger === id) return;
                exec.trigger = id;
                exec.triggerConfig = defaultTriggerConfig(id, exec.runtimeConfig?.serviceName || 'agent');
                onUpdate(true);
            });
        });

        qq('[data-exec-runtime]', panel).forEach(card => {
            card.addEventListener('click', e => {
                e.preventDefault();
                const id = card.dataset.execRuntime;
                if (exec.runtime === id) return;
                exec.runtime = id;
                exec.runtimeConfig = defaultRuntimeConfig(id, exec.runtimeConfig?.serviceName || 'Agent');
                onUpdate(true);
            });
        });

        qq('[data-exec-freq]', panel).forEach(item => {
            item.addEventListener('click', e => {
                e.preventDefault();
                const id = item.dataset.execFreq;
                exec.triggerConfig.frequency = id;
                const preset = FREQUENCY_PRESETS.find(p => p.id === id);
                if (preset?.cron) exec.triggerConfig.cronExpression = preset.cron;
                onUpdate(true);
            });
        });

        qq('[data-exec-path]', panel).forEach(el => {
            const evt = el.type === 'checkbox' ? 'change' : (el.tagName === 'SELECT' ? 'change' : 'input');
            el.addEventListener(evt, () => {
                const path = el.dataset.execPath;
                let val = el.type === 'checkbox' ? el.checked : (el.type === 'number' ? parseFloat(el.value) || 0 : el.value);
                setExecPath(exec, path, val);
                if (path === 'triggerConfig.frequency' && val === 'custom') onUpdate(true);
                else onUpdate(false);
            });
        });

        qq('[data-exec-array]', panel).forEach(item => {
            item.addEventListener('click', e => {
                e.preventDefault();
                const path = item.dataset.execArray;
                const val = item.dataset.execValue;
                const arr = getExecPath(exec, path) || [];
                const idx = arr.indexOf(val);
                if (idx >= 0) arr.splice(idx, 1); else arr.push(val);
                setExecPath(exec, path, arr);
                onUpdate(true);
            });
        });
    }

    function renderRuntimePreviewPanel(exec, agentName) {
        const wrap = document.getElementById('runtimePreviewCard');
        if (!wrap) return;
        const p = getRuntimePreview(exec, agentName);
        const rows = [
            ['Nom', p.name], ['Type', p.type], ['Déclencheur', p.trigger],
            ['Temps maximum', p.maxTime], ['Mémoire', p.memory], ['CPU', p.cpu],
            ['Mode', p.mode], ['Supervision', p.health], ['Monitoring', p.monitoring],
            ['Logs', p.logs], ['Restart', p.restart]
        ];
        wrap.innerHTML = `
            <div class="studio-runtime-preview-head">
                <h4><i class="bi bi-cpu"></i> Runtime Preview</h4>
                <span class="studio-runtime-preview-badge">${p.healthBadge === 'config' ? '○ Prévu' : '○ —'}</span>
            </div>
            <div class="studio-runtime-preview-grid">
                ${rows.map(([k, v]) => `
                    <div class="studio-runtime-preview-row">
                        <span class="studio-runtime-preview-key">${k}</span>
                        <span class="studio-runtime-preview-val">${escHtml(v)}</span>
                    </div>`).join('')}
            </div>`;
    }

    global.STUDIO_EXECUTION = {
        TRIGGERS, FREQUENCY_PRESETS, TIMEZONES, RUNTIMES,
        LOG_LEVELS, LOG_EXPORTS, NOTIFY_CHANNELS, MONITORING_TOOLS, ALERT_TYPES, PRIORITIES, MAILBOX_TYPES,
        createDefaultExecution, defaultTriggerConfig, defaultRuntimeConfig,
        migrateExecutionFromLegacy, getTriggerSummary, getRuntimeSummary,
        getCostMultiplier, buildExecutionPayload, getRuntimePreview, getFrequencyLabel,
        renderExecutionPanel, bindExecutionPanel, renderRuntimePreviewPanel
    };
})(window);
