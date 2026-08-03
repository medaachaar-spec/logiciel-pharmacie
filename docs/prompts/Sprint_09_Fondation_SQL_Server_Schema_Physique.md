# SPRINT 9 — FONDATION SQL SERVER ET SCHÉMA PHYSIQUE CATALOGUE V0.1

## Instruction courte à donner à Codex

Placer ce fichier dans le dépôt sous :

`docs/prompts/Sprint_09_Fondation_SQL_Server_Schema_Physique.md`

Puis transmettre uniquement :

> Lis intégralement `docs/prompts/Sprint_09_Fondation_SQL_Server_Schema_Physique.md`, vérifie que la dernière ligne non vide est exactement `FIN DU PROMPT SPRINT 9`, puis exécute toutes les opérations autorisées. Ne fais aucun commit, push, changement de branche ou ajout de remote.

---

## 1. RÔLE

Tu agis comme architecte de données senior, ingénieur .NET 10 / EF Core 10, spécialiste SQL Server 2025, migrations contrôlées, concurrence optimiste, sécurité des connexions et tests d’intégration sur un vrai moteur SQL Server.

Le projet « Logiciel Pharmacie » possède déjà :

- le noyau Domain Sales ;
- les cas d’utilisation Application Sales ;
- les adaptateurs temporaires en mémoire ;
- le POS connecté à Application ;
- 271 tests validés ;
- aucune persistance SQL.

Ta mission est de créer la première fondation physique de la base sans connecter encore le POS à SQL Server.

Ne t’arrête pas à un plan. Inspecte, implémente, migre, teste, documente et laisse les changements hors index.

---

## 2. MISSION

Créer **Persistence Foundation V0.1** et **Catalog Physical Schema V0.1** :

1. auditer l’instance SQL Server locale ;
2. identifier version et édition ;
3. distinguer environnement de développement et cible pilote SQL Server 2025 Express ;
4. ajouter EF Core SQL Server stable ;
5. ajouter un outil local `dotnet-ef` versionné ;
6. créer `PharmacyDbContext` dans Infrastructure ;
7. créer une factory design-time sécurisée ;
8. créer les modèles de persistance internes du Catalogue ;
9. créer les configurations Fluent API ;
10. créer la migration initiale ;
11. générer un script SQL idempotent ;
12. créer/appliquer la base `Pharmacy_Development` ;
13. exécuter des tests sur une vraie instance SQL Server ;
14. valider PK, FK, contraintes, index, types et `rowversion` ;
15. valider rollback puis réapplication sur une base temporaire ;
16. garantir l’absence de secret dans Git ;
17. garantir l’absence de migration automatique au démarrage Desktop ;
18. conserver le POS sur ses adaptateurs en mémoire ;
19. ne créer aucune implémentation SQL Sales ;
20. ne créer aucun commit ou push.

Statuts autorisés :

- `IMPLEMENTED — SQL SERVER EXPRESS VALIDATION PASS — MANUAL DATABASE REVIEW REQUIRED`
- `IMPLEMENTED — SQL SERVER DEVELOPER VALIDATION PASS — EXPRESS ENVIRONMENT GATE PENDING`
- `BLOCKED — SQL SERVER INSTANCE REQUIRED`
- `IMPLEMENTED — CORRECTIONS REQUIRED`
- `FAILED`

---

## 3. SOURCES DE VÉRITÉ

Lire intégralement :

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
16. `docs/infrastructure/IN_MEMORY_SALES_ADAPTERS_V0.1.md`
17. `docs/application/DESKTOP_SALES_INTEGRATION_V0.1.md`
18. `docs/ux/POS_APPLICATION_INTEGRATION_V0.1.md`
19. `docs/reports/sprint-08/codex-report.md`
20. `docs/reports/sprint-08/pos-application-integration-validation.md`
21. le présent fichier jusqu’au marqueur final.

Inspecter complètement :

- `src/Pharmacy.Domain`
- `src/Pharmacy.Application`
- `src/Pharmacy.Infrastructure`
- `src/Pharmacy.Desktop`
- `tests/Pharmacy.UnitTests`
- `tests/Pharmacy.IntegrationTests`
- les fichiers de solution, projets et configuration NuGet.

Consulter en lecture seule :

- `docs/reference/01_Dossier_ADR_Logiciel_Pharmacie_V1.0.pdf`
- `docs/reference/02_Dossier_Phase_2_Modelisation_Fonctionnelle_Complete_V0.1.pdf`
- `docs/reference/03_Dossier_Phase_3_Conception_Modele_Donnees_V0.1.pdf`
- `docs/reference/04_Dossier_Phase_4_Conception_Parcours_UX_UI_V0.1.pdf`
- `docs/reference/05_Dossier_Phase_5_Code_First_Vibe_Coding_V0.1.pdf`

Ne modifier aucun PDF.

Ordre de priorité : présent prompt, décisions validées, état courant, scope, Phase 2/3, autres documents, code.

Avant la migration, produire une matrice :

| Élément physique | Source exacte | Classification |
|---|---|---|
| Table/colonne/relation | document + section/page | confirmé / décision technique / différé |

Ne jamais présenter une décision technique comme une exigence pharmacien ou légale.

---

## 4. ÉTAT INITIAL ET PRÉREQUIS

Le Sprint 8 est annoncé commité. Ne suppose aucun hash.

Exécuter avant modification :

```powershell
git status
git status --short
git branch --show-current
git log --oneline -n 10
git remote -v
git diff --check

dotnet --info
dotnet sln Pharmacy.slnx list
dotnet restore Pharmacy.slnx
dotnet build Pharmacy.slnx -c Debug --no-restore
dotnet test Pharmacy.slnx -c Debug --no-build
```

Attendu :

- branche `main` ;
- working tree propre ;
- aucun fichier stagé ;
- aucun remote ;
- six projets ;
- au moins 271 tests ;
- zéro erreur et zéro avertissement raisonnable.

Si le dépôt n’est pas propre : ne pas reset, restore, stash ou supprimer. Inspecter et bloquer si l’origine est incertaine.

Auditer SQL Server sans installation automatique :

```powershell
Get-Service | Where-Object {
    $_.Name -match 'MSSQL|SQLAgent|SQLBrowser' -or
    $_.DisplayName -match 'SQL Server'
} | Sort-Object Name | Format-Table -AutoSize

Get-Command sqlcmd -ErrorAction SilentlyContinue
```

Tester les instances détectées et exécuter :

```sql
SELECT
    SERVERPROPERTY('ServerName') AS ServerName,
    SERVERPROPERTY('InstanceName') AS InstanceName,
    SERVERPROPERTY('ProductVersion') AS ProductVersion,
    SERVERPROPERTY('ProductMajorVersion') AS ProductMajorVersion,
    SERVERPROPERTY('Edition') AS Edition,
    SERVERPROPERTY('EngineEdition') AS EngineEdition;
```

Ne pas installer SQL Server Express automatiquement.

Si aucune instance réelle n’est utilisable, ne pas simuler les preuves SQL.

---

## 5. PACKAGES ET OUTILS

Utiliser les versions stables suivantes, sauf version stable 10.0.x plus récente déjà centralisée dans le dépôt :

- `Microsoft.EntityFrameworkCore.SqlServer` 10.0.10
- `Microsoft.EntityFrameworkCore.Design` 10.0.10
- outil local `dotnet-ef` 10.0.10

Ne jamais utiliser EF Core 11 preview.

Dans `Pharmacy.Infrastructure` :

- ajouter le provider SQL Server ;
- ajouter Design avec `PrivateAssets="all"` et `IncludeAssets` approprié.

Ne pas ajouter EF Core à Domain ou Application.

Créer ou compléter `.config/dotnet-tools.json`, puis exécuter :

```powershell
dotnet tool restore
dotnet ef --version
```

Packages interdits : Dapper, FluentMigrator, DbUp, EFCore.NamingConventions, Testcontainers, Respawn, provider EF InMemory, SQLite, repository générique, package de secrets tiers.

Les preuves de schéma doivent utiliser un vrai SQL Server, jamais LocalDB, SQLite ou EF InMemory.

---

## 6. PÉRIMÈTRE AUTORISÉ

Tu peux modifier/créer dans :

- `src/Pharmacy.Infrastructure`
- `tests/Pharmacy.IntegrationTests`
- `tests/Pharmacy.UnitTests` pour les contrôles d’architecture
- `.config/dotnet-tools.json`
- les `.csproj` nécessaires
- `Directory.Packages.props` seulement si le dépôt centralise déjà les versions
- `README.md`
- `docs/governance/CURRENT_STATE.md`
- `docs/governance/DECISIONS_REGISTER.md` uniquement pour les nouvelles décisions techniques réelles.

Créer :

- `database/migrations/README.md`
- `database/scripts/001_initial_catalog_foundation_idempotent.sql`
- `database/verification/verify_sprint09_schema.sql`
- `docs/database/PERSISTENCE_FOUNDATION_V0.1.md`
- `docs/database/CATALOG_PHYSICAL_SCHEMA_V0.1.md`
- `docs/database/CATALOG_ERD_V0.1.mmd`
- `docs/reports/sprint-09/database-validation.md`
- `docs/reports/sprint-09/codex-report.md`

Les migrations EF peuvent être créées sous :

`src/Pharmacy.Infrastructure/Persistence/Migrations`

Ne modifier Desktop que si une correction minimale de compilation est indispensable. Ne connecter ni le POS ni les handlers Sales à SQL Server.

---

## 7. HORS PÉRIMÈTRE

Ne développer aucune persistance pour :

- ventes, lignes de vente ou brouillons ;
- paiements, caisse, crédit ;
- stock, mouvements, balances, lots, péremption ou FEFO ;
- fournisseurs, commandes ou réceptions ;
- clients, retours ou remboursements ;
- sécurité persistante, audit complet, reporting, sauvegarde, import ou matériel.

Ne créer aucune table spéculative vide pour les modules futurs.

Ne créer aucune procédure stockée, trigger ou vue métier.

Ne créer aucun seed de médicament réel.

Interdictions :

- `EnsureCreated()` ;
- `EnsureDeleted()` dans le code de production ;
- `Database.Migrate()` au démarrage Desktop ;
- migration automatique au lancement ;
- connexion réelle versionnée ;
- `EnableSensitiveDataLogging` ;
- suppression en cascade non confirmée ;
- `datetime`, `float`, `real`, `money` ou `smallmoney` pour les valeurs concernées.

---

## 8. ARCHITECTURE ET SCHÉMA PHYSIQUE

Organisation recommandée :

```text
Persistence/
├── PharmacyDbContext.cs
├── PharmacyDbContextFactory.cs
├── DependencyInjection.cs
├── Entities/Catalog/
├── Configurations/Catalog/
└── Migrations/
```

Les entités EF sont des modèles de persistance internes à Infrastructure. Elles ne doivent pas être exposées à Desktop ou Application.

### `PharmacyDbContext`

- `DbSet` uniquement pour le périmètre Sprint 9 ;
- configurations séparées `IEntityTypeConfiguration<T>` ;
- application automatique ou explicite des configurations ;
- aucune configuration géante dans `OnModelCreating`.

### SQL Server

Configurer :

- `UseSqlServer` ;
- compatibilité SQL Server 2025 : niveau 170 ;
- `EnableRetryOnFailure()` ;
- assembly de migrations explicite si nécessaire ;
- aucun logging sensible.

### Factory design-time

Créer `IDesignTimeDbContextFactory<PharmacyDbContext>` lisant :

`PHARMACY_SQLSERVER_CONNECTION`

Elle doit refuser une valeur absente/vide sans révéler son contenu.

Tests :

`PHARMACY_TEST_SQLSERVER_CONNECTION`

Aucun secret ne doit être versionné.

### Schéma SQL

Utiliser le schéma :

`catalog`

### Tables minimales

Implémenter uniquement les tables Catalogue confirmées par les dossiers validés. Cible minimale :

1. `catalog.ProductCategories`
2. `catalog.Products`
3. `catalog.ProductReferences`
4. `catalog.ProductBarcodes`
5. `catalog.ProductPriceHistories`

Si Phase 3 impose des noms ou relations différents, suivre Phase 3 et documenter la différence.

Relations minimales :

```text
ProductCategory
    └── Products

Product
    └── ProductReferences

ProductReference
    ├── ProductBarcodes
    └── ProductPriceHistories
```

La cardinalité des catégories doit venir de Phase 3. Si plusieurs catégories sont confirmées, créer la table de liaison confirmée. Sinon, ne pas inventer de many-to-many.

### Types

- PK persistantes : C# `long`, SQL `bigint IDENTITY` ;
- montants : `decimal`, précision Phase 3, sinon décision technique `decimal(18,2)` ;
- instants : `datetime2`, UTC ;
- textes : `nvarchar` avec longueur explicite ;
- aucune colonne `nvarchar(max)` sans justification ;
- tables mutables : `rowversion` via `.IsRowVersion()`.

### Suppression

Utiliser `Restrict` ou `NoAction` pour les données référencées. La désactivation fonctionnelle remplace la suppression arbitraire lorsqu’elle est confirmée.

### Contraintes et index

Configurer et nommer explicitement selon le modèle :

- PK/FK ;
- référence interne unique ;
- code-barres textuel unique ;
- noms et références obligatoires ;
- prix non négatif ;
- périodes de prix cohérentes ;
- index de FK ;
- index de recherche réellement justifiés.

Ne pas indexer chaque colonne.

### Données

La migration crée un schéma vide. Aucun médicament ni produit Demo dans la migration. Les tests utilisent uniquement des données synthétiques temporaires.

---

## 9. MIGRATIONS ET BASES

Nom de migration :

`InitialCatalogPersistenceV01`

Commande indicative :

```powershell
dotnet tool restore

dotnet ef migrations add InitialCatalogPersistenceV01 `
  --project src/Pharmacy.Infrastructure/Pharmacy.Infrastructure.csproj `
  --startup-project src/Pharmacy.Desktop/Pharmacy.Desktop.csproj `
  --context PharmacyDbContext `
  --output-dir Persistence/Migrations
```

Adapter uniquement les chemins réels.

Inspecter manuellement `Up`, `Down` et le snapshot avant application.

Générer un script idempotent :

`database/scripts/001_initial_catalog_foundation_idempotent.sql`

avec `dotnet ef migrations script 0 InitialCatalogPersistenceV01 --idempotent`.

Créer/appliquer la base de développement :

`Pharmacy_Development`

Ne jamais utiliser ou supprimer une base existante inconnue.

### Bases de test

Créer des bases uniques :

`Pharmacy_Sprint09_Test_<suffixe>`

Règles :

- refuser toute suppression d’un nom ne commençant pas par ce préfixe ;
- appliquer les migrations, tester, supprimer dans un `finally` ;
- sérialiser les tests SQL si nécessaire ;
- aucune base temporaire résiduelle ;
- aucun `EnsureCreated`.

### Rollback

Sur une base temporaire :

1. migration vers la dernière version ;
2. vérification ;
3. retour à `0` ;
4. vérification de suppression ;
5. réapplication ;
6. comparaison du schéma final.

Le Desktop ne doit exécuter aucune migration automatiquement.

---

## 10. SÉCURITÉ, CONNEXIONS ET LOGS

Ne versionner aucun :

- mot de passe ;
- `Password=` ;
- `Pwd=` ;
- compte SQL ;
- chaîne de connexion réelle ;
- secret dans script ou rapport.

Les noms de variables d’environnement peuvent apparaître, pas leurs valeurs sensibles.

Pour le développement local, Windows Integrated Security est autorisée si disponible. Ce n’est pas une décision de déploiement définitive.

`TrustServerCertificate=True` est toléré seulement pour le développement local et doit être documenté comme provisoire.

Ne pas afficher une chaîne complète dans les logs ou exceptions.

Réutiliser les logs existants sans données sensibles.

Ne créer aucun login SQL ou utilisateur de production.

---

## 11. PLAN D’EXÉCUTION

Exécuter dans cet ordre :

1. vérifier le marqueur final ;
2. lire les sources ;
3. vérifier Git ;
4. exécuter la baseline ;
5. auditer SQL Server ;
6. identifier l’instance réelle ;
7. produire la matrice de traçabilité ;
8. confirmer le périmètre Catalogue ;
9. ajouter les packages stables ;
10. ajouter/restaurer `dotnet-ef` local ;
11. créer l’architecture Persistence ;
12. créer les entités internes ;
13. créer les configurations Fluent API ;
14. créer le DbContext ;
15. créer la factory ;
16. créer l’extension SQL Server ;
17. ajouter les tests d’architecture ;
18. créer la migration ;
19. inspecter la migration ;
20. générer le script idempotent ;
21. créer/appliquer `Pharmacy_Development` ;
22. créer le script de vérification ;
23. ajouter les tests SQL réels ;
24. tester contraintes et concurrence ;
25. tester rollback/réapplication ;
26. vérifier l’absence de base temporaire ;
27. exécuter Debug ;
28. exécuter Release ;
29. vérifier les migrations en attente ;
30. lancer Desktop pour non-régression ;
31. produire ERD et documentation ;
32. rechercher secrets et vérifier le diff ;
33. produire le rapport.

Toute modification de Domain ou Application doit être évitée. Si elle devient indispensable, la justifier avant exécution et la tester.

---

## 12. TESTS ET CRITÈRES D’ACCEPTATION

Conserver tous les anciens tests et ajouter au minimum :

### Architecture/packages

1. EF Core SQL Server stable 10.0.10 ;
2. Design stable 10.0.10 ;
3. aucun preview ;
4. `dotnet-ef` local 10.0.10 ;
5. Domain/Application sans EF Core ;
6. Application sans référence Infrastructure ;
7. Desktop sans DbContext ;
8. aucun nouveau projet ou ProjectReference interdit.

### Configuration

9. factory refuse une connexion absente ;
10. aucun secret révélé ;
11. SQL Server configuré ;
12. compatibilité 170 ;
13. retry activé ;
14. sensitive logging désactivé ;
15. aucune migration automatique ;
16. aucun `EnsureCreated`.

### Schéma

17. schéma `catalog` ;
18. tables attendues ;
19. PK `bigint` ;
20. relations et delete behavior corrects ;
21. colonnes obligatoires ;
22. longueurs explicites ;
23. prix `decimal` ;
24. dates `datetime2` ;
25. `rowversion` sur tables mutables ;
26. aucun type monétaire flottant ;
27. aucun type clinique ou bancaire.

### Contraintes/index

28. référence unique ;
29. code-barres unique ;
30. prix négatif refusé ;
31. FK invalide refusée ;
32. suppression parent référencé refusée ;
33. période de prix invalide refusée si prévue ;
34. index FK présents ;
35. noms de contraintes stables.

### Concurrence

36. `rowversion` créé ;
37. `rowversion` change à l’update ;
38. ancienne version déclenche un conflit ;
39. état cohérent après conflit.

### Migrations/SQL réel

40. migration et snapshot présents ;
41. script idempotent présent ;
42. `Up` réussi ;
43. `Down` réussi sur base temporaire ;
44. réapplication réussie ;
45. aucune migration en attente ;
46. `__EFMigrationsHistory` cohérente ;
47. SQL Server réel utilisé ;
48. version/édition enregistrées ;
49. base temporaire sécurisée ;
50. aucune base temporaire résiduelle ;
51. aucun LocalDB, SQLite ou InMemory comme preuve.

### Non-régression/sécurité

52. 271 anciens tests passent ;
53. Desktop compile et démarre ;
54. POS reste en mémoire ;
55. Diagnostic et UI Lab accessibles ;
56. aucun appel à `ISaleFinalizer` ;
57. aucun mot de passe ou connexion réelle dans Git ;
58. script sans secret.

Commandes obligatoires :

```powershell
dotnet tool restore
dotnet restore Pharmacy.slnx
dotnet build Pharmacy.slnx -c Debug --no-restore
dotnet test Pharmacy.slnx -c Debug --no-build
dotnet build Pharmacy.slnx -c Release --no-restore
dotnet test Pharmacy.slnx -c Release --no-build
```

Exécuter également les commandes EF adaptées :

```powershell
dotnet ef migrations list ...
dotnet ef migrations has-pending-model-changes ...
dotnet ef database update ...
```

Attendu : zéro erreur, zéro avertissement raisonnable, aucun test ignoré, aucune migration en attente, aucune base de test résiduelle.

Lancer :

```powershell
dotnet run --project src/Pharmacy.Desktop/Pharmacy.Desktop.csproj -c Debug
```

Confirmer techniquement : fenêtre créée, POS en mémoire, Diagnostic/UI Lab accessibles, aucune migration automatique, aucune exception, fermeture propre.

---

## 13. VALIDATION MANUELLE À FOURNIR

Fournir les commandes .NET et une commande `sqlcmd` sûre ciblant explicitement `Pharmacy_Development` et exécutant :

`database/verification/verify_sprint09_schema.sql`

Le script de vérification doit être uniquement en lecture et afficher :

- version/édition SQL Server ;
- migrations appliquées ;
- tables `catalog` ;
- colonnes et types ;
- PK/FK ;
- contraintes ;
- index ;
- colonnes `rowversion` ;
- nombre de lignes par table.

Demander six captures SSMS :

1. instance et base `Pharmacy_Development` ;
2. tables du schéma `catalog` ;
3. colonnes avec `bigint`, `datetime2`, `decimal`, `rowversion` ;
4. clés étrangères et index ;
5. résultat du script de vérification ;
6. `__EFMigrationsHistory`.

Checklist :

- [ ] base de développement présente ;
- [ ] migration appliquée ;
- [ ] tables Catalogue présentes ;
- [ ] relations visibles ;
- [ ] PK en bigint ;
- [ ] prix en decimal ;
- [ ] dates en datetime2 ;
- [ ] rowversion présent ;
- [ ] unicité et prix négatif testés ;
- [ ] suppression restrictive testée ;
- [ ] aucune donnée réelle ;
- [ ] aucun secret versionné ;
- [ ] aucune migration automatique ;
- [ ] POS toujours opérationnel en mémoire ;
- [ ] aucune exception.

Demander aussi : édition/version SQL, total de tests, nouveaux tests, gate Express, écarts.

Ne demander aucun commit.

---

## 14. DOCUMENTATION ET RAPPORT

Créer :

### `docs/database/PERSISTENCE_FOUNDATION_V0.1.md`

Documenter environnement SQL, édition validée, cible Express, packages, DbContext, factory, variables, sécurité, migrations contrôlées, tests SQL réels, limites et suite.

### `docs/database/CATALOG_PHYSICAL_SCHEMA_V0.1.md`

Documenter source, classification, tables, colonnes, types, PK, FK, cardinalités, contraintes, index, rowversion, UTC, montants, suppressions, éléments différés et dictionnaire de données.

### `docs/database/CATALOG_ERD_V0.1.mmd`

Créer un ERD Mermaid strictement fidèle à la migration.

### `docs/reports/sprint-09/database-validation.md`

Inclure environnement, connexion masquée, migration, script, tests, rollback, non-régression, limites et checklist humaine.

### `docs/reports/sprint-09/codex-report.md`

Structure :

1. Statut final
2. Résumé
3. Complétude du prompt
4. Documents consultés
5. Git initial
6. Baseline .NET
7. Audit SQL Server
8. Version et édition
9. Gate Express
10. Matrice de traçabilité
11. Packages et dotnet-ef
12. Architecture Persistence
13. DbContext
14. Factory/configuration
15. Sécurité des connexions
16. Périmètre Catalogue
17. Tables
18. Relations
19. Contraintes
20. Index
21. Rowversion/concurrence
22. Dates/montants
23. Migration
24. Script idempotent
25. Base de développement
26. Tests SQL réels
27. Rollback/réapplication
28. Bases résiduelles
29. Vérification des secrets
30. Tests ajoutés
31. Commandes
32. Debug
33. Release
34. Migrations en attente
35. Non-régression Desktop
36. Problèmes
37. Écarts
38. Risques/limites
39. Validation manuelle
40. Git final
41. Recommandation

Indiquer dans Git final : branche, HEAD initial, fichiers modifiés/non suivis, staging zéro, nouveaux commits zéro, remote zéro, push absent.

Mettre à jour `README.md`, `CURRENT_STATE.md` et seulement les nouvelles décisions techniques dans `DECISIONS_REGISTER.md`.

---

## 15. RESTRICTIONS GIT ABSOLUES

- NE PAS EXÉCUTER `git add`.
- NE PAS COMMIT.
- NE PAS PUSH.
- NE PAS CRÉER DE REMOTE.
- NE PAS CHANGER DE BRANCHE.
- NE PAS RÉÉCRIRE L’HISTORIQUE.
- NE PAS UTILISER STASH.
- NE PAS UTILISER RESET.
- LAISSER TOUTES LES MODIFICATIONS VISIBLES POUR REVUE.

FIN DU PROMPT SPRINT 9
