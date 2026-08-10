# SPRINT 12 — FOURNISSEURS, ACHATS ET RÉCEPTIONS SQL V0.1

## Instruction courte à donner à Codex

Place ce fichier dans le dépôt sous :

`docs/prompts/Sprint_12_Fournisseurs_Achats_Receptions_SQL.md`

Puis donne uniquement cette instruction à Codex :

> Lis intégralement `docs/prompts/Sprint_12_Fournisseurs_Achats_Receptions_SQL.md`, vérifie que la dernière ligne non vide est exactement `FIN DU PROMPT SPRINT 12`, puis exécute toutes les opérations autorisées. Ne fais aucun commit, push, changement de branche ou ajout de remote.

---

## 1. RÔLE

Tu agis comme :

- ingénieur logiciel senior .NET 10 ;
- architecte Clean Architecture et monolithe modulaire ;
- spécialiste EF Core 10 et SQL Server 2025 ;
- analyste fonctionnel des achats et réceptions en pharmacie ;
- spécialiste des transactions multi-tables ;
- spécialiste des lots, péremptions et mouvements de stock ;
- responsable de l’idempotence et de la concurrence optimiste ;
- développeur WPF et Blazor Hybrid ;
- responsable des tests unitaires, d’intégration SQL et de non-régression ;
- responsable de la sécurité des données et de la documentation.

Tu travailles sur le projet professionnel **Logiciel Pharmacie**.

Les éléments suivants sont déjà validés :

- Catalogue persistant SQL Server ;
- recherche POS depuis SQL ;
- Stock SQL réel ;
- lots et péremptions ;
- balances et mouvements ;
- inventaires ;
- concurrence par `rowversion` ;
- blocage du stock négatif ;
- proposition FEFO ;
- 321 tests réussis ;
- aucune migration automatique au démarrage ;
- aucune vente persistée.

Ta mission est de créer le module **Fournisseurs, achats et réceptions V0.1**, puis de connecter les réceptions au moteur de stock SQL.

Ne t’arrête pas après le plan. Exécute toutes les opérations autorisées.

---

## 2. MISSION

Le Sprint 12 doit permettre :

1. de gérer les fournisseurs ;
2. de créer une commande d’achat ;
3. d’ajouter, modifier et supprimer des lignes avant validation ;
4. de valider une commande ;
5. d’enregistrer un bon de livraison fournisseur ;
6. d’effectuer une réception directe sans commande si autorisée par le périmètre validé ;
7. d’effectuer une réception partielle ou complète d’une commande ;
8. de choisir ou créer un lot lors de la réception ;
9. de saisir une date de péremption lorsque le produit l’exige ou lorsque l’information est disponible ;
10. d’enregistrer le prix d’achat sans inventer de marge ;
11. d’augmenter les balances de stock de manière atomique ;
12. de créer les mouvements d’entrée correspondants ;
13. de bloquer toute double réception ;
14. de rendre l’opération idempotente par `OperationId` ;
15. de gérer la concurrence par `rowversion` ;
16. de conserver un journal immuable de réception ;
17. d’afficher une page **Achats et réceptions** ;
18. de fournir des lectures liste/détail ;
19. de tester sur un vrai SQL Server ;
20. de laisser les ventes et paiements non persistés ;
21. de ne faire aucun commit, push ou remote.

### Statut attendu

`IMPLEMENTED — SQL PURCHASING VALIDATION PASS — MANUAL RECEIVING REVIEW REQUIRED`

---

## 3. SOURCES DE VÉRITÉ

Lire intégralement avant toute modification :

- fichiers racine et gouvernance ;
- `docs/database/PERSISTENCE_FOUNDATION_V0.1.md` ;
- `docs/database/CATALOG_PHYSICAL_SCHEMA_V0.1.md` ;
- `docs/database/INVENTORY_PHYSICAL_SCHEMA_V0.1.md` ;
- `docs/application/CATALOG_USE_CASES_V0.1.md` ;
- `docs/infrastructure/SQL_CATALOG_ADAPTERS_V0.1.md` ;
- `docs/reports/sprint-10/codex-report.md` ;
- `docs/reports/sprint-11/codex-report.md` ;
- `docs/reports/sprint-11/inventory-validation.md` ;
- migrations EF Core actuelles ;
- scripts SQL Catalogue et Inventory ;
- le présent fichier jusqu’au marqueur final.

Inspecter complètement :

- `src/Pharmacy.Domain`
- `src/Pharmacy.Application`
- `src/Pharmacy.Infrastructure`
- `src/Pharmacy.Desktop`
- `tests/Pharmacy.UnitTests`
- `tests/Pharmacy.IntegrationTests`
- `database`
- `.config/dotnet-tools.json`
- les fichiers projet.

Consulter les cinq PDF de référence en lecture seule, en particulier les sections Phase 2 et Phase 3 relatives à :

- fournisseurs ;
- commandes ;
- bons de livraison ;
- réceptions ;
- lots ;
- prix d’achat ;
- mouvements ;
- statuts.

Ne modifier aucun PDF.

Ordre de priorité :

1. présent prompt ;
2. décisions validées ;
3. état courant ;
4. scope ;
5. Phase 2 et Phase 3 ;
6. schéma SQL réellement appliqué ;
7. code existant.

Distinguer clairement :

- information confirmée par la pharmacie ;
- règle fonctionnelle validée ;
- décision technique ;
- hypothèse de prototype ;
- donnée synthétique ;
- élément différé.

---

## 4. ÉTAT INITIAL ET PRÉREQUIS

Le Sprint 11 est annoncé comme validé et commité.

Ne suppose aucun hash.

Exécute :

```powershell
git status
git status --short
git branch --show-current
git log --oneline -n 10
git remote -v
git diff --check

dotnet tool restore
dotnet restore Pharmacy.slnx
dotnet build Pharmacy.slnx -c Debug --no-restore
dotnet test Pharmacy.slnx -c Debug --no-build
```

Attendu :

- branche `main` ;
- working tree propre ;
- index vide ;
- aucun remote ;
- six projets ;
- au moins 321 tests ;
- zéro erreur ;
- zéro avertissement raisonnable.

Utiliser `PHARMACY_SQLSERVER_CONNECTION` ciblant explicitement `Pharmacy_Development`.

Vérifier :

- version et édition SQL Server ;
- migrations appliquées ;
- absence de changement de modèle en attente ;
- schémas `catalog` et `inventory` ;
- seed synthétique existant ;
- base sans données réelles.

Aucun nouveau package ne devrait être nécessaire.

---

## 5. PÉRIMÈTRE AUTORISÉ

Tu peux modifier ou créer des fichiers dans :

- `Pharmacy.Domain` si un noyau Purchasing minimal est justifié ;
- `Pharmacy.Application` ;
- `Pharmacy.Infrastructure` ;
- `Pharmacy.Desktop` ;
- tests unitaires et intégration ;
- `database/dev` ;
- `database/verification` ;
- README et gouvernance.

Tu peux créer :

- `docs/domain/PURCHASING_CORE_V0.1.md`
- `docs/application/PURCHASING_USE_CASES_V0.1.md`
- `docs/infrastructure/SQL_PURCHASING_ADAPTERS_V0.1.md`
- `docs/database/PURCHASING_PHYSICAL_SCHEMA_V0.1.md`
- `docs/database/PURCHASING_ERD_V0.1.mmd`
- `docs/ux/PURCHASING_RECEIVING_UI_V0.1.md`
- `docs/reports/sprint-12/purchasing-validation.md`
- `docs/reports/sprint-12/codex-report.md`
- `database/dev/seed_synthetic_purchasing_v01.sql`
- `database/dev/clear_synthetic_purchasing_v01.sql`
- `database/verification/verify_sprint12_purchasing.sql`

Créer une migration EF Core minimale, par exemple :

`PurchasingPersistenceV01`

Ne jamais modifier les migrations déjà validées.

---

## 6. HORS PÉRIMÈTRE

Ne développer aucun des éléments suivants :

- ventes persistées ;
- finalisation de vente ;
- paiements ;
- caisse ;
- retours fournisseurs ;
- avoirs fournisseurs ;
- facturation comptable complète ;
- calcul de marge ;
- TVA inventée ;
- échéancier de paiement fournisseur ;
- import automatique fournisseur ;
- OCR de bons de livraison ;
- e-mail ou API fournisseur ;
- authentification et permissions réelles ;
- reporting avancé ;
- impression réelle ;
- stock réservé ;
- multi-pharmacie ;
- cloud.

Ne créer aucun fournisseur réel.

Ne créer aucune donnée bancaire.

Ne créer aucun médicament réel.

Ne créer aucun trigger ou procédure stockée.

Ne créer aucun repository générique.

Ne pas utiliser :

- `EnsureCreated()` ;
- migration automatique au runtime ;
- seed automatique au démarrage ;
- secrets versionnés ;
- `EnableSensitiveDataLogging` ;
- entités EF exposées hors Infrastructure ;
- DbContext dans Desktop ;
- `.Result`, `.Wait()` ou `Task.Run` pour contourner l’asynchronisme.

Interdictions Git :

- `git add`
- commit
- push
- remote
- changement de branche
- reset
- stash
- rebase.

---

## 7. ARCHITECTURE ET SCHÉMA PHYSIQUE

Créer un schéma SQL dédié :

`purchasing`

Le périmètre minimal attendu est :

1. `purchasing.Suppliers`
2. `purchasing.PurchaseOrders`
3. `purchasing.PurchaseOrderLines`
4. `purchasing.GoodsReceipts`
5. `purchasing.GoodsReceiptLines`

Ajouter uniquement les tables de support réellement justifiées par Phase 3.

Ne pas créer de tables spéculatives.

### Relations minimales

```text
Supplier
  ├── PurchaseOrders
  └── GoodsReceipts

PurchaseOrder
  └── PurchaseOrderLines

GoodsReceipt
  └── GoodsReceiptLines

GoodsReceiptLine
  ├── Product
  ├── StockBatch
  └── StockMovement
```

### Identifiants

- PK métier persistantes : `bigint` / `long` ;
- `IDENTITY` ;
- OperationId : `uniqueidentifier` ;
- aucune PK `int` ;
- aucune GUID comme PK principale des tables métier.

### Rowversion

Ajouter `rowversion` sur les entités mutables :

- fournisseurs ;
- commandes ;
- lignes de commande si modifiables ;
- réceptions tant qu’elles ne sont pas finalisées.

Les lignes finalisées ou journaux immuables peuvent être append-only.

### Dates

- `datetime2` UTC pour les instants ;
- `date` pour péremption ;
- `SYSUTCDATETIME()` si défaut serveur justifié ;
- aucun `GETDATE()`.

### Montants

- `decimal(18,2)` sauf règle validée différente ;
- prix d’achat non négatif ;
- aucun type `money`, `float` ou `real`.

### Suppression

- `NO_ACTION` / `RESTRICT` ;
- désactivation plutôt que suppression lorsqu’il existe un historique ;
- aucune cascade dangereuse.

### Statuts

Utiliser uniquement des statuts validés et explicites.

Exemples possibles à confirmer dans les documents :

- commande : Draft, Validated, PartiallyReceived, FullyReceived, Cancelled ;
- réception : Draft, Completed, Cancelled.

Ne pas inventer de statut sans justification.

---

## 8. RÈGLES FONCTIONNELLES — FOURNISSEURS

Créer des cas d’utilisation pour :

- lister ;
- rechercher ;
- lire le détail ;
- créer ;
- modifier ;
- activer/désactiver.

Données minimales, selon Phase 3 :

- code interne ;
- raison sociale ou nom ;
- téléphone facultatif ;
- e-mail facultatif ;
- adresse facultative ;
- état actif ;
- rowversion.

Contraintes :

- code unique ;
- nom obligatoire ;
- aucune donnée bancaire ;
- aucune donnée fiscale inventée ;
- aucune suppression physique si référencé.

Erreurs stables :

- `purchasing.supplier.not_found`
- `purchasing.supplier.code_duplicate`
- `purchasing.supplier.inactive`
- `purchasing.supplier.concurrency_conflict`

---

## 9. RÈGLES FONCTIONNELLES — COMMANDES D’ACHAT

Une commande en brouillon peut :

- recevoir des lignes ;
- modifier quantités et prix d’achat ;
- supprimer des lignes ;
- être vidée ;
- être validée ;
- être annulée avant réception selon les règles confirmées.

Une commande validée :

- devient immuable sauf opérations explicitement autorisées ;
- ne modifie aucun stock ;
- ne crée aucun lot ;
- ne crée aucun mouvement.

Chaque ligne doit contenir au minimum :

- ProductId ;
- référence/nomenclature snapshot si validé ;
- quantité commandée entière positive ;
- prix d’achat unitaire facultatif ou requis selon la règle validée ;
- quantité reçue calculée depuis les réceptions ;
- reste à recevoir.

Aucune quantité négative ou fractionnaire en V1.

Aucune marge calculée.

Erreurs stables :

- `purchasing.order.not_found`
- `purchasing.order.invalid_status`
- `purchasing.order.empty`
- `purchasing.order.line_not_found`
- `purchasing.order.quantity_invalid`
- `purchasing.order.concurrency_conflict`

---

## 10. RÈGLES FONCTIONNELLES — RÉCEPTIONS

Une réception peut être :

- liée à une commande ;
- partielle ;
- complète ;
- directe sans commande uniquement si confirmé par le périmètre validé.

Données minimales :

- SupplierId ;
- PurchaseOrderId facultatif ;
- numéro de bon de livraison fournisseur ;
- date du document ;
- date/heure de réception UTC ;
- OperationId ;
- lignes ;
- statut ;
- CreatedAtUtc ;
- rowversion si mutable.

### Idempotence

La réception doit être idempotente.

Même `OperationId` + même empreinte :

- même résultat ;
- aucun mouvement dupliqué ;
- aucune balance augmentée deux fois.

Même `OperationId` + contenu différent :

- conflit explicite.

Erreur :

- `purchasing.receipt.operation_conflict`

### Bon de livraison

Le numéro de bon de livraison doit être unique au minimum par fournisseur si le modèle le confirme.

La duplication doit être refusée ou signalée explicitement.

### Réception partielle

Pour une commande :

- quantité reçue cumulée ≤ quantité commandée ;
- dépassement refusé ;
- statut PartiallyReceived ou FullyReceived calculé ;
- aucune modification partielle en cas d’échec.

### Lot et péremption

Pour chaque ligne :

- sélectionner un lot existant compatible ;
- ou créer un nouveau lot ;
- lot facultatif seulement pour les produits configurés sans lot ;
- péremption facultative ou obligatoire selon configuration produit ;
- aucun lot d’un autre produit ;
- lot expiré à la date de réception : avertissement ou refus selon règle validée, sans inventer.

### Stock

La finalisation d’une réception doit, dans une transaction unique :

1. valider fournisseur et produit ;
2. vérifier commande et quantités ;
3. créer ou réutiliser le lot ;
4. insérer la réception et ses lignes ;
5. créer les mouvements d’entrée ;
6. augmenter les balances ;
7. mettre à jour les quantités reçues de la commande ;
8. mettre à jour les statuts ;
9. commit.

En cas d’échec : rollback total.

### Prix d’achat

Le prix d’achat :

- est un snapshot de la réception ;
- ne modifie pas automatiquement le prix de vente ;
- ne calcule aucune marge ;
- ne doit pas être négatif.

Erreurs stables :

- `purchasing.receipt.not_found`
- `purchasing.receipt.duplicate_delivery_note`
- `purchasing.receipt.quantity_exceeds_order`
- `purchasing.receipt.invalid_lot`
- `purchasing.receipt.invalid_expiry`
- `purchasing.receipt.product_not_found`
- `purchasing.receipt.supplier_not_found`
- `purchasing.receipt.concurrency_conflict`
- `purchasing.receipt.database_unavailable`

---

## 11. APPLICATION DESKTOP ET UX

Remplacer le placeholder **Achats et réceptions** par une page fonctionnelle.

Prévoir :

### Fournisseurs

- liste ;
- recherche ;
- détail ;
- création/modification ;
- activation/désactivation.

### Commandes

- liste ;
- filtres statut/fournisseur/date ;
- nouvelle commande ;
- ajout de lignes par recherche Catalogue SQL ;
- modification quantité/prix achat ;
- total descriptif ;
- validation ;
- annulation contrôlée ;
- détail des quantités reçues et restantes.

### Réceptions

- réception liée à une commande ;
- réception directe si autorisée ;
- fournisseur ;
- numéro BL ;
- lignes ;
- quantité reçue ;
- prix achat ;
- lot ;
- date de péremption ;
- aperçu avant finalisation ;
- confirmation ;
- résultat ;
- aucun double clic.

### États UX

- loading ;
- vide ;
- erreur ;
- accès interdit futur ;
- SQL indisponible ;
- conflit rowversion ;
- duplicate BL ;
- réception partielle ;
- réception terminée.

### Cohérence visuelle

Réutiliser :

- design system existant ;
- cartes, bordures, ombres, tableaux ;
- même densité que Catalogue et Stock ;
- clavier et focus ;
- responsive ;
- aucun débordement horizontal.

### Avertissements visibles

Afficher clairement :

- stock mis à jour à la réception ;
- ventes toujours non persistées ;
- aucun calcul de marge ;
- données Demo seulement.

---

## 12. DONNÉES SYNTHÉTIQUES

Les migrations restent sans données de démonstration.

Créer :

`database/dev/seed_synthetic_purchasing_v01.sql`

Le seed doit :

- cibler uniquement `Pharmacy_Development` ;
- être transactionnel ;
- être idempotent ;
- créer 3 à 5 fournisseurs Demo ;
- créer quelques commandes Demo ;
- créer au moins une commande partiellement reçue ;
- créer au moins une réception Demo ;
- utiliser uniquement les produits `DEMO-*` ;
- ne créer aucune donnée réelle ;
- ne dépendre d’aucun Id fixe ;
- ne dupliquer aucune réception au second passage.

Créer un clear ciblé supprimant uniquement les données Demo Purchasing dans l’ordre FK correct.

Le clear ne doit pas effacer le Catalogue ou le Stock hors données Purchasing créées par le seed.

---

## 13. TESTS ET CRITÈRES D’ACCEPTATION

Conserver tous les tests existants.

Ajouter des tests couvrant au minimum :

### Architecture

- Domain sans EF ;
- Application sans Infrastructure ;
- Desktop sans DbContext ;
- aucun type EF exposé ;
- aucun repository générique ;
- aucun nouveau package non autorisé.

### Fournisseurs

- création ;
- code unique ;
- modification ;
- activation/désactivation ;
- rowversion ;
- fournisseur référencé protégé.

### Commandes

- création brouillon ;
- ajout/fusion de lignes ;
- quantité positive ;
- prix achat non négatif ;
- suppression ;
- validation d’une commande non vide ;
- commande vide refusée ;
- commande validée immuable ;
- concurrence ;
- statut calculé.

### Réceptions

- réception complète ;
- réception partielle ;
- réception directe si autorisée ;
- quantité dépassant commande refusée ;
- fournisseur absent ;
- produit absent ;
- lot incompatible refusé ;
- péremption invalide ;
- duplicate BL ;
- OperationId vide refusé ;
- même OperationId/même contenu idempotent ;
- même OperationId/contenu différent conflit ;
- double clic sans double mouvement ;
- rollback après échec ;
- prix achat snapshot ;
- prix de vente inchangé.

### Stock

- balance augmentée ;
- mouvement d’entrée créé ;
- lot créé ou réutilisé ;
- aucune balance négative ;
- stock total cohérent ;
- commande mise à jour ;
- aucune écriture partielle.

### Seed

- fournisseurs Demo ;
- commandes Demo ;
- réception Demo ;
- second passage sans duplication ;
- clear ciblé ;
- aucune donnée réelle.

### Desktop

- page accessible ;
- fournisseurs ;
- commandes ;
- réceptions ;
- partial receipt ;
- erreurs contextualisées ;
- double soumission bloquée ;
- SQL indisponible sûr ;
- Diagnostic mis à jour ;
- Catalogue et Stock non régressés ;
- POS non régressé.

### SQL réel

- migration appliquée ;
- contraintes ;
- transactions ;
- rowversion ;
- idempotence ;
- base temporaire sécurisée ;
- nettoyage final ;
- aucune base résiduelle ;
- aucune migration en attente.

Commandes obligatoires :

```powershell
dotnet tool restore
dotnet restore Pharmacy.slnx
dotnet build Pharmacy.slnx -c Debug --no-restore
dotnet test Pharmacy.slnx -c Debug --no-build
dotnet build Pharmacy.slnx -c Release --no-restore
dotnet test Pharmacy.slnx -c Release --no-build

dotnet ef migrations list ...
dotnet ef migrations has-pending-model-changes ...
```

Exécuter également :

- migration update ;
- script seed deux fois ;
- script de vérification ;
- clear puis reseed ;
- tests SQL réels ;
- vérification des bases résiduelles ;
- smoke Desktop avec et sans SQL.

Attendu :

- 0 erreur ;
- 0 avertissement raisonnable ;
- tous tests réussis ;
- aucun ignoré ;
- aucune migration en attente ;
- aucun secret ;
- aucune base temporaire résiduelle.

---

## 14. PLAN D’EXÉCUTION

1. vérifier marqueur final ;
2. lire sources ;
3. vérifier Git ;
4. baseline ;
5. vérifier SQL et migrations ;
6. cartographier schéma Inventory existant ;
7. confirmer modèle Purchasing Phase 3 ;
8. créer matrice de traçabilité ;
9. créer Domain minimal si nécessaire ;
10. créer erreurs/modèles/contrats Application ;
11. créer use cases fournisseurs ;
12. créer use cases commandes ;
13. créer use cases réceptions ;
14. créer entités/configurations EF ;
15. créer migration PurchasingPersistenceV01 ;
16. inspecter migration ;
17. générer script idempotent ;
18. implémenter stores SQL ;
19. implémenter transaction réception-stock ;
20. implémenter concurrence et idempotence ;
21. créer seed/clear/vérification ;
22. exécuter seed deux fois ;
23. connecter Desktop ;
24. créer page Achats et réceptions ;
25. mettre à jour Diagnostic ;
26. ajouter tests ;
27. tester erreurs et rollback ;
28. Debug/Release ;
29. vérifier migrations ;
30. smoke Desktop ;
31. documentation ;
32. scan secrets/diff/bases ;
33. rapport final.

---

## 15. VALIDATION MANUELLE ET RAPPORT FINAL

Fournir un guide manuel pour tester :

1. création fournisseur ;
2. création commande ;
3. ajout de deux lignes ;
4. validation commande ;
5. réception partielle ;
6. création d’un lot ;
7. augmentation stock ;
8. mouvement d’entrée ;
9. seconde réception du reliquat ;
10. statut FullyReceived ;
11. double réception bloquée ;
12. duplicate BL refusé ;
13. panne SQL sûre ;
14. non-régression Catalogue/Stock/POS.

Demander des captures :

- liste fournisseurs ;
- commande brouillon ;
- commande validée ;
- réception partielle ;
- réception terminée ;
- lot et mouvement dans Stock ;
- vérification SSMS ;
- état SQL indisponible.

Créer :

- `docs/domain/PURCHASING_CORE_V0.1.md` si Domain créé ;
- `docs/application/PURCHASING_USE_CASES_V0.1.md` ;
- `docs/infrastructure/SQL_PURCHASING_ADAPTERS_V0.1.md` ;
- `docs/database/PURCHASING_PHYSICAL_SCHEMA_V0.1.md` ;
- `docs/database/PURCHASING_ERD_V0.1.mmd` ;
- `docs/ux/PURCHASING_RECEIVING_UI_V0.1.md` ;
- `docs/reports/sprint-12/purchasing-validation.md` ;
- `docs/reports/sprint-12/codex-report.md` ;
- scripts seed, clear et vérification.

Mettre à jour README, CURRENT_STATE et seulement les nouvelles décisions.

Le rapport Codex doit détailler :

- complétude du prompt ;
- état initial ;
- schéma ;
- fournisseurs ;
- commandes ;
- réceptions ;
- lots/péremptions ;
- prix d’achat ;
- transactions ;
- idempotence ;
- concurrence ;
- mouvements et balances ;
- migration ;
- seed ;
- UI ;
- tests ;
- Debug/Release ;
- bases résiduelles ;
- sécurité ;
- écarts ;
- validation manuelle ;
- état Git final.

État Git final :

- branche ;
- HEAD initial ;
- fichiers modifiés/non suivis ;
- staging zéro ;
- commits zéro ;
- remotes zéro ;
- push absent.

Restrictions absolues :

- NE PAS git add.
- NE PAS COMMIT.
- NE PAS PUSH.
- NE PAS CRÉER DE REMOTE.
- NE PAS CHANGER DE BRANCHE.
- NE PAS STASH/RESET/RÉÉCRIRE L’HISTORIQUE.
- LAISSER TOUTES LES MODIFICATIONS VISIBLES POUR REVUE.

FIN DU PROMPT SPRINT 12
