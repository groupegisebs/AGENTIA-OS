/* Agent Factory Studio — Runtime Agentic: Sensors (Observe) */
(function (global) {
    'use strict';

    const CATEGORIES = [
        { id: 'email', label: 'Email', icon: 'bi-envelope' },
        { id: 'documents', label: 'Documents', icon: 'bi-file-earmark-text' },
        { id: 'database', label: 'Bases de données', icon: 'bi-database' },
        { id: 'cloud', label: 'Cloud & stockage', icon: 'bi-cloud' },
        { id: 'api', label: 'API & intégrations', icon: 'bi-plug' },
        { id: 'filesystem', label: 'Filesystem', icon: 'bi-folder2-open' }
    ];

    const field = (key, label, type, placeholder, extra) =>
        Object.assign({ key, label, type: type || 'text', placeholder: placeholder || '' }, extra || {});

    const ITEMS = [
        { id: 'outlook', label: 'Outlook / Microsoft 365', icon: 'bi-envelope', category: 'email', fields: [
            field('tenantId', 'Tenant ID', 'text', 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'),
            field('clientId', 'Client ID', 'text', 'Application (client) ID'),
            field('clientSecret', 'Client Secret', 'password', '', { secret: true }),
            field('mailbox', 'Boîte aux lettres', 'text', 'agent@entreprise.com')
        ]},
        { id: 'gmail', label: 'Gmail / Google Workspace', icon: 'bi-envelope-at', category: 'email', fields: [
            field('clientId', 'Client ID OAuth', 'text', ''),
            field('clientSecret', 'Client Secret', 'password', '', { secret: true }),
            field('refreshToken', 'Refresh Token', 'password', '', { secret: true }),
            field('mailbox', 'Compte Gmail', 'text', 'agent@gmail.com')
        ]},
        { id: 'exchange-ews', label: 'Exchange EWS', icon: 'bi-envelope-check', category: 'email', fields: [
            field('ewsUrl', 'URL EWS', 'text', 'https://mail.domaine.com/EWS/Exchange.asmx'),
            field('username', 'Utilisateur', 'text', 'DOMAIN\\user'),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},
        { id: 'imap', label: 'IMAP', icon: 'bi-inbox', category: 'email', fields: [
            field('host', 'Serveur IMAP', 'text', 'imap.domaine.com'),
            field('port', 'Port', 'number', '993'),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true }),
            field('folder', 'Dossier', 'text', 'INBOX')
        ]},

        { id: 'pdf', label: 'PDF', icon: 'bi-file-pdf', category: 'documents', fields: [
            field('sourceRef', 'Source parente', 'text', 'Email, dossier, cloud…'),
            field('ocrEnabled', 'OCR', 'select', '', { options: ['Oui', 'Non'] })
        ]},
        { id: 'word', label: 'Word (.docx)', icon: 'bi-file-word', category: 'documents', fields: [
            field('sourceRef', 'Source parente', 'text', '')
        ]},
        { id: 'excel', label: 'Excel (.xlsx)', icon: 'bi-file-excel', category: 'documents', fields: [
            field('sourceRef', 'Source parente', 'text', ''),
            field('sheetName', 'Feuille', 'text', 'Sheet1')
        ]},
        { id: 'images', label: 'Images', icon: 'bi-image', category: 'documents', fields: [
            field('sourceRef', 'Source parente', 'text', ''),
            field('formats', 'Formats', 'text', 'jpg,png,tiff')
        ]},
        { id: 'audio', label: 'Audio', icon: 'bi-mic', category: 'documents', fields: [
            field('sourceRef', 'Source parente', 'text', ''),
            field('formats', 'Formats', 'text', 'mp3,wav,ogg')
        ]},
        { id: 'video', label: 'Vidéo', icon: 'bi-camera-video', category: 'documents', fields: [
            field('sourceRef', 'Source parente', 'text', ''),
            field('formats', 'Formats', 'text', 'mp4,avi,mov')
        ]},

        { id: 'sqlserver', label: 'SQL Server', icon: 'bi-database', category: 'database', fields: [
            field('server', 'Serveur', 'text', 'localhost\\SQLEXPRESS'),
            field('database', 'Base', 'text', 'AgentiaDB'),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},
        { id: 'postgresql', label: 'PostgreSQL', icon: 'bi-database-fill', category: 'database', fields: [
            field('host', 'Hôte', 'text', 'localhost'),
            field('port', 'Port', 'number', '5432'),
            field('database', 'Base', 'text', 'agentia'),
            field('username', 'Utilisateur', 'text', 'postgres'),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},
        { id: 'oracle', label: 'Oracle', icon: 'bi-database', category: 'database', fields: [
            field('connectionString', 'Connection String / TNS', 'password', '', { secret: true })
        ]},
        { id: 'mysql', label: 'MySQL / MariaDB', icon: 'bi-database', category: 'database', fields: [
            field('host', 'Hôte', 'text', 'localhost'),
            field('port', 'Port', 'number', '3306'),
            field('database', 'Base', 'text', ''),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},

        { id: 'sharepoint', label: 'SharePoint', icon: 'bi-share', category: 'cloud', fields: [
            field('siteUrl', 'URL du site', 'text', 'https://tenant.sharepoint.com/sites/projet'),
            field('library', 'Bibliothèque', 'text', 'Documents'),
            field('clientId', 'Client ID', 'text', ''),
            field('clientSecret', 'Client Secret', 'password', '', { secret: true })
        ]},
        { id: 'onedrive', label: 'OneDrive', icon: 'bi-cloud', category: 'cloud', fields: [
            field('tenantId', 'Tenant ID', 'text', ''),
            field('clientId', 'Client ID', 'text', ''),
            field('clientSecret', 'Client Secret', 'password', '', { secret: true }),
            field('drivePath', 'Chemin', 'text', '/Agents/Incoming')
        ]},
        { id: 'google-drive', label: 'Google Drive', icon: 'bi-cloud-arrow-up', category: 'cloud', fields: [
            field('serviceAccount', 'Compte de service (JSON)', 'textarea', '{ ... }', { secret: true }),
            field('folderId', 'ID dossier', 'text', '')
        ]},
        { id: 'azure-blob', label: 'Azure Blob Storage', icon: 'bi-hdd-stack', category: 'cloud', fields: [
            field('connectionString', 'Connection String', 'password', '', { secret: true }),
            field('container', 'Conteneur', 'text', 'agent-data')
        ]},
        { id: 'dropbox', label: 'Dropbox', icon: 'bi-dropbox', category: 'cloud', fields: [
            field('accessToken', 'Access Token', 'password', '', { secret: true }),
            field('rootPath', 'Chemin racine', 'text', '/Inbox')
        ]},

        { id: 'rest-api', label: 'REST', icon: 'bi-braces', category: 'api', fields: [
            field('baseUrl', 'URL de base', 'text', 'https://api.domaine.com/v1'),
            field('authType', 'Authentification', 'select', '', { options: ['Aucune', 'Bearer Token', 'API Key', 'OAuth2'] }),
            field('apiKey', 'API Key / Token', 'password', '', { secret: true })
        ]},
        { id: 'graphql', label: 'GraphQL', icon: 'bi-diagram-3', category: 'api', fields: [
            field('endpoint', 'Endpoint', 'text', 'https://api.domaine.com/graphql'),
            field('bearerToken', 'Bearer Token', 'password', '', { secret: true })
        ]},
        { id: 'webhook-in', label: 'Webhook entrant', icon: 'bi-link-45deg', category: 'api', fields: [
            field('secret', 'Secret signature', 'password', '', { secret: true }),
            field('allowedIps', 'IP autorisées', 'text', '')
        ]},
        { id: 'ftp', label: 'FTP / SFTP', icon: 'bi-hdd-network', category: 'api', fields: [
            field('host', 'Hôte', 'text', 'ftp.domaine.com'),
            field('port', 'Port', 'number', '21'),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true }),
            field('remotePath', 'Chemin distant', 'text', '/incoming')
        ]},
        { id: 'mqtt', label: 'MQTT', icon: 'bi-broadcast', category: 'api', fields: [
            field('broker', 'Broker URL', 'text', 'mqtt://broker:1883'),
            field('topic', 'Topic', 'text', 'agent/events'),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},
        { id: 'rabbitmq', label: 'RabbitMQ', icon: 'bi-envelope-arrow-down', category: 'api', fields: [
            field('host', 'Hôte', 'text', 'localhost'),
            field('port', 'Port', 'number', '5672'),
            field('queue', 'Queue', 'text', 'agentia.in'),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},
        { id: 'kafka', label: 'Apache Kafka', icon: 'bi-lightning', category: 'api', fields: [
            field('bootstrapServers', 'Bootstrap servers', 'text', 'kafka:9092'),
            field('topic', 'Topic', 'text', 'agent-events'),
            field('groupId', 'Consumer Group', 'text', 'agentia-group')
        ]},
        { id: 'azure-service-bus', label: 'Azure Service Bus', icon: 'bi-cloud-arrow-down', category: 'api', fields: [
            field('connectionString', 'Connection String', 'password', '', { secret: true }),
            field('queueOrTopic', 'Queue / Topic', 'text', '')
        ]},

        { id: 'windows-folder', label: 'Dossier Windows', icon: 'bi-folder', category: 'filesystem', fields: [
            field('path', 'Chemin local', 'text', 'C:\\Agentia\\Inbox'),
            field('pattern', 'Masque fichiers', 'text', '*.*'),
            field('watchSubfolders', 'Sous-dossiers', 'select', '', { options: ['Oui', 'Non'] })
        ]},
        { id: 'unc-path', label: 'Partage réseau (NAS)', icon: 'bi-hdd-network', category: 'filesystem', fields: [
            field('uncPath', 'Chemin UNC', 'text', '\\\\serveur\\partage'),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true }),
            field('pattern', 'Masque', 'text', '*.pdf')
        ]}
    ];

    const BY_ID = Object.fromEntries(ITEMS.map(i => [i.id, i]));

    global.STUDIO_SENSOR_CATALOG = { categories: CATEGORIES, items: ITEMS, byId: BY_ID };
    global.getStudioSensor = id => BY_ID[id] || global.getStudioSource?.(id) || null;
    global.getStudioSensorLabel = id => getStudioSensor(id)?.label || id;
    global.migrateSensorId = val => {
        if (BY_ID[val]) return val;
        if (global.migrateSourceId) return migrateSourceId(val);
        return val;
    };
})(window);
