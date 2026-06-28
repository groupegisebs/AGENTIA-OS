/* Agent Factory Studio — Execution Providers catalog (synced with DB seed) */
(function (global) {
    'use strict';

    const PROVIDERS = [
        { id: 'a1000001-0001-4001-8001-000000000001', providerType: 'InternalRuntime', name: 'Windows Runtime', icon: 'bi-pc-display', category: 'Runtime',
          description: 'Exécution locale via le Windows Service orchestrateur Agentia.',
          supportsMonitoring: true, supportsRetry: true, supportsRollback: true, supportsScheduling: true },
        { id: 'a1000001-0001-4001-8001-000000000002', providerType: 'PowerAutomate', name: 'Power Automate', icon: 'bi-lightning-charge', category: 'Cloud',
          description: 'Flows Microsoft 365 — Teams, Outlook, SharePoint.',
          supportsMonitoring: true, supportsRetry: true, supportsScheduling: true, supportsParameters: true },
        { id: 'a1000001-0001-4001-8001-000000000003', providerType: 'LogicApps', name: 'Logic Apps', icon: 'bi-diagram-2', category: 'Cloud',
          description: 'Workflows Azure Logic Apps enterprise.',
          supportsMonitoring: true, supportsRetry: true, supportsScheduling: true, supportsParameters: true },
        { id: 'a1000001-0001-4001-8001-000000000004', providerType: 'N8n', name: 'n8n', icon: 'bi-share', category: 'Automation',
          description: 'Automatisation open-source self-hosted ou cloud.',
          supportsMonitoring: true, supportsRetry: true, supportsParameters: true },
        { id: 'a1000001-0001-4001-8001-000000000005', providerType: 'Webhook', name: 'Webhook', icon: 'bi-link-45deg', category: 'Integration',
          description: 'Déclenchement HTTP vers endpoints externes.',
          supportsMonitoring: true, supportsRetry: true, supportsParameters: true },
        { id: 'a1000001-0001-4001-8001-000000000006', providerType: 'RestApi', name: 'REST API', icon: 'bi-braces', category: 'Integration',
          description: 'Appels REST configurables avec auth et mapping JSON.',
          supportsMonitoring: true, supportsRetry: true, supportsParameters: true },
        { id: 'a1000001-0001-4001-8001-000000000007', providerType: 'PowerShell', name: 'PowerShell', icon: 'bi-terminal', category: 'Script',
          description: 'Scripts PowerShell locaux ou distants.',
          supportsRetry: true, supportsParameters: true },
        { id: 'a1000001-0001-4001-8001-000000000008', providerType: 'Python', name: 'Python', icon: 'bi-filetype-py', category: 'Script',
          description: 'Scripts Python containerisés ou locaux.',
          supportsRetry: true, supportsParameters: true },
        { id: 'a1000001-0001-4001-8001-000000000009', providerType: 'DockerJob', name: 'Docker', icon: 'bi-box-seam', category: 'Container',
          description: 'Jobs containerisés via Docker.',
          supportsMonitoring: true, supportsRetry: true, supportsParameters: true },
        { id: 'a1000001-0001-4001-8001-00000000000a', providerType: 'AzureFunction', name: 'Azure Function', icon: 'bi-cloud', category: 'Cloud',
          description: 'Functions serverless Azure.',
          supportsMonitoring: true, supportsRetry: true, supportsScheduling: true, supportsParameters: true }
    ];

    const BY_ID = Object.fromEntries(PROVIDERS.map(p => [p.id, p]));
    const BY_TYPE = Object.fromEntries(PROVIDERS.map(p => [p.providerType, p]));
    const DEFAULT_ID = PROVIDERS[0].id;

    global.STUDIO_EXECUTION_PROVIDER_CATALOG = {
        providers: PROVIDERS,
        byId: BY_ID,
        byType: BY_TYPE,
        defaultProviderId: DEFAULT_ID
    };

    global.getStudioExecutionProvider = id => BY_ID[id] || BY_TYPE[id] || null;
    global.getStudioExecutionProviderLabel = id => getStudioExecutionProvider(id)?.name || id;
})(window);
