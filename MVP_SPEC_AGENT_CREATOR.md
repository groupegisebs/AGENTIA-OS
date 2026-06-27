# MVP SPEC — Agent Creator (État actuel)

## 1) Objectif du document

Ce document décrit le **MVP actuel** livré dans ce dépôt: périmètre, valeur, contraintes et critères d'acceptation.
Il clarifie ce qui est disponible maintenant, distinctement de la vision Agentia OS long terme.

## 2) Positionnement du MVP

Le MVP Agent Creator est un produit SaaS qui permet:
- de qualifier un besoin métier via dialogue;
- de générer un blueprint de solution;
- de déployer un agent facturable;
- d'exposer l'agent via API.

Le MVP optimise la vitesse de mise en service d'agents unitaires, pas encore l'orchestration OS complète.

## 3) Objectifs MVP

### Objectifs produit

- Valider l'intérêt marché pour la création guidée d'agents métier.
- Monétiser le déploiement d'agents via un modèle abonnement + pay-per-deployment.
- Réduire la friction entre idéation métier et agent exécutable.

### Objectifs techniques

- Fournir une API robuste pour le flux conversation -> blueprint -> déploiement.
- Garantir une base multi-tenant initiale (organisation, plans, quotas).
- Intégrer un paiement opérationnel pour industrialiser la mise en production.

## 4) Périmètre fonctionnel MVP

### Inclus (actuel)

1. **Conversation de cadrage**
   - Capture du besoin via endpoints conversationnels.

2. **Génération de blueprint**
   - Proposition structurée de solution, accessible avant paiement.

3. **Déploiement facturable**
   - Déploiement d'agent avec intégration paiement.

4. **Gestion d'organisation et abonnements**
   - Plans, quotas, suivi usage et facturation.

5. **Invocation d'agents publiés**
   - Endpoint d'exécution pour agents déployés.

6. **Marketplace de base**
   - Listing d'agents publics.

### Hors périmètre MVP

- Runtime noyau OS avec scheduling avancé et isolation forte des exécutions.
- Collaboration native multi-agents et création d'équipes orchestrées.
- Event bus transverse pour workflows événementiels complexes.
- Registre de capacités standardisé avec gouvernance complète.
- Mémoire multi-niveaux durable pilotée par politiques de cycle de vie.
- Routage multi-modèles intelligent basé sur SLO/SLA runtime.

## 5) Différence explicite MVP vs Vision Agentia OS

| Dimension | MVP Agent Creator (actuel) | Agentia OS (vision cible) |
|---|---|---|
| Unité d'orchestration | Agent unitaire | Équipe d'agents coordonnés |
| Exécution | Déploiement et invocation basiques | Runtime noyau gouverné et extensible |
| Mémoire | Contextes principalement transactionnels | Mémoire court/long/épisodique unifiée |
| Capacités | Intégrations spécifiques | Registre de capacités versionné |
| Collaboration | Limitée | Protocoles multi-agents natifs |
| Événements | Faible découplage événementiel | Event bus first-class |
| Modèles IA | Configuration fournisseur | Routage multi-modèles politique |
| Distribution | Marketplace initiale d'agents | Marketplace de skills certifiés |
| Observabilité | Logs/états orientés service | Observabilité agentique complète |

## 6) Exigences opérationnelles MVP

- Maintenir un parcours simple et compréhensible pour équipes non techniques.
- Préserver la stabilité des endpoints clés déjà consommés.
- Garantir la cohérence facturation (abonnements + déploiements).
- Assurer une expérience de déploiement reproductible en environnement de production.

## 7) Critères d'acceptation MVP

### Critères fonctionnels

- Un utilisateur authentifié peut créer une conversation et obtenir un blueprint.
- Un déploiement facturé aboutit à un agent publiable/invocable.
- Les plans d'abonnement et quotas sont appliqués côté API.
- L'organisation peut consulter ses informations de facturation.

### Critères qualité

- Les routes critiques retournent des statuts d'erreur explicites.
- Le système reste utilisable en mode mock quand les providers externes manquent.
- Les dépendances sensibles (secrets, paiement, base de données) sont gérées par configuration.

## 8) Limites connues et points d'attention

- Le MVP ne couvre pas encore les patterns collaboratifs multi-agents complexes.
- Les primitives de mémoire ne sont pas formalisées en service de plateforme dédié.
- Le registre de capacités n'est pas encore un composant central transverse.
- L'observabilité est utile pour exploitation API, mais pas encore orientée opérations agentiques de niveau OS.

## 9) Actions recommandées court terme

- Stabiliser les contrats API considérés comme pivots de migration.
- Identifier les composants MVP à extraire vers primitives OS (runtime, capacités, mémoire).
- Définir des objectifs de compatibilité ascendante pour éviter les ruptures.
- Préparer un backlog de migration piloté par valeur métier et réduction de risque.
