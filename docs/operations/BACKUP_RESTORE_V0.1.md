# Sauvegarde et restauration V0.1

## Portée

Le Sprint 17 fournit une sauvegarde SQL Server explicite, vérifiée et traçable.
La restauration proposée dans l’application est exclusivement un test vers une
base isolée `Pharmacy_RestoreTest_<suffixe>` ; elle ne remplace ni n’écrase
`Pharmacy`, `Pharmacy_Development` ou une autre base existante.

Objectifs d’exploitation : RPO 15 minutes, RTO 2 heures. Politique cible :
complète quotidienne, différentielle toutes les 4 heures, journal toutes les
15 minutes lorsque la base est en mode `FULL` et que la chaîne de journaux est
amorcée.

## Identités et ACL

Le runtime Windows (`RuntimeLoginName`) est le compte de l’application et des
tâches de backup. Dans `Pharmacy`, il est membre de `PharmacyAppRuntime`,
`db_datareader` et `db_datawriter`, reçoit `BACKUP DATABASE`/`BACKUP LOG` et le
rôle serveur dédié `PharmacyAppRestoreTest` (`CREATE ANY DATABASE` uniquement)
pour le seul restore-test contrôlé. Il n’est ni `sysadmin`, ni `dbcreator`, ni
`db_owner`, et ne peut pas exécuter de DDL ou un restore avec remplacement sur
la base live.

Le service SQL Server doit pouvoir écrire dans le dossier local. Le runtime
doit pouvoir relire les fichiers `.bak`/`.trn` afin de calculer le SHA-256 ;
l’ACL est limitée au dossier dédié. L’identité de déploiement est la seule
utilisée pour les sauvegardes pré-migration et les restaurations réelles,
avec `dbcreator` sur l’instance et `db_owner` sur `Pharmacy`.

Les chemins restent hors SQL et hors Git :

- `PHARMACY_BACKUP_LOCAL_DIRECTORY` : chemin absolu du dossier local ;
- `PHARMACY_BACKUP_EXTERNAL_DIRECTORY` : support externe absolu optionnel ;
- `PHARMACY_BACKUP_EXTERNAL_ENCRYPTED=true` : attestation opérateur d’un
  chiffrement maintenu (BitLocker ou équivalent).

Ne stocker ni mot de passe ni chaîne de connexion dans ces variables. La
chaîne SQL Desktop reste `PHARMACY_SQLSERVER_CONNECTION` et doit cibler
strictement `Pharmacy_Development` pendant les tests autorisés.

## Création et preuve

Chaque sauvegarde créée par l’application :

1. utilise un nom contrôlé `Pharmacy_<base>_<type>_<UTC>_<operationId>.bak|trn` ;
2. exécute `BACKUP ... WITH CHECKSUM` (`COPY_ONLY` pour la complète manuelle) ;
3. exécute `RESTORE VERIFYONLY ... WITH CHECKSUM` ;
4. vérifie une taille non nulle ;
5. calcule le SHA-256 ;
6. copie éventuellement vers le support externe et recalcule le hash ;
7. écrit l’historique dans `operations.BackupRecords` et l’audit métier.

`VERIFYONLY` n’est pas une preuve suffisante de restaurabilité. Utiliser
ensuite le restore-test isolé.

## Automatisation Windows

SQL Server Express ne fournit pas SQL Server Agent. La procédure commune
utilise donc le Planificateur de tâches Windows, sous l’identité runtime, sans
secret dans les arguments :

```powershell
pwsh -NoProfile -File scripts/Invoke-Sprint17Backup.ps1 -ServerInstance localhost -DatabaseName Pharmacy -Type Full
```

Créer trois tâches avec le compte runtime et l’option « exécuter même si
l’utilisateur n’est pas connecté ». Fréquences : `Full` quotidienne,
`Differential` toutes les 4 h, `TransactionLog` toutes les 15 min. Une édition
Express reste compatible avec `BACKUP`/`RESTORE` mais pas avec Agent.

## Restore-test isolé

Dans l’UI, `backup.restore` exige une réauthentification récente. Sélectionner
uniquement une sauvegarde complète gérée puis « Test restaurer ». Le service :

- refuse tout chemin hors des racines autorisées ;
- vérifie la sauvegarde ;
- obtient les fichiers logiques ;
- restaure sous `Pharmacy_RestoreTest_<12hex>` ;
- compte migrations et tables critiques ;
- ferme les connexions et supprime la base temporaire en succès comme en échec.

Équivalent headless :

```powershell
pwsh -NoProfile -File scripts/Test-Sprint17Restore.ps1 -ServerInstance localhost -BackupFile "<chemin-absolu>.bak"
```

Le rôle `PharmacyAppRestoreTest` peut créer cette nouvelle base ; le runtime en
devient propriétaire et peut donc la nettoyer, mais il ne dispose pas des
permissions `RESTORE` d’un `dbcreator` sur une base existante. Il ne peut donc
pas remplacer la base live. Une restauration en place reste une procédure hors
ligne soumise à décision humaine, sauvegarde de sécurité préalable,
interruption des écritures, réauthentification et plan de retour arrière ; elle
est exécutée uniquement par l’identité de déploiement.

## Rétention et contrôles

La rétention n’est pas automatisée en V0.1 : aucune suppression silencieuse de
`.bak`. L’opérateur définit la capacité disque, le nombre de générations
locales/externes et teste mensuellement une restauration. Ne jamais committer
les fichiers de sauvegarde.
