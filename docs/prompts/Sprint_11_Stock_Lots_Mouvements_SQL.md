# SPRINT 11 — STOCK RÉEL, LOTS, PÉREMPTION ET MOUVEMENTS SQL V0.1

## Instruction courte à donner à Codex

Place ce fichier dans le dépôt sous :

`docs/prompts/Sprint_11_Stock_Lots_Mouvements_SQL.md`

Puis donne uniquement cette instruction à Codex :

> Lis intégralement `docs/prompts/Sprint_11_Stock_Lots_Mouvements_SQL.md`, vérifie que la dernière ligne non vide est exactement `FIN DU PROMPT SPRINT 11`, puis exécute toutes les opérations autorisées. Ne fais aucun commit, push, changement de branche ou ajout de remote.

---

## 1. RÔLE

Tu agis comme :

- ingénieur logiciel senior .NET 10 ;
- architecte Clean Architecture et monolithe modulaire ;
- spécialiste SQL Server 2025 et Entity Framework Core 10 ;
- spécialiste des systèmes de stock, lots, péremption et inventaires ;
- développeur WPF et Blazor Hybrid ;
- spécialiste des transactions, de l’idempotence et de la concurrence optimiste ;
- responsable des tests SQL réels, de la sécurité et de la documentation.

Tu travailles sur le projet professionnel **Logiciel Pharmacie**.

Les étapes suivantes sont validées :

- Domain/Application Sales ;
- POS connecté à Application Sales ;
- Catalogue réel persisté dans SQL Server ;
- page Catalogue fonctionnelle ;
- POS utilisant le catalogue SQL ;
- base `Pharmacy_Development` sur SQL Server 2025 Developer ;
- migrations `InitialCatalogPersistenceV01` et `CatalogPersistenceEnhancementsV01` ;
- 300 tests réussis ;
- aucune migration automatique au démarrage ;
- aucune donnée réelle.

Ta mission est de créer le premier moteur de stock réel : emplacements, lots, péremption, balances, mouvements et inventaires, puis de remplacer la disponibilité synthétique du POS par une disponibilité lue dans SQL Server.

Le panier et les ventes restent non persistés pendant ce sprint.

Ne t’arrête pas après un plan. Exécute toutes les opérations autorisées.

---

## 2. MISSION

Créer **Inventory Persistence V0.1** et **Inventory Application V0.1**.

Le sprint doit produire :

1. un noyau métier Inventory minimal et indépendant d’EF Core ;
2. les contrats et cas d’utilisation Application du stock ;
3. un schéma SQL `inventory` ;
4. les emplacements de stock ;
5. les lots ou couches de stock conformément au modèle validé ;
6. les dates de péremption facultatives et configurables ;
7. les balances de stock ;
8. un journal append-only des mouvements ;
9. les entrées, sorties et ajustements contrôlés ;
10. le blocage du stock négatif ;
11. l’idempotence par `OperationId` ;
12. la concurrence optimiste par `rowversion` ;
13. une lecture de disponibilité SQL pour le POS ;
14. une proposition FEFO descriptive et testée ;
15. l’exclusion des lots expirés de la disponibilité vendable ;
16. les sessions d’inventaire et leurs lignes de comptage ;
17. la finalisation atomique d’un inventaire en mouvements d’ajustement ;
18. une page Stock fonctionnelle dans Desktop ;
19. un seed manuel de stock entièrement synthétique ;
20. des tests sur un vrai SQL Server ;
21. aucun enregistrement de vente ;
22. aucun achat ou réception fournisseur ;
23. aucun commit, push ou remote.

### Chaîne attendue

```text
Page Stock / POS
        ↓
Application Inventory
        ↓
Contrats Inventory
        ↓
Adaptateurs SQL
        ↓
PharmacyDbContext
        ↓
SQL Server / schéma inventory
```

### Statut attendu

`IMPLEMENTED — SQL INVENTORY VALIDATION PASS — MANUAL STOCK REVIEW REQUIRED`

Si SQL Server ou la connexion de développement n’est pas disponible :

`BLOCKED — SQL SERVER INVENTORY CONNECTION REQUIRED`

Aucun fallback silencieux vers un stock synthétique ne doit rester actif dans le POS après ce sprint.

---

## 3. SOURCES DE VÉRITÉ

Lire intégralement avant toute modification :

1. `README.md`
2. `Directory.Build.props`
3. `global.json`
4. `.editorconfig`
5. `.gitattributes`
6. `.gitignore`
7. `docs/governance/PROJECT_CONTEXT.md`
8. `docs/governance/DECISIONS_REGISTER.md`
9. `docs/governance/SCOPE.md`
10. `docs/governance/CURRENT_STATE.md`
11. `docs/governance/CODEX_WORKFLOW.md`
12. `docs/templates/CODEX_PROMPT_TEMPLATE.md`
13. `docs/templates/CODEX_REPORT_TEMPLATE.md`
14. `docs/domain/SALES_CORE_V0.1.md`
15. `docs/application/SALES_USE_CASES_V0.1.md`
16. `docs/application/CATALOG_USE_CASES_V0.1.md`
17. `docs/infrastructure/SQL_CATALOG_ADAPTERS_V0.1.md`
18. `docs/database/PERSISTENCE_FOUNDATION_V0.1.md`
19. `docs/database/CATALOG_PHYSICAL_SCHEMA_V0.1.md`
20. `docs/ux/CATALOG_MANAGEMENT_UI_V0.1.md`
21. `docs/reports/sprint-10/codex-report.md`
22. `docs/reports/sprint-10/catalog-validation.md`
23. toutes les migrations et le snapshot EF Core actuels ;
24. le présent fichier jusqu’à `FIN DU PROMPT SPRINT 11`.

Inspecter complètement :

- `src/Pharmacy.Domain`
- `src/Pharmacy.Application`
- `src/Pharmacy.Infrastructure`
- `src/Pharmacy.Desktop`
- `tests/Pharmacy.UnitTests`
- `tests/Pharmacy.IntegrationTests`
- `database`
- `.config/dotnet-tools.json`
- fichiers projet.

Consulter attentivement en lecture seule :

- `docs/reference/01_Dossier_ADR_Logiciel_Pharmacie_V1.0.pdf`
- `docs/reference/02_Dossier_Phase_2_Modelisation_Fonctionnelle_Complete_V0.1.pdf`
- `docs/reference/03_Dossier_Phase_3_Conception_Modele_Donnees_V0.1.pdf`
- `docs/reference/04_Dossier_Phase_4_Conception_Parcours_UX_UI_V0.1.pdf`
- `docs/reference/05_Dossier_Phase_5_Code_First_Vibe_Coding_V0.1.pdf`

Ne modifier aucun PDF.

### Ordre de priorité

1. présent prompt ;
2. `DECISIONS_REGISTER.md` ;
3. `CURRENT_STATE.md` ;
4. `SCOPE.md` ;
5. Phase 2 et Phase 3 validées ;
6. schéma SQL réellement appliqué ;
7. code existant.

### Traçabilité obligatoire

Avant de créer la migration, produire une matrice :

| Élément Inventory | Source | Classification |
|---|---|---|
| table/colonne/relation/règle | document + section/page | confirmé / décision technique / différé |

Distinguer clairement :

- information confirmée par la pharmacie ;
- règle fonctionnelle validée ;
- décision technique ;
- hypothèse de prototype ;
- donnée synthétique ;
- élément différé.

Ne pas inventer une règle réglementaire, clinique, fiscale ou de traçabilité légale.

---

## 4. ÉTAT INITIAL ET PRÉREQUIS

Le Sprint 10 est annoncé comme validé et commité.

Ne suppose aucun hash.

### Git

Exécuter avant toute modification :

```powershell
git status
git status --short
git branch --show-current
git log --oneline -n 10
git remote -v
git diff --check
```

Attendu :

- branche `main` ;
- working tree propre ;
- index vide ;
- aucun remote ;
- commit Sprint 10 en HEAD.

Si l’état n’est pas propre :

- ne rien supprimer ;
- ne pas reset ;
- inspecter ;
- bloquer si l’origine n’est pas expliquée.

### Baseline .NET

```powershell
dotnet tool restore
dotnet restore Pharmacy.slnx
dotnet build Pharmacy.slnx -c Debug --no-restore
dotnet test Pharmacy.slnx -c Debug --no-build
```

Attendu :

- .NET 10 ;
- six projets ;
- au moins 300 tests ;
- zéro erreur ;
- zéro avertissement raisonnable.

### SQL Server

Utiliser `PHARMACY_SQLSERVER_CONNECTION` ciblant explicitement `Pharmacy_Development`.

Vérifier sans afficher de secret :

- SQL Server 2025 Developer ;
- version ;
- édition ;
- compatibilité 170 ;
- deux migrations appliquées ;
- Catalogue SQL disponible ;
- données Demo présentes si le seed Sprint 10 est conservé.

Exécuter les scripts de vérification Sprints 9 et 10 avant modification.

### Packages

Réutiliser les versions déjà présentes :

- EF Core SQL Server 10.0.10 ;
- EF Core Design 10.0.10 ;
- `dotnet-ef` 10.0.10.

Aucun nouveau package ne devrait être nécessaire.

---

## 5. PÉRIMÈTRE AUTORISÉ

Tu peux modifier ou créer des fichiers dans :

- `src/Pharmacy.Domain`
- `src/Pharmacy.Application`
- `src/Pharmacy.Infrastructure`
- `src/Pharmacy.Desktop`
- `tests/Pharmacy.UnitTests`
- `tests/Pharmacy.IntegrationTests`
- `database/dev`
- `database/verification`
- `README.md`
- `docs/governance/CURRENT_STATE.md`
- `docs/governance/DECISIONS_REGISTER.md`

Tu peux créer :

- `docs/domain/INVENTORY_CORE_V0.1.md`
- `docs/application/INVENTORY_USE_CASES_V0.1.md`
- `docs/infrastructure/SQL_INVENTORY_ADAPTERS_V0.1.md`
- `docs/database/INVENTORY_PHYSICAL_SCHEMA_V0.1.md`
- `docs/database/INVENTORY_ERD_V0.1.mmd`
- `docs/ux/STOCK_MANAGEMENT_UI_V0.1.md`
- `docs/reports/sprint-11/inventory-validation.md`
- `docs/reports/sprint-11/codex-report.md`
- `database/dev/seed_synthetic_inventory_v01.sql`
- `database/dev/clear_synthetic_inventory_v01.sql`
- `database/verification/verify_sprint11_inventory.sql`

Créer une migration nouvelle, sans modifier les migrations précédentes.

Nom recommandé :

`InventoryPersistenceV01`

---

## 6. HORS PÉRIMÈTRE ET INTERDICTIONS

Ne développer aucun des éléments suivants :

- persistance de `SaleDraft` ;
- table Sales ou SaleLines ;
- finalisation transactionnelle d’une vente ;
- paiement ;
- caisse ;
- fournisseurs ;
- commandes d’achat ;
- réceptions fournisseur ;
- clients et crédit ;
- retours et remboursements ;
- authentification et autorisation ;
- audit métier complet ;
- reporting complet ;
- sauvegarde ;
- import fournisseur ;
- scanner, imprimante ou TPE réels ;
- API HTTP.

Ne créer aucune table des modules Sales, Purchasing, Cash, Customer ou Security.

Ne créer aucun repository générique.

Ne créer aucune procédure stockée ou trigger sans exigence validée. Pour ce sprint, privilégier EF Core, transactions et contraintes.

Ne créer aucune donnée réelle.

Ne créer aucune information clinique.

Ne traiter aucun numéro de lot Demo comme un numéro réel.

Ne pas :

- utiliser `EnsureCreated()` ;
- appeler `Database.Migrate()` au démarrage ;
- exécuter le seed au démarrage ;
- stocker une connexion dans Git ;
- activer `EnableSensitiveDataLogging` ;
- exposer les entités EF hors Infrastructure ;
- utiliser un `DbContext` long-lived dans Desktop ;
- permettre un stock négatif ;
- modifier ou supprimer un mouvement historique ;
- faire de fallback silencieux vers l’ancien stock synthétique ;
- inventer un seuil de péremption ;
- prétendre qu’une proposition FEFO est une obligation réglementaire.

Interdictions Git :

- `git add`
- commit
- push
- remote
- changement de branche
- reset
- stash
- rebase
- amend
- réécriture d’historique.

---

## 7. NOYAU MÉTIER INVENTORY

Créer un noyau minimal dans Domain.

### Types possibles

Adapter les noms aux conventions du dépôt :

- `StockLocationId`
- `StockBatchId`
- `InventorySessionId`
- `StockQuantity`
- `StockMovementQuantity`
- `StockMovementType`
- `InventorySessionStatus`
- `ExpiryDate`
- `LotNumber`
- `InventoryErrors`

### Quantités

Pour V1 :

- quantités entières ;
- aucune fraction ;
- balance toujours supérieure ou égale à zéro ;
- un mouvement possède une quantité strictement positive et un sens/type ;
- aucune utilisation de `double` ou `float`.

Ne pas réutiliser `SaleQuantity` comme quantité générale de stock.

### Types de mouvement minimum

Représenter explicitement :

- `Opening`
- `ManualIncrease`
- `ManualDecrease`
- `InventoryIncrease`
- `InventoryDecrease`

Les mouvements provenant des achats, ventes, retours ou transferts sont différés.

Ne pas créer un enum contenant des types non implémentés uniquement “pour plus tard”.

### Invariants

- quantité positive ;
- balance non négative ;
- produit valide ;
- emplacement valide ;
- lot requis lorsqu’un suivi de lot est activé ;
- expiration requise lorsqu’un suivi d’expiration est activé ;
- expiration cohérente avec le lot ;
- un mouvement historique est immuable ;
- une opération répétée avec le même `OperationId` ne doit pas doubler le stock.

### Lots et expiration

Respecter les paramètres confirmés dans le modèle Catalogue.

Si les colonnes de configuration n’existent pas encore, ajouter uniquement celles confirmées par Phase 2/3, par exemple :

- suivi de lot requis ;
- suivi de péremption requis.

Ne pas inventer de seuil d’alerte global.

Une date d’expiration est une date sans heure (`date`) si le modèle validé le confirme.

Un lot expiré :

- reste visible dans le stock ;
- n’est pas inclus dans la disponibilité vendable ;
- ne peut pas être proposé par FEFO ;
- n’est pas supprimé automatiquement.

---

## 8. SCHÉMA PHYSIQUE INVENTORY

Utiliser le schéma SQL :

`inventory`

### Tables minimales attendues

Le modèle final doit suivre Phase 3. La cible minimale est :

1. `inventory.StockLocations`
2. `inventory.StockBatches`
3. `inventory.StockBalances`
4. `inventory.StockMovements`
5. `inventory.InventorySessions`
6. `inventory.InventoryCountLines`

Si Phase 3 confirme des noms ou cardinalités différents, suivre Phase 3 et documenter l’écart.

### StockLocations

Contenir au minimum :

- `Id bigint identity` ;
- code unique ;
- nom ;
- état actif ;
- emplacement principal éventuel si confirmé ;
- `CreatedAtUtc datetime2` ;
- `RowVersion rowversion`.

Aucune suppression en cascade.

### StockBatches

Contenir au minimum :

- `Id bigint identity` ;
- `ProductId bigint` vers `catalog.Products` ;
- lot textuel facultatif ou requis selon configuration ;
- date d’expiration facultative ou requise selon configuration ;
- état actif ou statut minimal si nécessaire ;
- `CreatedAtUtc datetime2` ;
- `RowVersion rowversion`.

Contraintes :

- lot non blanc lorsqu’il est fourni ;
- unicité cohérente produit + lot + expiration selon le modèle ;
- aucune date/lot réel dans le seed.

### StockBalances

Contenir au minimum :

- `Id bigint identity` ou clé conforme au modèle ;
- `StockLocationId` ;
- `ProductId` ;
- `StockBatchId` selon la cardinalité retenue ;
- quantité disponible entière ;
- `UpdatedAtUtc datetime2` ;
- `RowVersion rowversion`.

Contraintes :

- quantité >= 0 ;
- unicité d’une balance par emplacement/produit/lot ;
- FK restrictives ;
- index pour lecture par produit et emplacement.

Ne pas stocker un total produit redondant si celui-ci peut être agrégé de façon fiable.

### StockMovements

Journal append-only contenant au minimum :

- `Id bigint identity` ;
- `OperationId uniqueidentifier` ;
- `ProductId` ;
- `StockLocationId` ;
- `StockBatchId` ;
- type ;
- quantité positive ;
- balance avant ;
- balance après ;
- raison ou note contrôlée ;
- source/type de source minimal ;
- référence source textuelle facultative ;
- `OccurredAtUtc datetime2` ;
- `CreatedAtUtc datetime2`.

Contraintes :

- `OperationId` unique selon la granularité validée ;
- quantité > 0 ;
- balances >= 0 ;
- cohérence type/before/after ;
- aucun update/delete fourni par l’application.

Aucun `rowversion` nécessaire si le mouvement est réellement immutable.

### InventorySessions

Contenir au minimum :

- `Id bigint identity` ou Guid technique selon Phase 3 ;
- référence technique ;
- emplacement ;
- statut ;
- date de début UTC ;
- date de finalisation UTC facultative ;
- note ;
- `RowVersion rowversion`.

États minimum :

- Draft
- InProgress
- Completed
- Cancelled

Une session Completed ou Cancelled ne peut plus être modifiée.

### InventoryCountLines

Contenir au minimum :

- `Id bigint identity` ;
- `InventorySessionId` ;
- produit ;
- lot ;
- quantité théorique capturée ;
- quantité comptée ;
- écart calculé ou stocké selon le modèle ;
- `RowVersion rowversion`.

Contraintes :

- comptage >= 0 ;
- unicité session/produit/lot ;
- aucune finalisation partielle silencieuse.

### Types SQL

- IDs persistants : `bigint` ;
- `OperationId` : `uniqueidentifier` ;
- quantités : entier adapté et documenté ;
- dates instant : `datetime2` UTC ;
- expiration : `date` ;
- textes : `nvarchar` avec longueurs explicites ;
- aucune quantité en `float`/`real` ;
- `rowversion` pour entités mutables.

### Suppression

Utiliser `NO_ACTION` ou `RESTRICT`.

Aucune suppression en cascade des historiques, lots ou balances.

---

## 9. CAS D’UTILISATION APPLICATION

Créer des contrats spécifiques, jamais `IRepository<T>`.

### Lectures

- `GetStockOverview`
- `SearchStockProducts`
- `GetProductStockDetails`
- `GetStockMovements`
- `GetExpiringBatches`
- `GetExpiredBatches`
- `SuggestFefoAllocation`
- `GetInventorySession`
- `ListInventorySessions`

### Mutations

- créer un emplacement ;
- activer/désactiver un emplacement ;
- enregistrer un stock d’ouverture ;
- ajuster à la hausse ;
- ajuster à la baisse ;
- créer/démarrer une session d’inventaire ;
- enregistrer ou modifier une ligne de comptage ;
- finaliser une session ;
- annuler une session non finalisée.

### Modèles de lecture minimum

- `StockOverviewItem`
- `ProductStockDetails`
- `StockBatchView`
- `StockMovementView`
- `FefoSuggestionView`
- `InventorySessionView`
- `InventoryCountLineView`

Collections en lecture seule.

Aucune entité EF exposée.

### Erreurs stables

Créer au minimum :

- `inventory.location.not_found`
- `inventory.location.inactive`
- `inventory.product.not_found`
- `inventory.batch.not_found`
- `inventory.batch.expired`
- `inventory.lot.required`
- `inventory.expiry.required`
- `inventory.quantity.invalid`
- `inventory.stock.insufficient`
- `inventory.operation.invalid`
- `inventory.operation.conflict`
- `inventory.concurrency_conflict`
- `inventory.session.not_found`
- `inventory.session.invalid_status`
- `inventory.session.already_completed`
- `inventory.database.unavailable`

Les erreurs attendues utilisent `Result`, pas des exceptions.

---

## 10. TRANSACTIONS, IDEMPOTENCE ET CONCURRENCE

### Écriture de stock

Toute mutation doit suivre :

```text
charger produit/configuration/emplacement/lot/balance
→ vérifier invariants et rowversion
→ créer le mouvement
→ mettre à jour la balance
→ sauvegarder dans une transaction unique
```

Aucun mouvement ne peut être enregistré si la balance correspondante n’est pas mise à jour.

Aucune balance ne peut être mise à jour sans mouvement associé.

### Stock négatif

Une sortie ou diminution supérieure à la balance disponible :

- retourne `inventory.stock.insufficient` ;
- ne crée aucun mouvement ;
- ne modifie aucune balance.

### Idempotence

Chaque mutation reçoit un `OperationId` non vide.

Même `OperationId` + même empreinte :

- retourne le résultat précédent ;
- ne double pas le mouvement.

Même `OperationId` + empreinte différente :

- retourne `inventory.operation.conflict`.

L’empreinte doit inclure au minimum :

- type de mouvement ;
- produit ;
- emplacement ;
- lot ;
- quantité ;
- source/référence.

### Concurrence

Les balances, lots, emplacements, sessions et lignes de comptage utilisent `rowversion`.

Sur `DbUpdateConcurrencyException` :

- rollback ;
- erreur Application ;
- aucun retry automatique ;
- aucun last-write-wins.

### Stratégie d’exécution

Respecter `EnableRetryOnFailure()` et exécuter correctement les transactions avec la stratégie EF Core.

### Mouvements immuables

Aucun cas d’utilisation d’update ou delete de mouvement.

Les corrections futures passent par des mouvements inverses, mais cette fonctionnalité complète peut être différée si elle n’est pas nécessaire à V0.1.

---

## 11. FEFO ET DISPONIBILITÉ POS

### Disponibilité SQL

Remplacer l’ancien lecteur synthétique de disponibilité par un lecteur SQL réel.

La disponibilité vendable d’un produit est la somme des balances :

- emplacements actifs ;
- produits actifs et localement actifs ;
- lots actifs ;
- lots non expirés ;
- quantités positives.

Ne pas inclure les lots expirés.

Ne pas réserver le stock dans ce sprint.

Le panier reste non persistant et n’immobilise aucune quantité.

Documenter clairement ce risque temporaire.

### FEFO

Créer une proposition descriptive :

1. lots avec expiration non expirée, par date la plus proche ;
2. en cas d’égalité, ordre déterministe par création/Id ;
3. lots sans expiration selon une règle déterministe documentée ;
4. ne jamais inclure les lots expirés ;
5. retourner une erreur si la quantité totale disponible est insuffisante.

FEFO est une proposition technique configurable, pas une obligation légale affirmée.

Le POS peut afficher la disponibilité totale et, si utile, la proposition FEFO dans le détail technique, sans consommer le stock.

### Expiration

Utiliser la date locale de travail uniquement pour l’affichage.

Les comparaisons métier de date doivent être testables via un contrat d’horloge/date injecté.

Ne pas appeler directement `DateTime.Today` dans le Domain ou Application.

---

## 12. INVENTAIRES

### Création

Une session est créée pour un emplacement actif.

Elle commence en Draft ou InProgress selon le flux documenté.

### Capture théorique

À l’ouverture/démarrage :

- capturer les quantités théoriques par produit/lot ;
- conserver cette référence même si le stock évolue ensuite ;
- ne pas modifier les balances.

### Comptage

L’utilisateur peut saisir une quantité comptée entière >= 0.

Une seule ligne par session/produit/lot.

### Finalisation

La finalisation doit :

1. vérifier le statut ;
2. vérifier les rowversions ;
3. calculer les écarts ;
4. créer un mouvement d’augmentation ou diminution pour chaque écart non nul ;
5. mettre à jour toutes les balances ;
6. marquer la session Completed ;
7. exécuter le tout dans une transaction ;
8. être idempotente ;
9. rollback complètement si une ligne échoue.

Une session Completed ne peut plus être modifiée ou finalisée à nouveau.

### Annulation

Une session Draft/InProgress peut être annulée.

L’annulation ne modifie pas le stock.

---

## 13. DONNÉES SYNTHÉTIQUES ET MIGRATION

### Migration

Créer :

`InventoryPersistenceV01`

Ne modifier aucune migration précédente.

Inspecter la migration générée :

- schéma ;
- tables ;
- types ;
- PK/FK ;
- contraintes ;
- index ;
- `rowversion` ;
- `Down`.

Générer un script idempotent versionné, par exemple :

`database/scripts/003_inventory_persistence_v01_idempotent.sql`

### Seed Inventory

Créer :

`database/dev/seed_synthetic_inventory_v01.sql`

Exigences :

- cible uniquement `Pharmacy_Development` ;
- transaction ;
- idempotent ;
- utilise uniquement les produits `DEMO-*` ;
- crée un emplacement principal synthétique ;
- crée des lots Demo pour certains produits ;
- crée des stocks sans lot uniquement si la configuration le permet ;
- ajoute des dates futures synthétiques ;
- peut inclure un lot expiré Demo pour validation, clairement marqué ;
- crée des mouvements Opening ;
- crée les balances correspondantes ;
- aucun doublon au second passage ;
- aucune donnée réelle.

Créer :

`database/dev/clear_synthetic_inventory_v01.sql`

Il doit :

- refuser toute autre base ;
- supprimer uniquement les mouvements/balances/lots/sessions Demo ;
- respecter les FK ;
- ne supprimer aucune donnée non Demo ;
- ne supprimer aucune table.

Exécuter le seed deux fois et vérifier l’absence de duplication.

---

## 14. DESKTOP ET UX

### Composition root

Enregistrer les adaptateurs SQL Inventory.

Retirer l’ancien lecteur synthétique de disponibilité du POS.

Conserver :

- SaleDraft en mémoire ;
- paiements simulés ;
- impression simulée ;
- suspensions Desktop.

### Panne SQL

Si SQL est indisponible :

- le shell démarre ;
- Catalogue, Stock et Ventes affichent un état indisponible ;
- aucun fallback silencieux ;
- aucune exception brute ;
- Diagnostic clair ;
- aucune migration automatique.

### Page Stock

Remplacer le placeholder Stock par une page fonctionnelle.

Prévoir :

- résumé par produit ;
- recherche ;
- filtre emplacement ;
- filtre disponible/épuisé/expiré ;
- détail des lots ;
- quantité disponible ;
- lots expirés visibles séparément ;
- historique des mouvements ;
- ouverture de stock ;
- ajustement hausse/baisse ;
- confirmation des actions sensibles ;
- gestion des erreurs ;
- concurrence ;
- sessions d’inventaire ;
- lignes de comptage ;
- finalisation ;
- états loading/vide/erreur/indisponible ;
- clavier, focus et responsive.

Ne pas créer une interface surchargée.

### POS

Vérifier :

- disponibilité venant de SQL ;
- produit sans stock non ajoutable ou clairement bloqué selon les handlers existants ;
- lot expiré exclu ;
- FEFO proposé si le flux l’affiche ;
- aucune décrémentation réelle du stock lors d’une vente simulée ;
- mention explicite : vente non persistée, stock non consommé ;
- code inconnu inchangé ;
- catalogue toujours SQL.

### Diagnostic

Afficher sans secret :

- connectivité SQL ;
- migrations ;
- nombre d’emplacements ;
- nombre de lots ;
- nombre de balances positives ;
- nombre de lots expirés Demo ;
- stock réel SQL actif ;
- ventes toujours non persistées.

---

## 15. TESTS ET CRITÈRES D’ACCEPTATION

Conserver tous les tests existants.

Ajouter au minimum les contrôles suivants.

### Domain

1. quantité stock positive ;
2. zéro/négatif refusé lorsque requis ;
3. balance négative impossible ;
4. types de mouvement validés ;
5. lot requis selon configuration ;
6. expiration requise selon configuration ;
7. lot expiré reconnu avec horloge déterministe ;
8. aucune dépendance EF dans Domain.

### Architecture

9. Application sans Infrastructure ;
10. Desktop sans DbContext ;
11. aucun modèle EF exposé ;
12. aucun repository générique ;
13. aucun nouveau projet ;
14. aucun package non autorisé.

### Schéma SQL

15. schéma inventory ;
16. tables attendues ;
17. PK bigint ;
18. OperationId uniqueidentifier ;
19. FK correctes ;
20. delete NO_ACTION/RESTRICT ;
21. quantités non négatives ;
22. mouvements quantité positive ;
23. index produit/emplacement/lot ;
24. rowversion sur mutables ;
25. aucun float/real ;
26. expiration en date ;
27. aucune table hors périmètre.

### Emplacements et lots

28. créer emplacement ;
29. code unique ;
30. désactivation ;
31. emplacement inactif refusé pour mutation ;
32. créer lot ;
33. lot dupliqué selon contrainte refusé ;
34. lot/expiration requis ;
35. lot expiré non vendable ;
36. rowversion et conflit.

### Balances et mouvements

37. stock d’ouverture ;
38. mouvement Opening ;
39. balance correspondante ;
40. augmentation ;
41. diminution ;
42. stock insuffisant refusé ;
43. aucun mouvement après refus ;
44. aucune balance partielle ;
45. balance before/after cohérente ;
46. mouvement immutable ;
47. idempotence même OperationId ;
48. conflit OperationId ;
49. conflit rowversion ;
50. transaction rollback.

### Lectures et FEFO

51. résumé par produit ;
52. détail par lot ;
53. historique des mouvements ;
54. lots expirés ;
55. lots proches selon horizon fourni, sans seuil inventé ;
56. disponibilité POS SQL ;
57. lot expiré exclu ;
58. emplacement inactif exclu ;
59. FEFO date la plus proche ;
60. FEFO quantité multi-lots ;
61. FEFO insuffisant ;
62. ordre déterministe sans expiration.

### Inventaires

63. créer session ;
64. emplacement invalide refusé ;
65. capture théorique ;
66. ligne unique ;
67. comptage >= 0 ;
68. modifier comptage ;
69. finaliser sans écarts ;
70. finaliser avec hausse ;
71. finaliser avec baisse ;
72. mouvements générés ;
73. balances mises à jour ;
74. transaction atomique ;
75. finalisation idempotente ;
76. finalisation double refusée ;
77. session Completed immuable ;
78. annulation sans mouvement ;
79. conflit concurrence.

### Seed

80. cible Pharmacy_Development uniquement ;
81. seed idempotent ;
82. emplacement Demo ;
83. lots Demo ;
84. balances Demo ;
85. mouvements Opening ;
86. aucune donnée réelle ;
87. clear supprime Demo uniquement ;
88. aucun seed runtime.

### Desktop

89. page Stock accessible ;
90. lectures SQL ;
91. ajustement via coordinateur ;
92. stock insuffisant mappé ;
93. inventaire via coordinateur ;
94. double soumission bloquée ;
95. POS utilise disponibilité SQL ;
96. ancien stock synthétique non enregistré ;
97. vente simulée ne décrémente pas ;
98. panne SQL sûre ;
99. Diagnostic sans secret ;
100. Catalogue et UI Lab non régressés.

### SQL réel

101. base temporaire avec préfixe sécurisé ;
102. migrations appliquées ;
103. contraintes réelles ;
104. rowversion réelle ;
105. idempotence réelle ;
106. transactions réelles ;
107. rollback réel ;
108. inventaire réel ;
109. base temporaire supprimée ;
110. aucune base résiduelle ;
111. aucun SQLite/EF InMemory comme preuve SQL ;
112. aucune migration en attente.

### Sécurité

113. aucun secret ;
114. sensitive logging désactivé ;
115. aucune migration runtime ;
116. aucune stack trace UI ;
117. CancellationToken propagé ;
118. aucun update/delete de mouvement exposé.

### Commandes obligatoires

```powershell
dotnet tool restore
dotnet restore Pharmacy.slnx
dotnet build Pharmacy.slnx -c Debug --no-restore
dotnet test Pharmacy.slnx -c Debug --no-build
dotnet build Pharmacy.slnx -c Release --no-restore
dotnet test Pharmacy.slnx -c Release --no-build

dotnet ef migrations list ...
dotnet ef migrations has-pending-model-changes ...
dotnet ef database update ...
```

Exécuter aussi :

- vérifications Sprints 9 et 10 ;
- seed Inventory deux fois ;
- vérification Sprint 11 ;
- tests SQL réels ;
- rollback/réapplication de la migration sur base temporaire ;
- contrôle des bases résiduelles.

Attendu :

- zéro erreur ;
- zéro avertissement raisonnable ;
- tous les tests réussis ;
- aucun ignoré ;
- aucune migration en attente ;
- aucun secret ;
- aucune base temporaire résiduelle.

Lancer Desktop et contrôler techniquement Stock SQL, POS avec disponibilité SQL, Catalogue, Diagnostic, UI Lab, aucune migration automatique et fermeture propre.

Ne pas revendiquer de validation visuelle humaine.

---

## 16. PLAN D’EXÉCUTION OBLIGATOIRE

1. vérifier le marqueur final ;
2. lire les sources ;
3. vérifier Git ;
4. baseline ;
5. vérifier SQL et migrations ;
6. exécuter vérifications Sprints 9/10 ;
7. produire la matrice de traçabilité ;
8. concevoir Domain Inventory minimal ;
9. concevoir le schéma inventory ;
10. créer erreurs, modèles et contrats ;
11. créer lectures et mutations Application ;
12. créer entités/configurations EF ;
13. créer migration ;
14. inspecter migration ;
15. générer script idempotent ;
16. implémenter adaptateurs SQL ;
17. transactions/idempotence/concurrence ;
18. disponibilité POS et FEFO ;
19. sessions d’inventaire ;
20. seed/clear ;
21. appliquer migration ;
22. exécuter seed deux fois ;
23. créer vérification Sprint 11 ;
24. connecter Desktop ;
25. page Stock ;
26. adapter POS et Diagnostic ;
27. tests unitaires ;
28. tests SQL réels ;
29. test panne SQL ;
30. Debug/Release ;
31. migrations en attente ;
32. rollback/réapplication temporaire ;
33. lancement Desktop ;
34. documentation ;
35. contrôle secrets/diff/bases ;
36. rapport final.

---

## 17. VALIDATION MANUELLE À FOURNIR

Fournir les commandes de :

- build ;
- tests ;
- seed Inventory ;
- vérification SQL ;
- lancement Desktop.

### SSMS

Demander de vérifier :

- schéma `inventory` ;
- tables ;
- emplacement Demo ;
- lots Demo ;
- balances ;
- mouvements Opening ;
- lot expiré Demo ;
- rowversions ;
- aucune duplication après second seed ;
- aucune donnée réelle ;
- migrations à jour.

### Page Stock

Demander de vérifier :

- liste et recherche ;
- détail des lots ;
- quantités ;
- lots expirés séparés ;
- historique des mouvements ;
- ajustement hausse ;
- ajustement baisse ;
- blocage stock insuffisant ;
- conflit/erreur clair ;
- création et finalisation d’un inventaire ;
- mouvements d’écart ;
- double soumission bloquée ;
- clavier/focus/responsive.

### POS

Demander de vérifier :

- disponibilité venant de SQL ;
- produit sans stock bloqué ;
- lot expiré exclu ;
- FEFO cohérent ;
- ajout au panier ;
- vente toujours non persistée ;
- stock non décrémenté après succès simulé ;
- paiement et impression toujours simulés.

### Panne SQL

- shell démarre ;
- Stock/Catalogue/Ventes indisponibles ;
- aucun fallback ;
- aucune exception brute ;
- Diagnostic clair ;
- aucune migration automatique.

### Captures demandées

1. SSMS avec tables inventory ;
2. balances et mouvements Demo ;
3. page Stock liste ;
4. détail produit/lots ;
5. ajustement réussi ;
6. inventaire finalisé ;
7. POS avec disponibilité SQL ;
8. Diagnostic/panne SQL ;
9. script de vérification.

Ne pas demander de commit.

---

## 18. DOCUMENTATION, RAPPORT FINAL ET RESTRICTIONS GIT

Créer :

- `docs/domain/INVENTORY_CORE_V0.1.md`
- `docs/application/INVENTORY_USE_CASES_V0.1.md`
- `docs/infrastructure/SQL_INVENTORY_ADAPTERS_V0.1.md`
- `docs/database/INVENTORY_PHYSICAL_SCHEMA_V0.1.md`
- `docs/database/INVENTORY_ERD_V0.1.mmd`
- `docs/ux/STOCK_MANAGEMENT_UI_V0.1.md`
- `docs/reports/sprint-11/inventory-validation.md`
- `docs/reports/sprint-11/codex-report.md`
- scripts migration/seed/clear/vérification.

Mettre à jour :

- `README.md`
- `docs/governance/CURRENT_STATE.md`
- `docs/governance/DECISIONS_REGISTER.md` seulement pour les nouvelles décisions techniques.

### Rapport Codex obligatoire

Créer :

`docs/reports/sprint-11/codex-report.md`

Structure :

# Sprint 11 — Rapport d’exécution Codex

## 1. Statut final
## 2. Résumé
## 3. Complétude du prompt
## 4. Documents consultés
## 5. État Git initial
## 6. Baseline .NET
## 7. État SQL initial
## 8. Matrice de traçabilité
## 9. Noyau Domain Inventory
## 10. Contrats et modèles Application
## 11. Cas d’utilisation
## 12. Schéma physique inventory
## 13. Tables et relations
## 14. Contraintes et index
## 15. Rowversion et concurrence
## 16. Mouvements append-only
## 17. Idempotence OperationId
## 18. Balances et atomicité
## 19. Lots et péremption
## 20. Disponibilité POS
## 21. FEFO
## 22. Sessions d’inventaire
## 23. Migration InventoryPersistenceV01
## 24. Script idempotent
## 25. Seed et clear
## 26. Données synthétiques
## 27. Adaptateurs SQL
## 28. Composition root Desktop
## 29. Page Stock
## 30. Adaptation POS
## 31. Diagnostic et panne SQL
## 32. Sécurité et secrets
## 33. Packages
## 34. Tests ajoutés
## 35. Tests SQL réels
## 36. Commandes exécutées
## 37. Résultats Debug
## 38. Résultats Release
## 39. Migrations en attente
## 40. Rollback et réapplication
## 41. Bases temporaires résiduelles
## 42. Lancement Desktop
## 43. Problèmes rencontrés
## 44. Écarts
## 45. Risques et limites
## 46. Validation manuelle requise
## 47. État Git final
## 48. Recommandation

Dans l’état Git final, indiquer :

- branche ;
- HEAD initial ;
- fichiers modifiés ;
- fichiers non suivis ;
- fichiers stagés : zéro ;
- nouveaux commits : zéro ;
- remotes : zéro ;
- push : absent.

À la fin, afficher une synthèse copiable :

- statut ;
- SQL Server ;
- schéma inventory ;
- migration ;
- Domain/Application ;
- mouvements ;
- balances ;
- lots/péremption ;
- FEFO ;
- inventaires ;
- seed ;
- page Stock ;
- POS SQL ;
- panne SQL ;
- packages ;
- builds ;
- tests ;
- secrets ;
- écarts ;
- validation manuelle ;
- Git.

### Restrictions Git absolues

- NE PAS EXÉCUTER `git add`.
- NE PAS COMMIT.
- NE PAS PUSH.
- NE PAS CRÉER DE REMOTE.
- NE PAS CHANGER DE BRANCHE.
- NE PAS STASH.
- NE PAS RESET.
- NE PAS RÉÉCRIRE L’HISTORIQUE.
- LAISSER TOUTES LES MODIFICATIONS VISIBLES POUR REVUE.

FIN DU PROMPT SPRINT 11
