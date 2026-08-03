# Sprint 9 — Rapport d’exécution Codex

## 1. Statut final

**IMPLEMENTED — SQL SERVER DEVELOPER VALIDATION PASS — EXPRESS ENVIRONMENT GATE PENDING**

## 2. Résumé

La première fondation EF Core/SQL Server est implémentée dans Infrastructure. Une migration versionnée crée cinq tables Catalogue vides, a été appliquée à `Pharmacy_Development` et validée sur une vraie instance SQL Server 2025 Developer. Le POS reste intégralement en mémoire.

## 3. Complétude du prompt

`docs/prompts/Sprint_09_Fondation_SQL_Server_Schema_Physique.md` a été lu intégralement : 776 lignes, 22 559 octets, dernière ligne non vide exactement `FIN DU PROMPT SPRINT 9`.

## 4. Documents consultés

Les vingt sources textuelles imposées, le prompt, les six projets, les deux projets de tests et les fichiers de solution/configuration ont été inventoriés et inspectés. Les cinq PDF validés ont été consultés en lecture seule ; les pages ADR-003/004/005/012, Catalogue Phase 2, conventions et Catalogue Phase 3, UX Catalogue et restrictions Phase 5 ont été extraites. Les pages structurantes ont aussi été rendues en images et contrôlées visuellement. Aucun PDF n’a été modifié.

## 5. Git initial

- branche `main` ;
- HEAD `49c2ff2 feat: connect POS to sales application workflows` ;
- aucun remote ;
- index vide ;
- seul le prompt Sprint 9 était non suivi ;
- aucun reset, restore, stash ou changement de branche.

## 6. Baseline .NET

- SDK 10.0.302, runtimes 10.0.10 ;
- six projets ;
- restore réussi ;
- build Debug : 0 avertissement, 0 erreur ;
- tests : 271 réussis, 0 échec, 0 ignoré.

## 7. Audit SQL Server

Services détectés : `MSSQLSERVER` actif, Browser et Agent arrêtés, `sqlcmd` ODBC 18 présent. Les deux variables Pharmacy étaient absentes à l’entrée. L’instance par défaut accepte la sécurité intégrée et les droits de création de base sont présents.

## 8. Version et édition

- serveur : `MED-AMINE` ;
- instance : défaut ;
- version : `17.0.1125.2` ;
- major : 17 ;
- édition : Standard Developer Edition 64 bits ;
- EngineEdition : 2.

## 9. Gate Express

L’environnement réel disponible est Developer. La validation technique SQL Server est réussie, mais le gate SQL Server 2025 Express demandé pour le pilote reste en attente. Aucun résultat Developer n’est présenté comme preuve Express.

## 10. Matrice de traçabilité

La matrice préalable à la migration se trouve dans `docs/database/CATALOG_PHYSICAL_SCHEMA_V0.1.md`. Chaque table, relation, type et différé est relié au prompt, aux ADR ou aux pages Phase 2/3.

## 11. Packages et dotnet-ef

Infrastructure référence `Microsoft.EntityFrameworkCore.SqlServer` et `Microsoft.EntityFrameworkCore.Design` 10.0.10. Design est privé. Le manifeste local restaure `dotnet-ef` 10.0.10. Aucun preview ou provider substitut n’est présent.

## 12. Architecture Persistence

La nouvelle arborescence `Persistence` sépare entités internes, configurations Fluent API, contexte, factory, options, DI et migrations. Domain/Application n’ont pas été modifiés. Desktop n’a pas été modifié.

## 13. DbContext

`PharmacyDbContext` contient seulement cinq `DbSet` internes du Catalogue. Les configurations sont chargées depuis l’assembly et aucune entité EF n’est exposée aux couches supérieures.

## 14. Factory/configuration

La factory lit `PHARMACY_SQLSERVER_CONNECTION`, refuse l’absence sans révéler de valeur et configure SQL Server, compatibilité 170, retry et assembly de migrations explicite.

## 15. Sécurité des connexions

La connexion TLS standard échoue sur une autorité de certification locale non approuvée. L’exception de confiance a été limitée aux processus de développement. Aucun mot de passe, compte SQL, valeur de connexion ou logging sensible n’a été écrit.

## 16. Périmètre Catalogue

Le périmètre comprend catégories, identité produit, paramètres locaux, codes-barres et historique des prix. Les données Sales, stock, lots, fournisseurs, clients, identité et audit sont absentes.

## 17. Tables

- `catalog.ProductCategories` ;
- `catalog.Products` ;
- `catalog.ProductLocalSettings` ;
- `catalog.ProductBarcodes` ;
- `catalog.ProductPriceHistories`.

## 18. Relations

Phase 3 impose le 1–1 Product/LocalSettings et les relations directes Product/Barcodes et Product/PriceHistories. `ProductReferences`, cible indicative du prompt, n’est donc pas créée. La catégorie principale est portée par LocalSettings.

## 19. Contraintes

Les noms/codes obligatoires sont non blancs, seuils/prix non négatifs, minimum inférieur au maximum, quantité scale 0–3, devise sur trois caractères et date de mise à jour postérieure ou égale à la création.

## 20. Index

Unicité sur code catégorie, référence interne filtrée et barcode normalisé ; unicité filtrée d’un seul barcode principal actif ; index justifiés sur noms, FK et historique par date.

## 21. Rowversion/concurrence

Quatre tables mutables portent `rowversion`. Un double chargement EF a produit un changement de version au premier update puis `DbUpdateConcurrencyException` sur l’ancienne version, sans écraser l’état gagnant.

## 22. Dates/montants

Instants en `datetime2(7)`, prix en `decimal(19,4)`, seuils en `decimal(18,3)`. Aucun `datetime`, `float`, `real`, `money` ou `smallmoney`.

## 23. Migration

`20260803185734_InitialCatalogPersistenceV01`, designer et snapshot générés puis inspectés. `Up` crée cinq tables sans seed ; `Down` les supprime sans cascade.

## 24. Script idempotent

Le script généré est `database/scripts/001_initial_catalog_foundation_idempotent.sql`. Il a été exécuté deux fois sur une base temporaire et deux fois sur la base de développement, sans duplication ; l’historique reste à une migration.

## 25. Base de développement

`Pharmacy_Development` était absente, a été créée explicitement, migrée et vérifiée. Elle est au niveau de compatibilité 170 et les cinq tables Catalogue contiennent zéro ligne.

## 26. Tests SQL réels

Un scénario complet utilise Microsoft.Data.SqlClient et EF Core contre SQL Server réel. Il valide serveur 17 Developer/Express, schéma, types, contraintes, FK, index, rowversion, historique et données synthétiques temporaires.

## 27. Rollback/réapplication

Sur base temporaire : migration, empreinte du schéma, rollback vers `0`, absence de tables `catalog`, réapplication, absence de migration en attente et empreinte finale identique.

## 28. Bases résiduelles

Le contrôle final retourne zéro base dont le nom commence par `Pharmacy_Sprint09_Test_`.

## 29. Vérification des secrets

Le scan spécialisé couvre clés privées, tokens AWS/GitHub/OpenAI/JWT, mots de passe, identifiants SQL et secrets assignés : zéro détection active. Deux faux positifs ont été examinés : connexion volontairement non utilisable `not-used.invalid` dans un test de modèle et syntaxe Razor `@token.CssClass`. Les variables temporaires étaient absentes après chaque commande.

## 30. Tests ajoutés

12 tests supplémentaires : 11 contrôles architecture/modèle/sécurité et un scénario SQL réel couvrant plusieurs invariants. Total : 283.

## 31. Commandes

Commandes exécutées : Git initial/final, lectures de sources, extraction/rendu PDF, baseline .NET, audit services/registre/sqlcmd, restore packages/outils, migrations EF, script idempotent, database update, script de vérification, tests Debug/Release, format ciblé, smoke Desktop et scan de secrets.

## 32. Debug

Build : réussite, 0 avertissement, 0 erreur. Tests : 283 réussis, 0 échec, 0 ignoré.

## 33. Release

Build : réussite, 0 avertissement, 0 erreur. Tests : 283 réussis, 0 échec, 0 ignoré.

## 34. Migrations en attente

`migrations list` retourne la migration initiale. `has-pending-model-changes` confirme l’absence de changement. `database update` confirme la base à jour.

## 35. Non-régression Desktop

Lancé sans variable SQL : fenêtre `Logiciel Pharmacie` répondante, handle non nul, fermeture normale, runner 0, stderr vide, aucun processus résiduel. Les tests d’architecture confirment le composition root en mémoire, Diagnostic et UI Lab.

## 36. Problèmes

1. La tentative design-time avec Desktop comme startup a été refusée car Desktop ne référence pas Design ; Infrastructure a été utilisé, sans polluer Desktop.
2. Le script idempotent n’a trouvé la migration qu’après reconstruction de l’assembly, comportement attendu avec `--no-build`.
3. Le script de vérification employait l’alias réservé `RowCount` ; il a été délimité puis validé.
4. Le format global signale des écarts historiques dans `Desktop/AssemblyInfo.cs` ; seuls les fichiers Sprint 9 ont été formatés et vérifiés.
5. Le premier harness smoke exigeait un ExitCode indisponible sur l’objet processus enfant ; la preuve a été relancée avec `HasExited` et le code du runner.

## 37. Écarts

- `ProductLocalSettings` remplace `ProductReferences` selon la priorité Phase 3 explicitement prévue par le prompt.
- Startup EF design-time = Infrastructure, pas Desktop, pour respecter l’isolation du package Design.
- Validation SQL Server Developer réussie ; preuve Express différée car l’édition n’est pas installée.
- Les captures SSMS et la revue humaine restent à fournir.

## 38. Risques/limites

Gate Express, certificat TLS approuvé, sauvegarde/restauration pilote et revue SSMS manquent encore. Aucune persistance Sales ou connexion POS/SQL n’existe. Les longueurs physiques restent des décisions techniques à revoir avec le dictionnaire complet.

## 39. Validation manuelle

Exécuter le script read-only et fournir six captures listées dans `database-validation.md`. Confirmer version/édition, 283 tests, 12 nouveaux tests, gate Express en attente et tout écart observé. Aucun commit n’est demandé.

## 40. Git final

- branche : `main` ;
- HEAD initial et final : `49c2ff2` ;
- index : vide ;
- fichiers Sprint 9 : modifiés/non suivis, visibles pour revue ;
- nouveaux commits : zéro ;
- remote : zéro ;
- push, changement de branche, stash, reset ou réécriture : aucun.

## 41. Recommandation

Maintenir le gate pilote ouvert. Réaliser la revue SSMS, installer/valider SQL Server 2025 Express et corriger la chaîne de confiance TLS avant tout sprint qui brancherait un adaptateur SQL ou préparerait une installation pilote.
