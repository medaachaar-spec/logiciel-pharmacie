# Package documentaire Codex - Logiciel Pharmacie

Release Candidate pilote actuelle : `1.0.0-rc.1` (Sprint 18). Packaging Windows self-contained, installation SQL contrôlée et documentation pilote sont disponibles ; SQL Server Express réel et UAT manuelle restent des gates obligatoires. Voir `docs/reports/sprint-18/codex-report.md`.

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

Après le lancement, l’application ouvre l’accueil du shell V0.1. `Ventes`, `Catalogue`, `Stock` et `Achats et réceptions` sont fonctionnels ; quatre autres domaines restent provisoires. Catalogue, Stock, Purchasing, ventes, paiements, suspensions et sorties de stock utilisent SQL Server. Seuls le brouillon actif avant validation et l’impression restent locaux au Desktop.

Le POS conserve les incidents de code inconnu/disponibilité, la suspension et la reprise contrôlée de paniers, l’abandon du brouillon, la machine d’états du TPE ambigu et l’impression simulée. La suspension persiste son snapshot dans SQL sans toucher au stock ; après reprise, la validation persiste atomiquement vente, paiement, allocations FEFO, mouvements de sortie et clôture de la suspension. Tous les montants sont `decimal` et aucune donnée bancaire ou réelle n’est collectée. Le brouillon actif et le ticket restent non persistés.

Le Sprint 6 ajoute un noyau Sales V0.1 indépendant dans `Pharmacy.Domain` (`Money`, identifiants typés, quantité entière, snapshot produit, ligne immuable et agrégat `SaleDraft`) ainsi que trois contrats non implémentés dans `Pharmacy.Application`. Ce noyau n’est pas connecté au POS synthétique : il ne persiste rien, ne finalise aucune vente et ne traite ni stock, lot, paiement, caisse, audit ou impression. Sa conception est décrite dans `docs/domain/SALES_CORE_V0.1.md`.

Le Sprint 7 ajoute les cas d’utilisation Sales Application V0.1 et cinq adaptateurs temporaires sous `Pharmacy.Infrastructure.Sales.InMemory`. Les brouillons sont versionnés, les mutations utilisent une concurrence optimiste et la préparation de finalisation est idempotente par `OperationId`. Tout reste strictement local au processus, synthétique et sans écriture disque. `Prepared` ne signifie ni finalisée, payée, réservée ou persistée.

Le Sprint 8 connecte le parcours nominal du POS à la chaîne `Desktop → Application → Infrastructure en mémoire → Domain`. `SaleDraftView` devient la source du panier, des quantités et des totaux ; le coordinateur Desktop conserve la version optimiste, recharge après conflit sans rejouer l’action et restaure les suspensions dans de nouveaux brouillons Application. Paiement, TPE et impression restent des simulations Desktop ; aucune finalisation, persistance, réservation ou écriture de stock n’est réalisée. Les détails se trouvent dans `docs/ux/POS_APPLICATION_INTEGRATION_V0.1.md` et `docs/application/DESKTOP_SALES_INTEGRATION_V0.1.md`.

Le Sprint 9 ajoute `Persistence Foundation V0.1` dans Infrastructure avec EF Core SQL Server 10.0.10, un outil local `dotnet-ef`, un `PharmacyDbContext` design-time sécurisé et la migration `InitialCatalogPersistenceV01`. La base locale `Pharmacy_Development` contient uniquement cinq tables vides du schéma `catalog`, suivant le modèle Phase 3 (`ProductLocalSettings` plutôt que la cible indicative `ProductReferences`). La validation automatisée utilise une vraie instance SQL Server 2025 Developer ; la validation sur SQL Server 2025 Express reste obligatoire avant le pilote. Le Desktop et le POS demeurent connectés exclusivement aux adaptateurs Sales en mémoire.

Le Sprint 10 rend le Catalogue réellement persistant. La migration minimale `CatalogPersistenceEnhancementsV01` ajoute l’activation globale manquante à `catalog.Products`. Les recherches du POS utilisent désormais `SqlProductCatalogReader` pour le nom, la présentation, la référence interne et les barcodes actifs ; les prix viennent de SQL Server. La page `/module/catalog` gère produits, catégories, activation, barcodes, prix et historique avec concurrence `rowversion`.

Le Sprint 11 ajoute le moteur Inventory SQL V0.1 : six tables pour emplacements, lots, balances, mouvements et inventaires, plus les contrats/handlers Domain et Application. La page `/module/inventory` expose recherche, filtres, lots expirés, historique, ajustements et comptages. Le POS lit la disponibilité SQL et exclut les lots expirés. Depuis le Sprint 13, une vente finalisée décrémente réellement les balances par allocations FEFO.

Le Sprint 12 ajoute le schéma Purchasing SQL V0.1 avec fournisseurs, commandes, lignes, réceptions et lignes de réception. La page `/module/purchasing` gère fournisseurs, brouillons/confirmations et réceptions partielles, complètes ou directes. Seule la finalisation d’une réception crée lots, mouvements `PurchaseReceipt` et augmentations de balances dans une transaction atomique ; elle ne modifie jamais automatiquement le prix de vente et ne calcule aucune marge. La migration `PurchasingPersistenceV01`, les seeds et les scripts restent manuels.

Le Sprint 13 ajoute le schéma `sales` et la migration `SalesPersistenceV01`. `SqlSaleFinalizer` revalide produits, prix et stock, répartit les quantités par FEFO, exclut les lots expirés et commite en une transaction vente, lignes, allocations, paiement, mouvements `Sale`, balances et registre idempotent. Le POS affiche la référence persistée et un historique SQL minimal ; une panne SQL, un prix changé ou un conflit conserve le panier. L’impression demeure une simulation post-commit.

Le Sprint 15 ajoute l’authentification locale, le RBAC deny-by-default et l’audit SQL sécurisé. Aucun compte ou mot de passe n’est seedé : le premier propriétaire est créé depuis un bootstrap Desktop contrôlé et reçoit des codes de récupération affichés une seule fois. La session se verrouille après dix minutes ; cinq échecs verrouillent le compte cinq minutes ; les opérations sensibles exigent une réauthentification récente. Les procédures sont décrites dans `docs/security/OWNER_BOOTSTRAP_AND_RECOVERY_V0.1.md` et la revue manuelle dans `docs/reports/sprint-15/manual-validation.md`.

Le Sprint 17 ajoute la page `/module/operations` : sauvegardes SQL avec CHECKSUM/VERIFYONLY/SHA-256, copie externe protégée par un gate de chiffrement, restore-test isolé, imports CSV UTF-8 prévisualisés (Catalogue CreateOnly, stock initial, lots) et diagnostic du poste. Le scanner suit le contrat clavier HID ; le ticket passe par `IReceiptPrinter` seulement après le commit de vente ; le TPE reste externe et manuel. Aucun matériel physique n’est certifié par le simulateur. Les procédures se trouvent dans `docs/operations/`, `docs/import/` et `docs/hardware/`.

BRAND-01 ajoute l’identité configurable de la pharmacie. Le nom, la couleur principale et un logo PNG/JPEG/WEBP peuvent être modifiés dans Administration avec contrôle RBAC, audit et concurrence optimiste. Le nom et le logo apparaissent sur Connexion et dans la Sidebar ; les tokens CSS sont calculés au runtime. Le logo personnalisé est conservé dans SQL et vérifié par le restore-test officiel. Voir `docs/customization/PHARMACY_BRANDING_V0.1.md`.

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

Sauvegarde/restore-test Sprint 17 (dossiers configurés hors Git et droits minimaux pour le compte SQL Server) :

```powershell
$env:PHARMACY_BACKUP_LOCAL_DIRECTORY = '<dossier-absolu-dédié>'
pwsh -NoProfile -File .\scripts\Invoke-Sprint17Backup.ps1 `
  -ServerInstance <instance-explicite> -DatabaseName Pharmacy_Development -Type Full
pwsh -NoProfile -File .\scripts\Test-Sprint17Restore.ps1 `
  -ServerInstance <instance-explicite> -BackupFile '<fichier-bak-dans-la-racine-autorisée>'
```

Pour une copie externe, définir aussi `PHARMACY_BACKUP_EXTERNAL_DIRECTORY` et n’utiliser `PHARMACY_BACKUP_EXTERNAL_ENCRYPTED=true` qu’après vérification effective du chiffrement du volume. Aucun `.bak` ne doit être versionné.

Seed et vérification Sprint 10, exclusivement sur `Pharmacy_Development` :

```powershell
sqlcmd -S <instance-explicite> -d Pharmacy_Development -E -C -b -f 65001 `
  -i .\database\dev\seed_synthetic_catalog_v01.sql
sqlcmd -S <instance-explicite> -d Pharmacy_Development -E -C -b -f 65001 `
  -i .\database\verification\verify_sprint10_catalog.sql
```

Le seed manuel est transactionnel et idempotent. Le script `database/dev/clear_synthetic_catalog_v01.sql` supprime uniquement les références `DEMO-*` et leurs dépendances. `-C` reste une exception locale provisoire tant que le certificat SQL de développement n’est pas approuvé.

Migration, seed et vérification Sprint 11, exclusivement avec une connexion fournie hors dépôt et la base `Pharmacy_Development` :

```powershell
$env:PHARMACY_SQLSERVER_CONNECTION = '<connexion locale Pharmacy_Development>'
dotnet tool run dotnet-ef database update `
  --project .\src\Pharmacy.Infrastructure `
  --startup-project .\src\Pharmacy.Infrastructure
sqlcmd -S <instance-explicite> -d Pharmacy_Development -E -C -b -f 65001 `
  -i .\database\dev\seed_synthetic_inventory_v01.sql
sqlcmd -S <instance-explicite> -d Pharmacy_Development -E -C -b -f 65001 `
  -i .\database\verification\verify_sprint11_inventory.sql
```

Le clear Inventory est conservateur et ne supprime que les artefacts portant les identifiants Demo canoniques. Ne jamais utiliser ces scripts sur une autre base.

Migration, seed et vérification Sprint 12 :

```powershell
$env:PHARMACY_SQLSERVER_CONNECTION = '<connexion locale Pharmacy_Development>'
dotnet tool run dotnet-ef database update `
  --project .\src\Pharmacy.Infrastructure `
  --startup-project .\src\Pharmacy.Infrastructure
sqlcmd -S <instance-explicite> -d Pharmacy_Development -E -C -b -f 65001 `
  -i .\database\dev\seed_synthetic_purchasing_v01.sql
sqlcmd -S <instance-explicite> -d Pharmacy_Development -E -C -b -f 65001 `
  -i .\database\verification\verify_sprint12_purchasing.sql
```

Le clear Purchasing retire uniquement les fournisseurs/commandes/réception `DEMO-S12-*` et inverse le mouvement de seed associé. Il refuse une base autre que `Pharmacy_Development` et ne supprime aucun produit Catalogue ni stock non créé par ce seed.

Migration et vérification Sprint 13 :

```powershell
$env:PHARMACY_SQLSERVER_CONNECTION = '<connexion locale Pharmacy_Development>'
dotnet tool run dotnet-ef database update `
  --project .\src\Pharmacy.Infrastructure `
  --startup-project .\src\Pharmacy.Infrastructure
sqlcmd -S <instance-explicite> -d Pharmacy_Development -E -C -b -f 65001 `
  -i .\database\verification\verify_sprint13_sales.sql
```

La migration ne crée aucune vente. La vérification est en lecture seule et contrôle schéma, contraintes, cohérence des ventes présentes, mouvements `Sale` et balances.

Migration et vérification Sprint 15 :

```powershell
$env:PHARMACY_SQLSERVER_CONNECTION = '<connexion locale contrôlée>'
dotnet tool run dotnet-ef database update `
  --project .\src\Pharmacy.Infrastructure `
  --startup-project .\src\Pharmacy.Infrastructure
sqlcmd -S <instance-explicite> -d <base-développement> -E -C -b -f 65001 `
  -i .\database\verification\verify_sprint15_security_rbac_audit.sql
```

Le script de vérification Sprint 15 est en lecture seule. Ne jamais placer le mot de passe propriétaire ou les codes de récupération dans une commande, un fichier `.env`, un script SQL ou le dépôt.

En configuration Debug, le groupe `Développement` donne également accès à :

- `Diagnostic technique` sur `/diagnostic` : smoke screen conservé du Sprint 1 ;
- `UI Lab` sur `/ui-lab` : fondations visuelles code-first V0.1 et états des composants.

Le sélecteur d’aperçu des accès est une prévisualisation locale et non persistante. Ses profils ne sont ni des rôles métier finaux ni un mécanisme de sécurité. Le POS, l’UI Lab et le shell V0.1 restent des prototypes soumis à revue visuelle humaine. La validation Sales Application Sprint 7 se trouve dans `docs/reports/sprint-07/sales-application-validation.md`.
