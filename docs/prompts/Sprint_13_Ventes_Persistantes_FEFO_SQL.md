# SPRINT 13 — VENTES PERSISTANTES, PAIEMENTS ET CONSOMMATION FEFO DU STOCK V0.1

## Instruction courte à donner à Codex

Placer ce fichier dans :

`docs/prompts/Sprint_13_Ventes_Persistantes_FEFO_SQL.md`

Puis transmettre uniquement :

> Lis intégralement `docs/prompts/Sprint_13_Ventes_Persistantes_FEFO_SQL.md`, vérifie que la dernière ligne non vide est exactement `FIN DU PROMPT SPRINT 13`, puis exécute toutes les opérations autorisées. Ne fais aucun commit, push, changement de branche ou ajout de remote.

---

## 1. RÔLE

Tu agis comme :

- ingénieur logiciel senior .NET 10 ;
- architecte Clean Architecture / monolithe modulaire ;
- spécialiste EF Core 10 et SQL Server 2025 ;
- spécialiste des transactions critiques et de l’idempotence ;
- spécialiste des systèmes de point de vente ;
- spécialiste de l’allocation de stock par lots et FEFO ;
- développeur WPF / Blazor Hybrid ;
- responsable des tests unitaires, d’intégration SQL et de non-régression ;
- responsable de la sécurité, de la traçabilité et de la documentation.

Le projet « Logiciel Pharmacie » dispose déjà de :

- Catalogue SQL réel ;
- Stock SQL réel ;
- lots, péremptions, balances et mouvements ;
- inventaires ;
- fournisseurs, commandes et réceptions SQL ;
- POS connecté à Application Sales ;
- panier courant encore en mémoire ;
- paiements et impression encore simulés ;
- SQL Server 2025 Developer ;
- migrations contrôlées ;
- aucune migration automatique au runtime.

Ta mission est de transformer la validation finale du POS en une vraie vente persistée et atomique, consommant réellement le stock SQL par lots FEFO.

Ne t’arrête pas après un plan. Exécute toutes les opérations autorisées.

---

## 2. MISSION

Créer **Sales Persistence V0.1**.

Le Sprint 13 doit permettre :

1. la création d’une vente persistée ;
2. la persistance des lignes de vente ;
3. la conservation du prix unitaire au moment de la vente ;
4. la persistance du mode de paiement de base ;
5. la gestion Espèces et Carte/TPE externe confirmé ;
6. l’allocation des quantités sur les lots vendables ;
7. la sélection FEFO ;
8. l’exclusion stricte des lots expirés ;
9. la création des mouvements de sortie ;
10. la mise à jour atomique des balances ;
11. le blocage du stock insuffisant ;
12. l’idempotence par `OperationId` ;
13. la protection contre la double validation ;
14. la prévention des doubles débits de stock ;
15. la concurrence optimiste sur les balances ;
16. la finalisation transactionnelle via `ISaleFinalizer` ;
17. la création d’un résultat de vente persistant ;
18. l’impression uniquement après commit ;
19. l’échec d’impression sans annulation de la vente ;
20. la réimpression simulée ou technique sans recréer la vente ;
21. l’affichage de l’historique minimal des ventes ;
22. la connexion du POS à la vraie finalisation SQL ;
23. la conservation des suspensions Desktop en mémoire pour ce sprint ;
24. l’absence de persistance de caisse avancée, crédit, retours ou remboursements ;
25. aucun commit, push ou remote.

### Chaîne critique attendue

```text
POS
 ↓
Prepare / Validate
 ↓
ISaleFinalizer
 ↓
Transaction SQL
 ├── Sales
 ├── SaleLines
 ├── SaleLineAllocations
 ├── Payments
 ├── StockMovements
 └── StockBalances
 ↓
COMMIT
 ↓
Succès vente
 ↓
Impression
```

### Statut attendu

`IMPLEMENTED — SQL SALES FINALIZATION PASS — MANUAL SALE REVIEW REQUIRED`

---

## 3. SOURCES DE VÉRITÉ

Lire intégralement avant modification :

- `README.md`
- `Directory.Build.props`
- `global.json`
- `.editorconfig`
- `.gitattributes`
- `.gitignore`
- `docs/governance/PROJECT_CONTEXT.md`
- `docs/governance/DECISIONS_REGISTER.md`
- `docs/governance/SCOPE.md`
- `docs/governance/CURRENT_STATE.md`
- `docs/governance/CODEX_WORKFLOW.md`
- `docs/templates/CODEX_PROMPT_TEMPLATE.md`
- `docs/templates/CODEX_REPORT_TEMPLATE.md`
- `docs/domain/SALES_CORE_V0.1.md`
- `docs/application/SALES_USE_CASES_V0.1.md`
- `docs/application/DESKTOP_SALES_INTEGRATION_V0.1.md`
- `docs/database/PERSISTENCE_FOUNDATION_V0.1.md`
- `docs/database/CATALOG_PHYSICAL_SCHEMA_V0.1.md`
- `docs/database/INVENTORY_PHYSICAL_SCHEMA_V0.1.md`
- `docs/database/PURCHASING_PHYSICAL_SCHEMA_V0.1.md`
- `docs/reports/sprint-10/codex-report.md`
- `docs/reports/sprint-11/codex-report.md`
- `docs/reports/sprint-12/codex-report.md`
- rapports de validation SQL des Sprints 10, 11 et 12 ;
- migrations et snapshots EF actuels ;
- le présent fichier jusqu’au marqueur final.

Inspecter complètement :

- `src/Pharmacy.Domain`
- `src/Pharmacy.Application`
- `src/Pharmacy.Infrastructure`
- `src/Pharmacy.Desktop`
- `tests/Pharmacy.UnitTests`
- `tests/Pharmacy.IntegrationTests`
- `database`

Consulter en lecture seule les cinq PDF de référence du projet, en particulier Phase 2 et Phase 3.

Ne modifier aucun PDF.

Ordre de priorité :

1. présent prompt ;
2. `DECISIONS_REGISTER.md` ;
3. `CURRENT_STATE.md` ;
4. `SCOPE.md` ;
5. Phase 2 et Phase 3 ;
6. schéma SQL réellement appliqué ;
7. code existant.

Toute différence entre source fonctionnelle, hypothèse et décision technique doit être documentée.

---

## 4. ÉTAT INITIAL ET BASELINE

Le Sprint 12 est annoncé comme validé et commité avant le démarrage du Sprint 13.

Ne suppose aucun hash.

Exécuter :

```powershell
git status
git status --short
git branch --show-current
git log --oneline -n 12
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
- tous les tests existants réussis ;
- zéro erreur ;
- zéro avertissement raisonnable.

Vérifier SQL Server réel via `PHARMACY_SQLSERVER_CONNECTION` et confirmer :

- base `Pharmacy_Development` ;
- migrations Catalogue, Inventory et Purchasing appliquées ;
- aucun changement EF en attente ;
- données Demo présentes ;
- stock SQL cohérent ;
- aucune base temporaire résiduelle.

Si le working tree n’est pas propre, ne pas poursuivre silencieusement.

---

## 5. PÉRIMÈTRE AUTORISÉ

Tu peux modifier/créer dans :

- `src/Pharmacy.Domain` uniquement si une extension minimale de Sales est nécessaire ;
- `src/Pharmacy.Application` ;
- `src/Pharmacy.Infrastructure` ;
- `src/Pharmacy.Desktop` ;
- tests ;
- `database/dev` ;
- `database/verification` ;
- README et gouvernance.

Créer :

- `docs/application/SALES_FINALIZATION_V0.1.md`
- `docs/infrastructure/SQL_SALES_FINALIZER_V0.1.md`
- `docs/database/SALES_PHYSICAL_SCHEMA_V0.1.md`
- `docs/database/SALES_ERD_V0.1.mmd`
- `docs/ux/POS_PERSISTENT_SALES_V0.1.md`
- `docs/reports/sprint-13/sales-validation.md`
- `docs/reports/sprint-13/codex-report.md`
- `database/dev/seed_synthetic_sales_v01.sql` seulement si nécessaire et strictement synthétique ;
- `database/dev/clear_synthetic_sales_v01.sql` seulement si nécessaire ;
- `database/verification/verify_sprint13_sales.sql`.

Nouvelle migration attendue, nom recommandé :

`SalesPersistenceV01`

Ne modifier aucune migration déjà validée.

---

## 6. HORS PÉRIMÈTRE

Ne développer aucun élément de Sprint 14+ :

- sessions de caisse complètes ;
- fond de caisse ;
- clôture et comptage de caisse ;
- crédit client ;
- clients persistants avancés ;
- ventes suspendues persistées ;
- retours ;
- échanges ;
- remboursements ;
- annulations après vente avec logique de reversal complète ;
- authentification/rôles/permissions ;
- audit complet ;
- reporting final ;
- sauvegarde/import/matériel réel.

Ne pas :

- créer de stock négatif ;
- consommer un lot expiré ;
- recalculer un prix depuis le Catalogue après finalisation ;
- modifier rétroactivement une ligne de vente terminée ;
- supprimer physiquement une vente terminée ;
- utiliser `EnsureCreated()` ;
- appeler `Database.Migrate()` au démarrage ;
- exposer des entités EF à Desktop ;
- utiliser un DbContext long-lived dans Desktop ;
- enregistrer une donnée bancaire ;
- générer un paiement carte réel ;
- appeler un périphérique TPE réel ;
- faire de fallback mémoire silencieux pour la finalisation ;
- utiliser `.Result`, `.Wait()` ou `Task.Run` pour contourner async ;
- créer de repository générique ;
- ajouter un package sans nécessité absolue.

Interdictions Git absolues :

- pas de `git add` ;
- pas de commit ;
- pas de push ;
- pas de remote ;
- pas de changement de branche ;
- pas de reset/stash/rebase.

---

## 7. SCHÉMA PHYSIQUE SALES

Créer un schéma SQL dédié :

`sales`

Le périmètre minimal attendu est :

1. `sales.Sales`
2. `sales.SaleLines`
3. `sales.SaleLineAllocations`
4. `sales.Payments`
5. `sales.SaleOperations`

Adapter uniquement si Phase 3 impose une structure différente.

### 7.1 Sales

Doit contenir au minimum :

- `Id` bigint identity ;
- référence métier/technique stable ;
- `OperationId` ou relation vers l’opération ;
- date/heure UTC ;
- statut finalisé ;
- total snapshot ;
- quantité totale ;
- nombre de lignes ;
- éventuellement informations de mode de règlement agrégées si le modèle le prévoit ;
- rowversion si l’entité doit encore évoluer techniquement.

Une vente finalisée est immuable fonctionnellement dans ce sprint.

### 7.2 SaleLines

Snapshots obligatoires :

- ProductId ;
- InternalCode/reference ;
- nom ;
- présentation ;
- prix unitaire ;
- quantité ;
- sous-total.

Ne jamais dépendre du prix courant après finalisation.

### 7.3 SaleLineAllocations

Pour chaque ligne :

- SaleLineId ;
- BatchId ;
- StockBalanceId si pertinent ;
- quantité consommée ;
- snapshot du lot si le modèle Phase 3 l’exige.

Permettre une ligne répartie sur plusieurs lots.

### 7.4 Payments

V0.1 :

- Espèces ;
- Carte/TPE externe confirmé.

Conserver :

- type ;
- montant ;
- confirmation externe booléenne/état technique nécessaire ;
- aucune donnée carte ;
- date UTC.

Pour espèces, le montant reçu et la monnaie rendue peuvent être persistés seulement si le modèle validé le prévoit. Sinon conserver uniquement le montant de paiement et le snapshot nécessaire au ticket.

### 7.5 SaleOperations

Doit garantir l’idempotence :

- OperationId unique ;
- fingerprint/empreinte ;
- statut ;
- SaleId résultant ;
- CreatedAtUtc ;
- CompletedAtUtc éventuel.

Même OperationId + même empreinte : retourner le même résultat.

Même OperationId + empreinte différente : conflit explicite.

### 7.6 Contraintes

Configurer :

- PK/FK ;
- unicité OperationId ;
- montants non négatifs ;
- quantités strictement positives ;
- total cohérent au niveau applicatif et contraintes simples côté SQL ;
- suppression restrictive ;
- index sur FK, date, OperationId et référence de vente.

Aucune cascade destructive vers historique de stock.

---

## 8. FINALISATION TRANSACTIONNELLE

Implémenter réellement `ISaleFinalizer` dans Infrastructure.

### Ordre obligatoire

1. valider OperationId ;
2. vérifier l’idempotence ;
3. charger/valider le brouillon préparé ;
4. vérifier que le brouillon n’est pas vide ;
5. charger les produits SQL ;
6. vérifier que les produits sont encore vendables ;
7. vérifier le prix snapshot du brouillon et définir la règle si le prix catalogue a changé ;
8. charger le stock vendable ;
9. exclure les lots expirés ;
10. trier FEFO ;
11. calculer les allocations ;
12. refuser si stock insuffisant ;
13. créer l’opération idempotente ;
14. créer Sale ;
15. créer SaleLines ;
16. créer SaleLineAllocations ;
17. créer Payments ;
18. créer StockMovements de sortie ;
19. décrémenter StockBalances ;
20. enregistrer l’opération terminée ;
21. commit SQL unique ;
22. retourner le résultat persistant.

Toutes les étapes 13 à 20 doivent être dans la même transaction.

### Atomicité

En cas d’échec avant commit :

- aucune Sale ;
- aucune ligne ;
- aucun paiement ;
- aucune allocation ;
- aucun mouvement ;
- aucune balance modifiée.

### Concurrence

Si un autre processus consomme le stock entre lecture et sauvegarde :

- détecter `rowversion`/conflit ;
- rollback ;
- retourner erreur de stock/concurrence ;
- ne pas faire de last-write-wins ;
- ne pas relancer automatiquement une transaction de vente sans revalidation.

---

## 9. FEFO ET STOCK

### FEFO

Pour chaque produit :

1. lots vendables uniquement ;
2. quantité positive ;
3. lot non expiré ;
4. date de péremption la plus proche d’abord ;
5. lot sans péremption après les lots datés, selon règle Inventory validée ;
6. ordre déterministe secondaire par Id.

### Allocation multi-lots

Exemple :

```text
Demande : 8
Lot A : 5
Lot B : 10
→ allocation A = 5
→ allocation B = 3
```

Créer deux allocations et deux mouvements de sortie si le modèle de mouvement est par lot.

### Stock insuffisant

Refuser l’opération avant toute écriture métier définitive.

Erreur stable :

`sales.stock.insufficient`

### Expiration

Un lot expiré peut rester visible dans Stock mais ne doit jamais être alloué.

### Balances

Après commit :

`nouvelle quantité = ancienne quantité - allocation`

Aucune valeur négative.

---

## 10. PRIX ET COHÉRENCE DU BROUILLON

Le brouillon POS utilise un snapshot de prix.

Définir une règle explicite et documentée si le prix SQL a changé après ajout au panier.

Pour V0.1, privilégier une règle sûre :

- si le prix courant diffère du snapshot du brouillon au moment de finaliser, bloquer la finalisation ;
- retourner `sales.price.changed` ;
- demander au POS de recharger/mettre à jour le panier ;
- ne pas modifier silencieusement le prix.

Aucune remise ou TVA dans ce sprint.

---

## 11. PAIEMENTS

### Espèces

Exiger :

- total couvert ;
- montant reçu valide si utilisé ;
- monnaie calculée côté Application/UI avec `decimal` ;
- paiement persisté à hauteur du total de la vente.

Ne pas autoriser une valeur négative.

### Carte

Carte = TPE externe.

Exiger confirmation manuelle avant finalisation.

Ne jamais stocker :

- PAN ;
- titulaire ;
- expiration ;
- CVV ;
- token bancaire ;
- piste magnétique.

Un paiement ambigu doit rester bloquant et ne pas finaliser.

### Paiement mixte

Hors périmètre sauf si déjà explicitement validé dans les documents. Ne pas l’inventer.

---

## 12. APPLICATION SALES

Créer/adapter les modèles et handlers nécessaires.

Prévoir :

- `FinalizeSaleCommand`
- `FinalizeSaleHandler`
- `FinalizedSaleView`
- `FinalizedSaleLineView`
- `SalePaymentView`
- `SaleAllocationView`
- `GetSaleByIdHandler`
- `GetRecentSalesHandler` minimal pour contrôle/historique.

Erreurs stables minimales :

- `sales.draft.not_found`
- `sales.finalization.empty_draft`
- `sales.operation.invalid`
- `sales.operation.conflict`
- `sales.stock.insufficient`
- `sales.stock.concurrency_conflict`
- `sales.price.changed`
- `sales.payment.invalid`
- `sales.payment.card_unconfirmed`
- `sales.product.not_sellable`
- `sales.database.unavailable`

Ne pas exposer d’entité EF.

---

## 13. DESKTOP / POS

### 13.1 Validation réelle

Remplacer la fausse finalisation du POS par `FinalizeSaleHandler`.

Le panier peut rester en mémoire jusqu’au commit.

Après succès SQL :

- afficher la référence persistante ;
- afficher le total ;
- afficher le mode de paiement ;
- afficher les lignes ;
- afficher les lots/allocation seulement dans un détail technique si utile, pas surcharger l’écran ;
- réinitialiser le brouillon ;
- créer un nouveau brouillon ;
- lancer ensuite le workflow d’impression.

### 13.2 Impression

La vente doit être commitée avant impression.

Échec d’impression :

- vente reste finalisée ;
- stock reste consommé ;
- paiement reste persisté ;
- afficher `Vente enregistrée — ticket non imprimé` ;
- permettre réessai sans recréer la vente.

### 13.3 Double clic

Après premier clic de validation :

- bouton busy ;
- même OperationId ;
- deuxième appel éventuel doit retourner le même résultat ;
- aucune deuxième consommation de stock.

### 13.4 Panne SQL

Si SQL indisponible au moment de finaliser :

- vente non finalisée ;
- panier conservé ;
- aucun fallback mémoire présenté comme succès ;
- message sûr ;
- possibilité de réessayer après retour SQL.

### 13.5 Historique minimal

Ajouter une vue minimale des ventes récentes si cohérente avec la navigation actuelle, ou une section dans Ventes.

Lecture seule :

- référence ;
- date ;
- nombre de lignes ;
- total ;
- paiement ;
- statut finalisé.

Pas encore de retour/annulation.

---

## 14. SEED ET DONNÉES DE TEST

La migration doit rester sans ventes Demo automatiques.

Les tests d’intégration peuvent créer des ventes synthétiques dans une base temporaire.

Un script de seed de ventes n’est nécessaire que s’il apporte une vraie valeur de validation manuelle.

Si créé :

- cible `Pharmacy_Development` uniquement ;
- données `DEMO-*` uniquement ;
- pas de faux paiement bancaire ;
- idempotent ;
- clear ciblé ;
- ne modifie pas les historiques hors Demo.

Ne pas modifier manuellement les balances pour fabriquer une vente.

---

## 15. TESTS OBLIGATOIRES

Conserver tous les anciens tests.

Ajouter des tests au minimum pour :

### Schéma

1. tables sales présentes ;
2. PK bigint ;
3. FK correctes ;
4. OperationId unique ;
5. quantités positives ;
6. montants non négatifs ;
7. suppression restrictive ;
8. index attendus ;
9. aucune migration en attente.

### Finalisation nominale

10. vente espèces réussie ;
11. Sale créée ;
12. lignes créées ;
13. paiement créé ;
14. allocations créées ;
15. mouvements de sortie créés ;
16. balances décrémentées ;
17. total exact ;
18. snapshots produit/prix conservés ;
19. commit unique.

### FEFO

20. lot le plus proche utilisé d’abord ;
21. lot expiré exclu ;
22. allocation multi-lots correcte ;
23. quantité exacte consommée ;
24. lots restants corrects.

### Stock insuffisant

25. refus ;
26. aucune vente ;
27. aucun paiement ;
28. aucun mouvement ;
29. aucune balance modifiée.

### Prix changé

30. prix différent détecté ;
31. vente refusée ;
32. stock inchangé ;
33. panier non perdu.

### Idempotence

34. même OperationId + même empreinte → même Sale ;
35. aucune double ligne ;
36. aucun double mouvement ;
37. aucun double débit stock ;
38. même OperationId + empreinte différente → conflit.

### Concurrence

39. conflit balance détecté ;
40. transaction rollback ;
41. aucune écriture partielle.

### Paiement

42. espèces valides ;
43. espèces insuffisantes refusées ;
44. carte confirmée acceptée ;
45. carte non confirmée refusée ;
46. aucune donnée bancaire sensible dans les modèles ou DB.

### Impression

47. impression après commit ;
48. échec impression ne rollback pas vente ;
49. réimpression ne recrée pas vente ;
50. réimpression ne redécrémente pas stock.

### Desktop

51. finalisation utilise SQL ;
52. aucun faux succès mémoire ;
53. SQL indisponible conserve panier ;
54. double clic bloqué ;
55. succès affiche référence persistée ;
56. nouvelle vente crée nouveau brouillon ;
57. historique récent accessible ;
58. Diagnostic/UI Lab restent accessibles.

### Architecture/sécurité

59. Domain sans EF ;
60. Application sans Infrastructure ;
61. Desktop sans DbContext ;
62. aucun type EF exposé ;
63. aucune donnée carte ;
64. aucune migration runtime ;
65. aucun secret ;
66. aucun nouveau package non justifié.

### SQL réel

67. tests sur SQL Server réel ;
68. base temporaire préfixée et sécurisée ;
69. migration appliquée ;
70. finalisation réelle ;
71. contraintes réelles ;
72. rowversion réelle ;
73. rollback réel ;
74. base temporaire supprimée ;
75. aucune base résiduelle.

### Commandes

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

Exécuter aussi les scripts de vérification Sprints 10, 11, 12, puis `verify_sprint13_sales.sql`.

Attendu :

- 0 erreur ;
- 0 avertissement raisonnable ;
- tous tests réussis ;
- aucun ignoré ;
- aucune migration en attente ;
- aucun secret ;
- aucune base temporaire résiduelle.

---

## 16. PLAN D’EXÉCUTION

1. vérifier marqueur final ;
2. lire les sources ;
3. vérifier Git ;
4. baseline ;
5. vérifier SQL et migrations ;
6. cartographier Domain/Application Sales ;
7. cartographier Inventory SQL ;
8. confirmer modèle Sales Phase 3 ;
9. créer schéma de persistance Sales ;
10. créer migration ;
11. inspecter migration ;
12. créer modèles/contrats Application nécessaires ;
13. implémenter finalizer SQL ;
14. implémenter FEFO ;
15. implémenter allocations ;
16. implémenter mouvements de sortie ;
17. implémenter idempotence ;
18. implémenter paiement ;
19. implémenter lectures ventes ;
20. connecter POS ;
21. adapter impression ;
22. créer vérification SQL ;
23. ajouter tests unitaires ;
24. ajouter tests SQL réels ;
25. tester concurrence ;
26. tester panne SQL ;
27. tester impression échouée ;
28. Debug ;
29. Release ;
30. vérifier migrations ;
31. lancer Desktop ;
32. documentation ;
33. secrets/diff/bases ;
34. rapport final.

---

## 17. VALIDATION MANUELLE À FOURNIR

Fournir un guide clair pour l’utilisateur.

### Scénarios minimum

1. noter stock avant vente ;
2. vente espèces d’un produit avec un seul lot ;
3. vérifier stock décrémenté ;
4. vérifier mouvement de sortie ;
5. vérifier vente dans historique ;
6. vente consommant plusieurs lots FEFO ;
7. vérifier allocations ;
8. tentative stock insuffisant ;
9. vérifier aucun débit ;
10. paiement carte confirmé ;
11. paiement carte non confirmé refusé ;
12. double clic validation ;
13. vérifier une seule vente ;
14. simuler échec impression ;
15. vérifier vente/stock restent finalisés ;
16. réimprimer ;
17. vérifier aucune seconde consommation ;
18. changer prix dans Catalogue pendant qu’un produit est dans le panier ;
19. vérifier finalisation bloquée ;
20. panne SQL pendant validation ;
21. vérifier panier conservé.

### Captures demandées

- POS avant validation ;
- écran succès avec référence persistée ;
- Stock avant/après ;
- mouvements de sortie ;
- historique de vente ;
- allocation multi-lots/FEFO ;
- refus stock insuffisant ;
- échec impression après vente persistée ;
- SSMS tables Sales ;
- script `verify_sprint13_sales.sql`.

Ne pas demander de commit.

---

## 18. DOCUMENTATION ET RAPPORT FINAL

Créer :

- `docs/application/SALES_FINALIZATION_V0.1.md`
- `docs/infrastructure/SQL_SALES_FINALIZER_V0.1.md`
- `docs/database/SALES_PHYSICAL_SCHEMA_V0.1.md`
- `docs/database/SALES_ERD_V0.1.mmd`
- `docs/ux/POS_PERSISTENT_SALES_V0.1.md`
- `docs/reports/sprint-13/sales-validation.md`
- `docs/reports/sprint-13/codex-report.md`
- `database/verification/verify_sprint13_sales.sql`

Mettre à jour :

- README ;
- CURRENT_STATE ;
- DECISIONS_REGISTER seulement si décision réellement nouvelle.

Rapport obligatoire :

# Sprint 13 — Rapport d’exécution Codex

## 1. Statut final
## 2. Résumé
## 3. Complétude du prompt
## 4. Documents consultés
## 5. Git initial
## 6. Baseline
## 7. SQL initial
## 8. Modèle Sales confirmé
## 9. Schéma physique
## 10. Migration
## 11. Entités/configurations
## 12. Use cases Application
## 13. ISaleFinalizer SQL
## 14. Transaction
## 15. FEFO
## 16. Allocations
## 17. Mouvements de sortie
## 18. Balances
## 19. Prix snapshot
## 20. Paiements
## 21. Idempotence
## 22. Concurrence
## 23. POS
## 24. Impression
## 25. Historique ventes
## 26. Panne SQL
## 27. Sécurité
## 28. Packages
## 29. Tests ajoutés
## 30. Tests SQL réels
## 31. Commandes
## 32. Debug
## 33. Release
## 34. Migrations en attente
## 35. Bases résiduelles
## 36. Smoke Desktop
## 37. Problèmes
## 38. Écarts
## 39. Risques
## 40. Validation manuelle
## 41. Git final
## 42. Recommandation

État Git final :

- branche ;
- HEAD initial ;
- fichiers modifiés/non suivis ;
- staging 0 ;
- commits 0 ;
- remotes 0 ;
- push absent.

---

## 19. RESTRICTIONS GIT ABSOLUES

- NE PAS EXÉCUTER `git add`.
- NE PAS COMMIT.
- NE PAS PUSH.
- NE PAS CRÉER DE REMOTE.
- NE PAS CHANGER DE BRANCHE.
- NE PAS UTILISER STASH.
- NE PAS UTILISER RESET.
- NE PAS REBASE.
- NE PAS RÉÉCRIRE L’HISTORIQUE.
- LAISSER TOUTES LES MODIFICATIONS VISIBLES POUR REVUE.

FIN DU PROMPT SPRINT 13
