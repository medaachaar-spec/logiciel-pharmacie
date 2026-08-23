# Installation pilote V1

Version ciblée : `1.0.0-rc.1` — Windows 11 x64 — mono-poste — SQL Server
Express nommé `PHARMACY`.

## Prérequis bloquants

- compte Windows administrateur pendant l’installation ;
- une identité Windows de déploiement séparée de l’identité runtime ;
- au moins 2 Gio libres sur le volume de sauvegarde ;
- Microsoft Edge WebView2 Evergreen installé ;
- SQL Server Express installé comme instance nommée `PHARMACY`, moteur démarré ;
- authentification Windows active ;
- aucune instance `Pharmacy.Desktop.exe` en cours.

L’identité runtime n’a pas besoin de créer une base ou d’appliquer des
migrations. Le Setup et `Pharmacy.Deployment` doivent être lancés par
`DeploymentLoginName`. L’application quotidienne est lancée par
`RuntimeLoginName`.

## Identités et provisionnement SQL

Le runtime utilise le rôle personnalisé `PharmacyAppRuntime` dans `Pharmacy`.
Il possède `db_datareader`, `db_datawriter`, `BACKUP DATABASE`, `BACKUP LOG` et
le rôle serveur dédié `PharmacyAppRestoreTest` (`CREATE ANY DATABASE` uniquement)
pour le restore-test vers les noms contrôlés `Pharmacy_RestoreTest_<12hex>`.
Il n’est ni `sysadmin`, ni `dbcreator`, ni `db_owner`, et ne reçoit aucun droit
DDL. Ce rôle dédié est nécessaire car `dbcreator` permettrait de restaurer une
base existante.

L’identité de déploiement possède `dbcreator` sur l’instance et `db_owner` sur
`Pharmacy`. Elle est réservée à la création de base, aux migrations EF, à
l’upgrade et au rollback/live restore. Aucun des deux chemins n’utilise SQL
Authentication ou un mot de passe stocké.

Après création de la base, exécuter en SQLCMD mode :

```powershell
sqlcmd -S .\PHARMACY -E -d master -b `
  -i .\database\scripts\008_pilot_least_privilege_idempotent.sql `
  -v RuntimeLoginName="CONTOSO\PharmacyStaff" `
     DeploymentLoginName="CONTOSO\PharmacyInstall" `
     PharmacyDatabaseName="Pharmacy" `
     RuntimeRoleName="PharmacyAppRuntime"
sqlcmd -S .\PHARMACY -E -d master -b `
  -i .\database\verification\verify_pilot_least_privilege.sql `
  -v RuntimeLoginName="CONTOSO\PharmacyStaff" `
     DeploymentLoginName="CONTOSO\PharmacyInstall" `
     PharmacyDatabaseName="Pharmacy" `
     RuntimeRoleName="PharmacyAppRuntime"
```

La seconde commande est en lecture seule et doit renvoyer `PASS`. Ne pas
remplacer un échec par l’ajout de `sysadmin`.

## Artefact

`artifacts/sprint-18/1.0.0-rc.1/installer/Logiciel-Pharmacie-1.0.0-rc.1-win-x64-Setup.exe`

SHA-256 attendu : lire `artifacts/sprint-18/1.0.0-rc.1/SHA256SUMS.txt`, puis
comparer avec `Get-FileHash`. L’artefact est volontairement non signé :
`CODE SIGNING GATE PENDING`. Vérifier le hash et la provenance avant tout
poste pilote.

## Installation contrôlée

1. Sauvegarder toute base pilote existante et la vérifier.
2. Fermer l’application.
3. Exécuter le Setup avec l’identité de déploiement élevée.
4. Le Setup vérifie Windows x64 et WebView2, installe les binaires et préserve
   ou crée `pilot.json`. Créer `Pharmacy` si nécessaire, puis exécuter
   manuellement le script de provisionnement et son verifier avec les deux
   identités comme indiqué ci-dessus.
5. `Pharmacy.Deployment` applique les migrations sous l’identité de
   déploiement ; une base existante reçoit d’abord une sauvegarde `COPY_ONLY`
   vérifiée et hashée.
6. Le démarrage de l’application est refusé si le schéma est incompatible.
   `Pharmacy.Desktop` n’applique jamais de migration.
7. Exécuter le verifier SQL puis ouvrir Diagnostic et confirmer version,
   connectivité et migrations.

## Configuration locale

Fichier : `%ProgramData%\LogicielPharmacie\config\pilot.json`.

```json
{
  "ServerInstance": ".\\PHARMACY",
  "DatabaseName": "Pharmacy",
  "BackupDirectory": "%ProgramData%\\LogicielPharmacie\\backups"
}
```

Cette configuration ne contient ni login, ni mot de passe, ni secret.
L’application construit une connexion chiffrée avec sécurité Windows intégrée.
Toute clé JSON inconnue est rejetée. Limiter l’écriture aux administrateurs et
accorder au dossier de backup les ACL minimales nécessaires au service SQL et
à la lecture runtime.

## Premier démarrage

La base métier est vide. Aucune identité ni mot de passe par défaut n’est
seedé. L’écran de bootstrap demande la création du premier Owner ; conserver
hors poste les codes de récupération affichés une seule fois. Voir
`docs/security/OWNER_BOOTSTRAP_AND_RECOVERY_V0.1.md`.

## Contrôles après installation

```powershell
& "$env:ProgramFiles\Logiciel Pharmacie\deployment\Pharmacy.Deployment.exe" health `
  --config "$env:ProgramData\LogicielPharmacie\config\pilot.json"
```

Attendu : `Passed=true`, 13 migrations appliquées, 0 pending et 5 tables
critiques. Vérifier ensuite l’identité de la pharmacie, connexion Owner, vente
synthétique, reporting, backup/verify, restore-test isolé et arrêt propre. Ne
pas importer de données réelles avant GO pilote.

## Échec

Conserver le log Inno Setup, ne pas relancer en boucle et ne jamais supprimer
la base. Générer un bundle support, consulter `SUPPORT_RUNBOOK_V1.md`, puis
appliquer le rollback documenté uniquement avec l’identité de déploiement et la
confirmation explicite `Pharmacy`.
