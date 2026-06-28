/* Agent Factory Studio — Runtime Agentic: Skills (Understand) */
(function (global) {
    'use strict';

    const CATEGORIES = [
        { id: 'perception', label: 'Perception', icon: 'bi-eye' },
        { id: 'language', label: 'Langage & contenu', icon: 'bi-chat-text' },
        { id: 'knowledge', label: 'Connaissance', icon: 'bi-lightbulb' },
        { id: 'logic', label: 'Logique & contrôle', icon: 'bi-cpu' }
    ];

    const field = (key, label, type, placeholder, extra) =>
        Object.assign({ key, label, type: type || 'text', placeholder: placeholder || '' }, extra || {});

    const ITEMS = [
        { id: 'ocr', label: 'OCR', icon: 'bi-file-earmark-image', category: 'perception', fields: [
            field('languages', 'Langues', 'text', 'fra,eng'),
            field('engine', 'Moteur', 'select', '', { options: ['CogniDoc', 'Tesseract', 'Azure Vision'] })
        ]},
        { id: 'vision', label: 'Vision', icon: 'bi-camera', category: 'perception', fields: [
            field('model', 'Modèle', 'select', '', { options: ['GPT-4 Vision', 'Azure Vision', 'Custom'] })
        ]},
        { id: 'stt', label: 'STT (Speech-to-Text)', icon: 'bi-mic', category: 'perception', fields: [
            field('language', 'Langue', 'text', 'fr-FR'),
            field('provider', 'Fournisseur', 'select', '', { options: ['Azure Speech', 'Whisper', 'Google'] })
        ]},
        { id: 'tts', label: 'TTS (Text-to-Speech)', icon: 'bi-volume-up', category: 'perception', fields: [
            field('voice', 'Voix', 'text', 'fr-FR-DeniseNeural'),
            field('provider', 'Fournisseur', 'select', '', { options: ['Azure Speech', 'ElevenLabs', 'Google'] })
        ]},
        { id: 'extraction', label: 'Extraction', icon: 'bi-box-arrow-down', category: 'language', fields: [
            field('schema', 'Schéma champs (JSON)', 'textarea', '{"field":""}'),
            field('mode', 'Mode', 'select', '', { options: ['Règles', 'IA', 'Hybride'] })
        ]},
        { id: 'classification', label: 'Classification', icon: 'bi-tags', category: 'language', fields: [
            field('categories', 'Catégories', 'textarea', 'Facture\nDevis\nRéclamation'),
            field('threshold', 'Seuil confiance (%)', 'number', '80')
        ]},
        { id: 'summary', label: 'Summary', icon: 'bi-text-paragraph', category: 'language', fields: [
            field('maxWords', 'Longueur max (mots)', 'number', '200'),
            field('style', 'Style', 'select', '', { options: ['Exécutif', 'Technique', 'Bullet points'] })
        ]},
        { id: 'translation', label: 'Translation', icon: 'bi-translate', category: 'language', fields: [
            field('sourceLang', 'Langue source', 'text', 'auto'),
            field('targetLang', 'Langue cible', 'text', 'fr')
        ]},
        { id: 'analysis', label: 'Analysis', icon: 'bi-graph-up', category: 'language', fields: [
            field('analysisType', 'Type', 'select', '', { options: ['Sentiment', 'Thématique', 'Comparatif', 'Statistique'] })
        ]},
        { id: 'search', label: 'Search', icon: 'bi-search', category: 'knowledge', fields: [
            field('index', 'Index / source', 'text', 'documents-*'),
            field('maxResults', 'Résultats max', 'number', '10')
        ]},
        { id: 'rag', label: 'RAG', icon: 'bi-journal-bookmark', category: 'knowledge', fields: [
            field('knowledgeBase', 'Base de connaissances', 'text', 'kb-finance'),
            field('topK', 'Top K chunks', 'number', '5')
        ]},
        { id: 'embeddings', label: 'Embeddings', icon: 'bi-diagram-2', category: 'knowledge', fields: [
            field('model', 'Modèle', 'select', '', { options: ['text-embedding-3-small', 'text-embedding-3-large', 'Custom'] }),
            field('dimensions', 'Dimensions', 'number', '1536')
        ]},
        { id: 'calculation', label: 'Calculation', icon: 'bi-calculator', category: 'logic', fields: [
            field('precision', 'Précision décimale', 'number', '2')
        ]},
        { id: 'workflow', label: 'Workflow', icon: 'bi-arrow-repeat', category: 'logic', fields: [
            field('workflowRef', 'Référence workflow', 'text', 'process-invoice-v1')
        ]},
        { id: 'prediction', label: 'Prediction', icon: 'bi-graph-up-arrow', category: 'logic', fields: [
            field('modelRef', 'Modèle ML', 'text', 'risk-scorer-v2')
        ]},
        { id: 'detection', label: 'Detection', icon: 'bi-exclamation-triangle', category: 'logic', fields: [
            field('detectionType', 'Type', 'select', '', { options: ['Anomalie', 'Fraude', 'PII', 'Doublon'] })
        ]},
        { id: 'validation', label: 'Validation', icon: 'bi-patch-check', category: 'logic', fields: [
            field('rules', 'Règles', 'textarea', 'montant > 0\ndate <= aujourd\'hui'),
            field('onFail', 'Si échec', 'select', '', { options: ['Rejeter', 'Flag review', 'Notifier'] })
        ]}
    ];

    const BY_ID = Object.fromEntries(ITEMS.map(i => [i.id, i]));

    global.STUDIO_SKILL_CATALOG = { categories: CATEGORIES, items: ITEMS, byId: BY_ID };
    global.getStudioSkill = id => BY_ID[id] || null;
    global.getStudioSkillLabel = id => BY_ID[id]?.label || id;
})(window);
