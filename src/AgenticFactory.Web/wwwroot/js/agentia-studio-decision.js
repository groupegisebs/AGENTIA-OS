/* Agent Factory Studio — Runtime Agentic: Decision Engine (Decide) */
(function (global) {
    'use strict';

    const ENGINES = [
        { id: 'gpt', label: 'GPT (OpenAI)', icon: 'bi-stars', desc: 'GPT-4o / GPT-4.1 — polyvalent et performant' },
        { id: 'claude', label: 'Claude', icon: 'bi-chat-square-text', desc: 'Anthropic Claude — raisonnement long contexte' },
        { id: 'gemini', label: 'Gemini', icon: 'bi-google', desc: 'Google Gemini — multimodal' },
        { id: 'deepseek', label: 'DeepSeek', icon: 'bi-cpu', desc: 'Modèle open-weight économique' },
        { id: 'llama', label: 'Llama', icon: 'bi-box', desc: 'Meta Llama — déploiement flexible' },
        { id: 'ollama', label: 'Ollama', icon: 'bi-hdd', desc: 'LLM local via Ollama' },
        { id: 'azure-openai', label: 'Azure OpenAI', icon: 'bi-cloud', desc: 'GPT hébergé Azure — conformité entreprise' },
        { id: 'business-rules', label: 'Business rules', icon: 'bi-list-check', desc: 'Règles métier déterministes sans LLM' },
        { id: 'human-validation', label: 'Human validation', icon: 'bi-person-check', desc: 'Validation humaine obligatoire avant action' },
        { id: 'workflow', label: 'Workflow', icon: 'bi-diagram-3', desc: 'Orchestration par workflow prédéfini' },
        { id: 'hybrid', label: 'Hybrid', icon: 'bi-shuffle', desc: 'Combine LLM + règles + validation humaine' }
    ];

    const field = (key, label, type, placeholder, extra) =>
        Object.assign({ key, label, type: type || 'text', placeholder: placeholder || '' }, extra || {});

    const CONFIG_FIELDS = {
        gpt: [
            field('model', 'Modèle', 'select', '', { options: ['gpt-4o', 'gpt-4o-mini', 'gpt-4.1', 'o3-mini'] }),
            field('temperature', 'Température', 'number', '0.3'),
            field('maxTokens', 'Tokens max', 'number', '4096')
        ],
        claude: [
            field('model', 'Modèle', 'select', '', { options: ['claude-sonnet-4', 'claude-opus-4', 'claude-haiku-3.5'] }),
            field('temperature', 'Température', 'number', '0.3')
        ],
        gemini: [
            field('model', 'Modèle', 'select', '', { options: ['gemini-2.0-flash', 'gemini-1.5-pro'] })
        ],
        deepseek: [
            field('model', 'Modèle', 'select', '', { options: ['deepseek-chat', 'deepseek-reasoner'] })
        ],
        llama: [
            field('model', 'Modèle', 'select', '', { options: ['llama-3.3-70b', 'llama-3.1-8b'] })
        ],
        ollama: [
            field('host', 'Host Ollama', 'text', 'http://localhost:11434'),
            field('model', 'Modèle local', 'text', 'llama3.2')
        ],
        'azure-openai': [
            field('endpoint', 'Endpoint Azure', 'text', 'https://….openai.azure.com'),
            field('deployment', 'Deployment', 'text', 'gpt-4o'),
            field('apiKey', 'API Key', 'password', '', { secret: true })
        ],
        'business-rules': [
            field('ruleset', 'Jeu de règles', 'textarea', 'SI montant > 1000 ALORS valider_humain')
        ],
        'human-validation': [
            field('approverRole', 'Rôle approbateur', 'text', 'Manager'),
            field('channel', 'Canal', 'select', '', { options: ['Email', 'Teams', 'UI Agentia'] }),
            field('timeoutHours', 'Timeout (h)', 'number', '24')
        ],
        workflow: [
            field('workflowId', 'ID workflow', 'text', 'invoice-processing-v1')
        ],
        hybrid: [
            field('primaryEngine', 'Moteur principal', 'select', '', { options: ['GPT', 'Claude', 'Gemini', 'Business rules'] }),
            field('fallbackRules', 'Règles fallback', 'textarea', 'Confiance < 85% → validation humaine')
        ]
    };

    const BY_ID = Object.fromEntries(ENGINES.map(e => [e.id, e]));

    global.STUDIO_DECISION_CATALOG = { engines: ENGINES, configFields: CONFIG_FIELDS, byId: BY_ID };
    global.getStudioDecisionEngine = id => BY_ID[id] || null;
    global.getStudioDecisionLabel = id => BY_ID[id]?.label || id;
    global.getStudioDecisionFields = engineId => CONFIG_FIELDS[engineId] || [];
})(window);
