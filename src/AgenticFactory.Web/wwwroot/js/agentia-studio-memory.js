/* Agent Factory Studio — Runtime Agentic: Memory */
(function (global) {
    'use strict';

    const TYPES = [
        { id: 'session', label: 'Session', icon: 'bi-clock-history', desc: 'Contexte de la session en cours' },
        { id: 'user', label: 'User', icon: 'bi-person', desc: 'Préférences et historique utilisateur' },
        { id: 'project', label: 'Project', icon: 'bi-kanban', desc: 'Contexte projet / workspace' },
        { id: 'business', label: 'Business', icon: 'bi-building', desc: 'Règles et données métier persistantes' },
        { id: 'vector-db', label: 'Vector DB', icon: 'bi-diagram-2', desc: 'Embeddings et recherche sémantique' },
        { id: 'cache', label: 'Cache', icon: 'bi-lightning-charge', desc: 'Cache court terme pour performance' },
        { id: 'history', label: 'History', icon: 'bi-journal-text', desc: 'Journal des exécutions passées' }
    ];

    const field = (key, label, type, placeholder, extra) =>
        Object.assign({ key, label, type: type || 'text', placeholder: placeholder || '' }, extra || {});

    const CONFIG_FIELDS = {
        session: [
            field('ttlMinutes', 'TTL session (min)', 'number', '60'),
            field('maxMessages', 'Messages max', 'number', '50')
        ],
        user: [
            field('storage', 'Stockage', 'select', '', { options: ['Agentia DB', 'Redis', 'External'] })
        ],
        project: [
            field('projectId', 'ID projet', 'text', ''),
            field('sharedWithTeam', 'Partagé équipe', 'select', '', { options: ['Oui', 'Non'] })
        ],
        business: [
            field('domain', 'Domaine métier', 'text', 'Finance, RH…'),
            field('retentionDays', 'Rétention (jours)', 'number', '365')
        ],
        'vector-db': [
            field('provider', 'Provider', 'select', '', { options: ['Azure AI Search', 'Pinecone', 'Qdrant', 'pgvector'] }),
            field('indexName', 'Index', 'text', 'agent-knowledge'),
            field('connectionString', 'Connection', 'password', '', { secret: true })
        ],
        cache: [
            field('provider', 'Provider', 'select', '', { options: ['Redis', 'In-memory', 'Azure Cache'] }),
            field('ttlMinutes', 'TTL (min)', 'number', '15')
        ],
        history: [
            field('retentionDays', 'Rétention (jours)', 'number', '90'),
            field('includePayloads', 'Inclure payloads', 'select', '', { options: ['Métadonnées', 'Complet masqué', 'Complet'] })
        ]
    };

    const BY_ID = Object.fromEntries(TYPES.map(t => [t.id, t]));

    global.STUDIO_MEMORY_CATALOG = { types: TYPES, configFields: CONFIG_FIELDS, byId: BY_ID };
    global.getStudioMemoryType = id => BY_ID[id] || null;
    global.getStudioMemoryLabel = id => BY_ID[id]?.label || id;
    global.getStudioMemoryFields = typeId => CONFIG_FIELDS[typeId] || [];
})(window);
