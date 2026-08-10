# SPRINT 16 — Reporting SQL, tableaux de bord, alertes descriptives et exports sécurisés

## 1. Rôle
Tu agis comme ingénieur logiciel senior, architecte .NET/SQL Server, spécialiste reporting, analyste métier, QA, sécurité et UX pour le projet **Logiciel Pharmacie**.

Tu dois implémenter le Sprint 16 dans le dépôt existant sans casser les Sprints 0 à 15.

Tu ne dois faire aucune opération Git d’écriture : le propriétaire du projet fera désormais les commits et pushes manuellement.

## 2. Mission
Implémenter une couche de reporting opérationnel fiable et descriptive couvrant :

- tableau de bord exécutif ;
- ventes par période ;
- chiffre d’affaires descriptif ;
- modes de paiement ;
- produits vendus ;
- stock vendable ;
- ruptures ;
- stock faible selon seuil configurable ;
- lots proches de péremption selon horizon configurable ;
- lots expirés ;
- achats et réceptions ;
- fournisseurs ;
- inventaires et ajustements ;
- crédits clients ;
- règlements de crédit ;
- retours et remboursements ;
- journal cash minimal ;
- vues/requêtes SQL de reporting ;
- filtres par période ;
- alertes descriptives ;
- exports sécurisés ;
- protection via RBAC `reports.view` et permissions associées ;
- tests unitaires, intégration SQL réelle, architecture, sécurité et performance raisonnable.

Aucun KPI métier, seuil, classement ou règle de conformité ne doit être inventé.

## 3. Sources de vérité
Respecter en priorité :
1. ADR validés du projet ;
2. dossier Phase 2 — Modélisation fonctionnelle ;
3. dossier Phase 3 — Modèle de données ;
4. dossier Phase 4 — UX/UI ;
5. dossier Phase 5 — Code First / Vibe Coding ;
6. architecture réellement présente dans le dépôt ;
7. schémas SQL et rapports Sprints 9 à 15 ;
8. conventions UI existantes ;
9. règles validées dans ce prompt.

Ne jamais remplacer une information absente par une supposition métier silencieuse.

## 4. État attendu du dépôt avant démarrage
Branche : `main`.

Le HEAD doit être le commit manuel du Sprint 15 :
`feat: add local authentication RBAC and secure audit`

Le hash exact peut varier : vérifier le message du dernier commit.

Le working tree doit être propre avant ajout du prompt Sprint 16, à l’exception autorisée de :
`docs/prompts/Sprint_16_Reporting_Dashboards_Alertes_Exports_SQL.md`

Avant toute modification :
- `git status --short`
- `git log --oneline -n 8`
- vérifier `main`
- vérifier que le dernier commit correspond au Sprint 15
- vérifier index vide
- inspecter les modules existants
- inspecter les tables et migrations jusqu’au Sprint 15
- inspecter RBAC/audit
- inspecter styles/composants UI.

Si seul le prompt Sprint 16 est non suivi, continuer.

## 5. Politique Git
NE PAS exécuter :
- `git add`
- `git commit`
- `git push`
- `git remote add`
- création/changement de branche
- `git reset --hard`
- stash destructif
- checkout/restauration destructive.

À la fin, seulement rapporter :
- `git status --short`
- branche
- HEAD
- index vide
- aucun commit/push/remote.

## 6. Principes métier VALIDÉS

### 6.1 Reporting descriptif uniquement
Le reporting décrit les données réellement persistées.
Ne pas :
- qualifier automatiquement une performance de bonne/mauvaise ;
- inventer objectif/seuil ;
- créer meilleur/pire ;
- recommander acte clinique ;
- produire une prédiction métier non validée.

### 6.2 Argent
Les montants proviennent des transactions historiques.
Ne jamais utiliser le prix catalogue actuel pour recalculer une vente passée.

### 6.3 Stock
Distinguer :
- total ;
- vendable ;
- expiré ;
- non revendable si disponible ;
- lots.
Un lot expiré n’est jamais stock vendable.

### 6.4 Stock faible
Aucun seuil universel.
Le seuil doit être configurable.
Seuil absent/désactivé => aucune fausse alerte.

### 6.5 Péremption proche
Horizon configurable.
Ne pas hardcoder 30/60/90 jours comme règle métier.

### 6.6 Crédit
Le solde client doit être dérivé du journal financier immuable Sprint 14.

### 6.7 Retours
Distinguer ventes, retours, remboursements.
Tout net affiché doit avoir une formule documentée.

### 6.8 Cash
Pas d’ouverture/fermeture obligatoire.
Reporter uniquement les écritures cash réelles existantes.

### 6.9 Sécurité
Reporting protégé par `reports.view` ou permission équivalente existante.

## 7. Périmètre fonctionnel

### 7.1 Dashboard
Sur période sélectionnée, afficher au minimum :
- montant ventes finalisées ;
- nombre ventes ;
- retours/remboursements ;
- net si défini ;
- répartition paiements ;
- créances ouvertes ;
- règlements crédit ;
- stock vendable actuel ;
- références en rupture ;
- références sous seuil configuré ;
- lots expirés ;
- lots proches péremption selon horizon ;
- achats/réceptions.

### 7.2 Ventes
Filtres date début/fin, mode paiement si pertinent, recherche produit si raisonnable.
Afficher ventes, lignes, quantités, prix historiques, crédit, retours, remboursement.

### 7.3 Paiements
Séparer clairement espèces, carte/TPE, crédit et règlements ultérieurs.

### 7.4 Produits vendus
Afficher référence, désignation, quantité vendue, montant, quantité retournée, quantité nette si définie.
Tri autorisé, mais pas de label “best/worst”.

### 7.5 Stock
Afficher produit, total, vendable, expiré, seuil local, statut descriptif.

### 7.6 Péremptions
Afficher produit, lot, date, quantité, statut, jours restants si calculé, filtre horizon configurable.

### 7.7 Achats
Afficher fournisseurs, commandes, réceptions, quantités commandées/reçues/restantes, montants historiques si disponibles.

### 7.8 Inventaires et ajustements
Afficher sessions, état, dates, écarts, ajustements, références mouvement, acteur si fiable.

### 7.9 Clients et crédit
Afficher crédit autorisé, créance actuelle, crédits générés, règlements, historique financier.

### 7.10 Retours/remboursements
Afficher vente origine, retour, produit, quantité, remboursement, restock status, date, acteur.

## 8. Couche SQL Reporting
Créer une couche dédiée, de préférence schéma `reporting` si cohérent.

Favoriser vues SQL/read models/AsNoTracking.
Aucune mutation.

Vues possibles à adapter :
- `reporting.vw_ExecutiveSummary`
- `reporting.vw_SalesSummary`
- `reporting.vw_ProductSales`
- `reporting.vw_StockSummary`
- `reporting.vw_ExpiryAlerts`
- `reporting.vw_PurchasingSummary`
- `reporting.vw_InventoryAdjustments`
- `reporting.vw_CustomerCredit`
- `reporting.vw_ReturnsSummary`
- `reporting.vw_CashMovements`

Créer seulement le minimum utile.
Documenter granularité de chaque vue.
Éviter doublons par jointure.

## 9. Migration
Si nécessaire :
`ReportingViewsAndAlertSettingsV01`

Créer une table de paramètres uniquement si aucune configuration existante ne convient.

## 10. Alertes
Types :
- rupture ;
- sous seuil configuré ;
- lot expiré ;
- lot proche péremption.

Pas d’email/SMS/push.
Pas de recommandation clinique.

## 11. Configuration
Prévoir :
- activation/désactivation stock faible ;
- seuil par produit si adapté ;
- horizon péremption proche.

Toute valeur initiale doit être modifiable et documentée comme paramètre, pas règle métier.

## 12. Exports sécurisés

### CSV requis
- UTF-8 ;
- en-têtes ;
- dates explicites ;
- montants stables ;
- période/filtres repris ;
- pas de données sensibles ;
- défense contre CSV Formula Injection pour cellules commençant par `=`, `+`, `-`, `@`.

### XLSX
Seulement si capacité déjà présente sans nouvelle dépendance risquée.
Sinon ne pas ajouter de package externe : livrer CSV sécurisé et documenter XLSX comme futur.

### Autorisation
Vérifier permission côté backend.

### Interdictions
Ne jamais exporter :
- PasswordHash ;
- recovery codes ;
- secrets ;
- données carte ;
- PII inutile.

## 13. Architecture
Respecter Domain/Application/Infrastructure/Desktop/tests.
Créer module logique `Reporting`.
Préférer query services Application + read models + Infrastructure SQL dédiée.
Pas de dépendance Domain inutile.

## 14. UX/UI
Respecter strictement le design system existant.

Ajouter navigation `Reporting` ou `Tableau de bord`.

Organisation recommandée :
1. Vue générale
2. Ventes
3. Stock & péremptions
4. Achats
5. Clients & crédit
6. Retours & cash

Peut être condensé si nécessaire.

États :
- loading ;
- data ;
- empty ;
- invalid filter ;
- denied ;
- SQL unavailable ;
- degraded ;
- export in progress ;
- success/error.

Responsive :
- 1100×700
- 1366×768
- 1920×1080

## 15. Visualisations
Utiliser composants existants.
Pas de nouvelle librairie de charting sans nécessité.

Graphiques simples possibles :
- ventes dans le temps ;
- paiements ;
- achats/réceptions ;
- créances/règlements.

Tableaux prioritaires.
Chaque graphique : titre, période, unités, état vide.

## 16. Performance
Éviter N+1.
Filtres dates au SQL.
Pagination/limitation des grosses listes.
Nouveaux index uniquement justifiés.
Pas d’optimisation prématurée complexe.

## 17. RBAC
Au minimum :
- `reports.view`
- permission export distincte si propre à l’architecture, sinon documenter que `reports.view` couvre export V0.1.

Deny-by-default.
Tester UI + backend.

## 18. Audit
Auditer :
- export généré ;
- changement seuil/horizon.
Ne pas auditer toutes les simples lectures sauf règle contraire.

## 19. Plan d’exécution
1. Audit dépôt/SQL.
2. Lire docs.
3. Inventorier tables.
4. Définir métriques/granularité.
5. Documenter formules.
6. Concevoir read models.
7. Concevoir vues SQL.
8. Migration/script si nécessaire.
9. Services Reporting.
10. Paramètres alertes.
11. RBAC.
12. Dashboard.
13. Pages détail.
14. CSV sécurisé.
15. XLSX seulement si support existant.
16. Tests unitaires.
17. Tests SQL réels.
18. Tests RBAC.
19. Tests CSV injection.
20. Tests performance simples.
21. Build/test Debug.
22. Build/test Release.
23. Smoke avec SQL.
24. Smoke sans SQL.
25. Pending model changes.
26. Scripts validation.
27. Scan secrets/PII.
28. Documentation.
29. Rapport final.

## 20. Tests d’acceptation

### Calculs
Tester :
- ventes ;
- nombre ventes ;
- quantité ;
- retours ;
- remboursements ;
- net ;
- paiements ;
- créances ;
- règlements ;
- stock vendable ;
- rupture ;
- expiration ;
- achats/réceptions.

Utiliser datasets synthétiques déterministes.

### Anti-doublons
Tester vente avec :
- plusieurs paiements ;
- plusieurs lignes ;
- plusieurs allocations ;
- retours multiples ;
- crédit + paiement.
Aucun montant multiplié artificiellement.

### Filtres
Jour, plage, bornes, vide, date invalide.

### Alertes
Seuil absent/présent, rupture, sous seuil, expiré, proche péremption, horizon modifié.

### Exports
CSV nominal, UTF-8, accents, arabe si présent, neutralisation `= + - @`, RBAC, absence données sensibles.

### SQL réel
SQL Server Developer isolé selon pattern dépôt.

### Régression
Tous tests Sprints 0–15 restent PASS.

### Build
Debug/Release : 0 erreur, 0 warning.

## 21. Validation manuelle à préparer
Créer guide pour :
1. Dashboard ;
2. changement période ;
3. comparer ventes avec historique ;
4. paiements ;
5. retours/remboursements ;
6. stock vendable ;
7. rupture ;
8. seuil configurable ;
9. lot expiré ;
10. horizon péremption ;
11. achats ;
12. créance client ;
13. règlements ;
14. cash ;
15. export CSV ;
16. caractères spéciaux ;
17. utilisateur sans `reports.view` ;
18. panne SQL ;
19. redémarrage ;
20. responsive.

Demander captures Dashboard, Ventes, Stock/péremption, Achats, Crédit, export et denied.

## 22. Documentation attendue
Créer/mettre à jour :
- `docs/reports/sprint-16/codex-report.md`
- `docs/reports/sprint-16/manual-validation.md`
- `docs/database/REPORTING_SQL_V0.1.md`
- `docs/reporting/REPORTING_DEFINITIONS_V0.1.md`
- `docs/reporting/ALERT_SETTINGS_V0.1.md`
- `docs/reporting/EXPORT_SECURITY_V0.1.md`

Documenter chaque métrique : nom, définition, source, granularité, formule, exclusions, période.

## 23. Validation SQL
Créer :
`database/verification/verify_sprint16_reporting.sql`

Vérifier objets, principaux calculs, doublons, paramètres alertes, index/contraintes pertinents.

## 24. Rapport final
Succès :
`IMPLEMENTED — SQL REPORTING/ALERTS/EXPORT VALIDATION PASS — MANUAL REPORTING REVIEW REQUIRED`

Blocage :
`BLOCKED — SPRINT 16 IMPLEMENTATION REQUIRES MANUAL DECISION`

Échec :
`FAILED — SPRINT 16 VALIDATION FAILED`

Le rapport doit inclure résumé, objets SQL, métriques, alertes, exports, RBAC, tests Debug/Release, SQL réel, performance, pending model changes, smoke Desktop, scan secrets/PII, Git status lecture seule, limites et guide manuel.

FIN DU PROMPT SPRINT 16
