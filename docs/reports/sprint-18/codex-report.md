# Rapport Codex — Sprint 18

Date : 11 août 2026  
Branche : `main`  
HEAD initial : `79a446df0c7dbc703c8e611a620595f047089340`  
Version : `1.0.0-rc.1`

## Résultat

Statut : `IMPLEMENTED — OWNER BOOTSTRAP FIX VALIDATION PASS — SQL EXPRESS PILOT GATE PENDING — MANUAL PILOT UAT REQUIRED`

La Release Candidate Windows est implémentée, publiée et packagée. Le statut ne constitue ni GO pilote ni validation production. L’instance SQL Server Express réelle `.\PHARMACY` et sa base `Pharmacy` ont validé de manière non destructive la détection d’une base à zéro utilisateur et l’affichage du bootstrap Owner. La création manuelle du compte, les opérations du Setup et l’UAT restent à valider.

## Correction ciblée — bootstrap Owner

La configuration pilote ProgramData était absente lors du constat initial. Le Desktop utilisait donc la factory SQL indisponible. Le coordinateur réduisait ensuite l’échec de `HasUsersAsync` à `RequiresBootstrap = false`, ce qui rendait l’erreur indiscernable d’une base contenant déjà un utilisateur et affichait à tort le Login.

La navigation distingue désormais explicitement quatre états : vérification, bootstrap requis, Login requis et SQL/configuration indisponible. Aucun échec SQL ne déclenche un bootstrap local. La création Owner reste transactionnelle et acquiert en plus un verrou applicatif SQL exclusif lié à la transaction, afin de sérialiser les doubles soumissions et les processus concurrents. Le rôle Owner, ses permissions, les huit recovery codes hashés et l’audit restent créés dans la même transaction.

## Livré

- version centrale MSBuild, affichée dans fenêtre, Diagnostic, barre d’état et métadonnées ;
- publish `win-x64`, self-contained, non trimmed, non single-file ;
- installateur Inno Setup 6 avec AppId stable, contrôles x64/WebView2/SQL, upgrade et préservation ;
- configuration ProgramData sans secret, sécurité Windows intégrée, cible `.\PHARMACY` / `Pharmacy` ;
- outil `Pharmacy.Deployment` : `preflight`, `install-or-upgrade`, `health`, `rollback`, `support-bundle` ;
- sauvegarde pré-migration COPY_ONLY, VERIFYONLY, SHA-256 ; rollback par restauration ;
- garde de compatibilité au démarrage sans migration automatique ;
- tests version/config/package/préservation/Developer fresh DB/migrations/bootstrap/support ;
- guides installation, Express, upgrade, uninstall, release, UAT, go-live, support et utilisateur.

## Artefact local non commité

- dossier : `artifacts/sprint-18/1.0.0-rc.1/` ;
- Setup : `Logiciel-Pharmacie-1.0.0-rc.1-win-x64-Setup.exe` ;
- taille : 125 246 684 octets ;
- SHA-256 : `3AD747B86EECB4844E3F14A47D55A9E274BEACBF2127D4D4F0E0A60BD70836D4` ;
- manifestes : `release-manifest.json`, `SHA256SUMS.txt` ;
- validation package : 752 fichiers présents et hashés, aucun symbole PDB ni artefact sensible.

## Environnement et gates

- Windows 11 x64 : PASS ;
- .NET SDK/runtime 10.0.302/10.0.10 : PASS ;
- WebView2 Evergreen détecté : PASS ;
- SQL Server Developer local : PASS pour tests isolés ;
- SQL Server Express `PHARMACY` : instance, base, 9 migrations et écran bootstrap à zéro utilisateur PASS ; création Owner et UAT PENDING ;
- signature de code réelle : `CODE SIGNING GATE PENDING` ; aucun faux certificat créé ;
- scanner/imprimante/TPE/UPS réels : revue manuelle requise ;
- UAT pharmacienne : requise.

## Validations

Baseline avant modification : Debug build 0 warning/0 erreur, 368/368 PASS. Une première exécution a révélé uniquement que le compte service SQL ne pouvait pas écrire dans le dossier temporaire de backup ; une ACL locale sur `tmp/sprint18-sql-backups` a permis la preuve sans changement produit.

- tests ciblés bootstrap/SQL : PASS, 5/5 ;
- Debug build/tests : PASS, 0 warning/0 erreur, 384/384 ;
- Release build/tests : PASS, 0 warning/0 erreur, 384/384 ;
- migrations : 9 listées, 0 pending model changes ;
- package : PASS, 752/752 fichiers vérifiés ;
- smoke publish Desktop + SQL Developer + WebView2 : PASS, fermeture sans processus résiduel ;
- base temporaire Sprint 18 : création, 9 migrations, health, aucun Owner seedé, bundle expurgé et nettoyage PASS ;
- SQL Express réel `.\PHARMACY` / `Pharmacy` : `UserAccounts=0`, affichage « Initialiser le propriétaire » confirmé à 1100×700 sans soumission, puis `UserAccounts=0` et aucun processus résiduel ;
- préflight publié : contrôles Windows/version/disque/WebView2/service/connexion PASS ; édition Express et élévation hors Setup correctement signalées FAIL/PENDING ;
- résidus SQL/processus : aucun ;
- `git diff --check` : PASS ; index Git : vide.

## Limites et NO-GO actuels

La création manuelle du premier Owner sur SQL Express, installation/repair/upgrade/uninstall/reinstall sur environnement propre, matériel et UAT n’ont pas été exécutés. Le Setup est non signé. Ces points empêchent tout GO pilote. Les procédures et feuilles de preuve sont prêtes pour la validation humaine.
