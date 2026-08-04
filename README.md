# Package documentaire Codex - Logiciel Pharmacie

Version : V0.1  
Date : 2026-08-02

## Objectif

Ce dossier est la source de contexte permanente utilisée pour préparer et exécuter les sprints Codex du projet **Logiciel Pharmacie**.

## Ordre de priorité des sources

1. Prompt du sprint actuel.
2. `docs/governance/DECISIONS_REGISTER.md`.
3. `docs/governance/CURRENT_STATE.md`.
4. `docs/governance/SCOPE.md`.
5. PDF de référence dans `docs/reference/`.
6. Code existant.

En cas de contradiction, Codex doit arrêter uniquement la partie concernée, signaler le conflit et ne pas arbitrer silencieusement.

## Contenu

- `docs/reference/` : dossiers PDF validés des Phases 1 à 5.
- `docs/governance/` : contexte, décisions, périmètre, état courant et workflow Codex.
- `docs/templates/` : modèle de prompt et modèle de rapport.
- `docs/reports/` : rapports produits après chaque sprint.

## Première utilisation

1. Copier le dossier `docs/` à la racine du dépôt.
2. Vérifier les noms et l’ouverture de tous les PDF.
3. Mettre à jour `CURRENT_STATE.md` avec le chemin réel du dépôt, la branche et les outils installés.
4. Ne pas lancer Codex avant validation du premier prompt de sprint.

## Développement local

Depuis la racine du dépôt :

```powershell
dotnet restore .\Pharmacy.slnx
dotnet build .\Pharmacy.slnx -c Debug --no-restore
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet run --project .\src\Pharmacy.Desktop\Pharmacy.Desktop.csproj -c Debug
```

Après le lancement, l’application ouvre l’accueil synthétique du shell V0.1. La barre latérale expose les domaines prévus de la V1. `Ventes` ouvre le POS V0.2, vertical slice de démonstration entièrement local et non persistant ; les sept autres domaines non implémentés ouvrent une page provisoire descriptive.

Le POS V0.2 conserve le parcours nominal V0.1 et ajoute les incidents de code inconnu/disponibilité, la suspension et la reprise contrôlée de paniers, l’abandon du brouillon, la machine d’états du TPE ambigu et une impression post-vente entièrement simulée. Tous les montants sont calculés avec `decimal` ; aucune donnée bancaire ou réelle n’est collectée, aucune donnée n’est persistée, aucun stock n’est modifié et aucun ticket réel n’est produit. Les spécifications et limites sont documentées dans `docs/ux/POS_V0.1.md` et `docs/ux/POS_V0.2_CONTINUITY.md`.

Le Sprint 6 ajoute un noyau Sales V0.1 indépendant dans `Pharmacy.Domain` (`Money`, identifiants typés, quantité entière, snapshot produit, ligne immuable et agrégat `SaleDraft`) ainsi que trois contrats non implémentés dans `Pharmacy.Application`. Ce noyau n’est pas connecté au POS synthétique : il ne persiste rien, ne finalise aucune vente et ne traite ni stock, lot, paiement, caisse, audit ou impression. Sa conception est décrite dans `docs/domain/SALES_CORE_V0.1.md`.

Le Sprint 7 ajoute les cas d’utilisation Sales Application V0.1 et cinq adaptateurs temporaires sous `Pharmacy.Infrastructure.Sales.InMemory`. Les brouillons sont versionnés, les mutations utilisent une concurrence optimiste et la préparation de finalisation est idempotente par `OperationId`. Tout reste strictement local au processus, synthétique et sans écriture disque. `Prepared` ne signifie ni finalisée, payée, réservée ou persistée.

Le Sprint 8 connecte le parcours nominal du POS à la chaîne `Desktop → Application → Infrastructure en mémoire → Domain`. `SaleDraftView` devient la source du panier, des quantités et des totaux ; le coordinateur Desktop conserve la version optimiste, recharge après conflit sans rejouer l’action et restaure les suspensions dans de nouveaux brouillons Application. Paiement, TPE et impression restent des simulations Desktop ; aucune finalisation, persistance, réservation ou écriture de stock n’est réalisée. Les détails se trouvent dans `docs/ux/POS_APPLICATION_INTEGRATION_V0.1.md` et `docs/application/DESKTOP_SALES_INTEGRATION_V0.1.md`.

Le Sprint 9 ajoute `Persistence Foundation V0.1` dans Infrastructure avec EF Core SQL Server 10.0.10, un outil local `dotnet-ef`, un `PharmacyDbContext` design-time sécurisé et la migration `InitialCatalogPersistenceV01`. La base locale `Pharmacy_Development` contient uniquement cinq tables vides du schéma `catalog`, suivant le modèle Phase 3 (`ProductLocalSettings` plutôt que la cible indicative `ProductReferences`). La validation automatisée utilise une vraie instance SQL Server 2025 Developer ; la validation sur SQL Server 2025 Express reste obligatoire avant le pilote. Le Desktop et le POS demeurent connectés exclusivement aux adaptateurs Sales en mémoire.

Le Sprint 10 rend le Catalogue réellement persistant. La migration minimale `CatalogPersistenceEnhancementsV01` ajoute l’activation globale manquante à `catalog.Products`. Les recherches du POS utilisent désormais `SqlProductCatalogReader` pour le nom, la présentation, la référence interne et les barcodes actifs ; les prix viennent de SQL Server. La page `/module/catalog` gère produits, catégories, activation, barcodes, prix et historique avec concurrence `rowversion`. Le panier Sales, le stock synthétique, les suspensions, paiements et impressions restent en mémoire et aucune vente n’est enregistrée dans SQL Server.

### Commandes de persistance contrôlées

La connexion n’est jamais stockée dans le dépôt. Elle doit être fournie temporairement par `PHARMACY_SQLSERVER_CONNECTION` pour les commandes EF ou `PHARMACY_TEST_SQLSERVER_CONNECTION` pour les tests SQL réels.

```powershell
dotnet tool restore
dotnet ef migrations list `
  --project .\src\Pharmacy.Infrastructure\Pharmacy.Infrastructure.csproj `
  --startup-project .\src\Pharmacy.Infrastructure\Pharmacy.Infrastructure.csproj `
  --context PharmacyDbContext
```

Le script de contrôle en lecture seule se trouve dans `database/verification/verify_sprint09_schema.sql`. Aucune migration n’est exécutée automatiquement au lancement de l’application.

Seed et vérification Sprint 10, exclusivement sur `Pharmacy_Development` :

```powershell
sqlcmd -S <instance-explicite> -d Pharmacy_Development -E -C -b -f 65001 `
  -i .\database\dev\seed_synthetic_catalog_v01.sql
sqlcmd -S <instance-explicite> -d Pharmacy_Development -E -C -b -f 65001 `
  -i .\database\verification\verify_sprint10_catalog.sql
```

Le seed manuel est transactionnel et idempotent. Le script `database/dev/clear_synthetic_catalog_v01.sql` supprime uniquement les références `DEMO-*` et leurs dépendances. `-C` reste une exception locale provisoire tant que le certificat SQL de développement n’est pas approuvé.

En configuration Debug, le groupe `Développement` donne également accès à :

- `Diagnostic technique` sur `/diagnostic` : smoke screen conservé du Sprint 1 ;
- `UI Lab` sur `/ui-lab` : fondations visuelles code-first V0.1 et états des composants.

Le sélecteur d’aperçu des accès est une prévisualisation locale et non persistante. Ses profils ne sont ni des rôles métier finaux ni un mécanisme de sécurité. Le POS, l’UI Lab et le shell V0.1 restent des prototypes soumis à revue visuelle humaine. La validation Sales Application Sprint 7 se trouve dans `docs/reports/sprint-07/sales-application-validation.md`.
