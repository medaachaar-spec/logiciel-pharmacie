# SPRINT 10 — PERSISTANCE RÉELLE DU CATALOGUE ET CONNEXION DU POS À SQL SERVER V0.1

## 1. RÔLE

Tu agis comme :

- ingénieur logiciel senior .NET 10 ;
- architecte Clean Architecture ;
- spécialiste EF Core 10 et SQL Server 2025 ;
- développeur WPF et Blazor Hybrid ;
- spécialiste des transactions et de la concurrence rowversion ;
- responsable des tests SQL réels, de la sécurité et de la documentation.

Le projet « Logiciel Pharmacie » possède actuellement :

- un POS connecté à Application Sales ;
- SQL Server 2025 Developer 17.0.1125.2 ;
- la base Pharmacy_Development ;
- le niveau de compatibilité 170 ;
- la migration InitialCatalogPersistenceV01 ;
- cinq tables catalog ;
- 283 tests validés ;
- aucune migration automatique au démarrage.

Ta mission est de rendre le Catalogue réellement persistant et de connecter le POS à ce Catalogue SQL.

Ne t’arrête pas après le plan. Exécute toutes les opérations autorisées.

---

## 2. MISSION

Implémenter :

1. les cas d’utilisation Application du Catalogue ;
2. les modèles de lecture liste et détail ;
3. un lecteur Catalogue SQL ;
4. la recherche par nom, présentation, référence interne et code-barres ;
5. la lecture exacte par ProductId et code ;
6. la création transactionnelle d’un produit ;
7. la modification de son identité et de ses paramètres locaux ;
8. la gestion des codes-barres ;
9. le changement transactionnel du prix avec historique ;
10. l’activation et la désactivation ;
11. la concurrence optimiste par rowversion ;
12. les erreurs Application structurées ;
13. une page Catalogue fonctionnelle dans Desktop ;
14. la connexion des recherches du POS à SQL Server ;
15. un seed manuel de données entièrement synthétiques ;
16. les tests unitaires et SQL réels ;
17. la documentation du Sprint 10.

Le panier Sales reste en mémoire.

Le stock reste synthétique et en mémoire.

Le paiement et l’impression restent simulés.

Aucune vente ne doit être enregistrée dans SQL Server.

Statut attendu :

IMPLEMENTED — SQL CATALOG VALIDATION PASS — MANUAL CATALOG REVIEW REQUIRED

---

## 3. SOURCES DE VÉRITÉ

Lire avant toute modification :

- README.md
- Directory.Build.props
- global.json
- .editorconfig
- .gitattributes
- .gitignore
- docs/governance/PROJECT_CONTEXT.md
- docs/governance/DECISIONS_REGISTER.md
- docs/governance/SCOPE.md
- docs/governance/CURRENT_STATE.md
- docs/governance/CODEX_WORKFLOW.md
- docs/templates/CODEX_PROMPT_TEMPLATE.md
- docs/templates/CODEX_REPORT_TEMPLATE.md
- docs/domain/SALES_CORE_V0.1.md
- docs/application/SALES_USE_CASES_V0.1.md
- docs/application/DESKTOP_SALES_INTEGRATION_V0.1.md
- docs/database/PERSISTENCE_FOUNDATION_V0.1.md
- docs/database/CATALOG_PHYSICAL_SCHEMA_V0.1.md
- docs/database/CATALOG_ERD_V0.1.mmd
- docs/reports/sprint-09/codex-report.md
- docs/reports/sprint-09/database-validation.md
- migration InitialCatalogPersistenceV01
- snapshot EF Core actuel.

Inspecter complètement :

- src/Pharmacy.Domain
- src/Pharmacy.Application
- src/Pharmacy.Infrastructure
- src/Pharmacy.Desktop
- tests/Pharmacy.UnitTests
- tests/Pharmacy.IntegrationTests
- database
- fichiers projet.

Consulter les cinq PDF de référence en lecture seule.

Ne modifier aucun PDF.

Priorité :

1. présent prompt ;
2. DECISIONS_REGISTER ;
3. CURRENT_STATE ;
4. SCOPE ;
5. Phase 2 et Phase 3 ;
6. schéma SQL réellement appliqué ;
7. code existant.

---

## 4. ÉTAT INITIAL ET PRÉREQUIS

Le Sprint 9 est annoncé comme validé et commité.

Ne suppose aucun hash.

Exécute :

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

État attendu :

- branche main ;
- working tree propre ;
- index vide ;
- aucun remote ;
- six projets ;
- au moins 283 tests ;
- zéro erreur ;
- zéro avertissement raisonnable.

Utilise PHARMACY_SQLSERVER_CONNECTION.

La connexion doit cibler Pharmacy_Development.

Ne montre jamais une connexion contenant un secret.

Vérifie les tables réelles :

- catalog.ProductBarcodes
- catalog.ProductCategories
- catalog.ProductLocalSettings
- catalog.ProductPriceHistories
- catalog.Products

La table catalog.ProductReferences n’existe pas.

Réutilise :

- Microsoft.EntityFrameworkCore.SqlServer 10.0.10 ;
- Microsoft.EntityFrameworkCore.Design 10.0.10 ;
- dotnet-ef 10.0.10.

Aucun nouveau package ne devrait être nécessaire.

---

## 5. PÉRIMÈTRE AUTORISÉ

Tu peux modifier ou créer des fichiers dans :

- Pharmacy.Application ;
- Pharmacy.Infrastructure ;
- Pharmacy.Desktop ;
- tests unitaires ;
- tests d’intégration ;
- database/dev ;
- database/verification ;
- README.md ;
- CURRENT_STATE.md ;
- DECISIONS_REGISTER.md.

Tu peux modifier Domain uniquement si un noyau Catalogue minimal est indispensable, justifié et testé.

Créer :

- docs/application/CATALOG_USE_CASES_V0.1.md
- docs/infrastructure/SQL_CATALOG_ADAPTERS_V0.1.md
- docs/ux/CATALOG_MANAGEMENT_UI_V0.1.md
- docs/reports/sprint-10/catalog-validation.md
- docs/reports/sprint-10/codex-report.md
- database/dev/seed_synthetic_catalog_v01.sql
- database/dev/clear_synthetic_catalog_v01.sql
- database/verification/verify_sprint10_catalog.sql

Une nouvelle migration est autorisée uniquement si un manque réel du schéma est démontré.

Nom autorisé :

CatalogPersistenceEnhancementsV01

Ne modifie jamais la migration Sprint 9 déjà validée.

---

## 6. HORS PÉRIMÈTRE

Ne développe pas :

- persistance du panier ;
- persistance des ventes ;
- lignes de vente ;
- paiements ;
- caisse ;
- stock réel ;
- mouvements de stock ;
- lots ;
- expiration ;
- FEFO ;
- fournisseurs ;
- achats ;
- réceptions ;
- clients ;
- crédit ;
- retours ;
- authentification ;
- reporting ;
- sauvegarde ;
- import ;
- périphériques réels ;
- API HTTP.

Ne crée aucune table hors du module Catalogue.

Ne crée aucun repository générique.

Ne crée aucune procédure stockée ou trigger.

Ne crée aucune donnée réelle ou clinique.

N’invente aucune TVA, marge ou règle pharmaceutique.

Ne fais pas de fallback silencieux vers le catalogue mémoire lorsque SQL est indisponible.

Ne pas utiliser :

- EnsureCreated ;
- Database.Migrate au démarrage ;
- seed automatique ;
- EnableSensitiveDataLogging ;
- DbContext dans Desktop ;
- entité EF exposée à Application ou Desktop ;
- .Result ;
- .Wait() ;
- Task.Run pour contourner l’asynchronisme.

Ne pas exécuter :

- git add ;
- commit ;
- push ;
- remote ;
- changement de branche ;
- reset ;
- stash ;
- rebase.

---

## 7. ARCHITECTURE

Respecter :

- Domain : invariants purs éventuels, sans EF Core ;
- Application : contrats, commandes, requêtes, modèles et erreurs ;
- Infrastructure : EF Core, transactions, mapping et erreurs SQL ;
- Desktop : composants et coordinateurs UI, aucun DbContext.

Utiliser un contexte court par opération avec :

IDbContextFactory<PharmacyDbContext>

ou le mécanisme équivalent déjà validé.

Créer des contrats spécifiques, par exemple :

- ICatalogProductReader
- ICatalogProductStore
- ICatalogCategoryReader
- ICatalogCategoryStore

Ne pas créer IRepository<T>.

Réutiliser IProductCatalogReader pour le POS.

Créer au minimum :

- CatalogProductListItem
- CatalogProductDetails
- CatalogCategoryView

Les collections doivent être en lecture seule.

Aucun modèle EF ne doit sortir d’Infrastructure.

Encoder les rowversions en Base64 dans les modèles Application lorsque nécessaire.

---

## 8. RÈGLES FONCTIONNELLES

Un produit est vendable dans le POS seulement si :

- Products.IsActive = true ;
- ProductLocalSettings.IsActive = true ;
- les paramètres locaux existent ;
- le prix courant est valide ;
- une référence locale existe.

La référence utilisée par Application vient de :

ProductLocalSettings.InternalCode

Un code-barres actif peut également être utilisé pour le scan.

Ne suppose pas l’existence de ProductReferences.

Le lecteur SQL doit rechercher sur :

- nom ;
- présentation ;
- InternalCode ;
- code-barres normalisé.

Il doit :

- utiliser AsNoTracking ;
- limiter les résultats ;
- éviter N+1 ;
- ne pas charger tout le catalogue en mémoire ;
- exclure les produits non vendables ;
- ne rien créer pour un code inconnu.

Création transactionnelle :

- Products ;
- ProductLocalSettings ;
- codes-barres éventuels ;
- premier historique de prix éventuel.

Aucun stock, mouvement ou lot.

Modification autorisée :

- nom ;
- présentation ;
- catégorie ;
- activation globale ;
- référence interne ;
- paramètres locaux ;
- activation locale.

Le prix doit être modifié uniquement par un cas d’utilisation dédié.

Changement de prix transactionnel :

1. charger avec rowversion ;
2. valider le prix ;
3. clôturer l’ancien historique si le modèle le prévoit ;
4. ajouter le nouvel historique ;
5. mettre à jour CurrentSalePrice ;
6. sauvegarder atomiquement.

Codes-barres :

- texte ;
- trim ;
- normalisation déterministe ;
- zéros initiaux conservés ;
- unicité ;
- seuls les codes actifs sont scannables.

Catégories :

- lister ;
- créer ;
- modifier ;
- activer/désactiver.

Une catégorie référencée ne doit pas être supprimée physiquement.

Erreurs minimales :

- catalog.product.not_found
- catalog.category.not_found
- catalog.reference.duplicate
- catalog.barcode.duplicate
- catalog.barcode.not_found
- catalog.price.invalid
- catalog.product.concurrency_conflict
- catalog.category.concurrency_conflict
- catalog.validation.failed
- catalog.database.unavailable

---

## 9. ADAPTATEURS SQL ET TRANSACTIONS

Créer SqlProductCatalogReader :

- implémentation du contrat POS ;
- projection SQL directe ;
- AsNoTracking ;
- recherche exacte ProductId, InternalCode et barcode ;
- produits vendables seulement ;
- CancellationToken ;
- aucun type EF exposé.

Créer un store Catalogue SQL spécifique pour :

- création ;
- identité ;
- paramètres locaux ;
- activation ;
- codes-barres ;
- prix et historique ;
- catégories.

Utiliser des transactions pour toutes les opérations multi-tables.

En cas d’échec :

- rollback ;
- aucune donnée partielle ;
- aucun historique orphelin.

Traduire les contraintes SQL à partir :

- des codes SQL Server ;
- des noms de contraintes stables.

Ne pas dépendre uniquement du message localisé.

Sur DbUpdateConcurrencyException :

- retourner une erreur Application ;
- ne pas écraser ;
- ne pas relancer automatiquement.

Créer un adaptateur temporaire clairement nommé pour la disponibilité synthétique :

- produit identifié depuis SQL ;
- quantité stockée uniquement en mémoire ;
- aucun ProductId codé en dur ;
- aucune table Stock.

---

## 10. DONNÉES SYNTHÉTIQUES

Les migrations restent vides.

Créer :

database/dev/seed_synthetic_catalog_v01.sql

Le script doit :

- refuser toute base autre que Pharmacy_Development ;
- utiliser une transaction ;
- être idempotent ;
- créer dix produits entièrement synthétiques ;
- créer des catégories synthétiques ;
- utiliser DEMO-000001 à DEMO-000010 ;
- utiliser les prix validés du prototype ;
- utiliser des barcodes Demo non réels ;
- ne créer aucune donnée clinique ;
- ne dépendre d’aucun Id fixe ;
- ne rien dupliquer au second passage.

Créer :

database/dev/clear_synthetic_catalog_v01.sql

Il doit :

- refuser une autre base ;
- supprimer uniquement les références DEMO-* ;
- respecter les clés étrangères ;
- ne supprimer aucune donnée non Demo ;
- ne supprimer aucune table.

Exécuter le seed deux fois et vérifier l’absence de duplication.

---

## 11. DESKTOP ET UX

Dans le composition root :

- remplacer le lecteur Catalogue mémoire par le lecteur SQL ;
- conserver SaleDraft en mémoire ;
- conserver stock, suspensions, paiement et impression en mémoire/simulés.

Si SQL est indisponible :

- le shell démarre ;
- Catalogue et POS affichent un état indisponible ;
- aucun fallback silencieux ;
- aucune exception brute ;
- Diagnostic affiche la panne ;
- aucune migration automatique.

Remplacer le placeholder Catalogue par une page fonctionnelle :

- recherche ;
- filtres actif/inactif ;
- filtre catégorie ;
- liste paginée ou limitée ;
- loading ;
- état vide ;
- erreur ;
- détail ;
- création ;
- modification ;
- activation/désactivation ;
- gestion des barcodes ;
- changement de prix ;
- historique en lecture seule ;
- concurrence compréhensible ;
- double soumission bloquée ;
- navigation clavier ;
- focus visible ;
- responsive.

Dans le POS, vérifier :

- recherche SQL ;
- scan par référence Demo ;
- scan par barcode Demo ;
- code inconnu ;
- ajout et fusion ;
- prix SQL ;
- produit désactivé absent ;
- prix modifié visible après rafraîchissement ;
- stock toujours synthétique ;
- vente toujours non persistée.

Mettre à jour Diagnostic sans afficher la connexion complète :

- SQL Server ;
- base ;
- connectivité ;
- migration ;
- nombre de produits Demo actifs ;
- stock synthétique ;
- ventes non persistées.

---

## 12. TESTS ET ACCEPTATION

Conserver tous les tests existants.

Ajouter des tests pour :

Architecture :

- Domain sans EF Core ;
- Application sans Infrastructure ;
- Desktop sans DbContext ;
- aucun modèle EF exposé ;
- aucun repository générique.

Lectures SQL :

- ProductId existant/absent ;
- nom ;
- présentation ;
- InternalCode ;
- barcode ;
- casse ;
- code exact ;
- limite ;
- produits inactifs exclus ;
- pas de N+1.

Création :

- multi-table ;
- référence dupliquée ;
- barcode dupliqué ;
- catégorie absente ;
- prix négatif ;
- rollback ;
- aucun stock ;
- historique initial.

Modification :

- identité ;
- paramètres locaux ;
- rowversion mise à jour ;
- conflit détecté ;
- état intact après conflit ;
- désactivation/réactivation.

Prix :

- prix courant ;
- historique ;
- transaction ;
- prix invalide ;
- concurrence ;
- aucun historique après échec.

Barcodes et catégories :

- ajout ;
- normalisation ;
- zéros initiaux ;
- unicité ;
- actif/inactif ;
- création/modification catégorie ;
- catégorie référencée protégée.

Seed :

- cible correcte ;
- dix produits ;
- deuxième exécution sans duplication ;
- aucune donnée réelle ;
- clear uniquement Demo ;
- aucun seed automatique.

Desktop :

- lecteur SQL enregistré ;
- ancien lecteur mémoire non utilisé par le POS ;
- SaleDraft et stock encore mémoire ;
- page Catalogue ;
- POS SQL ;
- panne SQL sûre ;
- aucun fallback ;
- Diagnostic sans secret ;
- UI Lab accessible.

SQL réel :

- base temporaire sécurisée ;
- migration ;
- seed ;
- contraintes ;
- rowversion ;
- transactions ;
- suppression de la base temporaire ;
- aucune base résiduelle ;
- aucun SQLite ou EF InMemory comme preuve.

Commandes obligatoires :

dotnet tool restore
dotnet restore Pharmacy.slnx
dotnet build Pharmacy.slnx -c Debug --no-restore
dotnet test Pharmacy.slnx -c Debug --no-build
dotnet build Pharmacy.slnx -c Release --no-restore
dotnet test Pharmacy.slnx -c Release --no-build

dotnet ef migrations list ...
dotnet ef migrations has-pending-model-changes ...

Exécuter aussi :

- vérification Sprint 9 ;
- seed deux fois ;
- vérification Sprint 10 ;
- tests SQL réels ;
- contrôle des bases temporaires.

Attendu :

- zéro erreur ;
- zéro avertissement raisonnable ;
- tous les tests réussis ;
- aucun test ignoré ;
- aucune migration en attente ;
- aucun secret ;
- aucune base temporaire résiduelle.

Lancer Desktop et contrôler techniquement Catalogue SQL, POS SQL, formulaires, Diagnostic, UI Lab et fermeture propre.

---

## 13. PLAN D’EXÉCUTION

1. vérifier le marqueur final ;
2. lire les sources ;
3. vérifier Git ;
4. baseline ;
5. vérifier SQL et schéma ;
6. exécuter vérification Sprint 9 ;
7. cartographier contrats et entités ;
8. confirmer InternalCode/barcode/prix ;
9. décider si Domain Catalog minimal est nécessaire ;
10. créer erreurs, modèles et contrats ;
11. créer requêtes et commandes ;
12. implémenter lecteur et store SQL ;
13. transactions, contraintes et concurrence ;
14. vérifier si une migration est nécessaire ;
15. créer une migration minimale seulement si nécessaire ;
16. créer seed/clear ;
17. exécuter seed deux fois ;
18. créer vérification Sprint 10 ;
19. connecter Desktop ;
20. créer la page Catalogue ;
21. adapter POS et Diagnostic ;
22. ajouter tests ;
23. tester panne SQL ;
24. Debug et Release ;
25. vérifier migrations ;
26. lancer Desktop ;
27. documenter ;
28. contrôler secrets, diff et bases ;
29. produire le rapport.

---

## 14. VALIDATION MANUELLE

Fournir les commandes de :

- build ;
- tests ;
- seed ;
- vérification SQL ;
- lancement Desktop.

Demander de vérifier dans SSMS :

- dix produits Demo ;
- catégories ;
- paramètres locaux ;
- barcodes ;
- prix ;
- historiques ;
- aucune duplication ;
- aucune donnée réelle ;
- rowversions ;
- aucune migration en attente.

Demander de vérifier dans Desktop :

- Catalogue ;
- recherche ;
- filtres ;
- détail ;
- création ;
- modification ;
- prix et historique ;
- barcode ;
- activation/désactivation ;
- erreurs et concurrence ;
- anti-double-soumission ;
- clavier, focus, responsive ;
- POS recherchant SQL ;
- scan référence/barcode ;
- prix SQL ;
- inactifs absents ;
- stock synthétique ;
- vente non persistée.

Tester également une panne SQL.

Demander sept captures :

1. SSMS avec produits Demo ;
2. liste Catalogue ;
3. détail produit ;
4. création/modification ;
5. POS utilisant SQL ;
6. Diagnostic ou panne SQL ;
7. script de vérification.

Ne pas demander de commit.

---

## 15. DOCUMENTATION, RAPPORT ET RESTRICTIONS GIT

Créer :

- docs/application/CATALOG_USE_CASES_V0.1.md
- docs/infrastructure/SQL_CATALOG_ADAPTERS_V0.1.md
- docs/ux/CATALOG_MANAGEMENT_UI_V0.1.md
- docs/reports/sprint-10/catalog-validation.md
- docs/reports/sprint-10/codex-report.md
- scripts seed, clear et vérification.

Mettre à jour :

- README.md ;
- CURRENT_STATE.md ;
- DECISIONS_REGISTER.md uniquement pour les nouvelles décisions.

Le rapport doit détailler :

- état initial ;
- schéma réel ;
- mapping ;
- architecture ;
- use cases ;
- lecteur/store SQL ;
- recherche ;
- création/modification ;
- prix/historique ;
- barcodes/catégories ;
- rowversion ;
- contraintes ;
- migration éventuelle ;
- seed ;
- page Catalogue ;
- POS SQL ;
- stock synthétique ;
- panne SQL ;
- sécurité ;
- packages ;
- tests ;
- Debug/Release ;
- migrations ;
- bases résiduelles ;
- lancement ;
- problèmes, écarts et risques ;
- validation manuelle ;
- état Git.

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
- NE PAS STASH, RESET OU RÉÉCRIRE L’HISTORIQUE.
- LAISSER LES MODIFICATIONS VISIBLES POUR REVUE.

FIN DU PROMPT SPRINT 10