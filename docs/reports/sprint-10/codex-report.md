# Sprint 10 — Rapport d’exécution Codex

## 1. Statut final

`IMPLEMENTED — SQL CATALOG VALIDATION PASS — MANUAL CATALOG REVIEW REQUIRED`

## 2. Résumé du travail

Le Catalogue est désormais réellement persistant dans SQL Server et le POS utilise ce lecteur pour les recherches, scans et prix. Produits, paramètres locaux, catégories, barcodes et prix/historique sont gérés par Application + Infrastructure SQL. Le panier, le stock synthétique, les paiements, suspensions et impressions restent en mémoire ; aucune vente n’est persistée.

## 3. Complétude du prompt

`docs/prompts/Sprint_10_Persistance_Reelle_Catalogue_SQL.md` a été lu intégralement : 867 lignes, 19 100 octets, dernière ligne non vide exactement `FIN DU PROMPT SPRINT 10`.

## 4. Documents consultés

Toutes les sources textuelles imposées, les projets, tests, migrations, snapshot et scripts SQL ont été inspectés. Les cinq PDF ont été consultés en lecture seule sur les pages ADR-003/004/005/012, Catalogue Phase 2, conventions/Catalogue/invariants Phase 3, UX Catalogue et méthode code-first Phase 5. Des pages représentatives ont été rendues et contrôlées visuellement. Aucun PDF n’a été modifié.

## 5. État initial

- branche `main` ;
- HEAD `e9a5fcf feat: add SQL Server persistence foundation and catalog schema` ;
- index vide ;
- aucun remote ;
- seul le prompt Sprint 10 était non suivi ;
- base `Pharmacy_Development` vide avec une migration et cinq tables ;
- build Debug initial : 0 avertissement, 0 erreur ;
- premier test sans variable SQL : 282 réussis, 1 échec attendu de prérequis, 0 ignoré ;
- baseline avec variable temporaire : 283/283.

## 6. Mapping et architecture

Application contient contrats spécifiques, modèles immuables, requêtes, commandes, validation et erreurs. Infrastructure contient lecteur, store, diagnostic, normalisation et composition SQL. Desktop ne connaît aucun `DbContext` et utilise `CatalogCoordinator`/handlers.

## 7. Use cases

Liste/détail, catégories, création produit, mise à jour identité/paramètres, activation, ajout/mise à jour barcode, prix dédié avec historique, diagnostic et erreurs structurées sont implémentés avec `CancellationToken`.

## 8. Lecteur SQL et recherche POS

`SqlProductCatalogReader` implémente le contrat Catalogue et `IProductCatalogReader`. Les requêtes sont `AsNoTracking`, bornées et projetées. Nom, présentation, référence interne et barcode sont recherchables ; lecture exacte par identifiant/référence/barcode ; inactifs exclus du POS ; code inconnu sans création.

## 9. Store, transaction et contraintes

Les mutations multi-entités utilisent un contexte court et un seul `SaveChangesAsync`, donc un commit EF atomique. Les contraintes uniques sont traduites via codes SQL 2601/2627 plus détection par valeur. Les FK et prix invalides sont traduits par opération. Aucun message localisé n’est la seule source de décision.

## 10. Rowversion

Les tokens Application sont Base64. Produit, paramètres, barcode et catégorie utilisent leur rowversion EF. Une version obsolète retourne une erreur structurée, sans écrasement, retry ou historique orphelin.

## 11. Migration

Le schéma Sprint 9 ne disposait pas de `Products.IsActive`, requis explicitement. `CatalogPersistenceEnhancementsV01` ajoute seulement le booléen global par défaut actif et son index. La migration Sprint 9 est intacte. Aucune autre évolution n’a été inventée.

## 12. Seed et scripts

Seed, clear et vérification sont protégés sur `Pharmacy_Development`. Le seed est transactionnel/idempotent et contient dix produits entièrement synthétiques. Il a été exécuté deux fois. Le clear ciblé a été exécuté pour corriger une première interprétation non UTF-8 de `sqlcmd`, puis le seed a été rejoué avec `-f 65001`.

## 13. Desktop et UX

Le placeholder Catalogue est remplacé par une page responsive : recherche, filtres, liste, loading/empty/error, détail, création/modification, activation, barcodes, prix/historique et catégories. Le coordinateur et `UiButton` bloquent les doubles soumissions. POS et Diagnostic indiquent précisément le périmètre SQL/mémoire.

## 14. Panne SQL

Sans connexion valide vers `Pharmacy_Development`, une factory indisponible retourne `catalog.database.unavailable`. Le shell démarre, Catalogue et POS signalent la panne, Diagnostic reste accessible, aucun fallback mémoire ou exception brute n’est présenté.

## 15. Tests ajoutés

17 tests portent le total de 283 à 300 : 8 unitaires Application, 8 architecture/UX/sécurité et 1 scénario SQL réel multi-assertions. Le scénario crée et supprime une base `Pharmacy_Sprint10_Test_<hex>`, applique les migrations, vérifie lecture/écriture/rollback/concurrence/recherche POS et refuse le seed sur la mauvaise cible.

## 16. Résultats automatisés

- Debug : build 0 avertissement/0 erreur ; 300/300 tests, 0 ignoré ;
- Release : build 0 avertissement/0 erreur ; 300/300 tests, 0 ignoré ;
- migrations : deux listées, aucune modification de modèle en attente ;
- scripts Sprint 9 et Sprint 10 : réussite ;
- seed deux fois : 10 produits, 4 catégories, 10 barcodes, 10 historiques ;
- bases temporaires Sprint 9/10 résiduelles : 0 ;
- formatage ciblé de tous les fichiers C# modifiés ou ajoutés au Sprint 10 : conforme ;
- smoke Release du shell avec et sans SQL : processus stable puis arrêté proprement.

## 17. Sécurité

Aucune chaîne réelle, mot de passe, login SQL, token, donnée clinique, personnelle ou bancaire n’est versionné. Le Diagnostic n’affiche pas la connexion. Aucun `EnableSensitiveDataLogging`, `EnsureCreated`, auto-migration, table Stock/Sales ou repository générique n’a été ajouté.

## 18. Problèmes et corrections

1. La baseline sans variable de test a confirmé le garde de sécurité du test SQL ; relance réussie avec variable limitée au processus.
2. Le seed initial a été rollbacké car SQL Server exige les options ANSI pour l'index filtré ; options explicites ajoutées.
3. `sqlcmd` sans codepage explicite interprétait mal les accents ; clear ciblé puis seed avec `-f 65001`.
4. Deux anciens tests de texte attendaient encore les libellés Diagnostic/base non connectée ; assertions mises à jour vers le comportement Sprint 10.
5. Le lanceur Microsoft.Testing.Platform de .NET 10 a rejeté les options VSTest `--nologo`/`--logger` ; relance avec sa syntaxe native, 300/300.

## 19. Écarts et limites

- Une migration était nécessaire, contrairement à l’hypothèse « aucune si possible », car l’activation globale imposée était physiquement absente.
- L'historique des prix reste append-only sans période de fin, conformément à la Phase 3.
- Express, certificat TLS approuvé, captures SSMS et revue visuelle/interactive restent manuels.
- Le contrôle `dotnet format` global signale encore une dette antérieure au Sprint 10 dans `src/Pharmacy.Desktop/AssemblyInfo.cs` et la migration initiale Sprint 9 (espaces, fins de ligne/encodage). Le périmètre C# modifié ou ajouté au Sprint 10 passe le contrôle ciblé.

## 20. Validation manuelle

Les commandes, checklist SSMS/Desktop et sept captures sont détaillées dans `catalog-validation.md`. Tester également une panne SQL. Aucun commit n’est demandé.

## 21. Git final

- branche : `main` ;
- HEAD initial/final : `e9a5fcf` ;
- index : vide ;
- fichiers Sprint 10 : modifiés/non suivis, visibles pour revue ;
- commits : zéro ;
- remotes : zéro ;
- push, changement de branche, stash, reset, rebase : aucun.

## 22. Recommandation

Effectuer la revue manuelle Catalogue/POS/Diagnostic et les captures. Maintenir le gate pilote ouvert jusqu’à validation SQL Server 2025 Express et remédiation de la confiance TLS locale.
