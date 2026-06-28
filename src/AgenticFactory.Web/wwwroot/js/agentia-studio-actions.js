/* Agent Factory Studio — catalogue actions + paramètres */
(function (global) {
    'use strict';

    const CATEGORIES = [
        { id: 'lecture', label: 'Lecture & collecte', icon: 'bi-eye' },
        { id: 'extraction', label: 'Extraction & analyse', icon: 'bi-box-arrow-down' },
        { id: 'transformation', label: 'Transformation', icon: 'bi-arrow-repeat' },
        { id: 'generation', label: 'Génération de contenu', icon: 'bi-stars' },
        { id: 'communication', label: 'Communication', icon: 'bi-send' },
        { id: 'integration', label: 'Intégration & workflow', icon: 'bi-plug' },
        { id: 'fichier', label: 'Fichiers & export', icon: 'bi-folder' },
        { id: 'controle', label: 'Contrôle & décision', icon: 'bi-shield-check' }
    ];

    const field = (key, label, type, placeholder, extra) =>
        Object.assign({ key, label, type: type || 'text', placeholder: placeholder || '' }, extra || {});

    const ITEMS = [
        { id: 'lire', label: 'Lire', icon: 'bi-book', category: 'lecture', fields: [
            field('inputFormat', 'Format attendu', 'select', '', { options: ['Auto', 'Texte', 'HTML', 'PDF', 'Binaire'] }),
            field('encoding', 'Encodage', 'select', '', { options: ['UTF-8', 'ISO-8859-1', 'Windows-1252'] }),
            field('maxSizeMb', 'Taille max (Mo)', 'number', '25')
        ]},
        { id: 'telecharger', label: 'Télécharger', icon: 'bi-download', category: 'lecture', fields: [
            field('urlPattern', 'URL / pattern', 'text', 'https://... ou {baseUrl}/{id}'),
            field('authRef', 'Auth (source liée)', 'text', 'API REST, FTP…'),
            field('destination', 'Dossier destination', 'text', 'C:\\Agentia\\Downloads'),
            field('overwrite', 'Écraser existant', 'select', '', { options: ['Non', 'Oui'] })
        ]},
        { id: 'surveiller', label: 'Surveiller', icon: 'bi-radar', category: 'lecture', fields: [
            field('watchTarget', 'Cible', 'text', 'Dossier, queue, endpoint…'),
            field('intervalSec', 'Intervalle (sec)', 'number', '60'),
            field('condition', 'Condition déclencheur', 'text', 'nouveau fichier, statut=error…')
        ]},
        { id: 'ecouter-webhook', label: 'Écouter webhook', icon: 'bi-broadcast', category: 'lecture', fields: [
            field('path', 'Chemin endpoint', 'text', '/hooks/agent'),
            field('secret', 'Secret validation', 'password', '', { secret: true }),
            field('method', 'Méthode', 'select', '', { options: ['POST', 'PUT', 'PATCH'] })
        ]},

        { id: 'extraire', label: 'Extraire des données', icon: 'bi-box-arrow-down', category: 'extraction', fields: [
            field('fieldsSchema', 'Champs à extraire (JSON)', 'textarea', '{"invoiceNumber":"","amount":0}'),
            field('extractionMode', 'Mode', 'select', '', { options: ['Règles', 'IA', 'Hybride'] }),
            field('confidenceMin', 'Confiance min (%)', 'number', '85')
        ]},
        { id: 'ocr', label: 'OCR', icon: 'bi-file-earmark-image', category: 'extraction', fields: [
            field('languages', 'Langues OCR', 'text', 'fra,eng'),
            field('dpi', 'Résolution DPI', 'number', '300'),
            field('engine', 'Moteur', 'select', '', { options: ['CogniDoc', 'Tesseract', 'Azure Vision'] })
        ]},
        { id: 'parser', label: 'Parser structure', icon: 'bi-diagram-2', category: 'extraction', fields: [
            field('parserType', 'Type', 'select', '', { options: ['Regex', 'XPath', 'JSONPath', 'CSV', 'IA'] }),
            field('template', 'Template / règles', 'textarea', 'Expression ou modèle…')
        ]},
        { id: 'scraper', label: 'Scraper web', icon: 'bi-globe', category: 'extraction', fields: [
            field('selectors', 'Sélecteurs CSS/XPath', 'textarea', '.invoice-total'),
            field('pagination', 'Pagination', 'text', 'next-button, page param…'),
            field('rateLimit', 'Délai entre requêtes (ms)', 'number', '1000')
        ]},
        { id: 'detecter-entites', label: 'Détecter entités (NER)', icon: 'bi-tags', category: 'extraction', fields: [
            field('entityTypes', 'Types d\'entités', 'text', 'personne, organisation, date, montant'),
            field('model', 'Modèle IA', 'select', '', { options: ['GPT-4.1', 'Modèle spécialisé', 'Règles'] })
        ]},

        { id: 'classifier', label: 'Classifier', icon: 'bi-folder2-open', category: 'transformation', fields: [
            field('categories', 'Catégories', 'textarea', 'Facture\nDevis\nRéclamation'),
            field('multiLabel', 'Multi-label', 'select', '', { options: ['Non', 'Oui'] }),
            field('threshold', 'Seuil confiance (%)', 'number', '80')
        ]},
        { id: 'comparer', label: 'Comparer', icon: 'bi-files', category: 'transformation', fields: [
            field('compareMode', 'Mode', 'select', '', { options: ['Texte diff', 'Champs structurés', 'Version document'] }),
            field('tolerance', 'Tolérance écart', 'text', '0 pour strict, 5% pour montants…'),
            field('outputFormat', 'Rapport', 'select', '', { options: ['Résumé', 'Détail ligne à ligne', 'JSON'] })
        ]},
        { id: 'valider', label: 'Valider', icon: 'bi-check-circle', category: 'transformation', fields: [
            field('rules', 'Règles de validation', 'textarea', 'montant > 0\n date <= aujourd\'hui'),
            field('schemaRef', 'Schéma JSON (optionnel)', 'text', 'invoice.schema.json'),
            field('onFail', 'Si échec', 'select', '', { options: ['Rejeter', 'Flag review', 'Notifier'] })
        ]},
        { id: 'enrichir', label: 'Enrichir', icon: 'bi-plus-circle', category: 'transformation', fields: [
            field('enrichmentSources', 'Sources enrichissement', 'text', 'CRM, API externe, base interne'),
            field('mapping', 'Mapping champs', 'textarea', '{"clientId":"crm.accountId"}'),
            field('cacheTtl', 'Cache TTL (min)', 'number', '60')
        ]},
        { id: 'normaliser', label: 'Normaliser / nettoyer', icon: 'bi-filter', category: 'transformation', fields: [
            field('rules', 'Règles', 'textarea', 'trim, lowercase email, format date ISO'),
            field('deduplicate', 'Dédoublonner', 'select', '', { options: ['Non', 'Oui'] })
        ]},
        { id: 'fusionner', label: 'Fusionner documents', icon: 'bi-layers', category: 'transformation', fields: [
            field('mergeStrategy', 'Stratégie', 'select', '', { options: ['Concaténer', 'Fusion PDF', 'Union JSON'] }),
            field('orderBy', 'Ordre', 'text', 'date, filename…')
        ]},
        { id: 'anonymiser', label: 'Anonymiser (PII)', icon: 'bi-incognito', category: 'transformation', fields: [
            field('piiTypes', 'Données à masquer', 'text', 'email, téléphone, NIR, IBAN'),
            field('method', 'Méthode', 'select', '', { options: ['Masquage', 'Pseudonymisation', 'Suppression'] })
        ]},

        { id: 'resumer', label: 'Résumer', icon: 'bi-text-paragraph', category: 'generation', fields: [
            field('maxWords', 'Longueur max (mots)', 'number', '200'),
            field('style', 'Style', 'select', '', { options: ['Exécutif', 'Technique', 'Bullet points'] }),
            field('language', 'Langue sortie', 'text', 'fr')
        ]},
        { id: 'generer', label: 'Générer', icon: 'bi-stars', category: 'generation', fields: [
            field('outputType', 'Type de sortie', 'select', '', { options: ['Texte', 'Email', 'Rapport', 'JSON', 'Code'] }),
            field('template', 'Template / consignes', 'textarea', 'Rédige un rapport…'),
            field('tone', 'Ton', 'select', '', { options: ['Professionnel', 'Formel', 'Amical', 'Technique'] })
        ]},
        { id: 'traduire', label: 'Traduire', icon: 'bi-translate', category: 'generation', fields: [
            field('sourceLang', 'Langue source', 'text', 'auto'),
            field('targetLang', 'Langue cible', 'text', 'fr'),
            field('preserveFormat', 'Conserver mise en forme', 'select', '', { options: ['Oui', 'Non'] })
        ]},
        { id: 'rediger-email', label: 'Rédiger email', icon: 'bi-envelope-paper', category: 'generation', fields: [
            field('subjectTemplate', 'Objet (template)', 'text', 'Re: {topic}'),
            field('bodyTemplate', 'Corps (template)', 'textarea', 'Bonjour {name}…'),
            field('signature', 'Signature', 'text', 'Cordialement, Agent IA')
        ]},
        { id: 'generer-rapport', label: 'Générer rapport', icon: 'bi-file-bar-graph', category: 'generation', fields: [
            field('format', 'Format', 'select', '', { options: ['PDF', 'Word', 'HTML', 'Excel'] }),
            field('sections', 'Sections', 'textarea', 'Résumé\nKPI\nRecommandations'),
            field('branding', 'Modèle / charte', 'text', 'template-entreprise')
        ]},

        { id: 'envoyer-email', label: 'Envoyer email', icon: 'bi-send', category: 'communication', fields: [
            field('smtpSource', 'Source SMTP', 'text', 'SMTP configuré étape 3'),
            field('to', 'Destinataires (template)', 'text', '{accounting@company.com}'),
            field('cc', 'CC (optionnel)', 'text', ''),
            field('subject', 'Objet (template)', 'text', '[Agentia] {subject}'),
            field('attachOutput', 'Joindre résultat', 'select', '', { options: ['Oui', 'Non'] })
        ]},
        { id: 'repondre-email', label: 'Répondre email', icon: 'bi-reply', category: 'communication', fields: [
            field('replyMode', 'Mode', 'select', '', { options: ['Réponse directe', 'Brouillon à valider', 'Forward'] }),
            field('includeQuote', 'Citer message original', 'select', '', { options: ['Oui', 'Non'] }),
            field('signature', 'Signature', 'text', '')
        ]},
        { id: 'notifier-teams', label: 'Notifier Teams', icon: 'bi-chat-dots', category: 'communication', fields: [
            field('webhookOrChannel', 'Webhook / Canal', 'text', 'Teams source étape 3'),
            field('messageTemplate', 'Message', 'textarea', '⚠ {alert}\n{details}'),
            field('mentionUsers', 'Mentionner (@)', 'text', 'user@domain.com')
        ]},
        { id: 'notifier-slack', label: 'Notifier Slack', icon: 'bi-slack', category: 'communication', fields: [
            field('channel', 'Canal', 'text', '#agent-alerts'),
            field('messageTemplate', 'Message', 'textarea', '{summary}'),
            field('threadReply', 'Répondre en thread', 'select', '', { options: ['Non', 'Oui'] })
        ]},
        { id: 'envoyer-sms', label: 'Envoyer SMS', icon: 'bi-phone', category: 'communication', fields: [
            field('provider', 'Fournisseur', 'select', '', { options: ['Twilio', 'Azure SMS', 'Autre'] }),
            field('to', 'Numéro(s)', 'text', '+33…'),
            field('messageTemplate', 'Message', 'textarea', 'Alerte: {message}')
        ]},
        { id: 'webhook-sortant', label: 'Webhook sortant', icon: 'bi-link-45deg', category: 'communication', fields: [
            field('url', 'URL destination', 'text', 'https://api.partner.com/hook'),
            field('method', 'Méthode', 'select', '', { options: ['POST', 'PUT'] }),
            field('headers', 'En-têtes (JSON)', 'textarea', '{"Authorization":"Bearer …"}', { secret: true }),
            field('payloadTemplate', 'Payload', 'textarea', '{"event":"processed","data":{payload}}')
        ]},

        { id: 'appeler-api', label: 'Appeler API', icon: 'bi-braces', category: 'integration', fields: [
            field('method', 'Méthode HTTP', 'select', '', { options: ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'] }),
            field('endpoint', 'Endpoint', 'text', 'https://api.domaine.com/v1/resource'),
            field('headers', 'En-têtes', 'textarea', '{"Content-Type":"application/json"}'),
            field('bodyTemplate', 'Corps requête', 'textarea', '{"id":"{id}"}'),
            field('authRef', 'Auth (Vault)', 'text', 'Bearer token / API key source')
        ]},
        { id: 'creer-ticket', label: 'Créer ticket', icon: 'bi-ticket', category: 'integration', fields: [
            field('system', 'Système', 'select', '', { options: ['Jira', 'ServiceNow', 'Zendesk', 'Freshdesk', 'Azure DevOps'] }),
            field('project', 'Projet / Queue', 'text', 'SUP'),
            field('issueType', 'Type', 'text', 'Incident, Task, Bug…'),
            field('priority', 'Priorité', 'select', '', { options: ['Basse', 'Moyenne', 'Haute', 'Critique'] }),
            field('titleTemplate', 'Titre', 'text', '[Agent] {summary}')
        ]},
        { id: 'maj-crm', label: 'Mettre à jour CRM', icon: 'bi-person-badge', category: 'integration', fields: [
            field('crmSystem', 'CRM', 'select', '', { options: ['Salesforce', 'Dynamics', 'HubSpot', 'Pipedrive'] }),
            field('objectType', 'Objet', 'text', 'Lead, Contact, Deal…'),
            field('recordIdField', 'Champ ID record', 'text', 'externalId'),
            field('fieldMapping', 'Mapping', 'textarea', '{"status":"Qualified"}')
        ]},
        { id: 'maj-db', label: 'Mettre à jour base', icon: 'bi-database', category: 'integration', fields: [
            field('dbSource', 'Source DB (étape 3)', 'text', 'PostgreSQL, SQL Server…'),
            field('operation', 'Opération', 'select', '', { options: ['INSERT', 'UPDATE', 'UPSERT', 'DELETE'] }),
            field('table', 'Table / collection', 'text', 'invoices'),
            field('keyFields', 'Clés', 'text', 'id, reference'),
            field('sqlOrMapping', 'SQL / mapping', 'textarea', 'UPDATE … SET …')
        ]},
        { id: 'publier-sharepoint', label: 'Publier SharePoint', icon: 'bi-share', category: 'integration', fields: [
            field('library', 'Bibliothèque', 'text', 'Documents/Processed'),
            field('fileNameTemplate', 'Nom fichier', 'text', '{date}_{type}_{id}.pdf'),
            field('metadata', 'Métadonnées', 'textarea', '{"Category":"Agentia"}')
        ]},
        { id: 'declencher-flow', label: 'Déclencher Power Automate', icon: 'bi-lightning', category: 'integration', fields: [
            field('flowUrl', 'URL HTTP trigger', 'text', 'https://prod-…'),
            field('payload', 'Payload', 'textarea', '{"correlationId":"{id}"}')
        ]},

        { id: 'archiver', label: 'Archiver', icon: 'bi-archive', category: 'fichier', fields: [
            field('destination', 'Destination', 'text', '\\\\archive\\processed\\'),
            field('naming', 'Convention nom', 'text', '{yyyy}/{MM}/{original}'),
            field('retentionDays', 'Rétention (jours)', 'number', '365'),
            field('compress', 'Compresser', 'select', '', { options: ['Non', 'ZIP'] })
        ]},
        { id: 'exporter-json', label: 'Exporter JSON', icon: 'bi-filetype-json', category: 'fichier', fields: [
            field('outputPath', 'Chemin sortie', 'text', 'C:\\Agentia\\Out\\result.json'),
            field('prettyPrint', 'Format lisible', 'select', '', { options: ['Oui', 'Non'] }),
            field('schemaValidate', 'Valider schéma', 'select', '', { options: ['Non', 'Oui'] })
        ]},
        { id: 'exporter-csv', label: 'Exporter CSV', icon: 'bi-filetype-csv', category: 'fichier', fields: [
            field('outputPath', 'Chemin sortie', 'text', ''),
            field('delimiter', 'Séparateur', 'select', '', { options: [';', ',', '|'] }),
            field('headers', 'En-têtes colonnes', 'text', 'id,date,amount,status')
        ]},
        { id: 'exporter-xml', label: 'Exporter XML', icon: 'bi-filetype-xml', category: 'fichier', fields: [
            field('outputPath', 'Chemin sortie', 'text', ''),
            field('rootElement', 'Élément racine', 'text', 'Export')
        ]},
        { id: 'copier-fichier', label: 'Copier fichier', icon: 'bi-files', category: 'fichier', fields: [
            field('sourcePath', 'Source (template)', 'text', '{inputPath}'),
            field('destPath', 'Destination', 'text', 'C:\\Agentia\\Copy\\'),
            field('overwrite', 'Écraser', 'select', '', { options: ['Non', 'Oui'] })
        ]},
        { id: 'deplacer-fichier', label: 'Déplacer fichier', icon: 'bi-arrow-right-circle', category: 'fichier', fields: [
            field('destPath', 'Dossier destination', 'text', 'C:\\Agentia\\Done\\'),
            field('onConflict', 'Si conflit', 'select', '', { options: ['Renommer', 'Écraser', 'Ignorer'] })
        ]},
        { id: 'supprimer-fichier', label: 'Supprimer fichier', icon: 'bi-trash', category: 'fichier', fields: [
            field('pattern', 'Pattern fichiers', 'text', '*.tmp'),
            field('secureDelete', 'Suppression sécurisée', 'select', '', { options: ['Non', 'Oui'] }),
            field('requireApproval', 'Approbation requise', 'select', '', { options: ['Oui', 'Non'] })
        ]},

        { id: 'approuver', label: 'Approuver', icon: 'bi-hand-thumbs-up', category: 'controle', fields: [
            field('approverRole', 'Rôle approbateur', 'text', 'Manager, Comptable…'),
            field('approvalChannel', 'Canal', 'select', '', { options: ['Email', 'Teams', 'UI Agentia'] }),
            field('timeoutHours', 'Timeout (h)', 'number', '24')
        ]},
        { id: 'rejeter', label: 'Rejeter', icon: 'bi-hand-thumbs-down', category: 'controle', fields: [
            field('reasonRequired', 'Motif obligatoire', 'select', '', { options: ['Oui', 'Non'] }),
            field('notifySender', 'Notifier émetteur', 'select', '', { options: ['Oui', 'Non'] })
        ]},
        { id: 'escalader', label: 'Escalader', icon: 'bi-arrow-up-circle', category: 'controle', fields: [
            field('escalationLevel', 'Niveau', 'select', '', { options: ['L1 → L2', 'L2 → Manager', 'Manager → Direction'] }),
            field('criteria', 'Critères', 'textarea', 'montant > 10000, SLA dépassé…'),
            field('notifyChannel', 'Notification', 'text', 'Teams #escalations')
        ]},
        { id: 'assigner', label: 'Assigner tâche', icon: 'bi-person-check', category: 'controle', fields: [
            field('assignTo', 'Assigner à (template)', 'text', '{owner} ou queue Finance'),
            field('dueInHours', 'Échéance (h)', 'number', '48'),
            field('priority', 'Priorité', 'select', '', { options: ['Basse', 'Normale', 'Haute'] })
        ]},
        { id: 'planifier', label: 'Planifier action', icon: 'bi-calendar-plus', category: 'controle', fields: [
            field('schedule', 'Planification CRON', 'text', '0 8 * * 1-5'),
            field('timezone', 'Fuseau', 'text', 'Europe/Paris'),
            field('actionOnTrigger', 'Action planifiée', 'text', 'Relancer workflow…')
        ]},
        { id: 'logger-audit', label: 'Journaliser (audit)', icon: 'bi-journal-text', category: 'controle', fields: [
            field('logLevel', 'Niveau', 'select', '', { options: ['Info', 'Warning', 'Error', 'Audit'] }),
            field('includePayload', 'Inclure payload', 'select', '', { options: ['Métadonnées seules', 'Complet masqué', 'Complet'] }),
            field('retentionDays', 'Rétention logs (j)', 'number', '90')
        ]},
        { id: 'quarantaine', label: 'Mettre en quarantaine', icon: 'bi-shield-exclamation', category: 'controle', fields: [
            field('quarantinePath', 'Dossier quarantaine', 'text', 'C:\\Agentia\\Quarantine\\'),
            field('reason', 'Motif (template)', 'text', 'Validation échouée: {errors}'),
            field('autoReleaseHours', 'Libération auto (h, 0=jamais)', 'number', '0')
        ]}
    ];

    const BY_ID = Object.fromEntries(ITEMS.map(i => [i.id, i]));
    const LABEL_MAP = {
        'lire': 'lire', 'extraire': 'extraire', 'résumer': 'resumer', 'resumer': 'resumer',
        'classifier': 'classifier', 'comparer': 'comparer', 'valider': 'valider',
        'générer': 'generer', 'generer': 'generer',
        'envoyer email': 'envoyer-email', 'créer ticket': 'creer-ticket', 'creer ticket': 'creer-ticket',
        'notifier teams': 'notifier-teams', 'archiver': 'archiver',
        'exporter json': 'exporter-json', 'appeler api': 'appeler-api',
        'ocr': 'ocr', 'télécharger': 'telecharger', 'telecharger': 'telecharger'
    };

    global.STUDIO_ACTION_CATALOG = { categories: CATEGORIES, items: ITEMS, byId: BY_ID };
    global.getStudioAction = id => BY_ID[id] || null;
    global.getStudioActionLabel = id => BY_ID[id]?.label || id;
    global.migrateActionId = val => {
        if (BY_ID[val]) return val;
        const k = val?.toLowerCase?.();
        return LABEL_MAP[k] || val;
    };
})(window);
