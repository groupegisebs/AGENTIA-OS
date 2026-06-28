/* Agent Factory Studio — Runtime Agentic: Tools (Act capabilities) */
(function (global) {
    'use strict';

    const CATEGORIES = [
        { id: 'productivity', label: 'Productivité', icon: 'bi-envelope' },
        { id: 'enterprise', label: 'ERP & CRM', icon: 'bi-building' },
        { id: 'devops', label: 'Dev & automation', icon: 'bi-code-slash' },
        { id: 'infra', label: 'Infrastructure', icon: 'bi-server' }
    ];

    const field = (key, label, type, placeholder, extra) =>
        Object.assign({ key, label, type: type || 'text', placeholder: placeholder || '' }, extra || {});

    const ITEMS = [
        { id: 'outlook-tool', label: 'Outlook', icon: 'bi-envelope', category: 'productivity', fields: [
            field('tenantId', 'Tenant ID', 'text', ''),
            field('clientId', 'Client ID', 'text', ''),
            field('clientSecret', 'Client Secret', 'password', '', { secret: true })
        ]},
        { id: 'teams', label: 'Teams', icon: 'bi-microsoft-teams', category: 'productivity', fields: [
            field('webhookUrl', 'Webhook URL', 'password', '', { secret: true }),
            field('clientId', 'Client ID Bot', 'text', '')
        ]},
        { id: 'slack', label: 'Slack', icon: 'bi-slack', category: 'productivity', fields: [
            field('botToken', 'Bot Token', 'password', '', { secret: true }),
            field('channelId', 'Canal ID', 'text', '')
        ]},
        { id: 'smtp-tool', label: 'SMTP', icon: 'bi-send', category: 'productivity', fields: [
            field('host', 'Serveur SMTP', 'text', 'smtp.domaine.com'),
            field('port', 'Port', 'number', '587'),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},
        { id: 'sap', label: 'SAP', icon: 'bi-boxes', category: 'enterprise', fields: [
            field('host', 'Application Server', 'text', ''),
            field('client', 'Client', 'text', '100'),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},
        { id: 'dynamics365', label: 'Dynamics 365', icon: 'bi-microsoft', category: 'enterprise', fields: [
            field('orgUrl', 'URL org', 'text', ''),
            field('tenantId', 'Tenant ID', 'text', ''),
            field('clientSecret', 'Client Secret', 'password', '', { secret: true })
        ]},
        { id: 'cognidoc', label: 'CogniDoc', icon: 'bi-file-earmark-text', category: 'enterprise', fields: [
            field('apiUrl', 'URL API', 'text', 'https://api.cognidoc.com'),
            field('apiKey', 'API Key', 'password', '', { secret: true })
        ]},
        { id: 'quickbooks', label: 'QuickBooks', icon: 'bi-receipt', category: 'enterprise', fields: [
            field('realmId', 'Realm ID', 'text', ''),
            field('clientId', 'Client ID', 'text', ''),
            field('clientSecret', 'Client Secret', 'password', '', { secret: true })
        ]},
        { id: 'sql-tool', label: 'SQL', icon: 'bi-database', category: 'enterprise', fields: [
            field('connectionString', 'Connection String', 'password', '', { secret: true })
        ]},
        { id: 'python', label: 'Python', icon: 'bi-filetype-py', category: 'devops', fields: [
            field('runtime', 'Runtime', 'select', '', { options: ['3.11', '3.12', '3.13'] }),
            field('venvPath', 'Environnement virtuel', 'text', '')
        ]},
        { id: 'powershell', label: 'PowerShell', icon: 'bi-terminal', category: 'devops', fields: [
            field('version', 'Version', 'select', '', { options: ['5.1', '7.x'] }),
            field('executionPolicy', 'Execution Policy', 'select', '', { options: ['RemoteSigned', 'Bypass', 'Restricted'] })
        ]},
        { id: 'rest-tool', label: 'REST', icon: 'bi-braces', category: 'devops', fields: [
            field('baseUrl', 'URL de base', 'text', 'https://api.domaine.com'),
            field('apiKey', 'API Key', 'password', '', { secret: true })
        ]},
        { id: 'ftp-tool', label: 'FTP', icon: 'bi-hdd-network', category: 'devops', fields: [
            field('host', 'Hôte', 'text', ''),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},
        { id: 'docker-tool', label: 'Docker', icon: 'bi-box', category: 'infra', fields: [
            field('registry', 'Registry', 'text', 'docker.io'),
            field('image', 'Image', 'text', 'agentia/tools:latest')
        ]},
        { id: 'github', label: 'GitHub', icon: 'bi-github', category: 'devops', fields: [
            field('org', 'Organisation', 'text', ''),
            field('token', 'Personal Access Token', 'password', '', { secret: true })
        ]},
        { id: 'azure-devops', label: 'Azure DevOps', icon: 'bi-cloud', category: 'devops', fields: [
            field('orgUrl', 'Organisation URL', 'text', 'https://dev.azure.com/org'),
            field('pat', 'PAT', 'password', '', { secret: true })
        ]},
        { id: 'power-automate', label: 'Power Automate', icon: 'bi-lightning', category: 'devops', fields: [
            field('flowUrl', 'URL HTTP trigger', 'text', 'https://prod-…')
        ]},
        { id: 'n8n', label: 'n8n', icon: 'bi-diagram-3', category: 'devops', fields: [
            field('webhookUrl', 'Webhook URL', 'text', 'https://n8n.domaine.com/webhook/…'),
            field('apiKey', 'API Key', 'password', '', { secret: true })
        ]}
    ];

    const BY_ID = Object.fromEntries(ITEMS.map(i => [i.id, i]));

    global.STUDIO_TOOL_CATALOG = { categories: CATEGORIES, items: ITEMS, byId: BY_ID };
    global.getStudioTool = id => BY_ID[id] || null;
    global.getStudioToolLabel = id => BY_ID[id]?.label || id;
})(window);
