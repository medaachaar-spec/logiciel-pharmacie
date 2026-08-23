# SQL Server Express — cible pilote V1

La cible pilote est SQL Server Express sur Windows 11 x64, instance nommée
`PHARMACY` (`.\PHARMACY`) et base `Pharmacy`. Le déploiement est validé avec
13 migrations et zéro migration en attente. La base et l’instance de
production ne doivent pas être utilisées pour les tests jetables.

## Convention

- authentification Windows intégrée uniquement ;
- protocole local pour le pilote mono-poste ;
- sauvegardes planifiées par le Planificateur de tâches Windows, jamais par SQL
  Server Agent ;
- aucun login SQL, mot de passe ou secret dans `pilot.json`, les scripts ou
  l’application.

## Deux identités Windows

Le compte utilisé au quotidien par `Pharmacy.Desktop` est différent du compte
qui installe et met à niveau le produit. Tous deux sont des utilisateurs ou
groupes Windows, jamais des comptes SQL.

| Identité | Utilisation | SQL Server / `Pharmacy` |
| --- | --- | --- |
| runtime (`RuntimeLoginName`) | POS, reporting, backup et restore-test isolé | rôle `PharmacyAppRuntime`, `db_datareader`, `db_datawriter`, `BACKUP DATABASE`, `BACKUP LOG`, et serveur `PharmacyAppRestoreTest` (`CREATE ANY DATABASE` uniquement) pour les copies `Pharmacy_RestoreTest_*` ; jamais `sysadmin`, `dbcreator` ou `db_owner` |
| déploiement (`DeploymentLoginName`) | création de base, EF migrations, upgrade, rollback et restore réel | `dbcreator` sur l’instance et `db_owner` sur `Pharmacy` ; utilisé uniquement depuis Setup/`Pharmacy.Deployment` |

Le rôle runtime ne reçoit aucun droit DDL. `db_datareader` rend les vues
`reporting.vw_*` lisibles et `db_datawriter` couvre les écritures EF. Les
permissions publiques utilisées par l’application (`SERVERPROPERTY` et
`sys.sp_getapplock`) restent publiques. Le runtime peut toujours modifier des
lignes auxquelles l’application donne accès : cette limite honnête d’une
application desktop n’est pas présentée comme supprimée.

## Provisionnement idempotent

Après création de `Pharmacy`, exécuter une fois, en SQLCMD mode, avec le compte
de déploiement élevé :

```powershell
sqlcmd -S .\PHARMACY -E -d master -b `
  -i .\database\scripts\008_pilot_least_privilege_idempotent.sql `
  -v RuntimeLoginName="CONTOSO\PharmacyStaff" `
     DeploymentLoginName="CONTOSO\PharmacyInstall" `
     PharmacyDatabaseName="Pharmacy" `
     RuntimeRoleName="PharmacyAppRuntime"
```

Le script crée ou réutilise les logins Windows, retire du runtime les rôles
serveur et base inattendus, puis converge les memberships attendues. Il ne
contient aucune commande de mot de passe et ne donne jamais `sysadmin` au
runtime. Il est sûr à rejouer ; il n’effectue aucune migration.

Vérifier ensuite, en lecture seule :

```powershell
sqlcmd -S .\PHARMACY -E -d master -b `
  -i .\database\verification\verify_pilot_least_privilege.sql `
  -v RuntimeLoginName="CONTOSO\PharmacyStaff" `
     DeploymentLoginName="CONTOSO\PharmacyInstall" `
     PharmacyDatabaseName="Pharmacy" `
     RuntimeRoleName="PharmacyAppRuntime"
```

La sortie doit contenir `PASS`. Un échec est un NO-GO ; ne pas contourner la
vérification en ajoutant `sysadmin` au compte quotidien.

## Installation de l’instance

Télécharger SQL Server Express depuis Microsoft. Choisir une installation
personnalisée, moteur de base uniquement, instance `PHARMACY`, authentification
Windows, puis documenter la version, le média et son hash. Ajouter uniquement
le compte de déploiement comme administrateur SQL pendant l’installation si le
Setup l’exige ; exécuter ensuite le script ci-dessus et retirer toute
administration SQL du compte runtime.

## Validation obligatoire

Sur un poste/VM sans donnée réelle :

1. confirmer le service `MSSQL$PHARMACY` et l’édition `Express` ;
2. exécuter le Setup RC avec l’identité de déploiement ;
3. confirmer 13 migrations, zéro pending et aucune variation de modèle ;
4. exécuter le verifier least-privilege avec les deux identités ;
5. lancer l’application sous le runtime et vérifier catalogue, vente, reporting,
   backup/`VERIFYONLY` et restore-test isolé ;
6. tester upgrade, rollback, uninstall/reinstall sur des bases jetables ;
7. confirmer l’absence de base temporaire ou processus résiduel.

Commande préflight :

```powershell
Pharmacy.Deployment.exe preflight --config "$env:ProgramData\LogicielPharmacie\config\pilot.json"
```

Tous les contrôles, y compris `SQL Server Express`, doivent être `PASS`. Un
échec de permission, backup, restore-test, migration ou préservation produit
est un NO-GO.

## Backup Express

Express prend en charge `BACKUP`/`RESTORE`, mais l’automatisation ne dépend pas
de SQL Server Agent. La tâche Windows de backup s’exécute sous l’identité
runtime autorisée ; le service SQL Server doit seulement pouvoir écrire dans le
dossier dédié. Vérifier régulièrement hashes et restore-test et conserver une
copie externe chiffrée et déconnectée. Toute restauration en place reste une
opération de déploiement hors ligne sous l’identité de déploiement.
