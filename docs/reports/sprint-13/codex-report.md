# Sprint 13 — Rapport d’exécution Codex

## 1. Statut final

`IMPLEMENTED — SQL SALES FINALIZATION PASS — MANUAL SALE REVIEW REQUIRED`

## 2. Résumé

Le POS finalise désormais réellement les ventes dans SQL Server. Une transaction unique persiste vente, snapshots de lignes, allocations FEFO, paiement, mouvements de sortie, balances et registre idempotent. Le succès et l’impression ne sont déclenchés qu’après commit.

## 3. Complétude du prompt

Le fichier de 1 003 lignes a été lu intégralement. Taille : 24 679 octets ; SHA-256 : `F9EF8CCE3CEA54EBC8BF9032441AEC125807D70695D83F5D7D5C96845C084FB8`. Sa dernière ligne non vide est exactement `FIN DU PROMPT SPRINT 13`.

## 4. Documents consultés

README, gouvernance, templates, documentation Sales/Catalogue/Inventory/Purchasing, rapports Sprints 9 à 12, code et tests ont été lus. Les cinq PDF Phases 1 à 5 ont été extraits, recherchés et leurs pages représentatives inspectées visuellement : atomicité/idempotence, use cases Sales/FEFO, modèle Sales, incidents POS et règles code-first ont été confirmés.

## 5. Git initial

Branche `main`, HEAD `a46ffb7`, aucun remote et index vide. Contrairement à l’annonce du prompt, le working tree contenait déjà les 17 fichiers modifiés et 41 fichiers non suivis du Sprint 12. Cette contradiction a été signalée ; les changements ont été conservés sans staging, commit, reset, stash ou changement de branche.

## 6. Baseline

Restore et build Debug : succès, zéro avertissement/erreur. Baseline : 336/336 tests réussis avec connexion SQL réelle fournie au processus.

## 7. SQL initial

`Pharmacy_Development` sur SQL Server 17.0.1125.2 Developer, compatibilité 170. Quatre migrations Catalogue/Inventory/Purchasing étaient appliquées, aucune modification modèle en attente. Données synthétiques : 11 produits/settings, 1 emplacement, 6 lots, 13 balances, 17 mouvements, 4 fournisseurs, 2 commandes et 1 réception.

## 8. Modèle Sales confirmé

Le modèle suit Phase 3 : en-tête de vente, lignes snapshot, allocations par lots/balances, paiement et opération idempotente. La vente finalisée est immuable et toutes les FK historiques sont restrictives.

## 9. Schéma physique

Le schéma `sales` contient exactement `Sales`, `SaleLines`, `SaleLineAllocations`, `Payments` et `SaleOperations`. PK `bigint`, montants `decimal(19,4)`, instants `datetime2(7)`, textes bornés, contrôles de montants/quantités/statuts et index métier sont explicites.

## 10. Migration

`20260810103858_SalesPersistenceV01` ajoute `Sale` au journal Inventory puis les cinq tables. Aucune migration antérieure n’a été modifiée. Le script idempotent `005_sales_persistence_v01_idempotent.sql` a été généré et inspecté.

## 11. Entités/configurations

Les cinq entités EF restent internes à Infrastructure. Configurations, relations, conversions du mode de paiement, types SQL, contraintes, clés alternatives et index sont centralisés dans `Persistence/Configurations/Sales`.

## 12. Use cases Application

Ajout de `FinalizeSaleHandler`, `GetSaleByIdHandler`, `GetRecentSalesHandler`, vues persistantes, paiement et contrats `ISalesReader`/`ISalesClock`. Application demeure sans EF, SQL ou Infrastructure.

## 13. ISaleFinalizer SQL

`SqlSaleFinalizer` implémente réellement le contrat. Il revalide opération, produits, prix, stock et paiement, construit le plan, écrit le graphe et retourne une vue relue depuis le résultat persistant.

## 14. Transaction

Isolation `Serializable`, stratégie EF explicite et commit unique. Les étapes métier écrites sont dans la même transaction ; tous les refus précommit retournent sans écriture. Les conflits de stock sont capturés dans la transaction pour empêcher leur retry automatique.

## 15. FEFO

Lots actifs, positifs et non expirés ; dates les plus proches d’abord, lots sans date ensuite, puis ordre déterministe par identifiants. Le test réel consomme 3 unités du lot proche puis 2 du lot suivant et laisse intactes 9 unités expirées.

## 16. Allocations

Une allocation est créée par couple ligne/balance consommée. Le couple est unique, la quantité strictement positive et les snapshots lot/date sont conservés.

## 17. Mouvements de sortie

Chaque allocation crée un mouvement `Sale` avec identifiant déterministe, quantités avant/après, référence de vente, produit, emplacement et lot.

## 18. Balances

Les balances suivies par `rowversion` sont décrémentées de la quantité allouée et restent non négatives. Le script réconcilie Opening, achats, ajustements, inventaires et ventes.

## 19. Prix snapshot

Le prix préparé est comparé au prix courant. Toute différence bloque la vente avec `sales.price.changed` ; aucun repricing silencieux et aucun débit de stock.

## 20. Paiements

Espèces : montant reçu suffisant et monnaie exacte. Carte : uniquement confirmation TPE externe. Paiement mixte et données de carte absents.

## 21. Idempotence

`OperationId` et empreinte SHA-256 couvrent brouillon, snapshots et paiement. Rejeu équivalent : même `SaleId` sans double ligne/mouvement/débit. Empreinte différente : `sales.operation.conflict`.

## 22. Concurrence

Un test SQL réel synchronise deux finalisations après lecture du même stock : une seule réussit, l’autre reçoit `sales.stock.concurrency_conflict`. Les codes SQL 1205, 1222 et 3960 et `DbUpdateConcurrencyException` sont traités ; aucune écriture partielle n’est conservée.

## 23. POS

Le coordinateur appelle `FinalizeSaleHandler`, conserve le même OperationId pendant une tentative, garde le panier en cas d’échec, affiche la référence persistée et précrée le brouillon suivant. Aucun `DbContext` n’est exposé au Desktop.

## 24. Impression

La simulation de ticket est préparée uniquement après succès SQL. Échec et réimpression n’appellent pas le finalizer et ne modifient plus vente ou stock.

## 25. Historique ventes

Un panneau accessible affiche les ventes SQL récentes : référence, UTC, lignes, quantité, total, paiement et statut. `SqlSalesReader` utilise des lectures sans tracking et bornées.

## 26. Panne SQL

Les erreurs de connexion deviennent `sales.database.unavailable`, sans détail technique. Le POS ne produit aucun faux succès et conserve le brouillon.

## 27. Sécurité

Scan spécialisé : zéro candidat credential, zéro logging sensible actif. Les occurrences PAN/CVV/CardNumber sont uniquement des listes d’interdiction dans tests/script. Aucune donnée bancaire, personnelle ou réelle introduite ; aucune connexion versionnée.

## 28. Packages

Aucun package ajouté ou modifié. Graphe direct inchangé : WebView WPF 10.0.10, EF Core Design/SQL Server 10.0.10 et xUnit v3 MTP 3.2.2.

## 29. Tests ajoutés

Deux cas unitaires de rejet paiement et un scénario d’intégration SQL complet ont été ajoutés. Les assertions d’architecture et non-régression ont été adaptées au passage de la simulation à la persistance.

## 30. Tests SQL réels

Base `Pharmacy_Sprint13_Test_<12 hex>` créée avec validation stricte puis supprimée en `finally`. Sont prouvés : migration, contraintes, espèces, carte, FEFO, expiration, atomicité, prix, idempotence, concurrence, panne, lectures, rollback et réapplication.

## 31. Commandes

`dotnet tool restore`, restore solution, builds/tests Debug et Release, commandes EF list/pending/update/script, scripts SQL S9→S13, package list, smoke Desktop, scans secrets/diff/Git et recherche de bases résiduelles ont été exécutés.

## 32. Debug

Build : succès, 0 avertissement, 0 erreur. Tests : 339/339 réussis, 0 ignoré.

## 33. Release

Build : succès, 0 avertissement, 0 erreur. Tests : 339/339 réussis, 0 ignoré.

## 34. Migrations en attente

Cinq migrations listées et appliquées. `has-pending-model-changes` : aucun changement. `database update` : base déjà à jour après application Sales.

## 35. Bases résiduelles

Aucune base correspondant à `Pharmacy_Sprint%_Test_%`. La base de développement contient cinq tables Sales et zéro vente seedée.

## 36. Smoke Desktop

Exécutable Debug lancé avec connexion de processus : fenêtre WPF créée, processus répondant, six processus WebView2 observés, fermeture code 0 et aucun processus Desktop résiduel.

## 37. Problèmes

Le working tree Sprint 12 n’était pas committé contrairement au prérequis. La première génération du script EF a utilisé un startup project sans package Design, puis a été relancée avec Infrastructure. Une tentative de synchronisation du test concurrent par trigger était incompatible avec `OUTPUT`; elle a été retirée et remplacée par une barrière de test interne. Tous ces points sont résolus sans perte de travail.

## 38. Écarts

Aucun seed Sales n’a été créé, conformément à son caractère optionnel et à la règle de migration sans ventes Demo. La cible SQL Server Express n’est pas disponible ; les preuves utilisent Developer comme les Sprints précédents. Les validations visuelles/interactives restent humaines.

## 39. Risques

Les brouillons et suspensions restent locaux au processus. L’impression et le TPE restent simulés. SQL Express, certificat TLS approuvé, charge réelle et parcours complets sur poste pilote ne sont pas certifiés.

## 40. Validation manuelle

Le guide `sales-validation.md` fournit 8 scénarios et 10 familles de captures : vente simple/multi-lots, stock, historique, insuffisance, carte, double clic, prix, panne et impression postcommit.

## 41. Git final

Branche `main`, HEAD toujours `a46ffb7`, index vide, zéro commit Sprint 13, zéro remote et aucun push/changement de branche. État final : 37 entrées modifiées, 1 suppression suivie (`FinalizeSaleResult` remplacé) et 51 entrées non suivies couvrant Sprints 12 et 13 ; toutes restent hors index pour revue.

## 42. Recommandation

Effectuer la validation manuelle POS/Stock/SSMS documentée, puis faire relire séparément le périmètre Sprint 12 déjà présent et le Sprint 13 avant toute opération Git. Valider ensuite SQL Server Express et la confiance TLS sur le poste pilote.
