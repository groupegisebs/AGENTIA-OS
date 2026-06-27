# TRACEABILITY — MVP Agent Creator -> Agentia OS

## 1) Objectif du document

Ce document mappe les briques du MVP actuel vers les capacités cibles Agentia OS.
Il définit les jalons de migration et les critères concrets pour qualifier un composant comme **OS-ready**.

## 2) Principes de migration

- **Continuité business**: ne pas interrompre la chaîne de valeur du MVP.
- **Compatibilité progressive**: introduire les primitives OS sans rupture API majeure.
- **Traçabilité systématique**: chaque évolution doit relier "existant", "cible", "preuve".
- **Pilotage par risque**: commencer par les briques structurantes (runtime, observabilité, capacités).

## 3) Mapping MVP -> Cibles OS

| Brique MVP (actuel) | Cible Agentia OS | Écart principal | Jalons de migration | Critères OS-ready |
|---|---|---|---|---|
| Conversation + blueprint | Planification agentique orchestrable | Logique majoritairement mono-agent | M1: abstraire planning, M2: intégrer orchestrateur | Plan exécutable par plusieurs rôles agents |
| Déploiement d'agent | Runtime noyau avec policies | Exécution orientée déploiement unique | M1: runner unifié, M2: policies d'exécution | Exécution gouvernée par politiques et quotas |
| Invocation API agent | Bus d'exécution/event bus | Couplage fort entrée API -> traitement | M1: enveloppe événement, M2: routage asynchrone | Correlation ID, retry et replay supportés |
| Plans/quotas org | Control plane OS | Gouvernance limitée au SaaS actuel | M1: modèle policy générique, M2: RBAC OS | Policies centralisées appliquées au runtime |
| Intégration paiement | Monétisation usage OS + marketplace | Facturation focalisée déploiement | M1: métriques usage runtime, M2: pricing par capacité | Coûts traçables par tenant/équipe/capacité |
| Marketplace d'agents publics | Marketplace de skills/capacités | Distribution d'artefacts finaux uniquement | M1: packaging skill, M2: validation & certification | Skill versionné, validé et activable par policy |
| Config fournisseur LLM | Orchestrateur multi-modèles | Stratégie modèle peu dynamique | M1: interface provider unifiée, M2: policy routing | Sélection modèle pilotée coût/latence/qualité |
| Stockage conversationnel | Mémoire multi-niveaux | Absence de service mémoire transverse | M1: mémoire session normalisée, M2: mémoire long terme | API mémoire unifiée + gouvernance cycle de vie |
| Services métiers internes | Registre des capacités | Capacités non cataloguées globalement | M1: métadonnées capacités, M2: registre versionné | Contrats I/O, version et ownership publiés |
| Logs techniques actuels | Observabilité agentique | Vision partielle du cycle agentique | M1: traces corrélées, M2: dashboards et alerting OS | Traces bout en bout + SLO exploitables |
| Orchestration implicite | Collaboration multi-agents | Pas de protocoles natifs de coopération | M1: rôles standards, M2: protocoles de handoff | Workflow multi-agents stable et auditables |
| Workflow utilisateur unique | Création d'équipes d'agents | Pas de templates équipes/rôles | M1: templates équipes, M2: gouvernance par équipe | Équipe agentique configurable et réutilisable |

## 4) Jalons consolidés de migration

### M0 — Stabilisation de base MVP

- Geler les contrats API pivots et définir surfaces d'extension.
- Identifier les composants candidats à extraction "primitives OS".
- Introduire conventions de traceabilité fonctionnelle et technique.

### M1 — Extraction des primitives

- Mettre en place un runner d'exécution unifié.
- Normaliser les métadonnées de capacités (pré-registre).
- Introduire corrélation d'événements et traces minimales.
- Créer interface commune de routage modèles.

### M2 — Orchestration OS initiale

- Activer event bus pour orchestration découplée.
- Mettre en service registre des capacités versionné.
- Déployer premières politiques d'accès/exécution centralisées.
- Activer templates d'équipes multi-agents.

### M3 — Industrialisation OS

- Généraliser mémoire long terme et mécanismes de gouvernance.
- Déployer observabilité agentique complète (SLO, coût, audit).
- Ouvrir marketplace de skills certifiés à plus large échelle.

## 5) Critères transverses "OS-ready"

Un composant est considéré **OS-ready** si les critères suivants sont satisfaits:

1. **Interopérabilité**
   - Contrat d'interface formel, versionné, documenté.

2. **Gouvernance**
   - Policies d'accès et d'exécution appliquées en runtime.

3. **Observabilité**
   - Traces corrélées, métriques et erreurs exploitables en production.

4. **Résilience**
   - Stratégie de retry, timeout, fallback et reprise définie.

5. **Scalabilité**
   - Comportement validé sous charge représentative tenant/équipe.

6. **Auditabilité**
   - Journal d'actions et décisions suffisant pour analyse post-mortem.

## 6) Plan d'exécution actionnable (90 jours)

### Objectifs

- Sécuriser les fondations runtime et observabilité.
- Préparer la migration multi-agents sans casser la valeur MVP.
- Poser les bases d'un registre de capacités exploitable.

### Périmètre prioritaire

- Runner unifié d'exécution.
- Pré-registre des capacités (métadonnées + contrat).
- Corrélation événements/traces sur flux principal.
- Interface de routage multi-modèles.

### Critères d'acceptation du cycle

- Au moins un workflow MVP instrumenté avec corrélation complète.
- Au moins 5 capacités cataloguées dans le pré-registre.
- Première policy d'exécution testée sur un cas réel.
- Rapport de coût/latence disponible pour un scénario de bout en bout.

## 7) Points d'attention

- Prioriser la dette d'observabilité tôt pour éviter une migration opaque.
- Encadrer la montée en complexité multi-agents avec des garde-fous de gouvernance.
- Maintenir une communication explicite produit/tech sur ce qui reste "MVP" vs "OS".
