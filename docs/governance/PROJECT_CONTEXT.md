# PROJECT_CONTEXT - Logiciel Pharmacie

## 1. Vision

Construire un logiciel professionnel de gestion d’officine au Maroc, local-first, utilisable hors ligne pour les opérations critiques, robuste, traçable, sauvegardable et commercialisable.

## 2. Pharmacie pilote

- Petite officine gérée par un couple, dont une pharmacienne.
- Un seul comptoir et un ordinateur prévu initialement.
- Gestion actuelle principalement sur cahier et documents fournisseurs.
- Paiements : espèces et TPE autonome prévu.
- Crédit possible pour certains clients de confiance.
- Compte partagé accepté pour le pilote, avec limitation connue de l’attribution individuelle.

## 3. Stack validée

- Windows 11 Pro x64.
- C# et .NET 10 LTS.
- WPF comme shell desktop.
- Blazor Hybrid / Razor Components pour l’interface.
- Monolithe modulaire.
- EF Core dans Infrastructure.
- SQL Server 2025 Express comme moteur local.
- xUnit.net v3 et Microsoft Testing Platform.

## 4. Projets logiques prévus

1. `Pharmacy.Domain`
2. `Pharmacy.Application`
3. `Pharmacy.Infrastructure`
4. `Pharmacy.Desktop`
5. `Pharmacy.UnitTests`
6. `Pharmacy.IntegrationTests`

Dépendances :

- Domain -> aucune dépendance projet.
- Application -> Domain.
- Infrastructure -> Application + Domain.
- Desktop -> Application + Infrastructure.
- UI -> jamais d’accès direct au DbContext.

## 5. Modules V1

- Catalogue.
- Stock, lots, péremptions et inventaires.
- Fournisseurs, commandes, réceptions et retours.
- Vente/POS, paiements, suspensions, retours et annulations.
- Caisse.
- Clients et crédits simples.
- Reporting et alertes.
- Administration, sécurité, audit et configuration.
- Imports/exports, sauvegardes, restauration, installation et diagnostic.

## 6. Principes structurants

- Local-first, pas local-only.
- Chaque variation de stock est expliquée par un mouvement.
- Les opérations financières et stock critiques sont atomiques.
- Les documents finalisés ne sont jamais modifiés directement.
- Les corrections utilisent des opérations inverses contrôlées.
- Données synthétiques uniquement en développement et test.
- Aucun secret, donnée clinique ou donnée de carte bancaire dans le dépôt.
- Figma est facultatif ; le frontend suit une méthode code-first.

## 7. Mode de travail

ChatGPT pilote l’architecture, le périmètre, les prompts et la revue. Codex implémente et teste. L’utilisateur exécute et valide manuellement. La pharmacie pilote valide les parcours terrain.
