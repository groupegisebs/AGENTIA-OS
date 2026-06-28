/* Agent Factory Studio — catalogue sources + schémas de connexion */
(function (global) {
    'use strict';

    const CATEGORIES = [
        { id: 'email', label: 'Email & messagerie', icon: 'bi-envelope' },
        { id: 'cloud', label: 'Cloud & stockage', icon: 'bi-cloud' },
        { id: 'fichier', label: 'Fichiers & dossiers', icon: 'bi-folder2-open' },
        { id: 'format', label: 'Formats documentaires', icon: 'bi-file-earmark' },
        { id: 'database', label: 'Bases de données', icon: 'bi-database' },
        { id: 'api', label: 'API & intégrations', icon: 'bi-plug' },
        { id: 'queue', label: 'Files & événements', icon: 'bi-lightning' },
        { id: 'saas', label: 'SaaS & CRM', icon: 'bi-building' }
    ];

    const field = (key, label, type, placeholder, extra) =>
        Object.assign({ key, label, type: type || 'text', placeholder: placeholder || '' }, extra || {});

    const ITEMS = [
        { id: 'outlook', label: 'Outlook / Microsoft 365', icon: 'bi-envelope', category: 'email', fields: [
            field('tenantId', 'Tenant ID (Azure AD)', 'text', 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'),
            field('clientId', 'Client ID (App Registration)', 'text', 'Application (client) ID'),
            field('clientSecret', 'Client Secret', 'password', '••••••••', { secret: true }),
            field('mailbox', 'Boîte aux lettres', 'text', 'agent@entreprise.com'),
            field('authMode', 'Mode d\'authentification', 'select', '', { options: ['OAuth2 délégué', 'OAuth2 application', 'Graph API'] })
        ]},
        { id: 'gmail', label: 'Gmail / Google Workspace', icon: 'bi-envelope-at', category: 'email', fields: [
            field('clientId', 'Client ID OAuth', 'text', 'Google Cloud Console'),
            field('clientSecret', 'Client Secret', 'password', '', { secret: true }),
            field('refreshToken', 'Refresh Token', 'password', '', { secret: true }),
            field('mailbox', 'Compte Gmail', 'text', 'agent@gmail.com')
        ]},
        { id: 'imap', label: 'IMAP', icon: 'bi-inbox', category: 'email', fields: [
            field('host', 'Serveur IMAP', 'text', 'imap.domaine.com'),
            field('port', 'Port', 'number', '993'),
            field('ssl', 'SSL/TLS', 'select', '', { options: ['SSL', 'STARTTLS', 'Aucun'] }),
            field('username', 'Utilisateur', 'text', 'user@domaine.com'),
            field('password', 'Mot de passe / App Password', 'password', '', { secret: true }),
            field('folder', 'Dossier', 'text', 'INBOX')
        ]},
        { id: 'smtp', label: 'SMTP', icon: 'bi-send', category: 'email', fields: [
            field('host', 'Serveur SMTP', 'text', 'smtp.domaine.com'),
            field('port', 'Port', 'number', '587'),
            field('security', 'Sécurité', 'select', '', { options: ['TLS', 'SSL', 'Aucune'] }),
            field('username', 'Utilisateur SMTP', 'text', 'smtp-user'),
            field('password', 'Mot de passe SMTP', 'password', '', { secret: true }),
            field('fromAddress', 'Expéditeur', 'text', 'noreply@domaine.com')
        ]},
        { id: 'exchange-ews', label: 'Exchange EWS', icon: 'bi-envelope-check', category: 'email', fields: [
            field('ewsUrl', 'URL EWS', 'text', 'https://mail.domaine.com/EWS/Exchange.asmx'),
            field('username', 'Utilisateur', 'text', 'DOMAIN\\user'),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},

        { id: 'sharepoint', label: 'SharePoint', icon: 'bi-share', category: 'cloud', fields: [
            field('siteUrl', 'URL du site', 'text', 'https://tenant.sharepoint.com/sites/projet'),
            field('library', 'Bibliothèque', 'text', 'Documents'),
            field('tenantId', 'Tenant ID', 'text', ''),
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
        { id: 'dropbox', label: 'Dropbox', icon: 'bi-dropbox', category: 'cloud', fields: [
            field('accessToken', 'Access Token', 'password', '', { secret: true }),
            field('rootPath', 'Chemin racine', 'text', '/Inbox')
        ]},
        { id: 'azure-blob', label: 'Azure Blob Storage', icon: 'bi-hdd-stack', category: 'cloud', fields: [
            field('connectionString', 'Connection String', 'password', '', { secret: true }),
            field('container', 'Conteneur', 'text', 'agent-data'),
            field('prefix', 'Préfixe', 'text', 'incoming/')
        ]},
        { id: 'aws-s3', label: 'AWS S3', icon: 'bi-bucket', category: 'cloud', fields: [
            field('accessKeyId', 'Access Key ID', 'text', 'AKIA...'),
            field('secretAccessKey', 'Secret Access Key', 'password', '', { secret: true }),
            field('bucket', 'Bucket', 'text', ''),
            field('region', 'Région', 'text', 'eu-west-1')
        ]},
        { id: 'ftp', label: 'FTP', icon: 'bi-hdd-network', category: 'cloud', fields: [
            field('host', 'Hôte', 'text', 'ftp.domaine.com'),
            field('port', 'Port', 'number', '21'),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true }),
            field('remotePath', 'Chemin distant', 'text', '/incoming')
        ]},
        { id: 'sftp', label: 'SFTP / SSH', icon: 'bi-terminal', category: 'cloud', fields: [
            field('host', 'Hôte SFTP', 'text', ''),
            field('port', 'Port', 'number', '22'),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true }),
            field('privateKey', 'Clé privée SSH', 'textarea', '', { secret: true }),
            field('remotePath', 'Chemin distant', 'text', '/data/in')
        ]},

        { id: 'windows-folder', label: 'Dossier Windows', icon: 'bi-folder', category: 'fichier', fields: [
            field('path', 'Chemin local', 'text', 'C:\\Agentia\\Inbox'),
            field('pattern', 'Masque fichiers', 'text', '*.*'),
            field('watchSubfolders', 'Sous-dossiers', 'select', '', { options: ['Oui', 'Non'] })
        ]},
        { id: 'unc-path', label: 'Partage réseau (UNC)', icon: 'bi-hdd-network', category: 'fichier', fields: [
            field('uncPath', 'Chemin UNC', 'text', '\\\\serveur\\partage'),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true }),
            field('pattern', 'Masque', 'text', '*.pdf')
        ]},
        { id: 'linux-path', label: 'Chemin Linux', icon: 'bi-terminal', category: 'fichier', fields: [
            field('path', 'Chemin absolu', 'text', '/var/agentia/inbox'),
            field('pattern', 'Masque', 'text', '*.*')
        ]},
        { id: 'upload-manuel', label: 'Upload manuel', icon: 'bi-upload', category: 'fichier', fields: [
            field('maxSizeMb', 'Taille max (Mo)', 'number', '50'),
            field('allowedExtensions', 'Extensions', 'text', 'pdf,docx,xlsx,csv')
        ]},

        { id: 'pdf', label: 'PDF', icon: 'bi-file-pdf', category: 'format', fields: [
            field('sourceRef', 'Source parente', 'text', 'Dossier, email, SharePoint…'),
            field('ocrEnabled', 'OCR', 'select', '', { options: ['Oui', 'Non'] })
        ]},
        { id: 'word', label: 'Word (.docx)', icon: 'bi-file-word', category: 'format', fields: [
            field('sourceRef', 'Source parente', 'text', '')
        ]},
        { id: 'excel', label: 'Excel (.xlsx)', icon: 'bi-file-excel', category: 'format', fields: [
            field('sourceRef', 'Source parente', 'text', ''),
            field('sheetName', 'Feuille', 'text', 'Sheet1')
        ]},
        { id: 'csv', label: 'CSV', icon: 'bi-filetype-csv', category: 'format', fields: [
            field('sourceRef', 'Source parente', 'text', ''),
            field('delimiter', 'Séparateur', 'select', '', { options: [';', ',', '|', 'Tab'] }),
            field('encoding', 'Encodage', 'select', '', { options: ['UTF-8', 'ISO-8859-1', 'Windows-1252'] })
        ]},
        { id: 'json', label: 'JSON', icon: 'bi-filetype-json', category: 'format', fields: [
            field('sourceRef', 'Source parente', 'text', ''),
            field('jsonPath', 'JSONPath', 'text', '$.data')
        ]},
        { id: 'xml', label: 'XML', icon: 'bi-filetype-xml', category: 'format', fields: [
            field('sourceRef', 'Source parente', 'text', ''),
            field('xpath', 'XPath', 'text', '/root/items')
        ]},
        { id: 'zip', label: 'Archives ZIP', icon: 'bi-file-zip', category: 'format', fields: [
            field('sourceRef', 'Source parente', 'text', ''),
            field('password', 'Mot de passe archive', 'password', '', { secret: true })
        ]},
        { id: 'images', label: 'Images', icon: 'bi-image', category: 'format', fields: [
            field('sourceRef', 'Source parente', 'text', ''),
            field('ocrEnabled', 'OCR', 'select', '', { options: ['Oui', 'Non'] })
        ]},

        { id: 'sqlserver', label: 'SQL Server', icon: 'bi-database', category: 'database', fields: [
            field('server', 'Serveur', 'text', 'localhost\\SQLEXPRESS'),
            field('database', 'Base', 'text', 'AgentiaDB'),
            field('authType', 'Auth', 'select', '', { options: ['SQL Server', 'Windows intégrée'] }),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true }),
            field('connectionString', 'Connection String', 'password', '', { secret: true })
        ]},
        { id: 'postgresql', label: 'PostgreSQL', icon: 'bi-database-fill', category: 'database', fields: [
            field('host', 'Hôte', 'text', 'localhost'),
            field('port', 'Port', 'number', '5432'),
            field('database', 'Base', 'text', 'agentia'),
            field('username', 'Utilisateur', 'text', 'postgres'),
            field('password', 'Mot de passe', 'password', '', { secret: true }),
            field('sslMode', 'SSL', 'select', '', { options: ['disable', 'require', 'verify-full'] })
        ]},
        { id: 'mysql', label: 'MySQL / MariaDB', icon: 'bi-database', category: 'database', fields: [
            field('host', 'Hôte', 'text', 'localhost'),
            field('port', 'Port', 'number', '3306'),
            field('database', 'Base', 'text', ''),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},
        { id: 'oracle', label: 'Oracle', icon: 'bi-database', category: 'database', fields: [
            field('connectionString', 'Connection String / TNS', 'password', '', { secret: true })
        ]},
        { id: 'mongodb', label: 'MongoDB', icon: 'bi-database', category: 'database', fields: [
            field('connectionString', 'URI MongoDB', 'password', '', { secret: true }),
            field('database', 'Base', 'text', ''),
            field('collection', 'Collection', 'text', '')
        ]},
        { id: 'sqlite', label: 'SQLite', icon: 'bi-file-binary', category: 'database', fields: [
            field('filePath', 'Fichier .db', 'text', 'C:\\data\\agentia.db')
        ]},
        { id: 'redis', label: 'Redis', icon: 'bi-database', category: 'database', fields: [
            field('host', 'Hôte', 'text', 'localhost'),
            field('port', 'Port', 'number', '6379'),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},

        { id: 'rest-api', label: 'API REST', icon: 'bi-braces', category: 'api', fields: [
            field('baseUrl', 'URL de base', 'text', 'https://api.domaine.com/v1'),
            field('authType', 'Authentification', 'select', '', { options: ['Aucune', 'Bearer Token', 'API Key', 'Basic', 'OAuth2 Client Credentials'] }),
            field('apiKey', 'API Key / Token', 'password', '', { secret: true }),
            field('apiKeyHeader', 'En-tête API Key', 'text', 'X-API-Key'),
            field('username', 'Utilisateur (Basic)', 'text', ''),
            field('password', 'Mot de passe (Basic)', 'password', '', { secret: true }),
            field('clientId', 'Client ID OAuth2', 'text', ''),
            field('clientSecret', 'Client Secret OAuth2', 'password', '', { secret: true })
        ]},
        { id: 'graphql', label: 'GraphQL', icon: 'bi-diagram-3', category: 'api', fields: [
            field('endpoint', 'Endpoint', 'text', 'https://api.domaine.com/graphql'),
            field('bearerToken', 'Bearer Token', 'password', '', { secret: true })
        ]},
        { id: 'webhook-in', label: 'Webhook entrant', icon: 'bi-link-45deg', category: 'api', fields: [
            field('secret', 'Secret signature', 'password', '', { secret: true }),
            field('allowedIps', 'IP autorisées', 'text', '')
        ]},
        { id: 'soap', label: 'SOAP / WSDL', icon: 'bi-code-square', category: 'api', fields: [
            field('wsdlUrl', 'URL WSDL', 'text', ''),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},

        { id: 'kafka', label: 'Apache Kafka', icon: 'bi-lightning', category: 'queue', fields: [
            field('bootstrapServers', 'Bootstrap servers', 'text', 'kafka:9092'),
            field('topic', 'Topic', 'text', 'agent-events'),
            field('groupId', 'Consumer Group', 'text', 'agentia-group'),
            field('saslPassword', 'SASL Password', 'password', '', { secret: true })
        ]},
        { id: 'rabbitmq', label: 'RabbitMQ', icon: 'bi-envelope-arrow-down', category: 'queue', fields: [
            field('host', 'Hôte', 'text', 'localhost'),
            field('port', 'Port', 'number', '5672'),
            field('username', 'Utilisateur', 'text', 'guest'),
            field('password', 'Mot de passe', 'password', '', { secret: true }),
            field('queue', 'Queue', 'text', 'agentia.in')
        ]},
        { id: 'azure-service-bus', label: 'Azure Service Bus', icon: 'bi-cloud-arrow-down', category: 'queue', fields: [
            field('connectionString', 'Connection String', 'password', '', { secret: true }),
            field('queueOrTopic', 'Queue / Topic', 'text', '')
        ]},
        { id: 'teams', label: 'Microsoft Teams', icon: 'bi-microsoft-teams', category: 'queue', fields: [
            field('webhookUrl', 'Webhook URL', 'password', '', { secret: true }),
            field('clientId', 'Client ID Bot', 'text', ''),
            field('clientSecret', 'Client Secret', 'password', '', { secret: true })
        ]},
        { id: 'slack', label: 'Slack', icon: 'bi-slack', category: 'queue', fields: [
            field('botToken', 'Bot Token', 'password', '', { secret: true }),
            field('channelId', 'Canal ID', 'text', '')
        ]},

        { id: 'salesforce', label: 'Salesforce', icon: 'bi-cloud-check', category: 'saas', fields: [
            field('instanceUrl', 'Instance URL', 'text', 'https://login.salesforce.com'),
            field('clientId', 'Consumer Key', 'text', ''),
            field('clientSecret', 'Consumer Secret', 'password', '', { secret: true }),
            field('username', 'Utilisateur API', 'text', ''),
            field('password', 'Mot de passe + Token', 'password', '', { secret: true })
        ]},
        { id: 'dynamics365', label: 'Dynamics 365', icon: 'bi-microsoft', category: 'saas', fields: [
            field('orgUrl', 'URL org', 'text', ''),
            field('tenantId', 'Tenant ID', 'text', ''),
            field('clientId', 'Client ID', 'text', ''),
            field('clientSecret', 'Client Secret', 'password', '', { secret: true })
        ]},
        { id: 'hubspot', label: 'HubSpot', icon: 'bi-globe', category: 'saas', fields: [
            field('apiKey', 'Private App Token', 'password', '', { secret: true })
        ]},
        { id: 'sap', label: 'SAP', icon: 'bi-boxes', category: 'saas', fields: [
            field('host', 'Application Server', 'text', ''),
            field('client', 'Client', 'text', '100'),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},
        { id: 'elasticsearch', label: 'Elasticsearch', icon: 'bi-search', category: 'saas', fields: [
            field('url', 'Cluster URL', 'text', 'https://es:9200'),
            field('apiKey', 'API Key', 'password', '', { secret: true }),
            field('index', 'Index', 'text', 'logs-*')
        ]},
        { id: 'snowflake', label: 'Snowflake', icon: 'bi-snow', category: 'saas', fields: [
            field('account', 'Account', 'text', ''),
            field('warehouse', 'Warehouse', 'text', ''),
            field('username', 'Utilisateur', 'text', ''),
            field('password', 'Mot de passe', 'password', '', { secret: true })
        ]},
        { id: 'bigquery', label: 'Google BigQuery', icon: 'bi-google', category: 'saas', fields: [
            field('projectId', 'Project ID', 'text', ''),
            field('dataset', 'Dataset', 'text', ''),
            field('serviceAccount', 'Service Account JSON', 'textarea', '', { secret: true })
        ]}
    ];

    const BY_ID = Object.fromEntries(ITEMS.map(i => [i.id, i]));
    const LABEL_TO_ID = Object.fromEntries(ITEMS.flatMap(i => [[i.label.toLowerCase(), i.id], [i.id, i.id]]));

    global.STUDIO_SOURCE_CATALOG = { categories: CATEGORIES, items: ITEMS, byId: BY_ID };
    global.getStudioSource = id => BY_ID[id] || null;
    global.getStudioSourceLabel = id => BY_ID[id]?.label || id;
    global.migrateSourceId = val => {
        if (BY_ID[val]) return val;
        const k = val?.toLowerCase?.();
        if (k === 'sql server') return 'sqlserver';
        if (k === 'api rest') return 'rest-api';
        if (k === 'webhook') return 'webhook-in';
        if (k === 'dossier windows') return 'windows-folder';
        if (k === 'google drive') return 'google-drive';
        return LABEL_TO_ID[k] || val;
    };
})(window);
