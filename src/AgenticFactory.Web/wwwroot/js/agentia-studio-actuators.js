/* Agent Factory Studio — Runtime Agentic: Actuators (Act) */
(function (global) {
    'use strict';

    const CATEGORIES = [
        { id: 'communication', label: 'Communication', icon: 'bi-send' },
        { id: 'documents', label: 'Documents', icon: 'bi-file-earmark' },
        { id: 'data', label: 'Données & fichiers', icon: 'bi-database' },
        { id: 'integration', label: 'Intégration', icon: 'bi-plug' },
        { id: 'orchestration', label: 'Orchestration', icon: 'bi-diagram-3' }
    ];

    const field = (key, label, type, placeholder, extra) =>
        Object.assign({ key, label, type: type || 'text', placeholder: placeholder || '' }, extra || {});

    const ITEMS = [
        { id: 'send-email', label: 'Send email', icon: 'bi-envelope-paper', category: 'communication', fields: [
            field('to', 'Destinataires (template)', 'text', '{accounting@company.com}'),
            field('subject', 'Objet (template)', 'text', '[Agentia] {subject}'),
            field('attachOutput', 'Joindre résultat', 'select', '', { options: ['Oui', 'Non'] })
        ]},
        { id: 'notify-teams', label: 'Notify Teams', icon: 'bi-chat-dots', category: 'communication', fields: [
            field('channel', 'Canal / Webhook', 'text', ''),
            field('messageTemplate', 'Message', 'textarea', '⚠ {alert}\n{details}')
        ]},
        { id: 'notify-slack', label: 'Notify Slack', icon: 'bi-slack', category: 'communication', fields: [
            field('channel', 'Canal', 'text', '#agent-alerts'),
            field('messageTemplate', 'Message', 'textarea', '{summary}')
        ]},
        { id: 'create-ticket', label: 'Create ticket', icon: 'bi-ticket', category: 'integration', fields: [
            field('system', 'Système', 'select', '', { options: ['Jira', 'ServiceNow', 'Zendesk', 'Azure DevOps'] }),
            field('project', 'Projet / Queue', 'text', 'SUP'),
            field('priority', 'Priorité', 'select', '', { options: ['Basse', 'Moyenne', 'Haute', 'Critique'] })
        ]},
        { id: 'create-document', label: 'Create document', icon: 'bi-file-earmark-plus', category: 'documents', fields: [
            field('format', 'Format', 'select', '', { options: ['PDF', 'Word', 'HTML', 'Excel'] }),
            field('template', 'Template', 'text', 'template-entreprise'),
            field('outputPath', 'Chemin sortie', 'text', 'C:\\Agentia\\Out\\')
        ]},
        { id: 'create-report', label: 'Create report', icon: 'bi-file-bar-graph', category: 'documents', fields: [
            field('format', 'Format', 'select', '', { options: ['PDF', 'Word', 'HTML', 'Excel'] }),
            field('sections', 'Sections', 'textarea', 'Résumé\nKPI\nRecommandations')
        ]},
        { id: 'modify-sql', label: 'Modify SQL', icon: 'bi-database-gear', category: 'data', fields: [
            field('dbSource', 'Source DB', 'text', 'PostgreSQL, SQL Server…'),
            field('operation', 'Opération', 'select', '', { options: ['INSERT', 'UPDATE', 'UPSERT', 'DELETE'] }),
            field('table', 'Table', 'text', 'invoices'),
            field('sqlOrMapping', 'SQL / mapping', 'textarea', 'UPDATE … SET …')
        ]},
        { id: 'move-file', label: 'Move file', icon: 'bi-arrow-right-circle', category: 'data', fields: [
            field('destPath', 'Dossier destination', 'text', 'C:\\Agentia\\Done\\'),
            field('onConflict', 'Si conflit', 'select', '', { options: ['Renommer', 'Écraser', 'Ignorer'] })
        ]},
        { id: 'download', label: 'Download', icon: 'bi-download', category: 'data', fields: [
            field('urlPattern', 'URL / pattern', 'text', 'https://…'),
            field('destination', 'Dossier destination', 'text', 'C:\\Agentia\\Downloads')
        ]},
        { id: 'upload', label: 'Upload', icon: 'bi-upload', category: 'data', fields: [
            field('destination', 'Destination', 'text', 'SharePoint, S3, FTP…'),
            field('fileNameTemplate', 'Nom fichier', 'text', '{date}_{type}_{id}.pdf')
        ]},
        { id: 'call-api', label: 'Call API', icon: 'bi-braces', category: 'integration', fields: [
            field('method', 'Méthode HTTP', 'select', '', { options: ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'] }),
            field('endpoint', 'Endpoint', 'text', 'https://api.domaine.com/v1/resource'),
            field('bodyTemplate', 'Corps requête', 'textarea', '{"id":"{id}"}')
        ]},
        { id: 'create-task', label: 'Create task', icon: 'bi-check2-square', category: 'integration', fields: [
            field('assignTo', 'Assigner à', 'text', '{owner}'),
            field('dueInHours', 'Échéance (h)', 'number', '48'),
            field('priority', 'Priorité', 'select', '', { options: ['Basse', 'Normale', 'Haute'] })
        ]},
        { id: 'create-event', label: 'Create event', icon: 'bi-calendar-event', category: 'integration', fields: [
            field('calendar', 'Calendrier', 'text', 'Outlook, Google…'),
            field('titleTemplate', 'Titre', 'text', '[Agent] {summary}'),
            field('durationMin', 'Durée (min)', 'number', '30')
        ]},
        { id: 'launch-agent', label: 'Launch agent', icon: 'bi-robot', category: 'orchestration', fields: [
            field('agentId', 'Agent cible', 'text', 'uuid ou slug'),
            field('payload', 'Payload', 'textarea', '{"context":"{data}"}')
        ]},
        { id: 'trigger-workflow', label: 'Trigger workflow', icon: 'bi-lightning', category: 'orchestration', fields: [
            field('workflowRef', 'Workflow', 'select', '', { options: ['Power Automate', 'n8n', 'Agentia Flow'] }),
            field('triggerUrl', 'URL trigger', 'text', 'https://…'),
            field('payload', 'Payload', 'textarea', '{"correlationId":"{id}"}')
        ]}
    ];

    const BY_ID = Object.fromEntries(ITEMS.map(i => [i.id, i]));
    const LEGACY_MAP = {
        'envoyer-email': 'send-email', 'envoyer email': 'send-email',
        'creer-ticket': 'create-ticket', 'créer ticket': 'create-ticket',
        'notifier-teams': 'notify-teams', 'notifier teams': 'notify-teams',
        'notifier-slack': 'notify-slack', 'notifier slack': 'notify-slack',
        'appeler-api': 'call-api', 'appeler api': 'call-api',
        'maj-db': 'modify-sql', 'mettre à jour base': 'modify-sql',
        'deplacer-fichier': 'move-file', 'telecharger': 'download',
        'generer-rapport': 'create-report', 'assigner': 'create-task',
        'declencher-flow': 'trigger-workflow', 'planifier': 'create-event'
    };

    global.STUDIO_ACTUATOR_CATALOG = { categories: CATEGORIES, items: ITEMS, byId: BY_ID };
    global.getStudioActuator = id => BY_ID[id] || global.getStudioAction?.(id) || null;
    global.getStudioActuatorLabel = id => getStudioActuator(id)?.label || id;
    global.migrateActuatorId = val => {
        if (BY_ID[val]) return val;
        const k = val?.toLowerCase?.();
        if (LEGACY_MAP[k]) return LEGACY_MAP[k];
        if (global.migrateActionId) return migrateActionId(val);
        return val;
    };
})(window);
