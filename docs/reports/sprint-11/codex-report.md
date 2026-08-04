# Sprint 11 — Rapport d’exécution Codex

## 1. Statut final

`IMPLEMENTED — STOCK UI HARMONIZED — MANUAL VISUAL REVIEW REQUIRED`

## 2. Résumé

Le moteur Inventory V0.1 est implémenté de bout en bout : noyau métier, cas d’utilisation, six tables SQL, migration, adaptateurs, données Demo, page Stock, diagnostic et disponibilité POS SQL. La page Stock est désormais harmonisée avec Catalogue et le design system existant. Les ventes restent non persistées et ne consomment aucun stock.

## 3. Complétude du prompt

`docs/prompts/Sprint_11_Stock_Lots_Mouvements_SQL.md` a été lu intégralement : 1 431 lignes, 35 415 octets, SHA-256 `C78441EE2DE3DE213915B9C7E76756B053035F553E333B1E46B58D5175C5E4EF`. La dernière ligne non vide est exactement `FIN DU PROMPT SPRINT 11`.

## 4. Documents consultés

Les sources racine, gouvernance, modèles, documents Domain/Application/Infrastructure/Database/UX des sprints précédents, projets, migrations, snapshot, scripts et tests ont été lus. Les cinq PDF de référence ont été consultés sur les ADR, règles Inventory, modèle de données, parcours Stock/POS et méthode code-first ; des pages représentatives ont été rendues puis inspectées visuellement. Aucun PDF n’a été modifié.

## 5. État Git initial

- branche : `main` ;
- HEAD : `58e7315 feat: add persistent SQL catalog and POS product lookup` ;
- index : vide ;
- remote : aucun ;
- fichier non suivi initial : uniquement le prompt Sprint 11 ;
- aucun commit, push, changement de branche, remote, stash ou reset effectué.

## 6. Baseline .NET

SDK 10.0.302, runtimes 10.0.10, `dotnet-ef` 10.0.10 et six projets. La baseline Debug compilait avec 0 avertissement/0 erreur et comptait 300 tests. `dotnet tool restore` et `dotnet restore Pharmacy.slnx` réussissent.

## 7. État SQL initial

SQL Server 17.0.1125.2 Developer, base `Pharmacy_Development`, compatibilité 170 et deux migrations appliquées. Aucun schéma/table Inventory. Le Catalogue contenait les dix références canoniques plus une référence manuelle inactive `demo-manual-001`, préservée pendant le sprint.

## 8. Matrice de traçabilité

La matrice préalable se trouve dans `docs/database/INVENTORY_PHYSICAL_SCHEMA_V0.1.md`. Elle classe chaque élément comme confirmé, règle validée, décision technique du sprint ou différé. Elle documente notamment l’incertitude terrain sur le multi-emplacement, les quantités entières, l’absence de réservation et les états de session minimaux.

## 9. Noyau Domain Inventory

Identifiants typés, quantités, lot, péremption, état de balance, politique de suivi, types de mouvement, états d’inventaire et seize erreurs stables sont isolés dans `Pharmacy.Domain.Inventory`. Aucune dépendance EF, SQL, WPF ou Blazor.

## 10. Contrats et modèles Application

`IInventoryDateProvider`, `IInventoryReader` et `IInventoryStore` séparent l’Application de l’Infrastructure. Les vues sont immuables/lecture seule et les tokens `rowversion` sont encodés en Base64.

## 11. Cas d’utilisation

Emplacements, recherche/résumé, détail/lots, historique, expirations, FEFO, ouverture, ajustements, création/démarrage/comptage/finalisation/annulation d’inventaire et diagnostic sont disponibles. Identifiants, limites, textes, quantités, `OperationId` et tokens sont validés avant le store.

## 12. Schéma physique inventory

Le schéma `inventory` contient exactement six tables. Les PK sont des `bigint IDENTITY`, les quantités des `int`, les expirations des `date` et les instants des `datetime2(7)`. Aucun total produit redondant ni `ReservedQuantity`.

## 13. Tables et relations

`StockLocations`, `StockBatches`, `StockBalances`, `StockMovements`, `InventorySessions` et `InventoryCountLines` sont reliées au Catalogue et entre elles par FK `NO ACTION`. Aucune suppression en cascade.

## 14. Contraintes et index

Codes/références/opérations uniques, emplacement principal unique filtré, balance/ligne unique avec ou sans lot, quantité positive, balance non négative, direction cohérente et textes non blancs sont renforcés par SQL. Les index couvrent produit/emplacement/date/statut et expiration.

## 15. Rowversion et concurrence

Les tables mutables utilisent `rowversion`. Les commandes Desktop renvoient le token lu ; une version obsolète produit `inventory.concurrency_conflict`, sans écrasement ni retry aveugle. Le scénario SQL réel vérifie une écriture valide puis un second write avec token périmé.

## 16. Mouvements append-only

Le journal n’expose aucune mise à jour ou suppression. Il conserve type, quantité, avant, après, motif, source, référence et temps UTC. Les contraintes SQL vérifient que la direction correspond au type.

## 17. Idempotence OperationId

Une empreinte structurelle complète l’`OperationId`. Une répétition équivalente retourne le résultat existant ; la même clé avec une autre intention retourne `inventory.operation.conflict`. Cette règle est testée sur emplacement, mouvement et finalisation.

## 18. Balances et atomicité

Un mouvement et sa balance sont écrits sous stratégie d’exécution EF dans une transaction explicite. Une baisse insuffisante ne crée ni mouvement ni modification. La vérification SQL réconcilie chaque balance avec la somme signée du journal.

## 19. Lots et péremption

La création respecte `TracksLots`/`TracksExpiry`. Les lots expirés restent lisibles et comptés séparément, mais sont exclus de la disponibilité vendable. La comparaison utilise une date métier injectée.

## 20. Disponibilité POS

`SqlInventoryStockAvailabilityReader` remplace le lecteur synthétique dans le composition root. Il additionne uniquement les balances actives et non expirées. Le fichier de l’ancien adaptateur synthétique spécifique a été retiré ; aucun fallback n’est enregistré.

## 21. FEFO

La proposition FEFO trie les lots vendables par péremption la plus proche, puis par création/clé pour un résultat stable. Elle refuse une demande supérieure au disponible et ne réserve/consomme rien. Le test réel prouve l’ordre et l’exclusion du lot expiré.

## 22. Sessions d’inventaire

Création, démarrage avec capture du théorique, comptages versionnés, finalisation et annulation sont implémentés. La finalisation exige toutes les lignes, refuse une balance modifiée et transforme les écarts en mouvements dans la même transaction.

## 23. Migration InventoryPersistenceV01

`20260804181233_InventoryPersistenceV01` crée le schéma et les six tables sans seed. `Up` et `Down`, types, noms, index, FK et contraintes ont été inspectés. Les migrations Sprint 9/10 sont inchangées.

## 24. Script idempotent

EF a généré `database/scripts/003_inventory_persistence_v01_idempotent.sql` avec historique de migration et transaction contrôlée. Aucun secret ni donnée métier n’y figure.

## 25. Seed et clear

Le seed/clear ciblent strictement `Pharmacy_Development`, utilisent une transaction et des identifiants canoniques. Le seed a été exécuté deux fois avec UTF-8, le clear a ramené les quatre ensembles Demo Inventory à zéro, puis le seed a été rejoué deux fois sans duplication. Le clear ne vise aucune donnée manuelle/non Demo et l’état final reste seedé.

## 26. Données synthétiques

État final Demo : 1 emplacement, 5 lots, 12 balances positives, 12 mouvements `Opening` et 1 lot expiré. La référence manuelle inactive du Catalogue reste intacte.

## 27. Adaptateurs SQL

`SqlInventoryReader`, `SqlInventoryStore`, `SqlInventoryStockAvailabilityReader` et l’horloge système utilisent l’`IDbContextFactory` existante, des contextes courts et des `CancellationToken`. Aucun repository générique ni contexte Desktop long-lived.

## 28. Composition root Desktop

Le composition root ajoute les adaptateurs/handlers/coordinator Inventory. Brouillons Sales, paiements, suspensions et impression restent en mémoire. Le Desktop ne référence aucun `DbContext` et ne lance aucune migration.

## 29. Page Stock

`/module/inventory` remplace le placeholder : résumé, recherche, filtres, détail, lots expirés, mouvements, ouverture/ajustement confirmé, FEFO, sessions, comptages et finalisation. Loading, vide, erreur, indisponibilité et responsive sont couverts sans surcharge volontaire.

## 30. Adaptation POS

Les textes indiquent Catalogue + disponibilité SQL, vente non persistée et stock non consommé. Produit sans stock/lot expiré est bloqué par la disponibilité existante. Code inconnu, panier, paiement et impression simulée restent inchangés.

## 31. Diagnostic et panne SQL

Diagnostic affiche base, connectivité, migration, emplacements, lots, balances positives et expirés Demo sans chaîne de connexion. Sans SQL, shell/Diagnostic démarrent, Stock/Catalogue/Ventes signalent l’indisponibilité, sans exception brute ni fallback.

## 32. Sécurité et secrets

Sensitive logging et migration runtime restent absents. Le scan spécialisé secrets/PII a trouvé uniquement les motifs `Password=`/`Pwd=` dans les assertions de garde et le prompt, plus des connexions factices `not-used.invalid` dans les tests de modèle. Aucun secret, token, mot de passe, donnée patient ou export sensible actif détecté.

## 33. Packages

Aucun `PackageReference`, projet, framework ou relation de projet ajouté/modifié. Les versions stables existantes restent inchangées.

## 34. Tests ajoutés

20 cas portent le total de 300 à 320 : 12 cas Domain/Application, 7 tests architecture/UX/scripts/panne et 1 scénario SQL réel multi-assertions. Les tests historiques ont été adaptés seulement lorsque leurs hypothèses Sprint 9/10 étaient devenues obsolètes.

## 35. Tests SQL réels

La base `Pharmacy_Sprint11_Test_<12 hex>` applique toutes les migrations puis vérifie schéma, types, opérations, journal/balance, idempotence, concurrence, FEFO, disponibilité POS, expiration, inventaire, double soumission, contrainte négative, rollback et réapplication. Elle est supprimée en `finally`.

## 36. Commandes exécutées

`dotnet tool restore`, restore, builds/tests Debug et Release, `dotnet-ef migrations list`, `has-pending-model-changes`, `database update`, génération/revue du script, format ciblé, seeds doubles, vérifications S9/S10/S11, requêtes de preuve, smoke Desktop avec/sans SQL, scan secrets/PII et contrôles Git en lecture seule.

## 37. Résultats Debug

Build final : 0 avertissement, 0 erreur. Tests : 320 réussis, 0 échec, 0 ignoré. Le formatage ciblé des fichiers C# Sprint 11 passe `--verify-no-changes`.

## 38. Résultats Release

Build final : 0 avertissement, 0 erreur. Tests : 321 réussis, 0 échec, 0 ignoré.

## 39. Migrations en attente

Trois migrations sont appliquées à `Pharmacy_Development`. `dotnet-ef migrations has-pending-model-changes` indique qu’aucun changement du modèle n’est hors migration. Aucune migration de base n’est en attente.

## 40. Rollback et réapplication

Le test Sprint 11 migre explicitement vers `CatalogPersistenceEnhancementsV01`, vérifie la disparition des six tables Inventory et la conservation des cinq tables Catalogue, puis réapplique `InventoryPersistenceV01`. Le test de fondation conserve aussi le rollback vers `0` et la réapplication complète.

## 41. Bases temporaires résiduelles

Le contrôle de `master` trouve zéro base résiduelle correspondant aux préfixes `Pharmacy_Sprint09_Test_*`, `Pharmacy_Sprint10_Test_*` ou `Pharmacy_Sprint11_Test_*`.

## 42. Lancement Desktop

Le binaire Release a été lancé avec puis sans variable SQL. Dans les deux cas, le processus est resté répondant, le titre `Logiciel Pharmacie` a été détecté, l’arrêt a été proprement forcé après le smoke et aucun processus résiduel n’est resté. Le nombre de migrations est resté 3 avant/après : aucune migration automatique.

## 43. Problèmes rencontrés

Le premier scénario réel a révélé que le démarrage d’inventaire ouvrait une transaction explicite sans stratégie EF retry, traduite trop largement en indisponibilité. Le démarrage utilise désormais la stratégie d’exécution comme les autres opérations transactionnelles et le test passe. Le premier passage du clear a été refusé par SQL car les options ANSI requises par les index filtrés n’étaient pas déclarées ; `XACT_ABORT`/le `CATCH` ont annulé la transaction, les options ont été ajoutées, puis clear et double reseed ont réussi. Une option `--logger` non supportée par Microsoft Testing Platform a aussi été retirée de la commande de test, sans impact code.

## 44. Écarts

La vérification Sprint 9 passe. La vérification Sprint 10 historique échoue sur son assertion « exactement dix `DEMO-*` » car la collation insensible à la casse inclut `demo-manual-001`, donnée manuelle inactive créée avant Sprint 11. Elle a été volontairement conservée et aucun clear Sprint 10, trop large, n’a été exécuté. La vérification Sprint 11 passe.

## 45. Risques et limites

La cible pilote SQL Server 2025 Express, le certificat TLS approuvé, les permissions/audit utilisateur, les motifs administrables, la réservation, Purchasing et la consommation réelle par une vente restent hors de ce sprint. FEFO nécessite toujours validation humaine/configuration métier.

## 46. Validation manuelle requise

Captures SSMS et Desktop, revue de la liste/détail/expirés, ajustements, conflits, inventaire et mouvements d’écart, POS sans décrément, panne SQL, focus/clavier/scanner et rendu 1366×768/1920×1080. Aucune validation visuelle humaine n’est revendiquée.

## 47. État Git final

- branche : `main` ; HEAD initial et final : `58e7315` ; remote : zéro ; push : absent ; nouveaux commits : zéro ; fichiers stagés : zéro ;
- fichiers suivis modifiés : `README.md`, `docs/governance/CURRENT_STATE.md`, `docs/governance/DECISIONS_REGISTER.md`, composition/navigation/pages/diagnostic/POS/CSS Desktop, composition Catalogue, contexte/snapshot/navigation Catalogue Infrastructure, sept tests d’intégration historiques ;
- fichier suivi supprimé : `src/Pharmacy.Infrastructure/Sales/InMemory/SyntheticCatalogStockAvailabilityReader.cs` ;
- fichiers non suivis : quatre scripts SQL Inventory ; six documents Inventory et les trois rapports Sprint 11 ; prompt Sprint 11 ; dossiers Inventory Domain/Application/Infrastructure ; six entités + six configurations + schéma + migration/designer ; coordinator/page/CSS Stock ; trois fichiers de tests Sprint 11 ;
- toutes les modifications restent visibles pour revue ; aucun `git add`, commit, push, remote, branche, stash, reset ou réécriture d’historique.

## 48. Recommandation

Effectuer la revue manuelle/captures Sprint 11, puis traiter séparément la donnée manuelle `demo-manual-001` avec décision utilisateur explicite. Valider SQL Server Express et la confiance TLS avant toute certification pilote. Ne pas activer la consommation de stock par les ventes avant un sprint dédié.

## 49. Correction d’harmonisation UI Stock

La correction visuelle a été limitée à `InventoryPage.razor`, `inventory.css`, au test structurel Inventory et aux rapports Sprint 11. Aucun fichier Domain, Application, Infrastructure, SQL, migration, seed ou script n’a été modifié par cette correction.

Le titre est désormais `Stock SQL`. La page réutilise les surfaces `catalog-toolbar`, `catalog-panel`, `catalog-section-heading`, `catalog-table-scroll`, `catalog-table` et `catalog-row--selected`. Les quatre indicateurs utilisent `UiCard`, la recherche est isolée dans une barre cohérente avec Catalogue, le détail reste visible avec un état vide avant sélection et les sections ajustement/FEFO/inventaire partagent les mêmes tokens, champs, rayons, bordures et rythmes.

## 50. Dette CSS supprimée

Les anciens tokens Stock non déclarés (`--radius-lg`, `--radius-md`, `--color-brand-*`, `--color-text-muted`, `--space-5` et variantes) ont été retirés. Les 26 tokens désormais référencés par `inventory.css` sont tous déclarés dans `tokens.css`. Aucune valeur couleur locale, aucun deuxième design system et aucun package n’ont été ajoutés.

## 51. Responsive, états et accessibilité

Les cartes et filtres passent de quatre à deux puis une colonne ; les grilles détail/action passent sur une colonne à 1024 px ; les tables conservent un défilement local ; toutes les surfaces portent `min-width: 0`. Loading, erreur, vide et sélection utilisent les primitives existantes. Les contrôles conservent labels, hauteurs, focus visible, régions de table focusables et confirmations accessibles.

## 52. Validation de la correction

Les quatre commandes d’acceptation Debug/Release ont réussi avec 0 avertissement et 0 erreur. Les deux configurations comptent 321/321 tests réussis, dont un nouveau garde-fou structurel sur les primitives partagées, les quatre cartes, les états, les breakpoints, le scroll local et les tokens.

Le Desktop Debug a été lancé avec la base locale existante `Pharmacy_Development`. Il est resté répondant et a chargé les données existantes. Une fiche produit a été ouverte en lecture seule ; aucune opération d’écriture n’a été déclenchée. L’inspection assistée à 1100 px a couvert le haut de page, les indicateurs 2×2, la recherche, la liste, le détail, l’ajustement, FEFO et l’inventaire sans débordement global observé. La comparaison avec Catalogue à la même largeur a confirmé le titre, la largeur, les alertes et les surfaces partagées.

Le détail complet se trouve dans `docs/reports/sprint-11/ui-harmonization-validation.md`.

## 53. Revue visuelle humaine restante

Le statut demeure volontairement `MANUAL VISUAL REVIEW REQUIRED`. Les captures requises sont : haut de Stock ; tableau avec détail ; ajustement ou inventaire ; comparaison Stock/Catalogue à largeur identique. Elles doivent couvrir 1100×700, 1366×768 et 1920×1080 ainsi que clavier, focus, loading, vide, erreur et topbar.

## 54. Git après correction UI

Branche `main`, HEAD `58e7315`, index vide et aucun remote. Aucun `git add`, commit, push, changement de branche ou ajout de remote n’a été exécuté. Toutes les modifications Sprint 11 restent visibles pour revue.

## Synthèse copiable

- statut : `IMPLEMENTED — STOCK UI HARMONIZED — MANUAL VISUAL REVIEW REQUIRED` ;
- SQL Server : 17.0.1125.2 Developer, compatibilité 170, Express encore à valider ;
- schéma inventory : 6 tables, FK `NO ACTION`, contraintes et index contrôlés ;
- migration : `20260804181233_InventoryPersistenceV01`, appliquée, rollback/réapplication PASS ;
- Domain/Application : noyau, contrats, handlers, date injectable et erreurs stables ;
- mouvements : append-only, quantité positive, avant/après cohérents ;
- balances : transactionnelles, réconciliées, jamais négatives ;
- lots/péremption : expirés visibles mais non vendables ;
- FEFO : proposition déterministe testée, sans consommation ;
- inventaires : capture, comptage, finalisation atomique et idempotente ;
- seed : exécuté 2 fois, 1 emplacement, 5 lots, 12 balances/mouvements ;
- page Stock : fonctionnelle et harmonisée avec Catalogue, états/confirmations/responsive ;
- POS SQL : disponibilité réelle, vente non persistée, stock non décrémenté ;
- panne SQL : shell stable, erreurs structurées, aucun fallback ;
- packages : aucun changement ;
- builds : Debug/Release 0 avertissement, 0 erreur ;
- tests : 321/321 Debug et Release, 0 ignoré ;
- secrets : aucun secret/PII actif détecté ;
- écarts : vérification S10 historique bloquée par `demo-manual-001` préservé ;
- validation manuelle : SSMS/Desktop, clavier/focus/responsive, Express/TLS ;
- Git : `main`, HEAD `58e7315`, index vide, 0 commit/remote/push.
