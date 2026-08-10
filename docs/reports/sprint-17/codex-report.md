# Sprint 17 — Rapport Codex

Statut : **IMPLEMENTED — BACKUP/IMPORT/HARDWARE VALIDATION PASS — MANUAL OPERATIONS REVIEW REQUIRED — HARDWARE PILOT GATE PENDING**

## Résumé

Le Sprint 17 ajoute un module local de continuité et d’exploitation : sauvegardes SQL vérifiées, restore-test isolé, imports CSV contrôlés et abstractions de périphériques. L’écran `/module/operations` réutilise le design system, affiche les capacités réelles et ne déclare jamais le matériel simulé comme certifié.

## SQL, migration et RBAC

- Migration additive `BackupImportDeviceOperationsV01` appliquée à `Pharmacy_Development`.
- Schéma `operations` : `BackupRecords` et `ImportJobs`, PK bigint, contraintes, unicité OperationId/fingerprint et `rowversion`.
- Permissions ajoutées : `backup.view`, `backup.create`, `backup.restore`, `import.view`, `import.execute`, `devices.configure` ; deny-by-default.
- `backup.restore`, `import.execute` et `devices.configure` exigent une réauthentification récente.
- Les succès et refus Sprint 17 écrivent une trace minimale ; aucun contenu complet CSV, chemin secret ou donnée bancaire n’est audité.
- Base locale : 9 migrations, 2 tables `operations`, 28 permissions. `verify_sprint17_operations.sql` : PASS.

## Backup et restore-test

`SqlBackupRecoveryService` crée Full/Differential/TransactionLog avec CHECKSUM, exécute `RESTORE VERIFYONLY`, contrôle taille, calcule SHA-256 et conserve un historique. Les chemins sont configurables et bornés à des racines absolues. La copie externe est bloquée si le chiffrement du volume n’est pas attesté ; aucun chiffrement maison n’est introduit.

Le restore-test exige `backup.restore`, une réauthentification récente et un `.bak` autorisé. Il restaure uniquement vers `Pharmacy_RestoreTest_<suffixe>`, vérifie migrations et quatre tables critiques, puis supprime la base dans les chemins succès/échec. Aucun restore en place n’est proposé par l’application.

Preuves réelles :

- service automatisé sur une base `Pharmacy_Sprint17_Test_*` : création, hash, copie externe déclarée chiffrée, vérification, restauration et cleanup PASS ;
- scripts headless sur `Pharmacy_Development` : sauvegarde complète, `VERIFYONLY` PASS, SHA-256 calculé, restauration temporaire PASS avec 9 migrations et 4 tables critiques, fichier `.bak` supprimé après preuve ;
- zéro base `Pharmacy_RestoreTest_*`/`Pharmacy_Sprint17_Test_*` et zéro fichier de validation résiduel.

SQL Server Developer dispose d’Agent, mais l’exploitation documentée utilise le Planificateur de tâches Windows afin de rester compatible avec Express : Full quotidienne, Diff 4 h et Log 15 min sous recovery `FULL`.

## Import

- Format : CSV UTF-8 strict, BOM facultatif, 25 Mio/100 000 lignes, aperçu borné à 100 lignes ; XLSX non supporté et aucun package ajouté.
- Contrôles : nom simple, en-têtes/mapping, colonnes, références, codes-barres, décimaux, quantités, dates, doublons et préfixes de formule `= + - @` sur toute cellule.
- Catalogue : `CreateOnly`, catégorie existante, aucune écrasement silencieux, aucun PPH/marge inventé. Le prix achat reste visible dans l’aperçu uniquement.
- Stock initial et lots : produit/emplacement existants, cohérence `TracksLots/TracksExpiry`, mouvement `Opening`, lot/solde/mouvement atomiques et aucune création Catalogue implicite.
- Idempotence : OperationId stable après aperçu, SHA-256 + mode + mapping ; replay identique stable et contenu différent refusé.
- Tests SQL : Catalogue avec accents/arabe, collision code-barres et rollback, stock initial, lot/péremption, replay et 8 100 références synthétiques avec aperçu limité. PASS.

Le parsing CPU est déporté hors du thread UI. Le test d’intégration complet comprenant migration, 8 100 lignes, trois imports, backup et restore-test s’exécute en environ 13 secondes lors de la validation ciblée ; ce temps n’est pas un benchmark matériel contractuel.

## Scanner, impression, TPE et diagnostic

- `HidScannerBuffer` : suffixe Enter, intervalle maximum entre touches, suppression du double scan immédiat, saisie manuelle et aucune création sur code inconnu.
- `IReceiptPrinter` : adaptateur 80 mm simulé ; le POS lui transmet uniquement une vente SQL persistée. Échec/retry/sans ticket ne rollbackent pas la vente ; réimpression explicitement demandée et auditée.
- `IManualPaymentTerminal` : Accepted/Declined/Cancelled manuels, sans PAN, PIN, cryptogramme ou token bancaire.
- Diagnostic : SQL, dossiers de sauvegarde, support externe, scanner, imprimante, TPE, UPS, application et runtime avec états « simulateur », « manuel » ou « non configuré » explicites.
- Scanner, imprimante, TPE et onduleur physiques non disponibles : `HARDWARE PILOT GATE PENDING`.

## Validation automatisée

- Debug : build PASS, 0 erreur, 0 avertissement ; **368/368 tests PASS**, 0 ignoré.
- Release : build PASS, 0 erreur, 0 avertissement ; **368/368 tests PASS**, 0 ignoré.
- SQL réel : migration/rollback implicite des bases de test, Catalogue/Stock/Lots, RBAC, backup/copie/hash/restore/cleanup, printer/TPE/diagnostic PASS.
- Pending model changes : aucun ; migration Sprint 17 dernière appliquée.
- Scripts PowerShell : syntaxe PASS ; backup et restore-test réels PASS.
- Smokes Desktop Release : fenêtre `Logiciel Pharmacie` répondante avec SQL puis sans variable SQL ; fermeture contrôlée et aucun processus résiduel.
- Packages : aucun `PackageReference` ajouté ou modifié.
- Secrets/PII : 55 fichiers modifiés/non suivis contrôlés, lignes ajoutées et fichiers nouveaux inspectés ; aucune détection active. Un faux positif provenait d’un nom de classe CSS.
- `git diff --check` : PASS (seulement avertissements de normalisation LF/CRLF existante).

## Git en lecture seule

- Branche : `main`.
- HEAD : `25ea810e0bcc35691e997823f99d7938e68045ca` (`feat: add reporting dashboards alerts and secure exports`).
- Index : vide.
- Remote : aucun.
- Toutes les modifications Sprint 17, y compris le prompt autorisé, restent non stagées/non suivies pour revue.
- Aucun `git add`, commit, push, changement de branche ou ajout de remote effectué.

## Limites et revue manuelle

- SQL Server Express et le Planificateur de tâches doivent encore être validés sur la cible pilote.
- Le chiffrement externe repose sur un volume maintenu attesté par l’opérateur ; l’application ne peut pas certifier BitLocker seule.
- Rétention/suppression des anciennes sauvegardes et restauration réelle en place restent des procédures humaines hors V0.1.
- XLSX, impression physique et intégration TPE fournisseur sont hors périmètre.
- Responsive, clavier, focus, erreurs disque/support et workflow multi-rôle nécessitent les captures du guide manuel.

La procédure complète se trouve dans `manual-validation.md` et la checklist physique dans `docs/hardware/PILOT_HARDWARE_CHECKLIST_V0.1.md`.
