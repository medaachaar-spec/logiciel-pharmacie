# Rapport EMP-01 — Contrôle d’accès employés

## Résultat

Implémentation de deux presets, des exceptions individuelles persistantes, d’une résolution effective centralisée et de la navigation deny-by-default. La validation automatisée détaillée est consignée dans le rapport de fin d’exécution.

## Architecture

- `OWNER` : bootstrap et récupération uniquement, masqué dans l’expérience courante, attribution administrative interdite.
- `MANAGER` : gestion opérationnelle et administration courante ; restauration de sauvegarde exclue par défaut.
- `SELLER` / Vendeur : vente et lectures nécessaires au POS.
- rôles personnalisés : conservés.
- overrides : table additive `security.UserPermissionOverrides`, `Allow`/`Deny`, rowversion, audit et unicité.
- résolution : rôle(s) + Allow − Deny, avec Deny prioritaire.
- session : rafraîchissement garanti à la prochaine authentification.

## Migration

- migration ajoutée : `EmployeeAccessControlV01`
- nombre attendu : 11
- anciennes migrations modifiées : non
- mutation de la base pilote : non

## Validation

- EMP001–EMP030 : 30/30 PASS Debug ciblé
- EMP + SEC SQL ciblés : 60/60 PASS
- CUSTOM / BRAND / SEC ciblés : 66/66 PASS
- Debug build : PASS, 0 warning, 0 erreur
- Release build : PASS, 0 warning, 0 erreur
- Debug tests : 706/706 PASS
- Release tests : 706/706 PASS
- E2E-01 : 27/27 checkpoints PASS en Debug et Release
- migration count : 11
- pending model changes : NONE
- bases temporaires résiduelles : 0
- base pilote `.\PHARMACY / Pharmacy` : 9 migrations, aucune table EMP, aucune mutation
- processus `Pharmacy.Desktop` résiduel : 0
- `git diff --check` : PASS

## Git

Aucun staging, commit, push, merge, rebase, tag ou changement de branche n’a été effectué.
