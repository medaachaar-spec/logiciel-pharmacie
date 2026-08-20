# BRAND-01 — Rapport Codex

Statut : `IMPLEMENTED — PHARMACY BRANDING AUTOMATED GATE PASS`.

## Architecture

- stockage : singleton `configuration.PharmacyBranding` ;
- logo initial : actif Desktop embarqué ; logo remplacé : binaire SQL validé et sauvegardable ;
- chargement : coordinateur pré-authentification avec fallback central et sans valeur stale ;
- thème : palette runtime par tokens CSS ;
- modification : `security.manage_users` vérifiée dans le service SQL ;
- intégrité : `rowversion`, audit transactionnel des champs et restauration avec empreinte SHA-256.

## Migration

`20260820213400_PharmacyBrandingPersistenceV01` ajoute le schéma `configuration`, la table singleton, ses contraintes et le seed nom/couleur. Les neuf migrations précédentes ne sont pas modifiées. Total : 10. Pending model changes : NONE.

## Tests

Les tests `BRAND001` à `BRAND022` couvrent valeurs initiales, Login/Sidebar, modifications, refus RBAC, validations, persistance inter-contexte/provider, concurrence, audit sans blob, backup/restore, fallback, nom long, non-régression CUSTOM et invariance navigation/permissions.

Les mutations SQL utilisent uniquement une base `Pharmacy_BRAND01_Test_*`. La base pilote `.\PHARMACY / Pharmacy` n’est pas migrée par cette campagne.

Résultats finaux :

- BRAND core : 8/8 cas PASS (dont 4 variantes invalides pour BRAND012) ;
- BRAND UI/fallback/contraste : 9/9 PASS ;
- BRAND SQL : 9/9 PASS ;
- lot ciblé Security/Admin/AppShell/CUSTOM/packaging : 83/83 PASS ;
- CUSTOM : 12/12 PASS ;
- Debug build : 0 warning, 0 error ;
- Debug global : 676/676 PASS, 0 skipped ;
- Release build : 0 warning, 0 error ;
- Release global : 676/676 PASS, 0 skipped ;
- E2E-01 : 27/27 checkpoints PASS.

La première passe globale a détecté trois attentes d’architecture devenues obsolètes : nouvelle table absente de la liste attendue, compteur `rowversion` et convention `nvarchar`. Elles ont été alignées sans changement métier ; la passe complète finale est verte.

Le health check de restauration accepte encore une sauvegarde pré-BRAND saine à neuf migrations. Une sauvegarde BRAND exige en plus `configuration.PharmacyBranding` et expose uniquement nom, couleur et SHA-256 du logo pour comparaison.

Nettoyage final : zéro base BRAND, restore-test ou autre base de test ; zéro fichier backup ; zéro processus Desktop. La base pilote reste à 9 migrations, sans table BRAND.
