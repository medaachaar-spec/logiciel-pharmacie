# Sprint 16 — Rapport Codex

Statut : **IMPLEMENTED — SQL REPORTING/ALERTS/EXPORT VALIDATION PASS — MANUAL REPORTING REVIEW REQUIRED**

## Résumé

Le module Reporting fournit un tableau de bord descriptif, neuf familles d’exports CSV, des rapports SQL détaillés et deux alertes configurables sans seuil implicite. Les calculs utilisent les données persistées existantes et n’écrivent jamais dans les tables métier.

## Objets et architecture

- Application : contrats, modèles, période UTC, erreurs et neutralisation CSV.
- Infrastructure : `SqlReportingService`, agrégations SQL, RBAC/audit, migration `ReportingViewsAndAlertSettingsV01`.
- SQL : schéma `reporting`, `ReportSettings`, 11 vues granulaires et contraintes singleton/horizon.
- Desktop : route `/module/reporting`, filtres de période, synthèse, alertes, réglages, tables et exports dans Documents.
- Sécurité : `reports.view`, `reports.export`, `reports.manage_settings`; réglages sensibles avec réauthentification récente.

## Définitions et anti-doublons

Le brut, les remboursements et le net documenté sont affichés séparément. Les retours sont préagrégés avant jointure avec ventes/lignes ; paiements et allocations ne participent pas à ces jointures. Stock et crédit sont identifiés comme états courants. Les détails sont plafonnés mais les synthèses portent sur l’ensemble des lignes SQL filtrées.

## Alertes et exports

Le seuil bas vient exclusivement de `ProductLocalSettings.MinimumStock`. L’horizon de péremption est nullable, désactivé initialement et obligatoire à l’activation. Les changements sont protégés par `rowversion` et audités.

Les CSV sont UTF-8 BOM, séparés par point-virgule, utilisent des dates ISO/des décimaux invariants et neutralisent `= + - @`. Téléphones, notes, coordonnées fournisseur, secrets et données carte sont exclus. Aucun package XLSX n’a été ajouté.

## Validation automatisée

- Debug : build réussi, 0 avertissement, 0 erreur ; 364/364 tests PASS.
- Release : build réussi, 0 avertissement, 0 erreur ; 364/364 tests PASS.
- SQL réel : dataset déterministe avec deux produits/lignes/paiements/allocations/retours, crédit, caisse, ajustement, commande et réception ; calculs, anti-doublons, filtres, alertes, RBAC, CSV et audit PASS sur base temporaire isolée.
- Base locale : migration appliquée à `Pharmacy_Development`; script `verify_sprint16_reporting.sql` PASS ; 1 table, 11 vues et 3 permissions Reporting confirmées.
- Nettoyage : aucune base `Pharmacy_Sprint*_Test_*` et aucun processus `Pharmacy.Desktop` résiduel.
- Pending model changes : aucun ; huit migrations appliquées dans `Pharmacy_Development` jusqu’à `ReportingViewsAndAlertSettingsV01`.
- Smokes Desktop : processus/fenêtre stables et répondants avec SQL puis sans variable SQL ; fermeture contrôlée.
- Performance simple : cinq lectures synthèse sur la base locale, 108,5 à 226,4 ms processus `sqlcmd` inclus, moyenne 154,5 ms.
- Secrets/PII : 36 fichiers modifiés/non suivis inspectés ; aucun secret ou PII ajouté. Deux faux positifs étaient des mots de passe synthétiques préexistants dans le test Sprint 15, dont seul le compteur de permissions a changé.
- Git : branche `main`, HEAD `c9def7591eec82a56f1a81facffdc7d2139f4e01`, index vide, aucun remote ; `git diff --check` PASS ; aucun add/commit/push/changement de branche.

## Performance et limites

Les agrégats sont traduits/exécutés en SQL et les listes utilisent une limite technique de 200 lignes (maximum 1000). Aucun cache, table d’agrégats ou index de vue n’est introduit sans mesure. La devise historique n’existe pas sur les ventes ; le pilote est explicitement limité à MAD. La revue visuelle, les comparaisons SSMS sur scénarios riches et les tests multi-rôle restent humains.

## Revue manuelle

Le protocole complet et les captures requises sont décrits dans `manual-validation.md`. Cette revue est obligatoire avant toute validation fonctionnelle du Sprint 16.
