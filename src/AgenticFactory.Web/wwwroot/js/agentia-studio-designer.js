/* ============================================================
   Agentia Studio — Visual Designer
   Double méthode création agent — pure vanilla JS + SVG
   No external library dependencies.
   ============================================================ */
(function () {
    'use strict';

    // ── Palette node catalogue ────────────────────────────────
    const NODE_CATALOGUE = {
        Trigger: [
            { type: 'trigger-manual',  label: 'Manuel',   icon: 'bi-hand-index', iconBg: '#fef3c7', iconColor: '#d97706' },
            { type: 'trigger-cron',    label: 'Planifié', icon: 'bi-clock',       iconBg: '#fef3c7', iconColor: '#d97706' },
            { type: 'trigger-webhook', label: 'Webhook',  icon: 'bi-lightning',   iconBg: '#fef3c7', iconColor: '#d97706' },
        ],
        Sensors: [
            { type: 'sensor-email', label: 'Email',    icon: 'bi-envelope',     iconBg: '#dbeafe', iconColor: '#2563eb' },
            { type: 'sensor-rest',  label: 'API REST', icon: 'bi-cloud-arrow-down', iconBg: '#dbeafe', iconColor: '#2563eb' },
            { type: 'sensor-file',  label: 'Fichier',  icon: 'bi-file-earmark', iconBg: '#dbeafe', iconColor: '#2563eb' },
            { type: 'sensor-sql',   label: 'SQL',      icon: 'bi-database',     iconBg: '#dbeafe', iconColor: '#2563eb' },
        ],
        Skills: [
            { type: 'skill-analysis',       label: 'Analyse',       icon: 'bi-bar-chart',      iconBg: '#d1fae5', iconColor: '#059669' },
            { type: 'skill-extraction',     label: 'Extraction',    icon: 'bi-scissors',        iconBg: '#d1fae5', iconColor: '#059669' },
            { type: 'skill-classification', label: 'Classification',icon: 'bi-tags',            iconBg: '#d1fae5', iconColor: '#059669' },
            { type: 'skill-rag',            label: 'RAG',           icon: 'bi-search',          iconBg: '#d1fae5', iconColor: '#059669' },
        ],
        Tools: [
            { type: 'tool-outlook',  label: 'Outlook',  icon: 'bi-envelope-at',  iconBg: '#ede9fe', iconColor: '#7c3aed' },
            { type: 'tool-teams',    label: 'Teams',    icon: 'bi-people',       iconBg: '#ede9fe', iconColor: '#7c3aed' },
            { type: 'tool-dynamics', label: 'Dynamics', icon: 'bi-grid-3x3-gap',iconBg: '#ede9fe', iconColor: '#7c3aed' },
        ],
        Decision: [
            { type: 'decision-gpt',   label: 'GPT',          icon: 'bi-stars',          iconBg: '#fce7f3', iconColor: '#db2777' },
            { type: 'decision-rules', label: 'Règles métier',icon: 'bi-diagram-2',      iconBg: '#fce7f3', iconColor: '#db2777' },
            { type: 'decision-human', label: 'Validation humaine', icon: 'bi-person-check', iconBg: '#fce7f3', iconColor: '#db2777' },
        ],
        Actuators: [
            { type: 'actuator-email',  label: 'Envoyer email', icon: 'bi-send',          iconBg: '#ffedd5', iconColor: '#ea580c' },
            { type: 'actuator-report', label: 'Rapport',       icon: 'bi-file-richtext', iconBg: '#ffedd5', iconColor: '#ea580c' },
            { type: 'actuator-api',    label: 'Appel API',     icon: 'bi-send-arrow-up', iconBg: '#ffedd5', iconColor: '#ea580c' },
            { type: 'actuator-teams',  label: 'Teams message', icon: 'bi-chat-dots',     iconBg: '#ffedd5', iconColor: '#ea580c' },
        ],
        Memory: [
            { type: 'memory-business', label: 'Mémoire métier', icon: 'bi-journal-bookmark', iconBg: '#dcfce7', iconColor: '#16a34a' },
            { type: 'memory-vector',   label: 'Mémoire vectorielle', icon: 'bi-hdd-stack',    iconBg: '#dcfce7', iconColor: '#16a34a' },
        ],
        Output: [
            { type: 'actuator-report', label: 'Sortie rapport', icon: 'bi-box-arrow-up-right', iconBg: '#e0f2fe', iconColor: '#0284c7' },
        ],
    };

    // ── Node type → category mapping ─────────────────────────
    function getNodeCategory(type) {
        if (type.startsWith('trigger'))  return 'trigger';
        if (type.startsWith('sensor'))   return 'sensor';
        if (type.startsWith('skill'))    return 'skill';
        if (type.startsWith('tool'))     return 'tool';
        if (type.startsWith('decision')) return 'decision';
        if (type.startsWith('actuator')) return 'actuator';
        if (type.startsWith('memory'))   return 'memory';
        return 'output';
    }

    function getCatalogueEntry(type) {
        for (const items of Object.values(NODE_CATALOGUE)) {
            const found = items.find(n => n.type === type);
            if (found) return found;
        }
        return null;
    }

    // ── Type-specific config field definitions ────────────────
    const NODE_CONFIG_FIELDS = {
        'sensor-email': [
            { key: 'emailUrl',  label: 'Email / URL',  type: 'text',   placeholder: 'factures@entreprise.com' },
            { key: 'oauth',     label: 'OAuth',        type: 'text',   placeholder: 'Client ID / Tenant' },
            { key: 'folder',    label: 'Dossier',      type: 'text',   placeholder: 'Inbox' },
        ],
        'sensor-rest': [
            { key: 'url',    label: 'URL',     type: 'text',   placeholder: 'https://api.example.com/data' },
            { key: 'method', label: 'Méthode', type: 'select', options: ['GET','POST','PUT','DELETE'], placeholder: 'GET' },
            { key: 'auth',   label: 'Auth',    type: 'text',   placeholder: 'Bearer token / API key' },
        ],
        'sensor-file': [
            { key: 'path',    label: 'Chemin',  type: 'text', placeholder: '/data/input/' },
            { key: 'pattern', label: 'Filtre',  type: 'text', placeholder: '*.pdf' },
        ],
        'sensor-sql': [
            { key: 'connectionString', label: 'Connexion',   type: 'text', placeholder: 'Server=...;Database=...' },
            { key: 'query',            label: 'Requête SQL', type: 'textarea', placeholder: 'SELECT * FROM ...' },
        ],
        'trigger-cron': [
            { key: 'cronExpression', label: 'Expression cron', type: 'text', placeholder: '0 9 * * 1-5' },
        ],
        'trigger-webhook': [
            { key: 'webhookUrl', label: 'Webhook URL', type: 'text', readonly: true, placeholder: 'Auto-généré au déploiement' },
        ],
        'decision-gpt': [
            { key: 'model',  label: 'Modèle', type: 'select', options: ['gpt-4o-mini','gpt-4o','gpt-4-turbo','gpt-3.5-turbo'], placeholder: 'gpt-4o-mini' },
            { key: 'prompt', label: 'Prompt système', type: 'textarea', placeholder: 'Vous êtes un assistant…' },
        ],
        'decision-rules': [
            { key: 'rules', label: 'Règles (JSON)', type: 'textarea', placeholder: '[{"if":"...","then":"..."}]' },
        ],
        'actuator-email': [
            { key: 'recipient',       label: 'Destinataire',      type: 'text', placeholder: 'dest@entreprise.com' },
            { key: 'subjectTemplate', label: 'Sujet (template)',  type: 'text', placeholder: 'Rapport {date}' },
        ],
        'actuator-api': [
            { key: 'url',    label: 'URL endpoint', type: 'text',   placeholder: 'https://api.example.com/endpoint' },
            { key: 'method', label: 'Méthode',      type: 'select', options: ['POST','PUT','PATCH','GET'], placeholder: 'POST' },
        ],
    };

    // ── State ─────────────────────────────────────────────────
    const DRAFT_KEY     = 'agentia-designer-draft';
    const MODE_KEY      = 'agentia-creation-mode';
    const NODE_W        = 168;
    const NODE_H        = 90;

    let state = {
        version: 1,
        meta:    { name: '', mission: '', businessDomain: '' },
        nodes:   [],
        edges:   [],
        layout:  { zoom: 1, panX: 0, panY: 0 },
    };

    let selectedNodeIds  = new Set();
    let selectedEdgeIds  = new Set();
    let isPanning        = false;
    let panStart         = { x: 0, y: 0, panX: 0, panY: 0 };
    let isDraggingNode   = false;
    let dragNodeId       = null;
    let dragOffsets      = {};
    let dragStartPos     = {};
    let connectingFrom   = null;
    let tempEdgePath     = null;
    let palette_search   = '';

    // DOM refs (populated in init)
    let canvasWrap, canvasSvg, canvasNodes, minimap, minimapSvg;
    let propBody, propEmpty, toolbarStats;
    let validationBanner;

    // ── ID generator ─────────────────────────────────────────
    function uid() {
        return 'n' + Math.random().toString(36).slice(2, 9) + Date.now().toString(36).slice(-4);
    }

    // ── Serialization ─────────────────────────────────────────
    function saveDraft() {
        try { localStorage.setItem(DRAFT_KEY, JSON.stringify(state)); } catch (_) {}
    }

    function loadDraft() {
        try {
            const raw = localStorage.getItem(DRAFT_KEY);
            if (!raw) return;
            const parsed = JSON.parse(raw);
            if (parsed && parsed.version === 1) {
                state = parsed;
                state.nodes  = state.nodes  || [];
                state.edges  = state.edges  || [];
                state.layout = state.layout || { zoom: 1, panX: 0, panY: 0 };
                state.meta   = state.meta   || { name: '', mission: '', businessDomain: '' };
            }
        } catch (_) {}
    }

    // ── Canvas transform helpers ──────────────────────────────
    function setTransform() {
        const { zoom, panX, panY } = state.layout;
        canvasNodes.style.transform = `translate(${panX}px,${panY}px) scale(${zoom})`;
    }

    function canvasToScreen(x, y) {
        const { zoom, panX, panY } = state.layout;
        return { x: x * zoom + panX, y: y * zoom + panY };
    }

    function screenToCanvas(x, y) {
        const { zoom, panX, panY } = state.layout;
        return { x: (x - panX) / zoom, y: (y - panY) / zoom };
    }

    function getCanvasRect() {
        return canvasWrap.getBoundingClientRect();
    }

    // ── Port position (in canvas coords) ─────────────────────
    function portPos(node, side) {
        const x = side === 'out' ? node.x + NODE_W : node.x;
        const y = node.y + NODE_H / 2;
        return { x, y };
    }

    // ── SVG path for edge ─────────────────────────────────────
    function bezierPath(x1, y1, x2, y2) {
        const dx = Math.abs(x2 - x1) * 0.55 + 40;
        return `M ${x1} ${y1} C ${x1 + dx} ${y1}, ${x2 - dx} ${y2}, ${x2} ${y2}`;
    }

    // ── Render edges (SVG) ────────────────────────────────────
    function renderEdges() {
        // Remove all existing edge elements
        const existing = canvasSvg.querySelectorAll('.edge-group');
        existing.forEach(el => el.remove());

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

            const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            g.classList.add('edge-group');
            g.dataset.edgeId = edge.id;

            const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            path.classList.add('edge-path');
            if (selectedEdgeIds.has(edge.id)) path.classList.add('selected');
            path.setAttribute('d', bezierPath(sx1, sy1, sx2, sy2));
            path.setAttribute('marker-end', 'url(#arrowhead)');

            path.addEventListener('click', (e) => {
                e.stopPropagation();
                selectedEdgeIds.clear();
                selectedEdgeIds.add(edge.id);
                selectedNodeIds.clear();
                renderEdges();
                renderPropertiesPanel();
            });

            g.appendChild(path);
            canvasSvg.appendChild(g);
        });
    }

    // ── Render all nodes ──────────────────────────────────────
    function renderNodes() {
        // Diff: remove orphan, create missing
        const existingEls = {};
        canvasNodes.querySelectorAll('.designer-node').forEach(el => {
            existingEls[el.dataset.nodeId] = el;
        });

        const keepIds = new Set(state.nodes.map(n => n.id));
        Object.keys(existingEls).forEach(id => {
            if (!keepIds.has(id)) existingEls[id].remove();
        });

        state.nodes.forEach(node => {
            let el = existingEls[node.id];
            if (!el) {
                el = createNodeElement(node);
                canvasNodes.appendChild(el);
            }
            positionNodeElement(el, node);
            updateNodeSelected(el, selectedNodeIds.has(node.id));
        });
    }

    function positionNodeElement(el, node) {
        el.style.left = node.x + 'px';
        el.style.top  = node.y + 'px';
    }

    function updateNodeSelected(el, selected) {
        el.classList.toggle('selected', selected);
    }

    function createNodeElement(node) {
        const cat    = getCatalogueEntry(node.type);
        const icon   = cat ? cat.icon   : 'bi-box';
        const iconBg = cat ? cat.iconBg  : '#f1f5f9';
        const iconCl = cat ? cat.iconColor : '#475569';
        const category = getNodeCategory(node.type);

        const el = document.createElement('div');
        el.className = `designer-node node-type-${category}`;
        el.dataset.nodeId = node.id;
        el.style.width  = NODE_W + 'px';

        el.innerHTML = `
            <div class="designer-port designer-port-in" data-port="in" data-node="${node.id}"></div>
            <div class="designer-port designer-port-out" data-port="out" data-node="${node.id}"></div>
            <div class="designer-node-header">
                <div class="designer-node-icon" style="background:${iconBg};color:${iconCl}">
                    <i class="bi ${icon}"></i>
                </div>
                <div class="designer-node-title" title="${escHtml(node.label)}">${escHtml(node.label)}</div>
                <button class="designer-node-delete" data-action="delete-node" data-node="${node.id}" title="Supprimer">
                    <i class="bi bi-x"></i>
                </button>
            </div>
            <div class="designer-node-type-badge">${escHtml(category)}</div>
            <div class="designer-node-footer">
                <button class="designer-node-config-btn" data-action="config-node" data-node="${node.id}">
                    <i class="bi bi-sliders"></i> Configurer
                </button>
            </div>`;

        // Drag on header
        const header = el.querySelector('.designer-node-header');
        header.addEventListener('mousedown', onNodeDragStart);

        // Delete btn
        el.querySelector('[data-action="delete-node"]').addEventListener('click', e => {
            e.stopPropagation();
            deleteNode(node.id);
        });

        // Config btn
        el.querySelector('[data-action="config-node"]').addEventListener('click', e => {
            e.stopPropagation();
            selectNode(node.id, false);
        });

        // Click to select
        el.addEventListener('mousedown', e => {
            if (e.target.closest('[data-port]') || e.target.closest('[data-action]')) return;
            if (e.button !== 0) return;
            selectNode(node.id, e.shiftKey);
            e.stopPropagation();
        });

        // Port interactions
        el.querySelector('[data-port="out"]').addEventListener('mousedown', e => {
            e.stopPropagation();
            startConnect(node.id, e);
        });

        el.querySelector('[data-port="in"]').addEventListener('mouseup', e => {
            e.stopPropagation();
            endConnect(node.id);
        });

        return el;
    }

    // ── Node selection ────────────────────────────────────────
    function selectNode(id, multi) {
        if (!multi) selectedNodeIds.clear();
        selectedEdgeIds.clear();

        if (selectedNodeIds.has(id) && multi) {
            selectedNodeIds.delete(id);
        } else {
            selectedNodeIds.add(id);
        }
        renderNodes();
        renderPropertiesPanel();
    }

    function clearSelection() {
        selectedNodeIds.clear();
        selectedEdgeIds.clear();
        renderNodes();
        renderEdges();
        renderPropertiesPanel();
    }

    // ── Node operations ───────────────────────────────────────
    function addNode(type, x, y) {
        const cat = getCatalogueEntry(type);
        const defaults = getDefaultConfig(type);
        const node = {
            id:     uid(),
            type,
            label:  cat ? cat.label : type,
            x:      Math.round(x),
            y:      Math.round(y),
            config: defaults,
        };
        state.nodes.push(node);
        renderNodes();
        renderEdges();
        updateToolbarStats();
        updateMinimap();
        saveDraft();
        selectNode(node.id, false);
        return node;
    }

    function deleteNode(id) {
        state.nodes   = state.nodes.filter(n => n.id !== id);
        state.edges   = state.edges.filter(e => e.from !== id && e.to !== id);
        selectedNodeIds.delete(id);
        renderNodes();
        renderEdges();
        updateToolbarStats();
        updateMinimap();
        renderPropertiesPanel();
        saveDraft();
    }

    function deleteSelectedNodes() {
        if (selectedNodeIds.size === 0) return;
        selectedNodeIds.forEach(id => {
            state.nodes = state.nodes.filter(n => n.id !== id);
            state.edges = state.edges.filter(e => e.from !== id && e.to !== id);
        });
        selectedEdgeIds.forEach(id => {
            state.edges = state.edges.filter(e => e.id !== id);
        });
        selectedNodeIds.clear();
        selectedEdgeIds.clear();
        renderNodes();
        renderEdges();
        updateToolbarStats();
        updateMinimap();
        renderPropertiesPanel();
        saveDraft();
    }

    function getDefaultConfig(type) {
        if (type === 'decision-gpt') return { model: 'gpt-4o-mini' };
        return {};
    }

    // ── Connections ───────────────────────────────────────────
    function startConnect(fromNodeId, e) {
        connectingFrom = fromNodeId;
        canvasWrap.classList.add('connecting');
        document.addEventListener('mouseup', onConnectMouseUp);
        document.addEventListener('mousemove', onConnectMouseMove);
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
        document.removeEventListener('mouseup', onConnectMouseUp);
        document.removeEventListener('mousemove', onConnectMouseMove);
        connectingFrom = null;
        canvasWrap.classList.remove('connecting');
        if (tempEdgePath) { tempEdgePath.remove(); tempEdgePath = null; }
    }

    function endConnect(toNodeId) {
        if (!connectingFrom || connectingFrom === toNodeId) {
            onConnectMouseUp();
            return;
        }
        const duplicate = state.edges.find(e => e.from === connectingFrom && e.to === toNodeId);
        if (!duplicate) {
            state.edges.push({ id: uid(), from: connectingFrom, to: toNodeId });
        }
        onConnectMouseUp();
        renderEdges();
        updateToolbarStats();
        updateMinimap();
        saveDraft();
    }

    // ── Node dragging ─────────────────────────────────────────
    function onNodeDragStart(e) {
        if (e.button !== 0) return;
        const nodeEl = e.currentTarget.closest('.designer-node');
        if (!nodeEl) return;
        e.stopPropagation();
        e.preventDefault();

        const nodeId = nodeEl.dataset.nodeId;
        isDraggingNode = true;
        dragNodeId = nodeId;

        if (!selectedNodeIds.has(nodeId)) {
            if (!e.shiftKey) selectedNodeIds.clear();
            selectedNodeIds.add(nodeId);
            renderNodes();
            renderPropertiesPanel();
        }

        const rect = getCanvasRect();
        const startX = e.clientX - rect.left;
        const startY = e.clientY - rect.top;
        const canvasStart = screenToCanvas(startX, startY);

        dragOffsets = {};
        dragStartPos = {};
        selectedNodeIds.forEach(id => {
            const n = state.nodes.find(nn => nn.id === id);
            if (n) {
                dragOffsets[id]  = { dx: n.x - canvasStart.x, dy: n.y - canvasStart.y };
                dragStartPos[id] = { x: n.x, y: n.y };
            }
        });

        nodeEl.classList.add('dragging');

        document.addEventListener('mousemove', onNodeDragMove);
        document.addEventListener('mouseup',   onNodeDragEnd);
    }

    function onNodeDragMove(e) {
        if (!isDraggingNode) return;
        const rect   = getCanvasRect();
        const mouseX = e.clientX - rect.left;
        const mouseY = e.clientY - rect.top;
        const cp     = screenToCanvas(mouseX, mouseY);

        selectedNodeIds.forEach(id => {
            const n = state.nodes.find(nn => nn.id === id);
            if (!n) return;
            const off = dragOffsets[id] || { dx: 0, dy: 0 };
            n.x = Math.round(cp.x + off.dx);
            n.y = Math.round(cp.y + off.dy);
            const el = canvasNodes.querySelector(`[data-node-id="${id}"]`);
            if (el) positionNodeElement(el, n);
        });
        renderEdges();
        updateMinimap();
    }

    function onNodeDragEnd() {
        document.removeEventListener('mousemove', onNodeDragMove);
        document.removeEventListener('mouseup',   onNodeDragEnd);

        if (isDraggingNode) {
            const el = canvasNodes.querySelector(`[data-node-id="${dragNodeId}"]`);
            if (el) el.classList.remove('dragging');
        }
        isDraggingNode = false;
        dragNodeId     = null;
        saveDraft();
    }

    // ── Canvas pan ────────────────────────────────────────────
    function onCanvasMouseDown(e) {
        if (e.button === 1 || (e.button === 0 && e.target === canvasWrap)) {
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
        setTransform();
        renderEdges();
        updateMinimap();
    }

    function onCanvasMouseUp() {
        if (isPanning) {
            isPanning = false;
            canvasWrap.classList.remove('panning');
            saveDraft();
        }
    }

    // ── Canvas zoom ───────────────────────────────────────────
    function onCanvasWheel(e) {
        if (!e.ctrlKey) return;
        e.preventDefault();
        const delta = e.deltaY > 0 ? -0.1 : 0.1;
        const newZoom = Math.min(3, Math.max(0.2, state.layout.zoom + delta));
        const rect    = getCanvasRect();
        const mouseX  = e.clientX - rect.left;
        const mouseY  = e.clientY - rect.top;
        const wx = (mouseX - state.layout.panX) / state.layout.zoom;
        const wy = (mouseY - state.layout.panY) / state.layout.zoom;
        state.layout.zoom = newZoom;
        state.layout.panX = mouseX - wx * newZoom;
        state.layout.panY = mouseY - wy * newZoom;
        setTransform();
        renderEdges();
        updateMinimap();
    }

    function zoomBy(delta) {
        const newZoom = Math.min(3, Math.max(0.2, state.layout.zoom + delta));
        const rect    = getCanvasRect();
        const cx      = rect.width  / 2;
        const cy      = rect.height / 2;
        const wx = (cx - state.layout.panX) / state.layout.zoom;
        const wy = (cy - state.layout.panY) / state.layout.zoom;
        state.layout.zoom = newZoom;
        state.layout.panX = cx - wx * newZoom;
        state.layout.panY = cy - wy * newZoom;
        setTransform();
        renderEdges();
        updateMinimap();
        saveDraft();
    }

    function fitView() {
        if (state.nodes.length === 0) {
            state.layout = { zoom: 1, panX: 60, panY: 60 };
            setTransform();
            renderEdges();
            updateMinimap();
            return;
        }
        const rect = getCanvasRect();
        const xs = state.nodes.map(n => n.x);
        const ys = state.nodes.map(n => n.y);
        const minX = Math.min(...xs) - 40;
        const minY = Math.min(...ys) - 40;
        const maxX = Math.max(...xs) + NODE_W + 40;
        const maxY = Math.max(...ys) + NODE_H + 40;
        const bw = maxX - minX;
        const bh = maxY - minY;
        const zoom = Math.min(2, Math.max(0.2,
            Math.min((rect.width - 80) / bw, (rect.height - 80) / bh)));
        state.layout.zoom = zoom;
        state.layout.panX = (rect.width  - bw * zoom) / 2 - minX * zoom;
        state.layout.panY = (rect.height - bh * zoom) / 2 - minY * zoom;
        setTransform();
        renderEdges();
        updateMinimap();
        saveDraft();
    }

    // ── Minimap ───────────────────────────────────────────────
    function updateMinimap() {
        if (!minimapSvg) return;
        minimapSvg.innerHTML = '';

        const mmW = 160, mmH = 100;
        const padding = 8;

        if (state.nodes.length === 0) return;

        const xs = state.nodes.map(n => n.x);
        const ys = state.nodes.map(n => n.y);
        const minX = Math.min(...xs);
        const minY = Math.min(...ys);
        const maxX = Math.max(...xs) + NODE_W;
        const maxY = Math.max(...ys) + NODE_H;
        const bw   = maxX - minX || 1;
        const bh   = maxY - minY || 1;
        const scale = Math.min((mmW - padding * 2) / bw, (mmH - padding * 2) / bh);

        const toMmX = x => (x - minX) * scale + padding;
        const toMmY = y => (y - minY) * scale + padding;

        state.nodes.forEach(n => {
            const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            rect.setAttribute('x',  toMmX(n.x));
            rect.setAttribute('y',  toMmY(n.y));
            rect.setAttribute('width',  Math.max(4, NODE_W * scale));
            rect.setAttribute('height', Math.max(3, NODE_H * scale));
            rect.setAttribute('rx', 2);
            rect.classList.add('designer-minimap-node');
            minimapSvg.appendChild(rect);
        });

        // Viewport rect
        const cRect  = getCanvasRect();
        const vp     = screenToCanvas(0, 0);
        const vpEnd  = screenToCanvas(cRect.width, cRect.height);
        const vpRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        vpRect.setAttribute('x',  toMmX(vp.x));
        vpRect.setAttribute('y',  toMmY(vp.y));
        vpRect.setAttribute('width',  (vpEnd.x - vp.x) * scale);
        vpRect.setAttribute('height', (vpEnd.y - vp.y) * scale);
        vpRect.classList.add('designer-minimap-viewport');
        minimapSvg.appendChild(vpRect);
    }

    // ── Toolbar stats ─────────────────────────────────────────
    function updateToolbarStats() {
        if (toolbarStats) {
            toolbarStats.textContent = `${state.nodes.length} nœud${state.nodes.length !== 1 ? 's' : ''} · ${state.edges.length} connexion${state.edges.length !== 1 ? 's' : ''}`;
        }
    }

    // ── Properties Panel ──────────────────────────────────────
    function renderPropertiesPanel() {
        if (!propBody || !propEmpty) return;

        const selectedIds = [...selectedNodeIds];
        if (selectedIds.length !== 1) {
            propEmpty.style.display = 'flex';
            propBody.style.display  = 'none';
            return;
        }

        const node = state.nodes.find(n => n.id === selectedIds[0]);
        if (!node) { propEmpty.style.display = 'flex'; propBody.style.display = 'none'; return; }

        propEmpty.style.display = 'none';
        propBody.style.display  = 'flex';
        propBody.innerHTML = '';

        const cat      = getCatalogueEntry(node.type);
        const iconBg   = cat ? cat.iconBg    : '#f1f5f9';
        const iconCl   = cat ? cat.iconColor : '#475569';
        const iconName = cat ? cat.icon      : 'bi-box';
        const category = getNodeCategory(node.type);

        // Node info header
        const infoDiv = document.createElement('div');
        infoDiv.className = 'designer-prop-node-info';
        infoDiv.innerHTML = `
            <div class="designer-prop-node-icon" style="background:${iconBg};color:${iconCl}">
                <i class="bi ${iconName}"></i>
            </div>
            <div>
                <div class="designer-prop-node-title">${escHtml(node.label)}</div>
                <div class="designer-prop-node-type">${escHtml(category)} · ${escHtml(node.type)}</div>
            </div>`;
        propBody.appendChild(infoDiv);

        // Name field
        propBody.appendChild(buildPropGroup('Nom du nœud', buildTextInput('label', node.label, 'Nom du nœud', false, val => {
            node.label = val;
            const titleEl = canvasNodes.querySelector(`[data-node-id="${node.id}"] .designer-node-title`);
            if (titleEl) titleEl.textContent = val;
            saveDraft();
        })));

        const divider = document.createElement('div');
        divider.className = 'designer-prop-divider';
        propBody.appendChild(divider);

        // Type-specific fields
        const fields = NODE_CONFIG_FIELDS[node.type] || [];
        if (fields.length === 0) {
            const hint = document.createElement('div');
            hint.className = 'designer-prop-hint';
            hint.textContent = 'Aucune configuration requise pour ce type de nœud.';
            propBody.appendChild(hint);
        } else {
            fields.forEach(field => {
                const currentVal = node.config[field.key] || '';
                let input;
                if (field.type === 'select') {
                    input = buildSelectInput(field.key, currentVal, field.options || [], val => {
                        node.config[field.key] = val;
                        saveDraft();
                    });
                } else if (field.type === 'textarea') {
                    input = buildTextareaInput(field.key, currentVal, field.placeholder || '', val => {
                        node.config[field.key] = val;
                        saveDraft();
                    });
                } else {
                    input = buildTextInput(field.key, currentVal, field.placeholder || '', field.readonly || false, val => {
                        node.config[field.key] = val;
                        saveDraft();
                    });
                }
                propBody.appendChild(buildPropGroup(field.label, input));
            });
        }
    }

    function buildPropGroup(label, inputEl) {
        const group = document.createElement('div');
        group.className = 'designer-prop-group';
        const lbl = document.createElement('label');
        lbl.className = 'designer-prop-label';
        lbl.textContent = label;
        group.appendChild(lbl);
        group.appendChild(inputEl);
        return group;
    }

    function buildTextInput(key, value, placeholder, readonly, onChange) {
        const input = document.createElement('input');
        input.type = 'text';
        input.className = 'designer-prop-input';
        input.value = value;
        input.placeholder = placeholder;
        if (readonly) input.readOnly = true;
        input.addEventListener('input', e => onChange(e.target.value));
        return input;
    }

    function buildTextareaInput(key, value, placeholder, onChange) {
        const ta = document.createElement('textarea');
        ta.className = 'designer-prop-textarea';
        ta.value = value;
        ta.placeholder = placeholder;
        ta.rows = 3;
        ta.addEventListener('input', e => onChange(e.target.value));
        return ta;
    }

    function buildSelectInput(key, value, options, onChange) {
        const sel = document.createElement('select');
        sel.className = 'designer-prop-select';
        options.forEach(opt => {
            const o = document.createElement('option');
            o.value = opt;
            o.textContent = opt;
            if (opt === value) o.selected = true;
            sel.appendChild(o);
        });
        sel.addEventListener('change', e => onChange(e.target.value));
        return sel;
    }

    // ── Palette rendering ─────────────────────────────────────
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
            header.addEventListener('click', () => {
                section.classList.toggle('collapsed');
            });

            const itemsWrap = document.createElement('div');
            itemsWrap.className = 'designer-palette-items';

            filtered.forEach(item => {
                const el = document.createElement('div');
                el.className = 'designer-node-template';
                el.draggable = true;
                el.dataset.nodeType = item.type;
                el.innerHTML = `
                    <div class="designer-node-template-icon" style="background:${item.iconBg};color:${item.iconColor}">
                        <i class="bi ${item.icon}"></i>
                    </div>
                    <span>${escHtml(item.label)}</span>`;

                el.addEventListener('dragstart', e => {
                    e.dataTransfer.setData('text/plain', item.type);
                    e.dataTransfer.effectAllowed = 'copy';
                });

                itemsWrap.appendChild(el);
            });

            section.appendChild(header);
            section.appendChild(itemsWrap);
            sectionsWrap.appendChild(section);
        });
    }

    // ── Meta fields (name, mission, domain) ───────────────────
    function bindMetaFields() {
        const nameInput    = document.getElementById('designerAgentName');
        const missionInput = document.getElementById('designerMission');
        const domainsRow   = document.getElementById('designerDomainsRow');

        if (nameInput) {
            nameInput.value = state.meta.name;
            nameInput.addEventListener('input', e => { state.meta.name = e.target.value; saveDraft(); });
        }
        if (missionInput) {
            missionInput.value = state.meta.mission;
            missionInput.addEventListener('input', e => { state.meta.mission = e.target.value; saveDraft(); });
        }

        if (domainsRow && window.STUDIO_DOMAINS) {
            domainsRow.innerHTML = '';
            window.STUDIO_DOMAINS.slice(0, 10).forEach(d => {
                const chip = document.createElement('div');
                chip.className = 'designer-domain-chip';
                chip.style.background = d.bg || '#f1f5f9';
                chip.style.color      = d.color || '#475569';
                chip.textContent      = (d.icon || '') + ' ' + (d.name || d.id);
                chip.dataset.domainId = d.id;
                if (state.meta.businessDomain === d.id) chip.classList.add('selected');
                chip.addEventListener('click', () => {
                    if (state.meta.businessDomain === d.id) {
                        state.meta.businessDomain = '';
                        chip.classList.remove('selected');
                    } else {
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

    // ── Drop onto canvas ──────────────────────────────────────
    function bindDropTarget() {
        canvasWrap.addEventListener('dragover', e => {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'copy';
        });

        canvasWrap.addEventListener('drop', e => {
            e.preventDefault();
            const type = e.dataTransfer.getData('text/plain');
            if (!type) return;
            const rect = getCanvasRect();
            const cp   = screenToCanvas(e.clientX - rect.left, e.clientY - rect.top);
            addNode(type, cp.x - NODE_W / 2, cp.y - NODE_H / 2);
        });
    }

    // ── Keyboard shortcuts ────────────────────────────────────
    function bindKeyboard() {
        document.addEventListener('keydown', e => {
            const inInput = e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA' || e.target.isContentEditable;
            if (inInput) return;

            if ((e.key === 'Delete' || e.key === 'Backspace') && selectedNodeIds.size > 0) {
                e.preventDefault();
                deleteSelectedNodes();
            }
            if (e.key === 'Escape') clearSelection();
            if (e.ctrlKey && e.key === 'a') {
                e.preventDefault();
                state.nodes.forEach(n => selectedNodeIds.add(n.id));
                renderNodes();
                renderPropertiesPanel();
            }
        });
    }

    // ── Toolbar buttons ───────────────────────────────────────
    function bindToolbar() {
        const bind = (id, fn) => {
            const el = document.getElementById(id);
            if (el) el.addEventListener('click', fn);
        };

        bind('designerBtnDeselect',     () => clearSelection());
        bind('designerBtnDelete',       () => deleteSelectedNodes());
        bind('designerBtnZoomIn',       () => zoomBy(0.15));
        bind('designerBtnZoomOut',      () => zoomBy(-0.15));
        bind('designerBtnFit',          () => fitView());
        bind('designerBtnClear',        () => {
            if (!confirm('Vider le canvas ? Cette action est irréversible.')) return;
            state.nodes = [];
            state.edges = [];
            selectedNodeIds.clear();
            selectedEdgeIds.clear();
            renderNodes();
            renderEdges();
            updateToolbarStats();
            updateMinimap();
            renderPropertiesPanel();
            saveDraft();
        });
        bind('designerBtnBlueprint',    () => showBlueprintReview());
    }

    // ── Validation ────────────────────────────────────────────
    function validate() {
        const errors = [];
        const warnings = [];

        if (state.nodes.length === 0) {
            errors.push('Le canvas doit contenir au moins 1 nœud.');
        }
        if (!state.meta.name && !state.meta.mission) {
            errors.push("Renseignez au moins le nom ou la mission de l'agent.");
        }
        if (state.nodes.length > 0 && state.edges.length === 0) {
            warnings.push('Aucune connexion entre les nœuds — workflow potentiellement déconnecté.');
        }

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
        if (errors.length > 0) {
            validationBanner.classList.add('error');
            validationBanner.innerHTML = `<i class="bi bi-exclamation-circle-fill"></i><div><ul>${errors.map(e => `<li>${escHtml(e)}</li>`).join('')}</ul></div>`;
        } else {
            validationBanner.classList.remove('error');
            validationBanner.innerHTML = `<i class="bi bi-exclamation-triangle-fill"></i><div><ul>${warnings.map(w => `<li>${escHtml(w)}</li>`).join('')}</ul></div>`;
        }
    }

    // ── Payload serialization ─────────────────────────────────
    function getDesignerPayload() {
        const sensors    = state.nodes.filter(n => n.type.startsWith('sensor')).map(n => n.type.replace('sensor-', ''));
        const skills     = state.nodes.filter(n => n.type.startsWith('skill')).map(n => n.type.replace('skill-', ''));
        const tools      = state.nodes.filter(n => n.type.startsWith('tool')).map(n => n.type.replace('tool-', ''));
        const actuators  = state.nodes.filter(n => n.type.startsWith('actuator')).map(n => n.type.replace('actuator-', ''));
        const decNode    = state.nodes.find(n => n.type.startsWith('decision'));
        const memNodes   = state.nodes.filter(n => n.type.startsWith('memory'));
        const trigNode   = state.nodes.find(n => n.type.startsWith('trigger'));

        return {
            schemaVersion:    3,
            creationMode:     'designer',
            mission:          state.meta.mission,
            businessDomain:   state.meta.businessDomain,
            agentName:        state.meta.name,
            sensors,
            sensorDetails:    state.nodes.filter(n => n.type.startsWith('sensor')).map(n => ({ label: n.label, config: n.config })),
            skills,
            skillDetails:     state.nodes.filter(n => n.type.startsWith('skill')).map(n => ({ label: n.label, config: n.config })),
            tools,
            toolDetails:      state.nodes.filter(n => n.type.startsWith('tool')).map(n => ({ label: n.label, config: n.config })),
            actuators,
            actuatorDetails:  state.nodes.filter(n => n.type.startsWith('actuator')).map(n => ({ label: n.label, config: n.config })),
            decision:         decNode ? { engine: decNode.type, label: decNode.label, config: decNode.config } : undefined,
            memory:           { types: memNodes.map(n => n.label) },
            trigger:          trigNode ? trigNode.type : undefined,
            designerWorkflow: state,
        };
    }

    function buildDesignerMessage() {
        const n = state.nodes.length;
        const e = state.edges.length;
        const typeList = [...new Set(state.nodes.map(nd => getNodeCategory(nd.type)))].join(', ');
        const sensors   = state.nodes.filter(nd => nd.type.startsWith('sensor')).map(nd => nd.label).join(', ') || '—';
        const skills    = state.nodes.filter(nd => nd.type.startsWith('skill')).map(nd => nd.label).join(', ')  || '—';
        const acts      = state.nodes.filter(nd => nd.type.startsWith('actuator')).map(nd => nd.label).join(', ') || '—';
        const dec       = state.nodes.filter(nd => nd.type.startsWith('decision')).map(nd => nd.label).join(', ')  || '—';
        const trig      = state.nodes.filter(nd => nd.type.startsWith('trigger')).map(nd => nd.label).join(', ')   || '—';

        return [
            'Créer un agent IA via Workflow Designer :',
            `- Nom : ${state.meta.name || '(non défini)'}`,
            `- Mission : ${state.meta.mission || '(non définie)'}`,
            `- Domaine : ${state.meta.businessDomain || '—'}`,
            `- Nœuds : ${n} nœud${n !== 1 ? 's' : ''} (${typeList || '—'})`,
            `- Connexions : ${e} connexion${e !== 1 ? 's' : ''}`,
            `- Déclencheur : ${trig}`,
            `- Sensors : ${sensors}`,
            `- Skills : ${skills}`,
            `- Actuators : ${acts}`,
            `- Decision : ${dec}`,
        ].join('\n');
    }

    // ── Blueprint review overlay ──────────────────────────────
    function showBlueprintReview() {
        if (!validate()) return;
        const overlay = document.getElementById('designerBlueprintOverlay');
        if (!overlay) return;
        renderDesignerBlueprint(overlay.querySelector('.designer-blueprint-modal-body'));
        overlay.classList.add('visible');
    }

    function renderDesignerBlueprint(container) {
        if (!container) return;
        container.innerHTML = '';

        // Meta grid
        const metaGrid = document.createElement('div');
        metaGrid.className = 'designer-bp-meta-grid';
        metaGrid.innerHTML = `
            <div class="designer-bp-meta-card">
                <div class="designer-bp-meta-label">Nom de l'agent</div>
                <div class="designer-bp-meta-value">${escHtml(state.meta.name || '(non défini)')}</div>
            </div>
            <div class="designer-bp-meta-card">
                <div class="designer-bp-meta-label">Domaine</div>
                <div class="designer-bp-meta-value">${escHtml(state.meta.businessDomain || '—')}</div>
            </div>
            <div class="designer-bp-meta-card" style="grid-column:1/-1">
                <div class="designer-bp-meta-label">Mission</div>
                <div class="designer-bp-meta-value" style="font-weight:400;font-size:0.85rem;">${escHtml(state.meta.mission || '(non définie)')}</div>
            </div>`;
        container.appendChild(metaGrid);

        // Simplified workflow row
        if (state.nodes.length > 0) {
            const wfSection = document.createElement('div');
            wfSection.innerHTML = '<div style="font-size:0.8rem;font-weight:700;color:#64748b;margin-bottom:8px;text-transform:uppercase;letter-spacing:0.04em;">Workflow visuel</div>';
            const wfRow = document.createElement('div');
            wfRow.className = 'designer-bp-workflow-row';

            // Build ordered list via topological sort if possible, else raw order
            const ordered = getWorkflowOrder();
            ordered.forEach((node, i) => {
                const cat = getCatalogueEntry(node.type);
                const nodeEl = document.createElement('div');
                nodeEl.className = 'designer-bp-workflow-node';
                nodeEl.innerHTML = `<i class="bi ${cat ? cat.icon : 'bi-box'}" style="color:${cat ? cat.iconColor : '#475569'}"></i> ${escHtml(node.label)}`;
                wfRow.appendChild(nodeEl);
                if (i < ordered.length - 1) {
                    const arrow = document.createElement('span');
                    arrow.className = 'designer-bp-workflow-arrow';
                    arrow.innerHTML = '<i class="bi bi-arrow-right"></i>';
                    wfRow.appendChild(arrow);
                }
            });
            wfSection.appendChild(wfRow);
            container.appendChild(wfSection);
        }

        // Nodes table
        const tableSection = document.createElement('div');
        tableSection.innerHTML = '<div style="font-size:0.8rem;font-weight:700;color:#64748b;margin-bottom:8px;text-transform:uppercase;letter-spacing:0.04em;">Détail des nœuds</div>';
        const table = document.createElement('table');
        table.className = 'designer-bp-nodes-table';
        table.innerHTML = `<thead><tr><th>Type</th><th>Nom</th><th>Configuré</th></tr></thead>`;
        const tbody = document.createElement('tbody');
        state.nodes.forEach(node => {
            const cat = getCatalogueEntry(node.type);
            const hasConfig = Object.values(node.config).some(v => v && v !== '');
            const fields = NODE_CONFIG_FIELDS[node.type] || [];
            const configured = fields.length === 0 || hasConfig;
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td><span class="designer-bp-workflow-node" style="width:fit-content"><i class="bi ${cat ? cat.icon : 'bi-box'}" style="color:${cat ? cat.iconColor : '#475569'}"></i> ${escHtml(getNodeCategory(node.type))}</span></td>
                <td>${escHtml(node.label)}</td>
                <td><span class="designer-bp-configured-badge ${configured ? 'yes' : 'no'}">${configured ? '✓ Oui' : '○ Non'}</span></td>`;
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);
        tableSection.appendChild(table);
        container.appendChild(tableSection);
    }

    function getWorkflowOrder() {
        if (state.nodes.length === 0) return [];
        // Build adjacency
        const inDeg = {};
        const adj   = {};
        state.nodes.forEach(n => { inDeg[n.id] = 0; adj[n.id] = []; });
        state.edges.forEach(e => {
            if (adj[e.from]) adj[e.from].push(e.to);
            if (e.to in inDeg) inDeg[e.to]++;
        });
        const queue = state.nodes.filter(n => inDeg[n.id] === 0);
        const result = [];
        while (queue.length) {
            const n = queue.shift();
            result.push(n);
            (adj[n.id] || []).forEach(id => {
                inDeg[id]--;
                if (inDeg[id] === 0) queue.push(state.nodes.find(nn => nn.id === id));
            });
        }
        return result.length === state.nodes.length ? result : state.nodes;
    }

    // ── Form submission hook ──────────────────────────────────
    function bindFormSubmit() {
        const form = document.getElementById('studioForm');
        if (!form) return;
        form.addEventListener('submit', e => {
            const modeInput = document.getElementById('creationModeInput');
            if (!modeInput || modeInput.value !== 'designer') return;
            e.preventDefault();
            if (!validate()) return;

            const payload = getDesignerPayload();
            const message = buildDesignerMessage();

            const hiddenWizard = document.getElementById('hiddenWizardJson');
            const hiddenMsg    = document.getElementById('hiddenMessage');
            const hiddenDW     = document.getElementById('hiddenDesignerWorkflow');

            if (hiddenWizard) hiddenWizard.value = JSON.stringify(payload);
            if (hiddenMsg)    hiddenMsg.value    = message;
            if (hiddenDW)     hiddenDW.value     = JSON.stringify(state);

            form.submit();
        });
    }

    // ── Mode selector ─────────────────────────────────────────
    function bindModeSelector() {
        const cards = document.querySelectorAll('.creation-mode-card');
        const modeInput = document.getElementById('creationModeInput');

        function applyMode(mode) {
            cards.forEach(c => c.classList.toggle('active', c.dataset.mode === mode));
            const wizardEl   = document.getElementById('wizardContainer');
            const designerEl = document.getElementById('designerContainer');
            if (wizardEl)   wizardEl.style.display   = mode === 'guided'   ? '' : 'none';
            if (designerEl) designerEl.classList.toggle('visible', mode === 'designer');
            if (modeInput)  modeInput.value = mode;
            try { localStorage.setItem(MODE_KEY, mode); } catch (_) {}
        }

        cards.forEach(card => {
            card.addEventListener('click', () => applyMode(card.dataset.mode));
        });

        // Restore from localStorage
        const saved = (() => { try { return localStorage.getItem(MODE_KEY); } catch (_) { return null; } })();
        applyMode(saved === 'designer' ? 'designer' : 'guided');
    }

    // ── Blueprint overlay close + submit ──────────────────────
    function bindBlueprintOverlay() {
        const closeBtn  = document.getElementById('designerBlueprintClose');
        const editBtn   = document.getElementById('designerBlueprintEdit');
        const submitBtn = document.getElementById('designerBlueprintSubmit');
        const overlay   = document.getElementById('designerBlueprintOverlay');

        if (closeBtn)  closeBtn.addEventListener('click',  () => overlay && overlay.classList.remove('visible'));
        if (editBtn)   editBtn.addEventListener('click',   () => overlay && overlay.classList.remove('visible'));
        if (submitBtn) submitBtn.addEventListener('click', () => {
            if (!overlay) return;
            overlay.classList.remove('visible');
            const form = document.getElementById('studioForm');
            if (!form) return;

            const payload = getDesignerPayload();
            const message = buildDesignerMessage();

            const hiddenWizard = document.getElementById('hiddenWizardJson');
            const hiddenMsg    = document.getElementById('hiddenMessage');
            const hiddenDW     = document.getElementById('hiddenDesignerWorkflow');

            if (hiddenWizard) hiddenWizard.value = JSON.stringify(payload);
            if (hiddenMsg)    hiddenMsg.value    = message;
            if (hiddenDW)     hiddenDW.value     = JSON.stringify(state);

            form.submit();
        });
    }

    // ── Palette search ────────────────────────────────────────
    function bindPaletteSearch() {
        const searchInput = document.getElementById('paletteSearchInput');
        if (!searchInput) return;
        searchInput.addEventListener('input', e => {
            palette_search = e.target.value.toLowerCase().trim();
            renderPalette('designerPaletteContainer');
        });
    }

    // ── SVG defs (arrowhead marker) ───────────────────────────
    function injectSvgDefs() {
        const defs = document.createElementNS('http://www.w3.org/2000/svg', 'defs');
        defs.innerHTML = `
            <marker id="arrowhead" markerWidth="8" markerHeight="6" refX="8" refY="3" orient="auto">
                <polygon points="0 0, 8 3, 0 6" class="edge-arrow" />
            </marker>`;
        canvasSvg.appendChild(defs);
    }

    // ── Utility ───────────────────────────────────────────────
    function escHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    // ── Init ──────────────────────────────────────────────────
    function init() {
        canvasWrap   = document.getElementById('designerCanvasWrap');
        canvasSvg    = document.getElementById('designerCanvasSvg');
        canvasNodes  = document.getElementById('designerCanvasNodes');
        minimap      = document.getElementById('designerMinimap');
        minimapSvg   = document.getElementById('designerMinimapSvg');
        propBody     = document.getElementById('designerPropBody');
        propEmpty    = document.getElementById('designerPropEmpty');
        toolbarStats = document.getElementById('designerToolbarStats');
        validationBanner = document.getElementById('designerValidationBanner');

        if (!canvasWrap || !canvasSvg || !canvasNodes) return;

        loadDraft();
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
        bindMetaFields();
        bindModeSelector();
        bindFormSubmit();
        bindBlueprintOverlay();

        renderPalette('designerPaletteContainer');
        renderNodes();
        renderEdges();
        updateToolbarStats();
        setTransform();
        updateMinimap();
        renderPropertiesPanel();
    }

    // Run after DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Expose for external use (e.g. form submit handler)
    window.AgentiaDesigner = {
        getPayload:            getDesignerPayload,
        buildMessage:          buildDesignerMessage,
        validate,
        showBlueprintReview,
        renderDesignerBlueprint,
    };
})();
