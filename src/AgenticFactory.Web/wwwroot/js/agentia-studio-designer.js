/* ============================================================
   Agentia Studio — Visual Designer v2.0
   n8n-inspired dark canvas · Vertical flow · Agent UX
   Pure vanilla JS + SVG, zero external libraries
   ============================================================ */
(function () {
    'use strict';

    /* ── Catalogue des capacités (palette business) ─────────── */
    const NODE_CATALOGUE = {
        'DÉCLENCHEURS': [
            { type: 'trigger-cron',    label: 'Planifié (cron)',    icon: '📅', category: 'trigger',   output: 'Événement',     inputType: '—',      outputType: 'Événement',   desc: 'Exécute l\'agent selon une planification cron.' },
            { type: 'trigger-email',   label: 'Email entrant',      icon: '📧', category: 'trigger',   output: 'Email',         inputType: '—',      outputType: 'Email',       desc: 'Déclenché à chaque email reçu dans la boîte.' },
            { type: 'trigger-webhook', label: 'API / Webhook',      icon: '🌐', category: 'trigger',   output: 'Données JSON',  inputType: '—',      outputType: 'JSON',        desc: 'Déclenché par un appel HTTP entrant externe.' },
            { type: 'trigger-folder',  label: 'Changement fichier', icon: '📂', category: 'trigger',   output: 'Fichier',       inputType: '—',      outputType: 'Fichier',     desc: 'Surveille un dossier et réagit aux nouveaux fichiers.' },
            { type: 'trigger-event',   label: 'Événement système',  icon: '⚡', category: 'trigger',   output: 'Événement',     inputType: '—',      outputType: 'Payload',     desc: 'Déclenché par un événement applicatif ou système.' },
        ],
        'CONNECTEURS': [
            { type: 'gmail',            label: 'Gmail',          icon: '📧', category: 'connector', output: 'Email',         inputType: 'OAuth',       outputType: 'Email',    desc: 'Surveille la boîte Gmail et récupère les pièces jointes.',              metric1val: '352',   metric1label: 'emails',      metric2val: '320ms', metric2label: 'moyen' },
            { type: 'connector-outlook',    label: 'Outlook',        icon: '📧', category: 'connector', output: 'Email/Fichier', inputType: 'Credentials', outputType: 'Email',    desc: 'Lit et envoie des emails via Microsoft Outlook.' },
            { type: 'connector-teams',      label: 'Teams',          icon: '💬', category: 'connector', output: 'Message',       inputType: 'Credentials', outputType: 'Message',  desc: 'Envoie un message ou fichier sur un canal Teams.' },
            { type: 'connector-sharepoint', label: 'SharePoint',     icon: '📁', category: 'connector', output: 'Document',      inputType: 'Credentials', outputType: 'Document', desc: 'Lit et écrit des documents dans SharePoint.' },
            { type: 'connector-gmail',      label: 'Gmail',          icon: '✉️', category: 'connector', output: 'Email',         inputType: 'OAuth',       outputType: 'Email',    desc: 'Surveille la boîte Gmail et récupère les pièces jointes.' },
            { type: 'connector-gdrive',     label: 'Google Drive',   icon: '🗂', category: 'connector', output: 'Fichier',       inputType: 'OAuth',       outputType: 'Fichier',  desc: 'Accède aux fichiers et dossiers Google Drive.' },
            { type: 'connector-sap',        label: 'SAP',            icon: '🏭', category: 'connector', output: 'Données ERP',   inputType: 'Credentials', outputType: 'ERP Data', desc: 'Intégration avec les modules SAP (FI, MM, SD).' },
            { type: 'connector-dynamics',   label: 'Dynamics 365',   icon: '⚙️', category: 'connector', output: 'Données CRM',  inputType: 'OAuth',       outputType: 'CRM Data', desc: 'Connecté aux entités Dynamics CRM/ERP.' },
            { type: 'connector-dropbox',    label: 'Dropbox',        icon: '📦', category: 'connector', output: 'Fichier',       inputType: 'OAuth',       outputType: 'Fichier',  desc: 'Synchronise et transfère des fichiers Dropbox.' },
        ],
        'IA — COGNITION': [
            { type: 'ocr-classify',   label: 'OCR & Classification', icon: '🔍', category: 'ia', output: 'Type document',  inputType: 'Image',    outputType: 'Classification', desc: 'Analyse le document et identifie son type.',                            metric1val: '98.5%', metric1label: 'précision',   metric2val: '1.2s',  metric2label: 'moyen' },
            { type: 'extraction-ia',  label: 'Extraction IA',        icon: '🧠', category: 'ia', output: 'JSON structuré', inputType: 'Document', outputType: 'JSON structuré', desc: "Extrait les champs d'identité selon le type de document.",              metric1val: '97.8%', metric1label: 'précision',   metric2val: '1.8s',  metric2label: 'moyen' },
            { type: 'skill-understand',   label: 'Compréhension IA',     icon: '🧠', category: 'ia', output: 'Classification', inputType: 'Texte',    outputType: 'Classification', desc: 'Analyse et classe le contenu avec un modèle LLM.' },
            { type: 'skill-extraction',   label: 'Extraction IA',        icon: '🔍', category: 'ia', output: 'JSON structuré', inputType: 'Document', outputType: 'JSON structuré', desc: 'Extraction intelligente de données depuis documents.' },
            { type: 'skill-summary',      label: 'Résumé IA',            icon: '📝', category: 'ia', output: 'Texte',          inputType: 'Texte',    outputType: 'Résumé',         desc: 'Génère un résumé concis du contenu fourni.' },
            { type: 'skill-classify',     label: 'Classification IA',    icon: '🏷', category: 'ia', output: 'Catégorie',      inputType: 'Texte',    outputType: 'Catégorie',      desc: 'Classe selon des catégories métier prédéfinies.' },
            { type: 'skill-vision',       label: 'Vision / OCR',         icon: '👁', category: 'ia', output: 'Texte extrait',  inputType: 'Image',    outputType: 'Texte',          desc: 'Extrait le texte d\'images et documents scannés.' },
            { type: 'skill-rag',          label: 'Recherche sémantique', icon: '🔎', category: 'ia', output: 'Résultats',      inputType: 'Requête',  outputType: 'Résultats',      desc: 'Recherche dans une base vectorielle par sémantique.' },
        ],
        'ACTIONS': [
            { type: 'action-email',    label: 'Envoi email',       icon: '📤', category: 'action',   output: 'Email envoyé', inputType: 'Données', outputType: 'Email envoyé', desc: 'Envoie un email formaté vers les destinataires.' },
            { type: 'action-api',      label: 'Appel API',         icon: '🔗', category: 'api',      output: 'Réponse HTTP', inputType: 'Payload', outputType: 'Réponse HTTP', desc: 'Effectue un appel HTTP vers une API externe.' },
            { type: 'action-database', label: 'Base de données',   icon: '🗄', category: 'database', output: 'Résultat SQL', inputType: 'Requête', outputType: 'Résultat SQL', desc: 'Exécute des requêtes SQL en lecture ou écriture.' },
            { type: 'action-file',     label: 'Fichier / Excel',   icon: '📁', category: 'action',   output: 'Fichier',      inputType: 'Données', outputType: 'Fichier',      desc: 'Crée ou met à jour des fichiers Excel / CSV.' },
            { type: 'action-ticket',   label: 'Ticket / Tâche',    icon: '🎫', category: 'action',   output: 'Ticket créé',  inputType: 'Données', outputType: 'Ticket ID',    desc: 'Crée un ticket dans Jira, Azure DevOps, etc.' },
            { type: 'action-teams',    label: 'Notif. Teams',      icon: '💬', category: 'action',   output: 'Message',      inputType: 'Texte',   outputType: 'Confirmé',     desc: 'Envoie un résumé ou alerte sur un canal Teams.' },
            { type: 'action-storage',  label: 'Stockage',          icon: '💾', category: 'action',   output: 'Fichier',      inputType: 'Données', outputType: 'Chemin',       desc: 'Enregistrement dans SharePoint & base SQL.' },
        ],
        'DÉCISION & CONTRÔLE': [
            { type: 'decision-if',        label: 'Condition (IF)',     icon: '⚡', category: 'decision', output: 'Branche',       inputType: 'Données', outputType: 'Branche',   desc: 'Dirige le flux selon une condition booléenne.' },
            { type: 'decision-switch',    label: 'Switch / Routage',   icon: '🔀', category: 'decision', output: 'Route',         inputType: 'Données', outputType: 'Route',     desc: 'Distribue vers plusieurs branches selon une valeur.' },
            { type: 'decision-loop',      label: 'Boucle',             icon: '🔁', category: 'decision', output: 'Itération',     inputType: 'Liste',   outputType: 'Itération', desc: 'Itère sur chaque élément d\'une collection.' },
            { type: 'decision-wait',      label: 'Attente',            icon: '⏳', category: 'decision', output: 'Signal',        inputType: '—',       outputType: 'Signal',    desc: 'Suspend l\'exécution pendant une durée définie.' },
            { type: 'decision-validate',  label: 'Validation humaine', icon: '✅', category: 'human',    output: 'Décision',      inputType: 'Données', outputType: 'Décision',  desc: 'Validation des données critiques par un opérateur.' },
            { type: 'decision-approve',   label: 'Approbation',        icon: '👤', category: 'human',    output: 'Approbation',   inputType: 'Demande', outputType: 'Décision',  desc: 'Demande d\'approbation à un responsable désigné.' },
            { type: 'decision-sign',      label: 'Signature',          icon: '✍', category: 'human',    output: 'Document signé',inputType: 'Document',outputType: 'Signé',     desc: 'Envoie le document pour signature électronique.' },
        ],
        'UTILITAIRES': [
            { type: 'util-variables', label: 'Variables',     icon: '📋', category: 'utility', output: 'Valeurs',      inputType: '—',    outputType: 'Valeurs',     desc: 'Stocke et partage des variables entre les étapes.' },
            { type: 'util-secrets',   label: 'Secrets Vault', icon: '🔐', category: 'utility', output: 'Credentials',  inputType: '—',    outputType: 'Credentials', desc: 'Accès sécurisé aux secrets chiffrés dans le Vault.' },
            { type: 'util-cache',     label: 'Cache',         icon: '💾', category: 'utility', output: 'Données',      inputType: 'Clé', outputType: 'Données',     desc: 'Met en cache les résultats pour éviter les doublons.' },
            { type: 'util-logs',      label: 'Journal / Logs',icon: '📊', category: 'monitor', output: 'Logs',         inputType: 'Tout', outputType: 'Logs',        desc: 'Enregistre les traces et métriques d\'exécution.' },
        ],
    };

    /* ── Category accent colors ─────────────────────────────── */
    const CAT_COLOR = {
        trigger:   '#f97316',
        ia:        '#8b5cf6',
        decision:  '#3b82f6',
        database:  '#22c55e',
        action:    '#10b981',
        api:       '#06b6d4',
        human:     '#f59e0b',
        monitor:   '#6b7280',
        connector: '#3b82f6',
        utility:   '#8b8fa8',
        validation:'#22c55e',
    };

    /* ── Category labels (for badges) ───────────────────────── */
    const CAT_LABEL = {
        trigger:   'DÉCLENCHEUR',
        ia:        'IA',
        decision:  'DÉCISION',
        database:  'BDD',
        action:    'ACTION',
        api:       'API',
        human:     'HUMAIN',
        monitor:   'LOGS',
        connector: 'CONNECTEUR',
        utility:   'UTIL',
        validation:'VALIDATION',
    };

    /* ── Config field definitions per type ───────────────────── */
    const NODE_CONFIG_FIELDS = {
        'trigger-cron':    [{ key:'cron', label:'Expression cron', type:'text', placeholder:'0 9 * * 1-5' }],
        'trigger-email':   [{ key:'account', label:'Compte email', type:'text', placeholder:'factures@entreprise.com' },
                            { key:'folder',  label:'Dossier',      type:'text', placeholder:'Inbox' }],
        'trigger-webhook': [{ key:'path', label:'Chemin', type:'text', placeholder:'/webhook/agent' }],
        'trigger-folder':  [{ key:'path', label:'Chemin dossier', type:'text', placeholder:'/data/entrants/' }],
        'skill-understand':  [{ key:'model', label:'Modèle IA', type:'select', options:['gpt-4o-mini','gpt-4o','claude-3-haiku','claude-3-sonnet'] },
                              { key:'mission', label:'Mission', type:'textarea', placeholder:'Identifier le type de document…' }],
        'skill-extraction':  [{ key:'model', label:'Modèle IA', type:'select', options:['gpt-4o-mini','gpt-4o'] },
                              { key:'fields', label:'Champs', type:'text', placeholder:'nom, date, montant…' }],
        'skill-summary':     [{ key:'model', label:'Modèle IA', type:'select', options:['gpt-4o-mini','gpt-4o'] },
                              { key:'length', label:'Longueur', type:'select', options:['Court (2-3 phrases)','Moyen (1 paragraphe)','Long (page)'] }],
        'skill-classify':    [{ key:'model', label:'Modèle IA', type:'select', options:['gpt-4o-mini','gpt-4o'] },
                              { key:'categories', label:'Catégories', type:'text', placeholder:'Facture, Contrat, CV…' }],
        'skill-vision':      [{ key:'model', label:'Modèle Vision', type:'select', options:['gpt-4o','claude-3-sonnet'] }],
        'skill-rag':         [{ key:'index', label:'Index vectoriel', type:'text', placeholder:'Nom de l\'index' }],
        'decision-if':     [{ key:'condition', label:'Condition', type:'text', placeholder:'valeur > 0' }],
        'decision-switch': [{ key:'field', label:'Champ de routage', type:'text', placeholder:'type_document' }],
        'decision-wait':   [{ key:'duration', label:'Durée', type:'text', placeholder:'5m, 1h, 1d…' }],
        'action-email':    [{ key:'recipient', label:'Destinataire', type:'text', placeholder:'dest@entreprise.com' },
                            { key:'subject',   label:'Sujet', type:'text', placeholder:'Notification {date}' }],
        'action-api':      [{ key:'url', label:'URL', type:'text', placeholder:'https://api.exemple.com/endpoint' },
                            { key:'method', label:'Méthode', type:'select', options:['POST','GET','PUT','PATCH','DELETE'] }],
        'action-database': [{ key:'connection', label:'Connexion', type:'text', placeholder:'Server=...;Database=...' },
                            { key:'query', label:'Requête', type:'textarea', placeholder:'INSERT INTO ...' }],
        'action-file':     [{ key:'path', label:'Chemin', type:'text', placeholder:'/output/{date}.xlsx' }],
        'action-ticket':   [{ key:'system', label:'Système', type:'select', options:['Jira','Azure DevOps','ServiceNow','GitHub Issues'] },
                            { key:'project', label:'Projet', type:'text', placeholder:'MON-PROJET' }],
        'connector-outlook':  [{ key:'account', label:'Compte', type:'text', placeholder:'user@contoso.com' }],
        'connector-teams':    [{ key:'team', label:'Équipe', type:'text', placeholder:'Nom de l\'équipe' }],
        'connector-sap':      [{ key:'server', label:'Serveur SAP', type:'text', placeholder:'hostname:port' }],
        'connector-dynamics': [{ key:'org', label:'Organisation', type:'text', placeholder:'orgname.crm.dynamics.com' }],
        'util-variables':  [{ key:'vars', label:'Variables (JSON)', type:'textarea', placeholder:'{"clé": "valeur"}' }],
        'util-cache':      [{ key:'ttl', label:'Durée de vie', type:'text', placeholder:'1h, 24h, 7d' }],
    };

    /* ── Node body builders (rich card content) ─────────────── */
    function buildNodeBodyHtml(node) {
        const entry = getCatalogueEntry(node.type);
        const cat = entry ? entry.category : getNodeCategory(node.type);
        const cfg = node.config || {};

        switch (cat) {
            case 'trigger': return buildTriggerBody(node, entry, cfg);
            case 'ia':      return buildIABody(node, entry, cfg);
            case 'action':  case 'api': case 'database': return buildActionBody(node, entry, cfg);
            case 'decision': return buildDecisionBody(node, entry, cfg);
            case 'human':   return buildHumanBody(node, entry, cfg);
            case 'connector': return buildConnectorBody(node, entry, cfg);
            default:        return buildDefaultBody(node, entry, cfg);
        }
    }

    function field(label, value, cls) {
        return `<div class="designer-node-field">
            <span class="designer-node-field-label">${escHtml(label)}</span>
            <span class="designer-node-field-value${cls ? ' ' + cls : ''}">${escHtml(value || '—')}</span>
        </div>`;
    }

    function buildTriggerBody(node, entry, cfg) {
        const type = node.type;
        if (type === 'trigger-cron') {
            const cron = cfg.cron || '— à configurer';
            return field('Planification', cron, cron === '— à configurer' ? 'pending' : '') +
                   field('Sortie', entry ? entry.output : 'Événement');
        }
        if (type === 'trigger-email') {
            return field('Compte', cfg.account || '— à configurer', cfg.account ? '' : 'pending') +
                   field('Dossier', cfg.folder || 'Inbox') +
                   field('Sortie', 'Email + Pièces jointes');
        }
        if (type === 'trigger-webhook') {
            return field('Chemin', cfg.path || '/webhook/agent') +
                   field('Méthode', 'POST') +
                   field('Sortie', 'Données JSON');
        }
        return field('Type', node.label) + field('Sortie', entry ? entry.output : '');
    }

    function buildIABody(node, entry, cfg) {
        const model = cfg.model || 'gpt-4o-mini';
        const mission = cfg.mission || cfg.fields || cfg.categories || '';
        const confidence = node.meta && node.meta.confidence ? node.meta.confidence : 0;
        let html = field('Modèle', model) +
                   (mission ? field('Mission', mission.length > 40 ? mission.slice(0, 40) + '…' : mission) : '');
        if (confidence > 0) {
            html += `<div class="designer-node-field" style="flex-direction:column;gap:4px;">
                <span class="designer-node-field-label">Confiance</span>
                <div class="designer-node-progress-wrap">
                    <div class="designer-node-progress-bar"><div class="designer-node-progress-fill" style="width:${confidence}%"></div></div>
                    <span class="designer-node-progress-value">${confidence}%</span>
                </div></div>`;
        }
        html += field('Sortie', entry ? entry.output : 'JSON');
        return html;
    }

    function buildActionBody(node, entry, cfg) {
        const type = node.type;
        if (type === 'action-email') {
            return field('Destinataire', cfg.recipient || '— à configurer', cfg.recipient ? '' : 'pending') +
                   field('Sujet', cfg.subject || 'Notification') +
                   field('Sortie', 'Email envoyé');
        }
        if (type === 'action-api') {
            const url = cfg.url || '— à configurer';
            return field('URL', url.length > 36 ? url.slice(0, 36) + '…' : url, url === '— à configurer' ? 'pending' : '') +
                   field('Méthode', cfg.method || 'POST') +
                   field('Sortie', 'Réponse HTTP');
        }
        if (type === 'action-database') {
            return field('Connexion', cfg.connection ? 'Configurée ✓' : '— à configurer', cfg.connection ? 'configured' : 'pending') +
                   field('Sortie', 'Résultat');
        }
        if (type === 'action-ticket') {
            return field('Système', cfg.system || 'Jira') +
                   field('Projet', cfg.project || '— à configurer', cfg.project ? '' : 'pending');
        }
        return field('Type', node.label) + field('Sortie', entry ? entry.output : '');
    }

    function buildDecisionBody(node, entry, cfg) {
        if (node.type === 'decision-if') {
            return field('Condition', cfg.condition || '— à définir', cfg.condition ? '' : 'pending') +
                   `<div class="designer-node-radio-group">
                        <div class="designer-node-radio-opt active"><span class="designer-node-radio-dot"></span>✓ Vrai</div>
                        <div class="designer-node-radio-opt"><span class="designer-node-radio-dot"></span>✗ Faux</div>
                    </div>`;
        }
        if (node.type === 'decision-switch') {
            return field('Champ', cfg.field || '— à définir') +
                   field('Routes', '2 branches');
        }
        if (node.type === 'decision-loop') {
            return field('Itération', 'Sur chaque élément') +
                   field('Sortie', 'Itération');
        }
        if (node.type === 'decision-wait') {
            return field('Attente', cfg.duration || '5 minutes') +
                   field('Sortie', 'Signal');
        }
        return field('Type', node.label);
    }

    function buildHumanBody(node, entry, cfg) {
        return field('Assigné à', cfg.assignee || 'Équipe métier') +
               field('Délai', cfg.timeout || '24h') +
               `<div class="designer-node-status-badge todo">⚠ Validation manuelle requise</div>`;
    }

    function buildConnectorBody(node, entry, cfg) {
        const hasConfig = Object.values(cfg).some(v => v && v !== '');
        return field('Connexion', hasConfig ? 'Configurée' : '— à configurer', hasConfig ? 'configured' : 'pending') +
               field('Sortie', entry ? entry.output : 'Données');
    }

    function buildDefaultBody(node, entry, cfg) {
        return field('Type', node.label) +
               field('Sortie', entry ? entry.output : 'Données');
    }

    /* ── Category helpers ────────────────────────────────────── */
    function getNodeCategory(type) {
        const map = {
            'trigger':   'trigger',
            'connector': 'connector',
            'skill':     'ia',
            'action-email': 'action',
            'action-file': 'action',
            'action-ticket': 'action',
            'action-database': 'database',
            'action-api': 'api',
            'decision-if': 'decision',
            'decision-switch': 'decision',
            'decision-loop': 'decision',
            'decision-wait': 'decision',
            'decision-validate': 'human',
            'decision-approve': 'human',
            'decision-sign': 'human',
            'util-logs': 'monitor',
            'util': 'utility',
        };
        for (const [k, v] of Object.entries(map)) {
            if (type.startsWith(k)) return v;
        }
        if (type.startsWith('action')) return 'action';
        return 'utility';
    }

    function getCatalogueEntry(type) {
        for (const items of Object.values(NODE_CATALOGUE)) {
            const found = items.find(n => n.type === type);
            if (found) return found;
        }
        return null;
    }

    function getCategoryColor(category) {
        return CAT_COLOR[category] || '#8b8fa8';
    }

    /* ── State ───────────────────────────────────────────────── */
    const DRAFT_KEY = 'agentia-designer-draft';
    const MODE_KEY  = 'agentia-creation-mode';
    const NODE_W    = 220;
    const NODE_H    = 128;
    const NODE_GAP  = 60;

    let state = {
        version: 2,
        meta:    { name: '', mission: '', businessDomain: '', avatar: '🤖' },
        nodes:   [],
        edges:   [],
        layout:  { zoom: 0.75, panX: 60, panY: 60 },
    };

    // Heights are tracked after DOM render
    const nodeHeights = {};
    // Recent node types for "add step" popup
    const recentTypes = [];

    let selectedNodeIds = new Set();
    let selectedEdgeIds = new Set();
    let isPanning       = false;
    let panStart        = { x: 0, y: 0, panX: 0, panY: 0 };
    let isDraggingNode  = false;
    let dragNodeId      = null;
    let dragOffsets     = {};
    let connectingFrom  = null;
    let tempEdgePath    = null;
    let palette_search  = '';
    let addPopupAfterNode = null;
    let addPopupBeforeNode = null;
    let addPopupEdgeId     = null;
    let activeInspectorTab = 'params';
    let aiDrawerOpen    = false;
    let selectedAiSuggestions = new Set();

    // DOM refs
    let canvasWrap, canvasSvg, canvasNodes, minimapSvg;
    let propEmpty, propBody, inspectorBody;
    let toolbarStats, toolbarZoom;
    let validationBanner;
    let emojiPicker;

    /* ── ID generator ────────────────────────────────────────── */
    function uid() {
        return 'n' + Math.random().toString(36).slice(2, 8) + Date.now().toString(36).slice(-4);
    }

    /* ── Draft persistence ───────────────────────────────────── */
    function saveDraft() {
        try { localStorage.setItem(DRAFT_KEY, JSON.stringify(state)); } catch (_) {}
    }
    function loadDraft() {
        try {
            const raw = localStorage.getItem(DRAFT_KEY);
            if (!raw) return;
            const p = JSON.parse(raw);
            if (p && (p.version === 1 || p.version === 2 || p.version === 3)) {
                state = p;
                state.nodes   = state.nodes   || [];
                state.edges   = state.edges   || [];
                state.layout  = state.layout  || { zoom: 0.75, panX: 60, panY: 60 };
                state.meta    = state.meta    || { name: '', mission: '', businessDomain: '', avatar: '🤖' };
                if (!state.meta.avatar) state.meta.avatar = '🤖';
            }
        } catch (_) {}
    }

    /* ── Canvas transform ────────────────────────────────────── */
    function setTransform() {
        const { zoom, panX, panY } = state.layout;
        canvasNodes.style.transform = `translate(${panX}px,${panY}px) scale(${zoom})`;
        if (toolbarZoom) toolbarZoom.textContent = Math.round(zoom * 100) + '%';
    }
    function screenToCanvas(x, y) {
        const { zoom, panX, panY } = state.layout;
        return { x: (x - panX) / zoom, y: (y - panY) / zoom };
    }
    function getCanvasRect() { return canvasWrap.getBoundingClientRect(); }

    /* ── Port positions (horizontal flow: left/right center) ── */
    function getNodeH(nodeId) {
        return nodeHeights[nodeId] || NODE_H;
    }
    function portPos(node, side) {
        const h = getNodeH(node.id);
        if (side === 'in')  return { x: node.x,          y: node.y + h / 2 };
        return                       { x: node.x + NODE_W, y: node.y + h / 2 };
    }

    /* ── Horizontal bezier path ──────────────────────────────── */
    function bezierPath(x1, y1, x2, y2) {
        const ctl = Math.max(80, Math.abs(x2 - x1) * 0.45);
        return `M ${x1} ${y1} C ${x1 + ctl} ${y1}, ${x2 - ctl} ${y2}, ${x2} ${y2}`;
    }

    /* ── Render edges ────────────────────────────────────────── */
    function renderEdges() {
        canvasSvg.querySelectorAll('.edge-group').forEach(el => el.remove());
        const { zoom, panX, panY } = state.layout;

        state.edges.forEach(edge => {
            const fromNode = state.nodes.find(n => n.id === edge.from);
            const toNode   = state.nodes.find(n => n.id === edge.to);
            if (!fromNode || !toNode) return;

            const p1 = portPos(fromNode, 'out');
            const p2 = portPos(toNode,   'in');
            const sx1 = p1.x * zoom + panX;
            const sy1 = p1.y * zoom + panY;
            const sx2 = p2.x * zoom + panX;
            const sy2 = p2.y * zoom + panY;

            const fromEntry = getCatalogueEntry(fromNode.type);
            const fromCat   = fromEntry ? fromEntry.category : getNodeCategory(fromNode.type);
            const edgeColor = getCategoryColor(fromCat);

            const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            g.classList.add('edge-group');
            g.dataset.edgeId = edge.id;

            // Ghost path for wider click target
            const ghostPath = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            ghostPath.setAttribute('d', bezierPath(sx1, sy1, sx2, sy2));
            ghostPath.setAttribute('fill', 'none');
            ghostPath.setAttribute('stroke', 'transparent');
            ghostPath.setAttribute('stroke-width', '12');
            ghostPath.style.cursor = 'pointer';
            g.appendChild(ghostPath);

            const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            path.classList.add('edge-path');
            path.style.stroke = edgeColor;
            path.style.opacity = selectedEdgeIds.has(edge.id) ? '1' : '0.65';
            if (selectedEdgeIds.has(edge.id)) path.classList.add('selected');
            path.setAttribute('d', bezierPath(sx1, sy1, sx2, sy2));
            path.setAttribute('marker-end', `url(#arrowhead-${fromCat})`);

            ghostPath.addEventListener('click', (e) => {
                e.stopPropagation();
                selectedEdgeIds.clear(); selectedEdgeIds.add(edge.id);
                selectedNodeIds.clear();
                renderEdges(); renderInspectorPanel();
            });
            path.addEventListener('click', (e) => {
                e.stopPropagation();
                selectedEdgeIds.clear(); selectedEdgeIds.add(edge.id);
                selectedNodeIds.clear();
                renderEdges(); renderInspectorPanel();
            });

            g.appendChild(path);

            // Data flow label at horizontal bezier midpoint
            if (zoom > 0.5) {
                const mx = (sx1 + sx2) / 2;
                const my = (sy1 + sy2) / 2;
                const flowLabel = getEdgeFlowLabel(fromNode, edge);
                const fo = document.createElementNS('http://www.w3.org/2000/svg', 'foreignObject');
                fo.setAttribute('x', mx - 40);
                fo.setAttribute('y', my - 13);
                fo.setAttribute('width', '80');
                fo.setAttribute('height', '26');
                fo.setAttribute('pointer-events', 'none');
                const div = document.createElement('div');
                div.className = 'edge-flow-label';
                div.style.textAlign = 'center';
                div.style.color = edgeColor;
                div.style.borderColor = edgeColor + '44';
                div.textContent = flowLabel;
                fo.appendChild(div);
                g.appendChild(fo);
            }

            canvasSvg.appendChild(g);
        });
    }

    function getEdgeFlowLabel(fromNode, edge) {
        const entry = getCatalogueEntry(fromNode.type);
        if (entry && entry.outputType) return entry.outputType.split(' ')[0];
        return '→';
    }

    function isNodeConfigured(node) {
        const fields = NODE_CONFIG_FIELDS[node.type] || [];
        if (fields.length === 0) return true;
        const cfg = node.config || {};
        return fields.some(f => cfg[f.key] && cfg[f.key] !== '');
    }

    /* ── Render nodes ────────────────────────────────────────── */
    function renderNodes() {
        const existing = {};
        canvasNodes.querySelectorAll('.designer-node').forEach(el => {
            existing[el.dataset.nodeId] = el;
        });
        const keepIds = new Set(state.nodes.map(n => n.id));
        Object.keys(existing).forEach(id => {
            if (!keepIds.has(id)) existing[id].remove();
        });
        state.nodes.forEach(node => {
            let el = existing[node.id];
            if (!el) {
                el = createNodeElement(node);
                canvasNodes.appendChild(el);
            } else {
                refreshNodeCard(node, el);
            }
            positionNode(el, node);
            el.classList.toggle('selected', selectedNodeIds.has(node.id));
        });
        // Update heights after DOM render
        requestAnimationFrame(() => {
            canvasNodes.querySelectorAll('.designer-node').forEach(el => {
                nodeHeights[el.dataset.nodeId] = el.offsetHeight;
            });
            renderEdges();
            renderAddStepButtons();
            updateMinimap();
        });
    }

    function positionNode(el, node) {
        el.style.left = node.x + 'px';
        el.style.top  = node.y + 'px';
    }

    function refreshNodeCard(node, el) {
        if (!el) return;
        const configured = isNodeConfigured(node);
        const dot = el.querySelector('.node-status-dot');
        if (dot) dot.className = 'node-status-dot ' + (configured ? 'configured' : 'warning');
        const titleEl = el.querySelector('.node-title');
        if (titleEl) titleEl.textContent = node.label;
    }

    function createNodeElement(node) {
        const entry    = getCatalogueEntry(node.type);
        const category = entry ? entry.category : getNodeCategory(node.type);
        const icon     = entry ? entry.icon : '⚙️';
        const badge    = CAT_LABEL[category] || category.toUpperCase();
        const catColor = getCategoryColor(category);
        const configured = isNodeConfigured(node);
        const isTrigger  = category === 'trigger';
        const desc     = entry ? (entry.desc || '') : '';
        const inputType  = entry ? (entry.inputType  || '—') : '—';
        const outputType = entry ? (entry.outputType || '—') : '—';

        // Node number from index in state
        const numIdx = state.nodes.findIndex(n => n.id === node.id);
        const num = numIdx >= 0 ? numIdx + 1 : '';

        const el = document.createElement('div');
        el.className = `designer-node node-cat-${category}`;
        el.dataset.nodeId = node.id;
        el.style.borderLeftColor = catColor;

        el.innerHTML = `
            <div class="designer-port designer-port-left" data-port="in" data-node="${escHtml(node.id)}"${isTrigger ? ' style="display:none"' : ''}></div>
            <div class="node-header">
                <div class="node-icon-circle" style="background:${catColor}22;color:${catColor}">${escHtml(icon)}</div>
                <div class="node-meta">
                    <div class="node-num-name">
                        ${num ? `<span class="node-number">${num}.</span>` : ''}
                        <span class="node-title" title="${escHtml(node.label)}">${escHtml(node.label)}</span>
                    </div>
                    <div class="node-type-badge" style="background:${catColor}22;color:${catColor};border-color:${catColor}44">${escHtml(badge)}</div>
                </div>
                <div class="node-status-dot ${configured ? 'configured' : 'warning'}"></div>
            </div>
            <button class="node-delete-btn" data-action="delete-node" data-node="${escHtml(node.id)}" title="Supprimer">×</button>
            <div class="node-divider"></div>
            <div class="node-description">${escHtml(desc || 'Configurez cette capacité dans le panneau Propriétés.')}</div>
            <div class="node-divider"></div>
            <div class="node-io-row">
                <span class="node-io-in">↗ Entrée: ${escHtml(inputType)}</span>
                <span class="node-io-out">↘ Sortie: ${escHtml(outputType)}</span>
            </div>
            <div class="designer-port designer-port-right" data-port="out" data-node="${escHtml(node.id)}"></div>`;

        // Drag the whole node (excluding ports and buttons)
        el.addEventListener('mousedown', e => {
            if (e.target.closest('[data-port]') || e.target.closest('[data-action]')) return;
            if (e.button !== 0) return;
            onNodeDragStart(e);
        });
        // Delete
        el.querySelector('[data-action="delete-node"]').addEventListener('click', e => { e.stopPropagation(); deleteNode(node.id); });
        // Click to select
        el.addEventListener('click', e => {
            if (e.target.closest('[data-port]') || e.target.closest('[data-action]')) return;
            selectNode(node.id, e.shiftKey);
            e.stopPropagation();
        });
        // Port connections (left=in, right=out)
        const portLeft  = el.querySelector('[data-port="in"]');
        const portRight = el.querySelector('[data-port="out"]');
        if (portRight) portRight.addEventListener('mousedown', e => { e.stopPropagation(); startConnect(node.id, e); });
        if (portLeft)  portLeft.addEventListener('mouseup',   e => { e.stopPropagation(); endConnect(node.id); });

        return el;
    }

    /* ── Render "+" add-step buttons (horizontal) ────────────── */
    function renderAddStepButtons() {
        canvasNodes.querySelectorAll('.designer-add-step-btn').forEach(el => el.remove());
        if (state.nodes.length === 0) return;

        const nodesWithOutput = new Set(state.edges.map(e => e.from));

        // "+" to the right of each leaf node (no outgoing edge)
        state.nodes.forEach(node => {
            if (nodesWithOutput.has(node.id)) return;
            const h = getNodeH(node.id);
            const btn = document.createElement('div');
            btn.className = 'designer-add-step-btn';
            btn.dataset.afterNode = node.id;
            btn.innerHTML = `<span style="font-size:1rem;font-weight:700">+</span> Étape`;
            btn.style.left = (node.x + NODE_W + 16) + 'px';
            btn.style.top  = (node.y + h / 2 - 14) + 'px';
            btn.addEventListener('click', e => { e.stopPropagation(); showAddStepPopup(node.id, null, null); });
            canvasNodes.appendChild(btn);
        });

        // Small "+" circles between connected nodes (horizontal midpoint)
        state.edges.forEach(edge => {
            const fromNode = state.nodes.find(n => n.id === edge.from);
            const toNode   = state.nodes.find(n => n.id === edge.to);
            if (!fromNode || !toNode) return;
            const midX = fromNode.x + NODE_W + (toNode.x - fromNode.x - NODE_W) / 2 - 11;
            const midY = fromNode.y + getNodeH(fromNode.id) / 2 - 11;
            const btn = document.createElement('div');
            btn.className = 'designer-add-step-btn designer-add-step-between';
            btn.dataset.afterNode  = edge.from;
            btn.dataset.beforeNode = edge.to;
            btn.dataset.edgeId     = edge.id;
            btn.innerHTML = '+';
            btn.style.left = midX + 'px';
            btn.style.top  = midY + 'px';
            btn.addEventListener('click', e => { e.stopPropagation(); showAddStepPopup(edge.from, edge.to, edge.id); });
            canvasNodes.appendChild(btn);
        });
    }

    /* ── Add Step Popup ──────────────────────────────────────── */
    const POPUP_CATEGORIES = [
        { cat: 'trigger',   label: 'Déclencheur',     color: '#f97316' },
        { cat: 'ia',        label: 'IA / Cognition',  color: '#8b5cf6' },
        { cat: 'decision',  label: 'Décision',         color: '#3b82f6' },
        { cat: 'database',  label: 'Base de données',  color: '#22c55e' },
        { cat: 'action',    label: 'Notification',     color: '#ef4444' },
        { cat: 'api',       label: 'API / Webhook',    color: '#06b6d4' },
        { cat: 'human',     label: 'Humain',           color: '#eab308' },
        { cat: 'monitor',   label: 'Monitoring',       color: '#6b7280' },
    ];

    function showAddStepPopup(afterNodeId, beforeNodeId, edgeId) {
        addPopupAfterNode  = afterNodeId;
        addPopupBeforeNode = beforeNodeId;
        addPopupEdgeId     = edgeId;

        const popup = document.getElementById('designerAddPopup');
        if (!popup) return;

        // Show main category grid, hide sub-cat list
        const grid    = popup.querySelector('.designer-add-popup-grid');
        const subcats = popup.querySelector('.designer-add-popup-subcats');
        const backBtn = popup.querySelector('.designer-add-popup-back-btn');
        if (grid)    { grid.style.display = ''; subcats.classList.remove('visible'); }
        if (backBtn) backBtn.style.display = 'none';

        // Update recents
        const recentsWrap = popup.querySelector('.designer-add-popup-recents');
        if (recentsWrap) {
            recentsWrap.innerHTML = '';
            recentTypes.slice(0, 5).forEach(type => {
                const entry = getCatalogueEntry(type);
                if (!entry) return;
                const chip = document.createElement('div');
                chip.className = 'designer-add-popup-recent-chip';
                chip.textContent = entry.icon + ' ' + entry.label;
                chip.addEventListener('click', () => { hideAddStepPopup(); doAddNode(type); });
                recentsWrap.appendChild(chip);
            });
        }

        // Position popup near canvas center
        const rect  = getCanvasRect();
        const left  = rect.left + rect.width / 2 - 180;
        const top   = rect.top  + rect.height / 2 - 200;
        popup.style.left = Math.max(8, left) + 'px';
        popup.style.top  = Math.max(8, top)  + 'px';
        popup.classList.add('visible');

        // Focus search
        const search = popup.querySelector('.designer-add-popup-search');
        if (search) { search.value = ''; search.focus(); }
    }

    function hideAddStepPopup() {
        const popup = document.getElementById('designerAddPopup');
        if (popup) popup.classList.remove('visible');
        addPopupAfterNode = addPopupBeforeNode = addPopupEdgeId = null;
    }

    function showAddPopupSubcats(category) {
        const popup = document.getElementById('designerAddPopup');
        if (!popup) return;
        const grid    = popup.querySelector('.designer-add-popup-grid');
        const subcats = popup.querySelector('.designer-add-popup-subcats');
        const backBtn = popup.querySelector('.designer-add-popup-back-btn');
        if (grid) grid.style.display = 'none';
        if (backBtn) backBtn.style.display = '';

        subcats.innerHTML = '';
        subcats.classList.add('visible');

        // Gather all types matching this category
        for (const items of Object.values(NODE_CATALOGUE)) {
            items.forEach(item => {
                if (item.category !== category) return;
                const el = document.createElement('div');
                el.className = 'designer-add-popup-subcat-item';
                el.innerHTML = `<span style="font-size:1.1rem">${escHtml(item.icon)}</span> ${escHtml(item.label)}`;
                el.addEventListener('click', () => { hideAddStepPopup(); doAddNode(item.type); });
                subcats.appendChild(el);
            });
        }
    }

    function doAddNode(type) {
        // Determine position — horizontal flow (left to right)
        let x, y;
        const afterNode = addPopupAfterNode ? state.nodes.find(n => n.id === addPopupAfterNode) : null;
        if (afterNode) {
            x = afterNode.x + NODE_W + NODE_GAP;
            y = afterNode.y;
        } else if (state.nodes.length > 0) {
            const lastNode = state.nodes[state.nodes.length - 1];
            x = lastNode.x + NODE_W + NODE_GAP;
            y = lastNode.y;
        } else {
            const rect = getCanvasRect();
            const cp   = screenToCanvas(60, rect.height / 2 - NODE_H / 2);
            x = cp.x; y = cp.y;
        }

        // Snap to grid
        x = Math.round(x / 20) * 20;
        y = Math.round(y / 20) * 20;

        const node = addNode(type, x, y);

        // Handle insertion between nodes
        if (addPopupBeforeNode && addPopupEdgeId) {
            // Remove old edge, add two new edges
            state.edges = state.edges.filter(e => e.id !== addPopupEdgeId);
            state.edges.push({ id: uid(), from: addPopupAfterNode,  to: node.id });
            state.edges.push({ id: uid(), from: node.id, to: addPopupBeforeNode });
            renderEdges();
        } else if (addPopupAfterNode && node) {
            // Auto-connect
            state.edges.push({ id: uid(), from: addPopupAfterNode, to: node.id });
            renderEdges();
        }

        // Track recent
        if (!recentTypes.includes(type)) recentTypes.unshift(type);
        if (recentTypes.length > 8) recentTypes.pop();

        addPopupAfterNode = addPopupBeforeNode = addPopupEdgeId = null;
        saveDraft();
    }

    /* ── Node selection ──────────────────────────────────────── */
    function selectNode(id, multi) {
        if (!multi) { selectedNodeIds.clear(); selectedEdgeIds.clear(); }
        if (selectedNodeIds.has(id) && multi) selectedNodeIds.delete(id);
        else selectedNodeIds.add(id);
        renderNodes();
        renderInspectorPanel();
    }
    function clearSelection() {
        selectedNodeIds.clear(); selectedEdgeIds.clear();
        renderNodes(); renderEdges(); renderInspectorPanel();
    }

    /* ── Node CRUD ───────────────────────────────────────────── */
    function addNode(type, x, y) {
        const entry = getCatalogueEntry(type);
        const node = {
            id:     uid(),
            type,
            label:  entry ? entry.label : type,
            x:      Math.round(x),
            y:      Math.round(y),
            config: type === 'skill-understand' ? { model: 'gpt-4o-mini' } : {},
            meta:   {},
        };
        state.nodes.push(node);
        renderNodes();
        updateToolbarStats();
        updateAgentStats();
        saveDraft();
        selectNode(node.id, false);
        return node;
    }

    function deleteNode(id) {
        state.nodes = state.nodes.filter(n => n.id !== id);
        state.edges = state.edges.filter(e => e.from !== id && e.to !== id);
        delete nodeHeights[id];
        selectedNodeIds.delete(id);
        renderNodes(); renderEdges();
        updateToolbarStats(); updateAgentStats(); updateMinimap();
        renderInspectorPanel(); saveDraft();
    }

    function deleteSelectedNodes() {
        selectedNodeIds.forEach(id => {
            state.nodes = state.nodes.filter(n => n.id !== id);
            state.edges = state.edges.filter(e => e.from !== id && e.to !== id);
            delete nodeHeights[id];
        });
        selectedEdgeIds.forEach(id => { state.edges = state.edges.filter(e => e.id !== id); });
        selectedNodeIds.clear(); selectedEdgeIds.clear();
        renderNodes(); renderEdges();
        updateToolbarStats(); updateAgentStats(); updateMinimap();
        renderInspectorPanel(); saveDraft();
    }

    /* ── Connections ─────────────────────────────────────────── */
    function startConnect(fromNodeId, e) {
        connectingFrom = fromNodeId;
        canvasWrap.classList.add('connecting');
        document.addEventListener('mouseup',    onConnectMouseUp);
        document.addEventListener('mousemove',  onConnectMouseMove);
        e.preventDefault();
    }
    function onConnectMouseMove(e) {
        if (!connectingFrom) return;
        const rect   = getCanvasRect();
        const mouseX = e.clientX - rect.left;
        const mouseY = e.clientY - rect.top;
        const fromNode = state.nodes.find(n => n.id === connectingFrom);
        if (!fromNode) return;
        const p1 = portPos(fromNode, 'out');
        const { zoom, panX, panY } = state.layout;
        const sx1 = p1.x * zoom + panX;
        const sy1 = p1.y * zoom + panY;
        if (!tempEdgePath) {
            tempEdgePath = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            tempEdgePath.classList.add('edge-temp');
            canvasSvg.appendChild(tempEdgePath);
        }
        tempEdgePath.setAttribute('d', bezierPath(sx1, sy1, mouseX, mouseY));
    }
    function onConnectMouseUp() {
        document.removeEventListener('mouseup',   onConnectMouseUp);
        document.removeEventListener('mousemove', onConnectMouseMove);
        connectingFrom = null;
        canvasWrap.classList.remove('connecting');
        if (tempEdgePath) { tempEdgePath.remove(); tempEdgePath = null; }
    }
    function endConnect(toNodeId) {
        if (!connectingFrom || connectingFrom === toNodeId) { onConnectMouseUp(); return; }
        const dup = state.edges.find(e => e.from === connectingFrom && e.to === toNodeId);
        if (!dup) state.edges.push({ id: uid(), from: connectingFrom, to: toNodeId });
        onConnectMouseUp();
        renderEdges(); renderAddStepButtons();
        updateToolbarStats(); updateMinimap(); saveDraft();
    }

    /* ── Node dragging ───────────────────────────────────────── */
    function onNodeDragStart(e) {
        if (e.button !== 0) return;
        const nodeEl = e.currentTarget.closest('.designer-node');
        if (!nodeEl) return;
        e.stopPropagation(); e.preventDefault();

        const nodeId = nodeEl.dataset.nodeId;
        isDraggingNode = true; dragNodeId = nodeId;
        if (!selectedNodeIds.has(nodeId)) {
            if (!e.shiftKey) selectedNodeIds.clear();
            selectedNodeIds.add(nodeId);
            renderNodes(); renderInspectorPanel();
        }

        const rect  = getCanvasRect();
        const start = screenToCanvas(e.clientX - rect.left, e.clientY - rect.top);
        dragOffsets = {};
        selectedNodeIds.forEach(id => {
            const n = state.nodes.find(nn => nn.id === id);
            if (n) dragOffsets[id] = { dx: n.x - start.x, dy: n.y - start.y };
        });
        nodeEl.classList.add('dragging');
        document.addEventListener('mousemove', onNodeDragMove);
        document.addEventListener('mouseup',   onNodeDragEnd);
    }
    function onNodeDragMove(e) {
        if (!isDraggingNode) return;
        const rect = getCanvasRect();
        const cp   = screenToCanvas(e.clientX - rect.left, e.clientY - rect.top);
        selectedNodeIds.forEach(id => {
            const n = state.nodes.find(nn => nn.id === id);
            if (!n) return;
            const off = dragOffsets[id] || { dx: 0, dy: 0 };
            n.x = Math.round((cp.x + off.dx) / 20) * 20;
            n.y = Math.round((cp.y + off.dy) / 20) * 20;
            const el = canvasNodes.querySelector(`[data-node-id="${id}"]`);
            if (el) positionNode(el, n);
        });
        renderEdges(); renderAddStepButtons(); updateMinimap();
    }
    function onNodeDragEnd() {
        document.removeEventListener('mousemove', onNodeDragMove);
        document.removeEventListener('mouseup',   onNodeDragEnd);
        if (isDraggingNode) {
            const el = canvasNodes.querySelector(`[data-node-id="${dragNodeId}"]`);
            if (el) el.classList.remove('dragging');
        }
        isDraggingNode = false; dragNodeId = null; saveDraft();
    }

    /* ── Canvas pan ──────────────────────────────────────────── */
    function onCanvasMouseDown(e) {
        if (e.button === 1 || (e.button === 0 && (e.target === canvasWrap || e.target === canvasSvg))) {
            if (e.button === 0) clearSelection();
            isPanning = true;
            panStart  = { x: e.clientX, y: e.clientY, panX: state.layout.panX, panY: state.layout.panY };
            canvasWrap.classList.add('panning');
            e.preventDefault();
        }
    }
    function onCanvasMouseMove(e) {
        if (!isPanning) return;
        state.layout.panX = panStart.panX + (e.clientX - panStart.x);
        state.layout.panY = panStart.panY + (e.clientY - panStart.y);
        setTransform(); renderEdges(); updateMinimap();
    }
    function onCanvasMouseUp() {
        if (isPanning) { isPanning = false; canvasWrap.classList.remove('panning'); saveDraft(); }
    }

    /* ── Zoom ────────────────────────────────────────────────── */
    function onCanvasWheel(e) {
        if (!e.ctrlKey) return;
        e.preventDefault();
        const delta  = e.deltaY > 0 ? -0.1 : 0.1;
        const newZ   = Math.min(3, Math.max(0.2, state.layout.zoom + delta));
        const rect   = getCanvasRect();
        const mouseX = e.clientX - rect.left;
        const mouseY = e.clientY - rect.top;
        const wx = (mouseX - state.layout.panX) / state.layout.zoom;
        const wy = (mouseY - state.layout.panY) / state.layout.zoom;
        state.layout.zoom = newZ;
        state.layout.panX = mouseX - wx * newZ;
        state.layout.panY = mouseY - wy * newZ;
        setTransform(); renderEdges(); updateMinimap();
    }
    function zoomBy(delta) {
        const newZ  = Math.min(3, Math.max(0.2, state.layout.zoom + delta));
        const rect  = getCanvasRect();
        const cx = rect.width / 2; const cy = rect.height / 2;
        const wx = (cx - state.layout.panX) / state.layout.zoom;
        const wy = (cy - state.layout.panY) / state.layout.zoom;
        state.layout.zoom = newZ;
        state.layout.panX = cx - wx * newZ;
        state.layout.panY = cy - wy * newZ;
        setTransform(); renderEdges(); updateMinimap(); saveDraft();
    }
    function fitView() {
        if (state.nodes.length === 0) {
            state.layout = { zoom: 0.9, panX: 60, panY: 60 };
            setTransform(); renderEdges(); updateMinimap(); return;
        }
        const rect = getCanvasRect();
        const xs = state.nodes.map(n => n.x);
        const ys = state.nodes.map(n => n.y);
        const minX = Math.min(...xs) - 60;
        const minY = Math.min(...ys) - 60;
        const maxX = Math.max(...xs) + NODE_W + 100;
        const maxY = Math.max(...state.nodes.map(n => n.y + getNodeH(n.id))) + 60;
        const bw = maxX - minX; const bh = maxY - minY;
        const zoom = Math.min(1.2, Math.max(0.2, Math.min((rect.width - 80) / bw, (rect.height - 80) / bh)));
        state.layout.zoom = zoom;
        state.layout.panX = (rect.width  - bw * zoom) / 2 - minX * zoom;
        state.layout.panY = (rect.height - bh * zoom) / 2 - minY * zoom;
        setTransform(); renderEdges(); updateMinimap(); saveDraft();
    }

    /* ── Minimap ─────────────────────────────────────────────── */
    function updateMinimap() {
        if (!minimapSvg) return;
        minimapSvg.innerHTML = '';
        if (state.nodes.length === 0) return;
        const mmW = 180, mmH = 110, pad = 8;
        const xs = state.nodes.map(n => n.x);
        const ys = state.nodes.map(n => n.y);
        const minX = Math.min(...xs); const minY = Math.min(...ys);
        const maxX = Math.max(...xs) + NODE_W;
        const maxY = Math.max(...state.nodes.map(n => n.y + getNodeH(n.id)));
        const bw = maxX - minX || 1; const bh = maxY - minY || 1;
        const scale = Math.min((mmW - pad*2) / bw, (mmH - pad*2) / bh);
        const toX = x => (x - minX) * scale + pad;
        const toY = y => (y - minY) * scale + pad;

        state.nodes.forEach(n => {
            const entry = getCatalogueEntry(n.type);
            const cat   = entry ? entry.category : getNodeCategory(n.type);
            const rect  = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            rect.setAttribute('x',      toX(n.x));
            rect.setAttribute('y',      toY(n.y));
            rect.setAttribute('width',  Math.max(6, NODE_W * scale));
            rect.setAttribute('height', Math.max(4, getNodeH(n.id) * scale));
            rect.setAttribute('rx', 2);
            rect.setAttribute('fill', getCategoryColor(cat));
            rect.setAttribute('opacity', '0.65');
            minimapSvg.appendChild(rect);
        });

        state.edges.forEach(e => {
            const fn = state.nodes.find(n => n.id === e.from);
            const tn = state.nodes.find(n => n.id === e.to);
            if (!fn || !tn) return;
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.setAttribute('x1', toX(fn.x + NODE_W/2));
            line.setAttribute('y1', toY(fn.y + getNodeH(fn.id)));
            line.setAttribute('x2', toX(tn.x + NODE_W/2));
            line.setAttribute('y2', toY(tn.y));
            line.setAttribute('stroke', '#7c3aed'); line.setAttribute('stroke-width', '1'); line.setAttribute('opacity', '0.4');
            minimapSvg.appendChild(line);
        });

        // Viewport
        const cRect  = getCanvasRect();
        const vp     = screenToCanvas(0, 0);
        const vpEnd  = screenToCanvas(cRect.width, cRect.height);
        const vpRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        vpRect.setAttribute('x', toX(vp.x)); vpRect.setAttribute('y', toY(vp.y));
        vpRect.setAttribute('width',  Math.max(0, (vpEnd.x - vp.x) * scale));
        vpRect.setAttribute('height', Math.max(0, (vpEnd.y - vp.y) * scale));
        vpRect.classList.add('designer-minimap-viewport');
        minimapSvg.appendChild(vpRect);
    }

    /* ── Toolbar stats ───────────────────────────────────────── */
    function updateToolbarStats() {
        if (toolbarStats) {
            const configured = state.nodes.filter(n => isNodeConfigured(n)).length;
            toolbarStats.textContent = `${state.nodes.length} capacité${state.nodes.length !== 1 ? 's' : ''} · ${state.edges.length} connexion${state.edges.length !== 1 ? 's' : ''} · ${configured} configurée${configured !== 1 ? 's' : ''}`;
        }
    }

    /* ── Agent identity card stats ───────────────────────────── */
    function updateAgentStats() {
        const total = state.nodes.length;
        const configured = state.nodes.filter(n => isNodeConfigured(n)).length;
        const edges  = state.edges.length;

        const elTotal  = document.getElementById('agentStatTotal');
        const elConf   = document.getElementById('agentStatConfigured');
        const elEdges  = document.getElementById('agentStatEdges');
        if (elTotal)  elTotal.textContent  = total;
        if (elConf)   elConf.textContent   = configured;
        if (elEdges)  elEdges.textContent  = edges;
    }

    /* ── Inspector Panel ─────────────────────────────────────── */
    function renderInspectorPanel() {
        propEmpty = document.getElementById('designerInspectorEmpty');
        propBody  = document.getElementById('designerInspectorContent');
        if (!propEmpty || !propBody) return;

        const ids = [...selectedNodeIds];
        if (ids.length !== 1) {
            propEmpty.style.display = 'flex';
            propBody.style.display  = 'none';
            updateEmptyStateStats();
            return;
        }
        const node = state.nodes.find(n => n.id === ids[0]);
        if (!node) { propEmpty.style.display = 'flex'; propBody.style.display = 'none'; return; }

        propEmpty.style.display = 'none';
        propBody.style.display  = 'block';
        propBody.innerHTML = buildInspectorHTML(node);

        // Bind tab switching
        propBody.querySelectorAll('.designer-inspector-tab').forEach(tab => {
            tab.addEventListener('click', () => {
                propBody.querySelectorAll('.designer-inspector-tab').forEach(t => t.classList.remove('active'));
                propBody.querySelectorAll('.designer-inspector-tab-panel').forEach(p => p.classList.remove('active'));
                tab.classList.add('active');
                const panel = propBody.querySelector(`[data-tab-panel="${tab.dataset.tab}"]`);
                if (panel) panel.classList.add('active');
                activeInspectorTab = tab.dataset.tab;
            });
        });

        // Bind delete button in inspector
        const delBtn = propBody.querySelector('[data-action="delete-node"]');
        if (delBtn) delBtn.addEventListener('click', e => { e.stopPropagation(); deleteNode(node.id); });

        // Bind config field inputs
        const entry    = getCatalogueEntry(node.type);
        const category = entry ? entry.category : getNodeCategory(node.type);

        // Name input
        const nameIn = propBody.querySelector('#inspectorNodeName');
        if (nameIn) {
            nameIn.value = node.label;
            nameIn.addEventListener('input', e => {
                node.label = e.target.value;
                const titleEl = canvasNodes.querySelector(`[data-node-id="${node.id}"] .node-title`);
                if (titleEl) titleEl.textContent = e.target.value;
                saveDraft();
            });
        }

        // Config fields
        const fields = NODE_CONFIG_FIELDS[node.type] || [];
        fields.forEach(f => {
            const el = propBody.querySelector(`[data-config-key="${f.key}"]`);
            if (!el) return;
            el.value = node.config[f.key] || '';
            el.addEventListener('input', e => {
                node.config[f.key] = e.target.value;
                refreshNodeCard(node, canvasNodes.querySelector(`[data-node-id="${node.id}"]`));
                updateToolbarStats(); updateAgentStats(); saveDraft();
            });
            el.addEventListener('change', e => {
                node.config[f.key] = e.target.value;
                refreshNodeCard(node, canvasNodes.querySelector(`[data-node-id="${node.id}"]`));
                updateToolbarStats(); updateAgentStats(); saveDraft();
            });
        });
    }

    function buildInspectorHTML(node) {
        const entry    = getCatalogueEntry(node.type);
        const category = entry ? entry.category : getNodeCategory(node.type);
        const icon     = entry ? entry.icon : '⚙️';
        const catColor = getCategoryColor(category);
        const badge    = CAT_LABEL[category] || category.toUpperCase();
        const configured = isNodeConfigured(node);
        const fields   = NODE_CONFIG_FIELDS[node.type] || [];
        const statusIcon = configured ? '✓' : '⚠';
        const statusTxt  = configured ? 'Connecté' : 'À configurer';
        const statusClr  = configured ? '#10b981' : '#f59e0b';

        const tabActive = (id) => activeInspectorTab === id ? ' active' : '';

        // Build config fields HTML
        let configFieldsHtml = `
            <div class="designer-inspector-group">
                <label class="designer-inspector-label">Nom de la capacité</label>
                <input class="designer-inspector-input" id="inspectorNodeName" type="text" value="${escHtml(node.label)}" />
            </div>`;

        if (fields.length === 0) {
            configFieldsHtml += `<div class="designer-inspector-hint">Aucune configuration requise pour ce type de capacité.</div>`;
        } else {
            fields.forEach(f => {
                configFieldsHtml += `<div class="designer-inspector-group"><label class="designer-inspector-label">${escHtml(f.label)}</label>`;
                if (f.type === 'select') {
                    configFieldsHtml += `<select class="designer-inspector-select" data-config-key="${escHtml(f.key)}">` +
                        (f.options || []).map(o => `<option value="${escHtml(o)}">${escHtml(o)}</option>`).join('') +
                        `</select>`;
                } else if (f.type === 'textarea') {
                    configFieldsHtml += `<textarea class="designer-inspector-textarea" data-config-key="${escHtml(f.key)}" placeholder="${escHtml(f.placeholder || '')}" rows="3"></textarea>`;
                } else {
                    const ro = f.readonly ? ' readonly' : '';
                    configFieldsHtml += `<input class="designer-inspector-input" data-config-key="${escHtml(f.key)}" type="text" placeholder="${escHtml(f.placeholder || '')}"${ro} />`;
                }
                configFieldsHtml += `</div>`;
            });
        }

        // IO tab
        const ioHtml = `
            <div class="designer-inspector-section">Entrées</div>
            <div style="display:flex;flex-wrap:wrap;gap:5px;margin-bottom:10px;">
                ${buildIOChips(node, 'in')}
            </div>
            <div class="designer-inspector-section">Sorties</div>
            <div style="display:flex;flex-wrap:wrap;gap:5px;">
                ${buildIOChips(node, 'out')}
            </div>`;

        // Metrics tab
        const cost = category === 'ia' ? '$0.0018' : '—';
        const time  = category === 'ia' ? '2.3 sec' : '< 0.5 sec';
        const conf  = node.meta && node.meta.confidence ? node.meta.confidence : (configured ? 97 : 0);
        const metricsHtml = `
            <div class="designer-inspector-metric"><span class="designer-inspector-metric-label">Coût / exécution</span><span class="designer-inspector-metric-value">${cost}</span></div>
            <div class="designer-inspector-metric"><span class="designer-inspector-metric-label">Temps moyen</span><span class="designer-inspector-metric-value">${time}</span></div>
            ${category === 'ia' && conf > 0 ? `
            <div class="designer-inspector-progress-wrap">
                <span style="font-size:0.7rem;color:#94a3b8;min-width:70px">Confiance</span>
                <div class="designer-inspector-progress-bar"><div class="designer-inspector-progress-fill" style="width:${conf}%"></div></div>
                <span class="designer-inspector-progress-val">${conf}%</span>
            </div>` : ''}
            <div class="designer-inspector-metric"><span class="designer-inspector-metric-label">Exécutions (30j)</span><span class="designer-inspector-metric-value">—</span></div>
            <div class="designer-inspector-metric"><span class="designer-inspector-metric-label">Erreurs</span><span class="designer-inspector-metric-value">0</span></div>`;

        // Logs tab
        const logsHtml = `<div class="designer-inspector-hint" style="text-align:center;padding-top:20px;">Aucun log disponible.<br>Déployez l'agent pour voir les logs en temps réel.</div>`;

        return `
            <div class="designer-inspector-node-preview">
                <div class="designer-inspector-node-icon" style="background:${catColor}22;color:${catColor}">${escHtml(icon)}</div>
                <div style="flex:1;min-width:0">
                    <div class="designer-inspector-node-name">${escHtml(node.label)}</div>
                    <div class="designer-inspector-node-type">
                        <span style="background:${catColor};color:#fff;padding:1px 7px;border-radius:4px;font-size:9.5px;font-weight:800;text-transform:uppercase;letter-spacing:.06em">${escHtml(badge)}</span>
                        <span class="designer-inspector-node-status" style="color:${statusClr}">${statusIcon} ${statusTxt}</span>
                    </div>
                </div>
            </div>
            <div class="designer-inspector-tabs">
                <div class="designer-inspector-tab${tabActive('params')}" data-tab="params">Paramètres</div>
                <div class="designer-inspector-tab${tabActive('io')}" data-tab="io">E/S</div>
                <div class="designer-inspector-tab${tabActive('metrics')}" data-tab="metrics">Avancé</div>
            </div>
            <div class="designer-inspector-tab-panel${tabActive('params')}" data-tab-panel="params">
                ${configFieldsHtml}
                <div class="designer-inspector-divider"></div>
                <div class="designer-inspector-toggle-row">
                    <span class="designer-inspector-toggle-label">Actif</span>
                    <label class="designer-inspector-toggle">
                        <input type="checkbox" checked />
                        <span class="designer-inspector-toggle-slider"></span>
                    </label>
                </div>
                <div class="designer-inspector-divider"></div>
                <div class="designer-inspector-actions">
                    <button class="designer-inspector-action-btn run">▶ Tester</button>
                    <button class="designer-inspector-action-btn">📋 Historique</button>
                </div>
                <button class="designer-inspector-delete-btn" data-action="delete-node" data-node="${escHtml(node.id)}">🗑 Supprimer cette capacité</button>
            </div>
            <div class="designer-inspector-tab-panel${tabActive('io')}" data-tab-panel="io">
                <div class="designer-inspector-section">Entrées</div>
                <div style="display:flex;flex-wrap:wrap;gap:5px;margin-bottom:14px;">${buildIOChips(node, 'in')}</div>
                <div class="designer-inspector-section">Sorties</div>
                <div style="display:flex;flex-wrap:wrap;gap:5px;">${buildIOChips(node, 'out')}</div>
            </div>
            <div class="designer-inspector-tab-panel${tabActive('metrics')}" data-tab-panel="metrics">${metricsHtml}</div>`;
    }

    function buildIOChips(node, direction) {
        const entry = getCatalogueEntry(node.type);
        const cat   = entry ? entry.category : getNodeCategory(node.type);
        const ioMap = {
            trigger:   { in: [],                    out: ['Événement', entry ? entry.output : 'Données'] },
            ia:        { in: ['Document', 'Texte'],  out: ['JSON', entry ? entry.output : 'Résultat'] },
            action:    { in: ['Données'],            out: ['Confirmation'] },
            api:       { in: ['Payload JSON'],       out: ['Réponse HTTP'] },
            database:  { in: ['Requête'],            out: ['Résultat SQL'] },
            decision:  { in: ['Données'],            out: ['Branche A', 'Branche B'] },
            human:     { in: ['Demande'],            out: ['Décision'] },
            connector: { in: ['Credentials'],        out: [entry ? entry.output : 'Données'] },
            monitor:   { in: ['Événements'],         out: ['Logs'] },
            utility:   { in: ['Tout'],               out: ['Valeurs'] },
        };
        const io = ioMap[cat] || { in: ['—'], out: ['—'] };
        const items = direction === 'in' ? io.in : io.out;
        const color = getCategoryColor(cat);
        return items.filter(Boolean).map(item =>
            `<span style="padding:3px 9px;border-radius:6px;background:${color}22;color:${color};font-size:0.7rem;font-weight:600;border:1px solid ${color}44">${escHtml(item)}</span>`
        ).join('');
    }

    function updateEmptyStateStats() {
        const el = document.getElementById('designerInspectorEmpty');
        if (!el) return;
        const stats = el.querySelectorAll('.designer-inspector-empty-stat-value');
        if (stats.length >= 3) {
            stats[0].textContent = state.nodes.length;
            stats[1].textContent = state.edges.length;
            stats[2].textContent = state.nodes.filter(n => isNodeConfigured(n)).length;
        }
    }

    /* ── AI Suggestions Drawer ───────────────────────────────── */
    const AI_SUGGESTIONS = [
        { id: 'cache',      type: 'util-cache',       title: 'Ajouter un cache',         desc: 'Réduire les appels API répétés et économiser des coûts.', saving: 35 },
        { id: 'validate',   type: 'decision-validate', title: 'Ajouter une validation',   desc: 'Vérifier les données avant l\'action finale.', saving: 0 },
        { id: 'gptmini',    type: null,                title: 'Réduire le coût GPT',      desc: 'Utiliser GPT-4o mini à la place de GPT-4o (−60% coût).', saving: 60 },
        { id: 'logs',       type: 'util-logs',        title: 'Ajouter un journal',        desc: 'Traçabilité complète des exécutions.', saving: 0 },
        { id: 'error',      type: 'decision-if',      title: 'Gestion d\'erreurs',         desc: 'Ajouter un branchement en cas d\'erreur.', saving: 0 },
    ];

    function initAiDrawer() {
        const fab   = document.getElementById('designerAiFab');
        const drawer= document.getElementById('designerAiDrawer');
        const close = document.getElementById('designerAiDrawerClose');
        const apply = document.getElementById('designerAiApplyBtn');
        if (!fab || !drawer) return;

        // Render suggestions
        const body = drawer.querySelector('.designer-ai-drawer-body');
        if (body) {
            const existingItems = body.querySelectorAll('.designer-ai-suggestion');
            existingItems.forEach(el => el.remove());
            const intro = body.querySelector('.designer-ai-drawer-intro');

            AI_SUGGESTIONS.forEach(sug => {
                const el = document.createElement('div');
                el.className = 'designer-ai-suggestion';
                el.dataset.suggId = sug.id;
                el.innerHTML = `
                    <div class="designer-ai-suggestion-check"></div>
                    <div class="designer-ai-suggestion-content">
                        <div class="designer-ai-suggestion-title">${escHtml(sug.title)}</div>
                        <div class="designer-ai-suggestion-desc">${escHtml(sug.desc)}</div>
                    </div>`;
                el.addEventListener('click', () => {
                    el.classList.toggle('selected');
                    if (el.classList.contains('selected')) {
                        selectedAiSuggestions.add(sug.id);
                        el.querySelector('.designer-ai-suggestion-check').textContent = '✓';
                    } else {
                        selectedAiSuggestions.delete(sug.id);
                        el.querySelector('.designer-ai-suggestion-check').textContent = '';
                    }
                    updateAiSavings();
                });
                if (intro && intro.nextSibling) body.insertBefore(el, intro.nextSibling);
                else body.appendChild(el);
            });
        }

        fab.addEventListener('click', () => {
            drawer.classList.add('visible');
            aiDrawerOpen = true;
        });
        if (close) close.addEventListener('click', () => { drawer.classList.remove('visible'); aiDrawerOpen = false; });
        if (apply) apply.addEventListener('click', () => {
            selectedAiSuggestions.forEach(id => {
                const sug = AI_SUGGESTIONS.find(s => s.id === id);
                if (sug && sug.type) {
                    const lastNode = state.nodes[state.nodes.length - 1];
                    const y = lastNode ? lastNode.y + getNodeH(lastNode.id) + 80 : 200;
                    const x = lastNode ? lastNode.x : 200;
                    addNode(sug.type, x, y);
                } else if (sug && sug.id === 'gptmini') {
                    // Switch all IA models to gpt-4o-mini
                    state.nodes.filter(n => n.type.startsWith('skill')).forEach(n => {
                        n.config = n.config || {};
                        n.config.model = 'gpt-4o-mini';
                    });
                    renderNodes(); saveDraft();
                    showToast('Modèles IA mis à jour : GPT-4o mini');
                }
            });
            selectedAiSuggestions.clear();
            drawer.classList.remove('visible'); aiDrawerOpen = false;
            showToast('Capacités ajoutées à votre agent');
        });
    }

    function updateAiSavings() {
        const footer = document.getElementById('designerAiSavings');
        if (!footer) return;
        const totalSaving = AI_SUGGESTIONS
            .filter(s => selectedAiSuggestions.has(s.id))
            .reduce((acc, s) => acc + (s.saving || 0), 0);
        footer.textContent = totalSaving > 0 ? `Économies estimées : ${Math.min(totalSaving, 99)}%` : '';
    }

    /* ── Toast notification ──────────────────────────────────── */
    function showToast(msg) {
        const toast = document.getElementById('draftToast');
        if (!toast) return;
        toast.textContent = msg;
        toast.classList.add('visible');
        setTimeout(() => toast.classList.remove('visible'), 2500);
    }

    /* ── Validation ──────────────────────────────────────────── */
    function validate() {
        const errors = [], warnings = [];
        if (state.nodes.length === 0)        errors.push('Votre agent doit avoir au moins une capacité.');
        if (!state.meta.name && !state.meta.mission) errors.push("Renseignez au moins le nom ou la description de l'agent.");
        if (state.nodes.length > 1 && state.edges.length === 0) warnings.push('Aucune connexion — les capacités sont déconnectées.');
        const hasTrigger = state.nodes.some(n => n.type.startsWith('trigger'));
        if (!hasTrigger && state.nodes.length > 0) warnings.push("Aucun déclencheur — votre agent ne sait pas quand s'exécuter.");
        showValidationBanner(errors, warnings);
        return errors.length === 0;
    }

    function showValidationBanner(errors, warnings) {
        if (!validationBanner) return;
        if (errors.length === 0 && warnings.length === 0) {
            validationBanner.classList.remove('visible', 'error');
            return;
        }
        validationBanner.classList.add('visible');
        const list = errors.length > 0 ? errors : warnings;
        validationBanner.classList.toggle('error', errors.length > 0);
        const icon = errors.length > 0 ? '⛔' : '⚠️';
        validationBanner.innerHTML = `<span>${icon}</span><div><ul>${list.map(m => `<li>${escHtml(m)}</li>`).join('')}</ul></div>`;
    }

    /* ── Serialization / Payload ─────────────────────────────── */
    function getDesignerPayload() {
        const sensors   = state.nodes.filter(n => n.type.startsWith('connector')).map(n => n.type.replace('connector-',''));
        const skills    = state.nodes.filter(n => n.type.startsWith('skill')).map(n => n.type.replace('skill-',''));
        const actions   = state.nodes.filter(n => n.type.startsWith('action')).map(n => n.type.replace('action-',''));
        const decisions = state.nodes.filter(n => n.type.startsWith('decision'));
        const triggers  = state.nodes.filter(n => n.type.startsWith('trigger'));
        const decNode   = decisions[0];
        const trigNode  = triggers[0];

        return {
            schemaVersion:    3,
            creationMode:     'designer',
            agentName:        state.meta.name,
            agentAvatar:      state.meta.avatar,
            mission:          state.meta.mission,
            businessDomain:   state.meta.businessDomain,
            sensors,
            sensorDetails:    state.nodes.filter(n => n.type.startsWith('connector')).map(n => ({ label: n.label, config: n.config })),
            skills,
            skillDetails:     state.nodes.filter(n => n.type.startsWith('skill')).map(n => ({ label: n.label, config: n.config })),
            tools:            [],
            actuators:        actions,
            actuatorDetails:  state.nodes.filter(n => n.type.startsWith('action')).map(n => ({ label: n.label, config: n.config })),
            decision:         decNode ? { engine: decNode.type, label: decNode.label, config: decNode.config } : undefined,
            memory:           { types: [] },
            trigger:          trigNode ? trigNode.type : undefined,
            designerWorkflow: state,
        };
    }

    function buildDesignerMessage() {
        const caps      = state.nodes.length;
        const edges     = state.edges.length;
        const iaNodes   = state.nodes.filter(n => n.type.startsWith('skill')).map(n => n.label).join(', ') || '—';
        const actNodes  = state.nodes.filter(n => n.type.startsWith('action')).map(n => n.label).join(', ') || '—';
        const connNodes = state.nodes.filter(n => n.type.startsWith('connector')).map(n => n.label).join(', ') || '—';
        const trigNode  = state.nodes.filter(n => n.type.startsWith('trigger')).map(n => n.label).join(', ') || '—';
        const types     = [...new Set(state.nodes.map(n => getNodeCategory(n.type)))].join(', ');

        return [
            'Créer un agent IA via le Compositeur Visuel :',
            `- Nom : ${state.meta.name || '(non défini)'}`,
            `- Description : ${state.meta.mission || '(non définie)'}`,
            `- Domaine : ${state.meta.businessDomain || '—'}`,
            `- Capacités : ${caps} (${types || '—'})`,
            `- Connexions : ${edges}`,
            `- Déclencheur : ${trigNode}`,
            `- Connecteurs : ${connNodes}`,
            `- IA / Cognition : ${iaNodes}`,
            `- Actions : ${actNodes}`,
        ].join('\n');
    }

    /* ── Workflow order (topological sort) ───────────────────── */
    function getWorkflowOrder() {
        if (state.nodes.length === 0) return [];
        const inDeg = {}; const adj = {};
        state.nodes.forEach(n => { inDeg[n.id] = 0; adj[n.id] = []; });
        state.edges.forEach(e => { if (adj[e.from]) adj[e.from].push(e.to); if (e.to in inDeg) inDeg[e.to]++; });
        const queue  = state.nodes.filter(n => inDeg[n.id] === 0);
        const result = [];
        while (queue.length) {
            const n = queue.shift();
            result.push(n);
            (adj[n.id] || []).forEach(id => { inDeg[id]--; if (inDeg[id] === 0) queue.push(state.nodes.find(nn => nn.id === id)); });
        }
        return result.length === state.nodes.length ? result : state.nodes;
    }

    /* ── Blueprint Review Overlay ────────────────────────────── */
    function showBlueprintReview() {
        if (!validate()) return;
        const overlay = document.getElementById('designerBlueprintOverlay');
        if (!overlay) return;
        renderBlueprintContent(overlay.querySelector('.designer-blueprint-modal-body'));
        overlay.classList.add('visible');
    }

    function renderBlueprintContent(container) {
        if (!container) return;
        container.innerHTML = '';

        // Meta grid
        const metaGrid = document.createElement('div');
        metaGrid.className = 'designer-bp-meta-grid';
        metaGrid.innerHTML = `
            <div class="designer-bp-meta-card">
                <div class="designer-bp-meta-label">Nom de l'agent</div>
                <div class="designer-bp-meta-value">${escHtml(state.meta.avatar + ' ' + (state.meta.name || '(non défini)'))}</div>
            </div>
            <div class="designer-bp-meta-card">
                <div class="designer-bp-meta-label">Domaine métier</div>
                <div class="designer-bp-meta-value">${escHtml(state.meta.businessDomain || '—')}</div>
            </div>
            <div class="designer-bp-meta-card" style="grid-column:1/-1">
                <div class="designer-bp-meta-label">Description / Mission</div>
                <div class="designer-bp-meta-value" style="font-weight:400;font-size:0.84rem;color:#94a3b8">${escHtml(state.meta.mission || '(non définie)')}</div>
            </div>
            <div class="designer-bp-meta-card">
                <div class="designer-bp-meta-label">Capacités</div>
                <div class="designer-bp-meta-value">${state.nodes.length}</div>
            </div>
            <div class="designer-bp-meta-card">
                <div class="designer-bp-meta-label">Configurées</div>
                <div class="designer-bp-meta-value">${state.nodes.filter(n => isNodeConfigured(n)).length} / ${state.nodes.length}</div>
            </div>`;
        container.appendChild(metaGrid);

        // Vertical flow diagram (CSS-only)
        if (state.nodes.length > 0) {
            const flowSection = document.createElement('div');
            flowSection.innerHTML = `<div class="designer-bp-section-header">Composition de l'agent (flux vertical)</div>`;
            const flow = document.createElement('div');
            flow.className = 'designer-bp-flow-vertical';
            const ordered = getWorkflowOrder();
            ordered.forEach((node, i) => {
                const entry   = getCatalogueEntry(node.type);
                const cat     = entry ? entry.category : getNodeCategory(node.type);
                const catColor = getCategoryColor(cat);
                const icon    = entry ? entry.icon : '⚙️';
                const nodeEl  = document.createElement('div');
                nodeEl.className = 'designer-bp-flow-node';
                nodeEl.style.borderLeft = `3px solid ${catColor}`;
                nodeEl.innerHTML = `<span style="font-size:1rem">${escHtml(icon)}</span> <span style="color:${catColor};font-size:0.65rem;font-weight:700;padding:1px 6px;background:${catColor}22;border-radius:4px">${CAT_LABEL[cat]||cat}</span> ${escHtml(node.label)} ${isNodeConfigured(node) ? '<span style="color:#22c55e;font-size:0.65rem">✓</span>' : '<span style="color:#f59e0b;font-size:0.65rem">⚠</span>'}`;
                flow.appendChild(nodeEl);
                if (i < ordered.length - 1) {
                    const arrow = document.createElement('div');
                    arrow.className = 'designer-bp-flow-arrow';
                    arrow.textContent = '↓';
                    flow.appendChild(arrow);
                }
            });
            flowSection.appendChild(flow);
            container.appendChild(flowSection);
        }

        // Nodes table
        const tableSection = document.createElement('div');
        tableSection.innerHTML = `<div class="designer-bp-section-header">Détail des capacités</div>`;
        const table = document.createElement('table');
        table.className = 'designer-bp-nodes-table';
        table.innerHTML = `<thead><tr><th>Type</th><th>Capacité</th><th>Configurée</th></tr></thead>`;
        const tbody = document.createElement('tbody');
        state.nodes.forEach(node => {
            const entry = getCatalogueEntry(node.type);
            const cat   = entry ? entry.category : getNodeCategory(node.type);
            const catColor = getCategoryColor(cat);
            const conf  = isNodeConfigured(node);
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td><span style="padding:1px 7px;border-radius:4px;background:${catColor}22;color:${catColor};font-size:0.65rem;font-weight:700">${escHtml(CAT_LABEL[cat]||cat)}</span></td>
                <td style="color:#e2e8f0">${escHtml(node.label)}</td>
                <td><span class="designer-bp-configured-badge ${conf ? 'yes' : 'no'}">${conf ? '✓ Oui' : '○ Non'}</span></td>`;
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);
        tableSection.appendChild(table);
        container.appendChild(tableSection);
    }

    /* ── Form submit hook ────────────────────────────────────── */
    function bindFormSubmit() {
        const form = document.getElementById('studioForm');
        if (!form) return;
        form.addEventListener('submit', e => {
            const modeInput = document.getElementById('creationModeInput');
            if (!modeInput || modeInput.value !== 'designer') return;
            e.preventDefault();
            if (!validate()) return;
            writeHiddenFields();
            form.submit();
        });
    }
    function writeHiddenFields() {
        const payload = getDesignerPayload();
        const message = buildDesignerMessage();
        const hWiz  = document.getElementById('hiddenWizardJson');
        const hMsg  = document.getElementById('hiddenMessage');
        const hDW   = document.getElementById('hiddenDesignerWorkflow');
        if (hWiz)  hWiz.value  = JSON.stringify(payload);
        if (hMsg)  hMsg.value  = message;
        if (hDW)   hDW.value   = JSON.stringify(state);
    }

    /* ── Mode selector ───────────────────────────────────────── */
    function bindModeSelector() {
        const cards     = document.querySelectorAll('.creation-mode-card');
        const modeInput = document.getElementById('creationModeInput');
        function applyMode(mode) {
            cards.forEach(c => c.classList.toggle('active', c.dataset.mode === mode));
            const wizardEl   = document.getElementById('wizardContainer');
            const designerEl = document.getElementById('designerContainer');
            if (wizardEl)   wizardEl.style.display = mode === 'guided' ? '' : 'none';
            if (designerEl) designerEl.classList.toggle('visible', mode === 'designer');
            // Body class for hiding page title/header when designer is active
            document.body.classList.toggle('designer-active', mode === 'designer');
            if (modeInput)  modeInput.value = mode;
            try { localStorage.setItem(MODE_KEY, mode); } catch (_) {}
        }
        cards.forEach(card => card.addEventListener('click', () => applyMode(card.dataset.mode)));
        const saved = (() => { try { return localStorage.getItem(MODE_KEY); } catch(_) { return null; } })();
        applyMode(saved === 'designer' ? 'designer' : 'guided');
    }

    /* ── Blueprint overlay events ────────────────────────────── */
    function bindBlueprintOverlay() {
        const overlay   = document.getElementById('designerBlueprintOverlay');
        const closeBtn  = document.getElementById('designerBlueprintClose');
        const editBtn   = document.getElementById('designerBlueprintEdit');
        const submitBtn = document.getElementById('designerBlueprintSubmit');
        if (closeBtn)  closeBtn.addEventListener('click',  () => overlay && overlay.classList.remove('visible'));
        if (editBtn)   editBtn.addEventListener('click',   () => overlay && overlay.classList.remove('visible'));
        if (submitBtn) submitBtn.addEventListener('click', () => {
            if (!overlay) return;
            overlay.classList.remove('visible');
            writeHiddenFields();
            const form = document.getElementById('studioForm');
            if (form) form.submit();
        });
    }

    /* ── Toolbar buttons ─────────────────────────────────────── */
    function bindToolbar() {
        const bind = (id, fn) => { const el = document.getElementById(id); if (el) el.addEventListener('click', fn); };
        bind('designerBtnZoomIn',    () => zoomBy(0.15));
        bind('designerBtnZoomOut',   () => zoomBy(-0.15));
        bind('designerBtnFit',       () => fitView());
        bind('designerBtnClear',     () => {
            if (!confirm('Vider le compositeur ? Cette action est irréversible.')) return;
            state.nodes = []; state.edges = [];
            Object.keys(nodeHeights).forEach(k => delete nodeHeights[k]);
            selectedNodeIds.clear(); selectedEdgeIds.clear();
            renderNodes(); renderEdges();
            updateToolbarStats(); updateAgentStats(); updateMinimap();
            renderInspectorPanel(); saveDraft();
        });
        bind('designerBtnBlueprint', () => showBlueprintReview());
        bind('designerBtnPublish',   () => showBlueprintReview());
        bind('designerBtnTest',      () => showToast('✨ Simulation de l\'agent en cours…'));

        // Initial "Add capacité" button in empty canvas
        const addFirstBtn = document.getElementById('designerAddFirstBtn');
        if (addFirstBtn) addFirstBtn.addEventListener('click', () => showAddStepPopup(null, null, null));
    }

    /* ── Agent Identity Card binding ─────────────────────────── */
    function bindAgentIdentityCard() {
        const nameInput = document.getElementById('designerAgentName');
        const descInput = document.getElementById('designerAgentDesc');
        const avatarBtn = document.getElementById('designerAgentAvatarBtn');
        emojiPicker     = document.getElementById('designerEmojiPicker');
        const domainsRow= document.getElementById('designerDomainsRow');

        if (nameInput) {
            nameInput.value = state.meta.name;
            nameInput.addEventListener('input', e => { state.meta.name = e.target.value; saveDraft(); });
        }
        if (descInput) {
            descInput.value = state.meta.mission;
            descInput.addEventListener('input', e => {
                state.meta.mission = e.target.value;
                // Auto grow
                e.target.style.height = 'auto';
                e.target.style.height = Math.min(e.target.scrollHeight, 60) + 'px';
                saveDraft();
            });
        }
        if (avatarBtn) {
            avatarBtn.textContent = state.meta.avatar || '🤖';
            avatarBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                if (emojiPicker) emojiPicker.classList.toggle('visible');
            });
        }
        if (emojiPicker) {
            const EMOJIS = ['🤖','🧠','📧','🔍','✅','🚀','📊','🛡','💼','📄','🔗','⚡','🎯','💡','🔬','📈','🌐','🏭','💰','🤝'];
            EMOJIS.forEach(em => {
                const opt = document.createElement('div');
                opt.className = 'designer-emoji-option';
                opt.textContent = em;
                opt.addEventListener('click', () => {
                    state.meta.avatar = em;
                    if (avatarBtn) avatarBtn.textContent = em;
                    emojiPicker.classList.remove('visible');
                    saveDraft();
                });
                emojiPicker.appendChild(opt);
            });
            document.addEventListener('click', () => emojiPicker.classList.remove('visible'));
        }

        if (domainsRow && window.STUDIO_DOMAINS) {
            domainsRow.innerHTML = '';
            window.STUDIO_DOMAINS.slice(0, 10).forEach(d => {
                const chip = document.createElement('div');
                chip.className = 'designer-domain-chip';
                chip.style.background = d.bg || '#1e2d40';
                chip.style.color      = d.color || '#94a3b8';
                chip.textContent      = (d.icon || '') + ' ' + (d.name || d.id);
                chip.dataset.domainId = d.id;
                if (state.meta.businessDomain === d.id) chip.classList.add('selected');
                chip.addEventListener('click', () => {
                    if (state.meta.businessDomain === d.id) { state.meta.businessDomain = ''; chip.classList.remove('selected'); }
                    else {
                        domainsRow.querySelectorAll('.designer-domain-chip').forEach(c => c.classList.remove('selected'));
                        state.meta.businessDomain = d.id;
                        chip.classList.add('selected');
                    }
                    saveDraft();
                });
                domainsRow.appendChild(chip);
            });
        }
    }

    /* ── Add Step Popup binding ──────────────────────────────── */
    function bindAddStepPopup() {
        const popup   = document.getElementById('designerAddPopup');
        if (!popup) return;

        // Close button
        popup.querySelector('.designer-add-popup-close').addEventListener('click', hideAddStepPopup);

        // Category grid items
        const grid = popup.querySelector('.designer-add-popup-grid');
        POPUP_CATEGORIES.forEach(cat => {
            const el = document.createElement('div');
            el.className = 'designer-add-popup-cat';
            el.innerHTML = `<div class="designer-add-popup-cat-dot" style="background:${cat.color}"></div><span class="designer-add-popup-cat-label">${escHtml(cat.label)}</span>`;
            el.addEventListener('click', () => showAddPopupSubcats(cat.cat));
            grid.appendChild(el);
        });

        // Back button
        const backBtn = popup.querySelector('.designer-add-popup-back-btn');
        if (backBtn) {
            backBtn.addEventListener('click', () => {
                const grid2   = popup.querySelector('.designer-add-popup-grid');
                const subcats = popup.querySelector('.designer-add-popup-subcats');
                if (grid2) grid2.style.display = '';
                if (subcats) subcats.classList.remove('visible');
                backBtn.style.display = 'none';
            });
        }

        // Search
        const search = popup.querySelector('.designer-add-popup-search');
        if (search) {
            search.addEventListener('input', e => {
                const q = e.target.value.toLowerCase().trim();
                if (!q) { popup.querySelector('.designer-add-popup-grid').style.display = ''; return; }
                // Show sub-cat with filtered items
                const subcats = popup.querySelector('.designer-add-popup-subcats');
                const backBtn2 = popup.querySelector('.designer-add-popup-back-btn');
                popup.querySelector('.designer-add-popup-grid').style.display = 'none';
                if (backBtn2) backBtn2.style.display = '';
                subcats.innerHTML = '';
                subcats.classList.add('visible');
                for (const items of Object.values(NODE_CATALOGUE)) {
                    items.filter(item => item.label.toLowerCase().includes(q) || item.type.toLowerCase().includes(q)).forEach(item => {
                        const el = document.createElement('div');
                        el.className = 'designer-add-popup-subcat-item';
                        el.innerHTML = `<span style="font-size:1.1rem">${escHtml(item.icon)}</span> ${escHtml(item.label)}`;
                        el.addEventListener('click', () => { hideAddStepPopup(); doAddNode(item.type); });
                        subcats.appendChild(el);
                    });
                }
            });
        }

        // Close on outside click
        document.addEventListener('click', e => {
            if (!popup.contains(e.target) && !e.target.closest('.designer-add-step-btn')) {
                hideAddStepPopup();
            }
        });
    }

    /* ── Palette rendering ───────────────────────────────────── */
    function renderPalette(containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;
        const sectionsWrap = container.querySelector('.designer-palette-sections');
        if (!sectionsWrap) return;
        sectionsWrap.innerHTML = '';

        Object.entries(NODE_CATALOGUE).forEach(([sectionName, items]) => {
            const filtered = palette_search
                ? items.filter(item => item.label.toLowerCase().includes(palette_search) || item.type.toLowerCase().includes(palette_search))
                : items;
            if (filtered.length === 0) return;

            const section = document.createElement('div');
            section.className = 'designer-palette-section';
            const header = document.createElement('div');
            header.className = 'designer-palette-section-header';
            header.innerHTML = `<span class="designer-palette-section-title">${escHtml(sectionName)}</span><span class="designer-palette-section-arrow"><i class="bi bi-chevron-down"></i></span>`;
            header.addEventListener('click', () => section.classList.toggle('collapsed'));

            const itemsWrap = document.createElement('div');
            itemsWrap.className = 'designer-palette-items';

            filtered.forEach(item => {
                const catColor = getCategoryColor(item.category);
                const el = document.createElement('div');
                el.className = 'designer-node-template';
                el.draggable = true;
                el.dataset.nodeType = item.type;
                el.innerHTML = `
                    <div class="designer-node-template-icon" style="background:${catColor}20;color:${catColor}">${escHtml(item.icon)}</div>
                    <span class="designer-node-template-label">${escHtml(item.label)}</span>`;
                el.addEventListener('dragstart', e => {
                    e.dataTransfer.setData('text/plain', item.type);
                    e.dataTransfer.effectAllowed = 'copy';
                });
                // Click to add directly
                el.addEventListener('click', () => {
                    addPopupAfterNode = state.nodes.length > 0 ? state.nodes[state.nodes.length - 1].id : null;
                    doAddNode(item.type);
                });
                itemsWrap.appendChild(el);
            });
            section.appendChild(header);
            section.appendChild(itemsWrap);
            sectionsWrap.appendChild(section);
        });
    }

    /* ── Drop target ─────────────────────────────────────────── */
    function bindDropTarget() {
        canvasWrap.addEventListener('dragover', e => { e.preventDefault(); e.dataTransfer.dropEffect = 'copy'; });
        canvasWrap.addEventListener('drop', e => {
            e.preventDefault();
            const type = e.dataTransfer.getData('text/plain');
            if (!type) return;
            const rect = getCanvasRect();
            const cp   = screenToCanvas(e.clientX - rect.left, e.clientY - rect.top);
            const x    = Math.round((cp.x - NODE_W / 2) / 20) * 20;
            const y    = Math.round(cp.y / 20) * 20;
            addPopupAfterNode = null;
            doAddNode(type);
            // Reposition the just-added node to drop location
            const n = state.nodes[state.nodes.length - 1];
            if (n) { n.x = x; n.y = y; renderNodes(); saveDraft(); }
        });
    }

    /* ── Palette search ──────────────────────────────────────── */
    function bindPaletteSearch() {
        const el = document.getElementById('paletteSearchInput');
        if (!el) return;
        el.addEventListener('input', e => {
            palette_search = e.target.value.toLowerCase().trim();
            renderPalette('designerPaletteContainer');
        });
    }

    /* ── Keyboard ────────────────────────────────────────────── */
    function bindKeyboard() {
        document.addEventListener('keydown', e => {
            const inInput = e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA' || e.target.isContentEditable;
            if (inInput) return;
            if ((e.key === 'Delete' || e.key === 'Backspace') && selectedNodeIds.size > 0) {
                e.preventDefault(); deleteSelectedNodes();
            }
            if (e.key === 'Escape') { clearSelection(); hideAddStepPopup(); }
            if (e.ctrlKey && e.key === 'a') {
                e.preventDefault();
                state.nodes.forEach(n => selectedNodeIds.add(n.id));
                renderNodes(); renderInspectorPanel();
            }
            if (e.key === '+' && e.ctrlKey) { e.preventDefault(); zoomBy(0.1); }
            if (e.key === '-' && e.ctrlKey) { e.preventDefault(); zoomBy(-0.1); }
            if (e.key === '0' && e.ctrlKey) { e.preventDefault(); fitView(); }
        });
    }

    /* ── SVG defs (arrowheads per category) ─────────────────── */
    function injectSvgDefs() {
        // Inject CSS animation for edge flow
        if (!document.getElementById('designer-edge-anim-style')) {
            const style = document.createElement('style');
            style.id = 'designer-edge-anim-style';
            style.textContent = `
                @keyframes flowDash { to { stroke-dashoffset: -20; } }
                .edge-path { stroke-dasharray: 6 4; animation: flowDash 1.5s linear infinite; }
                .edge-path.selected { stroke-dasharray: none; animation: none; stroke-width: 3; opacity: 1 !important; }
            `;
            document.head.appendChild(style);
        }

        const defs = document.createElementNS('http://www.w3.org/2000/svg', 'defs');
        let defsHtml = '';
        Object.entries(CAT_COLOR).forEach(([cat, color]) => {
            defsHtml += `<marker id="arrowhead-${cat}" markerWidth="7" markerHeight="5" refX="7" refY="2.5" orient="auto">
                <polygon points="0 0, 7 2.5, 0 5" fill="${color}" opacity="0.8" />
            </marker>`;
        });
        // Fallback arrowhead
        defsHtml += `<marker id="arrowhead" markerWidth="7" markerHeight="5" refX="7" refY="2.5" orient="auto">
            <polygon points="0 0, 7 2.5, 0 5" fill="#7c3aed" opacity="0.8" />
        </marker>`;
        defs.innerHTML = defsHtml;
        canvasSvg.appendChild(defs);
    }

    /* ── Metrics Bar ─────────────────────────────────────────── */
    function renderMetricsBar() {
        const bar = document.getElementById('designerMetricsBar');
        if (!bar) return;

        const metrics = [
            { icon: '📊', label: 'Exécutions (24h)', value: '128', delta: '+12%', positive: true,  sparkPoints: [40,55,48,60,52,70,65,80,72,85,78,90,84,95,88,100,92,105,98,110,102,115,108,118,112,120,115,125,120,128] },
            { icon: '✓',  label: 'Taux de réussite', value: '98.4%', delta: '+2.1%', positive: true, sparkPoints: [90,91,89,92,93,90,94,93,95,94,96,95,97,96,97,98,97,98,98,97,98,99,98,99,98,99,99,98,99,98] },
            { icon: '⏱',  label: 'Temps moyen',      value: '2.34s', delta: '-0.4s', positive: true, sparkPoints: [3.2,3.1,3.0,2.9,2.8,2.9,2.8,2.7,2.8,2.7,2.6,2.7,2.6,2.5,2.6,2.5,2.4,2.5,2.4,2.5,2.4,2.4,2.3,2.4,2.3,2.4,2.3,2.3,2.4,2.34] },
            { icon: '💰',  label: 'Coût IA (24h)',    value: '$3.42', delta: '-8%',   positive: true, sparkPoints: [5.2,5.0,4.8,4.6,4.5,4.4,4.3,4.2,4.1,4.0,3.9,3.9,3.8,3.8,3.7,3.7,3.7,3.6,3.6,3.6,3.5,3.5,3.5,3.5,3.4,3.4,3.4,3.4,3.4,3.42] },
            { icon: '⚠',  label: 'Erreurs',           value: '2',     delta: '-60%',  positive: true, sparkPoints: [8,7,6,5,6,5,5,4,5,4,4,4,3,4,3,3,4,3,3,3,3,2,3,2,3,2,2,3,2,2] },
            { icon: '✅',  label: 'Statut global',     value: 'Excellent', delta: '✓', positive: true, sparkPoints: [80,82,83,85,86,87,88,89,90,91,92,93,94,94,95,95,96,96,97,97,97,98,98,98,99,99,99,99,99,100] },
        ];

        bar.innerHTML = metrics.map(m => {
            const mn = Math.min(...m.sparkPoints);
            const mx2 = Math.max(...m.sparkPoints);
            const range = mx2 - mn || 1;
            const w = 50, h = 20;
            const pts = m.sparkPoints.map((v, i) => {
                const px = (i / (m.sparkPoints.length - 1)) * w;
                const py = h - ((v - mn) / range) * h;
                return `${px},${py}`;
            }).join(' ');
            const sparkColor = m.positive ? '#10b981' : '#ef4444';
            return `<div class="designer-metric-item">
                <div class="designer-metric-icon">${m.icon}</div>
                <div class="designer-metric-content">
                    <div class="designer-metric-label">${escHtml(m.label)}</div>
                    <div class="designer-metric-value">
                        ${escHtml(m.value)}
                        <span class="designer-metric-delta ${m.positive ? 'positive' : 'negative'}">${escHtml(m.delta)}</span>
                    </div>
                </div>
                <svg class="designer-metric-sparkline" viewBox="0 0 ${w} ${h}" preserveAspectRatio="none">
                    <polyline points="${pts}" fill="none" stroke="${sparkColor}" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" opacity="0.8"/>
                </svg>
            </div>`;
        }).join('');
    }

    /* ── Sub-nav tabs binding ────────────────────────────────── */
    function bindSubNavTabs() {
        // Bind new topbar subnav buttons
        const topbarBtns = document.querySelectorAll('.designer-subnav-btn');
        topbarBtns.forEach(btn => {
            btn.addEventListener('click', () => {
                topbarBtns.forEach(t => t.classList.remove('active'));
                btn.classList.add('active');
            });
        });
        // Also keep legacy sub-tab support for backward compat
        const legacyTabs = document.querySelectorAll('.designer-sub-tab');
        legacyTabs.forEach(tab => {
            tab.addEventListener('click', () => {
                legacyTabs.forEach(t => t.classList.remove('active'));
                tab.classList.add('active');
            });
        });
    }

    /* ── Utility ─────────────────────────────────────────────── */
    function escHtml(str) {
        if (!str) return '';
        return String(str).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;').replace(/'/g,'&#039;');
    }

    /* ── Init ────────────────────────────────────────────────── */
    function init() {
        canvasWrap  = document.getElementById('designerCanvasWrap');
        canvasSvg   = document.getElementById('designerCanvasSvg');
        canvasNodes = document.getElementById('designerCanvasNodes');
        minimapSvg  = document.getElementById('designerMinimapSvg');
        toolbarStats= document.getElementById('designerToolbarStats');
        toolbarZoom = document.getElementById('designerToolbarZoom');
        validationBanner = document.getElementById('designerValidationBanner');

        if (!canvasWrap || !canvasSvg || !canvasNodes) return;

        loadDraft();
        // If no draft, load sample nodes to demonstrate what's possible
        if (state.nodes.length === 0) {
            const nGmail = { id: uid(), type: 'connector-gmail', label: 'Gmail', x: 80,  y: 100, config: { account: 'demo@exemple.com' }, meta: {} };
            const nExtract = { id: uid(), type: 'skill-extraction', label: 'Extraction IA', x: 440, y: 100, config: { model: 'gpt-4o-mini', fields: 'nom, date, montant' }, meta: { confidence: 96 } };
            const nStorage = { id: uid(), type: 'action-storage', label: 'Stockage', x: 800, y: 100, config: {}, meta: {} };
            state.nodes = [nGmail, nExtract, nStorage];
            state.edges = [
                { id: uid(), from: nGmail.id,   to: nExtract.id, label: 'Email + PJ' },
                { id: uid(), from: nExtract.id, to: nStorage.id, label: 'JSON structuré' },
            ];
        }
        injectSvgDefs();

        // Canvas events
        canvasWrap.addEventListener('mousedown', onCanvasMouseDown);
        document.addEventListener('mousemove',  onCanvasMouseMove);
        document.addEventListener('mouseup',    onCanvasMouseUp);
        canvasWrap.addEventListener('wheel', onCanvasWheel, { passive: false });

        bindDropTarget();
        bindKeyboard();
        bindToolbar();
        bindPaletteSearch();
        bindAgentIdentityCard();
        bindModeSelector();
        bindFormSubmit();
        bindBlueprintOverlay();
        bindAddStepPopup();
        initAiDrawer();

        renderPalette('designerPaletteContainer');
        renderMetricsBar();
        bindSubNavTabs();

        // Initial render
        renderNodes();
        requestAnimationFrame(() => {
            renderEdges();
            renderAddStepButtons();
            updateMinimap();
        });
        updateToolbarStats();
        updateAgentStats();
        setTransform();
        renderInspectorPanel();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();

    /* ── Public API ──────────────────────────────────────────── */
    window.AgentiaDesigner = {
        getPayload:           getDesignerPayload,
        buildMessage:         buildDesignerMessage,
        validate,
        showBlueprintReview,
    };
})();
