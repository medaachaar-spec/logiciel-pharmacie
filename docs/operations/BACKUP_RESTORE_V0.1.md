# Sauvegarde et restauration V0.1

## Portée

Le Sprint 17 fournit une sauvegarde SQL Server explicite, vérifiée et traçable. Il ne transforme jamais une copie de fichier en preuve de restauration. La restauration proposée dans l’application est exclusivement un test vers une base isolée `Pharmacy_RestoreTest_<suffixe>` ; elle ne remplace ni n’écrase `Pharmacy_Development`.

Objectifs d’exploitation : RPO 15 minutes, RTO 2 heures. Politique cible : complète quotidienne, différentielle toutes les 4 heures, journal toutes les 15 minutes lorsque la base est en mode `FULL` et que la chaîne de journaux est amorcée.

## Configuration locale

Les chemins restent hors SQL et hors Git :

- `PHARMACY_BACKUP_LOCAL_DIRECTORY` : chemin absolu accessible en modification au compte du service SQL Server et en lecture au compte Desktop/tâche ; à défaut, l’application lit `InstanceDefaultBackupPath` ;
- `PHARMACY_BACKUP_EXTERNAL_DIRECTORY` : support externe absolu optionnel ;
- `PHARMACY_BACKUP_EXTERNAL_ENCRYPTED=true` : attestation opérateur que le support externe est chiffré par une technologie maintenue (BitLocker ou équivalent). L’application n’implémente aucun chiffrement maison.

Ne stocker ni mot de passe ni chaîne de connexion dans ces variables. La chaîne SQL Desktop reste `PHARMACY_SQLSERVER_CONNECTION` et doit cibler strictement `Pharmacy_Development`.

Le compte SQL Server doit pouvoir écrire dans le dossier local. Le compte qui exécute l’application doit pouvoir relire le `.bak/.trn` afin de produire le SHA-256. Un défaut d’ACL doit être corrigé sur le dossier dédié, jamais en donnant des droits larges au disque.

## Création et preuve

Chaque sauvegarde créée par l’application :

1. utilise un nom contrôlé `Pharmacy_<base>_<type>_<UTC>_<operationId>.bak|trn` ;
2. exécute `BACKUP ... WITH CHECKSUM` (`COPY_ONLY` pour la complète manuelle) ;
3. exécute `RESTORE VERIFYONLY ... WITH CHECKSUM` ;
4. vérifie une taille non nulle ;
5. calcule le SHA-256 ;
6. copie éventuellement vers le support externe, recalcule le hash et indique sans ambiguïté si le chiffrement est attesté ;
7. écrit l’historique technique dans `operations.BackupRecords` et l’audit métier.

`VERIFYONLY` n’est pas une preuve suffisante de restaurabilité. Utiliser ensuite le restore-test.

## Automatisation Windows

SQL Server Express ne fournit pas SQL Server Agent. La procédure commune utilise donc le Planificateur de tâches Windows, y compris lors du développement sur SQL Server Developer :

```powershell
pwsh -NoProfile -File scripts/Invoke-Sprint17Backup.ps1 -ServerInstance localhost -DatabaseName Pharmacy_Development -Type Full
```

Créer trois tâches avec un compte de service minimal et l’option « exécuter même si l’utilisateur n’est pas connecté ». Ne mettre aucun secret dans les arguments. Fréquences : `Full` quotidienne, `Differential` toutes les 4 h, `TransactionLog` toutes les 15 min. Une édition Express reste compatible avec les commandes BACKUP/RESTORE mais pas avec Agent.

## Restore-test isolé

Dans l’UI, `backup.restore` exige une réauthentification récente. Sélectionner uniquement une sauvegarde complète gérée puis « Test restaurer ». Le service :

- refuse tout chemin hors des racines autorisées ;
- vérifie la sauvegarde ;
- obtient les fichiers logiques ;
- restaure sous un nom temporaire strict ;
- compte les migrations et les tables critiques Catalogue, Stock, Ventes et Sécurité ;
- ferme les connexions et supprime la base temporaire dans le chemin succès comme échec.

Équivalent headless :

```powershell
pwsh -NoProfile -File scripts/Test-Sprint17Restore.ps1 -ServerInstance localhost -BackupFile "<chemin-absolu>.bak"
```

Une restauration réelle en place reste une procédure hors ligne soumise à décision humaine, sauvegarde de sécurité préalable, validation du périmètre, interruption des écritures, réauthentification et plan de retour arrière. La V0.1 ne l’exécute pas depuis l’application.

## Rétention et contrôles

La rétention n’est pas automatisée en V0.1 : aucune suppression silencieuse de `.bak`. L’opérateur définit et documente la capacité disque, le nombre de générations locales/externes et teste mensuellement une restauration. Ne jamais committer les fichiers de sauvegarde.
