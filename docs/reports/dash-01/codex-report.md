# Rapport DASH-01 — Dashboard opérationnel Manager / Vendeur

## Analyse initiale

| Dashboard item | Data source | Permission | Existing/new projection |
|---|---|---|---|
| Ventes et CA du jour | `sales.Sales`, convention Reporting | `reports.view` | Nouvelle projection Dashboard agrégée |
| Tendance 7/30 jours | `sales.Sales` | `reports.view` | Nouvelle série quotidienne agrégée |
| Top produits | `reporting.vw_ProductSales` | `reports.view` | Nouvelle projection limitée Top 10 |
| Crédit ouvert | `reporting.vw_CustomerCredit` | `customers.manage_credit` | Nouvelle projection granulaire |
| Stock faible | `reporting.vw_StockSummary` + `ReportSettings` | `inventory.view` | Nouvelle projection granulaire |
| Péremptions | `reporting.vw_ExpiryAlerts` + horizon configuré | `inventory.view` | Nouvelle projection granulaire |
| Ventes par employé | `IEmployeeActivityReader` EMP-02 | `security.view_employee_activity` | Projection existante réutilisée |
| Mes ventes | `IEmployeeActivityReader` EMP-02 | `sales.view_history` | Isolation personnelle existante réutilisée |
| Nouvelle vente | Route POS existante | `sales.create` | Action existante, visibilité recomposée |
| Session | Session de sécurité courante | Session authentifiée | Présentation locale sans requête métier |

Le projet ne contenait aucune librairie graphique à réutiliser. Une représentation SVG/CSS accessible a donc été retenue, sans package supplémentaire et sans introduire de moteur de graphiques.

## Architecture DASH-01

Le même composant Accueil sert les expériences Manager et Vendeur. `DashboardCoordinator` calcule les capacités effectives, remet chaque projection à zéro avant un chargement, puis appelle uniquement les sources autorisées. Il orchestre les sections indépendantes en parallèle et conserve une erreur courte par section.

`IDashboardService` sépare les lectures sensibles en trois contrats : ventes globales, crédit ouvert et alertes inventaire. `SqlDashboardService` impose respectivement `reports.view`, `customers.manage_credit` et `inventory.view` avant toute requête SQL. Les ventes personnelles et la comparaison des employés restent dans EMP-02, avec les contrôles backend déjà validés.

Les graphiques sont des composants de présentation sans calcul métier : SVG responsive pour la tendance, barres CSS horizontales pour les classements et liste opérationnelle pour les péremptions. Les permissions effectives, y compris les overrides, déterminent à la fois les appels et la composition visuelle.

## Dashboard Manager

Selon ses permissions effectives, le Manager voit :

- cartes **Ventes**, **CA**, **Crédit clients** et **Stock faible** ;
- sélecteur compact **7 jours / 30 jours** ;
- courbe quotidienne d'évolution des ventes ;
- classement horizontal des produits par quantité vendue, avec montant ;
- liste des lots expirés, très proches ou dans l'horizon configuré ;
- comparaison des ventes par employé, Managers vendeurs inclus ;
- durée de session et actions rapides autorisées ;
- bouton explicite **Actualiser**.

Un Manager privé d'une permission par override perd uniquement les blocs concernés. La page et les autres projections continuent de fonctionner.

## Dashboard Vendeur

Le Vendeur standard voit une salutation personnalisée, le bouton principal **Nouvelle vente** uniquement avec `sales.create`, ses ventes du jour, son montant vendu aujourd'hui, sa session en cours et une liste courte de ses ventes récentes. Il ne reçoit ni KPI global, ni comparaison entre employés, ni donnée d'un autre vendeur. Les alertes additionnelles n'apparaissent que si une permission effective les autorise.

## Graphiques

| Type | Source | Période | Métrique |
|---|---|---|---|
| Courbe SVG | `sales.Sales` agrégé par jour | 7 ou 30 jours | Montant brut quotidien et nombre de ventes accessible |
| Barres horizontales CSS | `reporting.vw_ProductSales` | 7 ou 30 jours | Quantité vendue, montant secondaire |
| Barres horizontales CSS | EMP-02 `EmployeeSalesSummary` | 7 ou 30 jours | Montant vendu, nombre de ventes secondaire |
| Liste d'alertes | `reporting.vw_ExpiryAlerts` | Horizon `ReportSettings` | Jours restants, lot et quantité |

Chaque visualisation possède un titre, des labels et des valeurs textuelles. La courbe fournit aussi une table destinée aux technologies d'assistance. Les statuts de péremption sont écrits explicitement et ne reposent pas uniquement sur la couleur.

## Permissions

| Widget | Permission | Manager | Vendeur |
|---|---|---|---|
| Ventes/CA globaux | `reports.view` | Visible si effective | Caché par défaut |
| Tendance et Top produits | `reports.view` | Visible si effective | Caché par défaut |
| Crédit clients | `customers.manage_credit` | Visible si effective | Caché par défaut |
| Stock et péremptions | `inventory.view` | Visible si effective | Visible uniquement si accordée |
| Comparaison employés | `security.view_employee_activity` | Visible si effective | Cachée par défaut et refus backend |
| Mes ventes | `sales.view_history` | Selon droits | Uniquement ses propres ventes |
| Nouvelle vente | `sales.create` | Visible si effective | Visible si effective, sinon absente |

## États loading / empty / error

Le chargement initial utilise un squelette cohérent pour la page, sans accumulation de grands spinners. Les doubles chargements sont bloqués. Les états vides sont formulés en langage métier : aucune vente aujourd'hui, aucune alerte de péremption, aucun produit en stock faible ou aucune vente récente.

Chaque refresh efface d'abord les anciennes valeurs. Une panne Reporting ne laisse donc jamais un KPI stale paraître actuel. Une panne EMP-02 efface la comparaison employé, affiche un message court dans cette section et laisse les projections valides disponibles.

## Performance

Les ventes du jour et la tendance utilisent des agrégations SQL directes. Aucune jointure avec `SaleLines`, `Payments` ou `SaleLineAllocations` n'est utilisée pour les KPI de vente. Le Top produits est agrégé et limité dans SQL ; les alertes stock/crédit/péremption lisent les vues Reporting existantes. EMP-02 fournit toutes les synthèses employés en une lecture : aucune requête par produit ou par employé n'est effectuée.

Les tests structuraux interdisent la matérialisation des ventes complètes et le motif de boucle SQL évident. Le dataset SQL de DASH004/DASH011 contient plusieurs paiements et allocations et prouve l'absence de multiplication.

## Fichiers modifiés

- `src/Pharmacy.Application/Dashboard/Contracts/IDashboardService.cs`
- `src/Pharmacy.Application/Dashboard/Errors/DashboardErrors.cs`
- `src/Pharmacy.Application/Dashboard/Models/DashboardModels.cs`
- `src/Pharmacy.Infrastructure/Reporting/SqlDashboardService.cs`
- `src/Pharmacy.Infrastructure/Reporting/ReportingServiceCollectionExtensions.cs`
- `src/Pharmacy.Desktop/Components/Dashboard/DashboardCoordinator.cs`
- `src/Pharmacy.Desktop/Components/Pages/Home.razor`
- `src/Pharmacy.Desktop/Components/Pages/HomePresentationModel.cs`
- `src/Pharmacy.Desktop/SalesServiceCollectionExtensions.cs`
- `src/Pharmacy.Desktop/_Imports.razor`
- `src/Pharmacy.Desktop/wwwroot/css/dashboard.css`
- `src/Pharmacy.Desktop/wwwroot/css/app.css`
- `tests/Pharmacy.IntegrationTests/DashboardTests.cs`
- `tests/Pharmacy.IntegrationTests/AppShellTests.cs`
- `tests/Pharmacy.IntegrationTests/CustomUxCleanupTests.cs`
- `tests/Pharmacy.IntegrationTests/Pharmacy.IntegrationTests.csproj`
- `docs/ux/OPERATIONAL_DASHBOARD_V0.1.md`
- `docs/reports/dash-01/codex-report.md`

## Tests DASH

| Test | Résultat | Preuve |
|---|---|---|
| DASH001 | PASS | KPI globaux Manager chargés |
| DASH002 | PASS | 3 ventes aujourd'hui |
| DASH003 | PASS | 500 MAD, identique à Reporting |
| DASH004 | PASS | lignes/paiements/allocations sans multiplication |
| DASH005 | PASS | crédit ouvert exact : 300 MAD |
| DASH006 | PASS | 2 produits en stock faible |
| DASH007 | PASS | 7 points quotidiens ordonnés |
| DASH008 | PASS | 30 points quotidiens ordonnés |
| DASH009 | PASS | jours vides complétés à zéro |
| DASH010 | PASS | classement quantité exact |
| DASH011 | PASS | allocations FEFO sans multiplication |
| DASH012 | PASS | horizon configuré respecté |
| DASH013 | PASS | expiré / très proche / horizon distingués |
| DASH014 | PASS | synthèse employé exacte |
| DASH015 | PASS | Manager vendeur inclus |
| DASH016 | PASS | permission EMP requise côté backend |
| DASH017 | PASS | vendeur refusé pour employés et KPI globaux |
| DASH018 | PASS | 2 ventes personnelles aujourd'hui |
| DASH019 | PASS | 250 MAD personnels aujourd'hui |
| DASH020 | PASS | autre vendeur inaccessible |
| DASH021 | PASS | ventes récentes limitées à l'utilisateur |
| DASH022 | PASS | CTA conditionné par `sales.create` |
| DASH023 | PASS | modules non autorisés non rendus |
| DASH024 | PASS | sources non autorisées non appelées |
| DASH025 | PASS | override Manager recompose la page |
| DASH026 | PASS | libellé « Durée de session » conforme |
| DASH027 | PASS | chargement et double appel contrôlés |
| DASH028 | PASS | panne Reporting sans KPI stale |
| DASH029 | PASS | panne EMP sans comparaison stale |
| DASH030 | PASS | états vides métier |
| DASH031 | PASS | graphiques fondés sur les tokens |
| DASH032 | PASS | noms produits longs maîtrisés |
| DASH033 | PASS | agrégations sans N+1 évident |
| DASH034 | PASS | jargon CUSTOM interdit absent |
| DASH035 | PASS | personnalisation BRAND runtime conservée |

Commande ciblée Debug : 35/35 PASS, 0 échec, 0 ignoré. Les fixtures créent une base SQL dédiée puis la suppriment.

## EMP / CUSTOM / BRAND / SEC / RPT regression

| Gate ciblé | Résultat |
|---|---|
| EMP001–EMP065 | 65/65 PASS |
| CUSTOM / BRAND / AppShell / Administration | 52/52 PASS |
| SEC | 45/45 PASS |
| RPT | 43/43 PASS |
| Sales / Returns / Credit / Inventory | 53/53 PASS |

La suite globale confirme également l'intégralité des régressions à 100 %.

## Migration / EF

- Migration added: **NO**
- Migration count: **12**
- Pending model changes: **NONE** (`dotnet ef migrations has-pending-model-changes`)

## Tests globaux

- Debug build: **PASS**, 0 warning, 0 error
- Release build: **PASS**, 0 warning, 0 error
- Debug tests: **776/776 PASS**, 0 failed, 0 skipped
- Release tests: **776/776 PASS**, 0 failed, 0 skipped
- E2E: **PASS**, 1 scénario avec assertion interne **27/27 checkpoints**

## SQL

- Temporary DB: bases DASH, gates et E2E créées uniquement sur l'instance de test, puis supprimées ; résiduel = 0.
- Pilot mutation: **NONE** ; `./PHARMACY / Pharmacy` reste à **9 migrations**.
- Read-only proof: les tests DASH comparent les comptes de lignes métier avant Manager, Vendeur et refresh, puis après ; tous sont identiques. Les lectures Dashboard n'écrivent aucun événement de session.
- Sauvegardes de test résiduelles : 0 ; dossier dédié supprimé après vérification qu'il était vide.
- Processus `Pharmacy.Desktop` résiduel : 0.

## Git

- branch: `customization/dashboard`
- HEAD: `ef7a608408ca6bb896fefacae51d007385d61058`
- status: uniquement les modifications et nouveaux fichiers DASH-01 non stagés attendus
- diff check: PASS
- index: vide
- Aucun commit, push, merge, rebase, tag ou changement de branche.

DASH-01 OPERATIONAL DASHBOARD — GATE PASS
