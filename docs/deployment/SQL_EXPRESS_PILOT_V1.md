# SQL Server Express — cible pilote V1

Statut au 11 août 2026 : `SQL EXPRESS PILOT GATE PENDING`. L’instance Express requise `.\PHARMACY` et la base `Pharmacy` sont présentes avec les 9 migrations. La détection à zéro utilisateur et l’affichage du bootstrap Owner ont été validés sans créer de compte. La création manuelle du premier Owner et les autres contrôles de cette procédure restent requis avant de lever le gate.

## Convention

- édition : SQL Server Express prise en charge sur Windows 11 x64 ;
- instance nommée : `PHARMACY` (`.\PHARMACY`) ;
- base : `Pharmacy` ;
- authentification : Windows intégrée ;
- protocole local uniquement pour le pilote mono-poste ;
- sauvegardes planifiées par Planificateur de tâches Windows, jamais SQL Server Agent.

Références Microsoft : [guide d’installation SQL Server](https://learn.microsoft.com/en-us/sql/database-engine/install-windows/install-sql-server?view=sql-server-ver17) et [installation en ligne de commande](https://learn.microsoft.com/en-us/sql/database-engine/install-windows/install-sql-server-from-the-command-prompt?view=sql-server-ver17).

## Installation de l’instance

Télécharger SQL Server Express depuis Microsoft. Choisir une installation personnalisée, moteur de base uniquement, instance `PHARMACY`, authentification Windows, ajouter le compte administrateur pilote comme administrateur SQL. Ne pas installer de sample database. Documenter version exacte, média et hash. Redémarrer si demandé avant l’installation du produit.

## Validation obligatoire pour lever le gate

Sur un poste/VM dédié sans donnée réelle :

1. confirmer service `MSSQL$PHARMACY` et édition contenant `Express` ;
2. vérifier connexion Windows, TLS local, création `Pharmacy` ;
3. exécuter le Setup RC fresh ;
4. confirmer 9 migrations, 0 pending, aucune variation de modèle ;
5. confirmer absence d’utilisateur métier seedé ;
6. exécuter les scénarios catalogue, achat/réception, vente FEFO, crédit, retour, inventaire, RBAC, reporting, import ;
7. créer backup, `RESTORE VERIFYONLY`, hash, copie externe et restore-test isolé ;
8. tester repair, upgrade fixture, uninstall, reinstall ;
9. mesurer taille DB, temps de démarrage et vente ;
10. confirmer aucune base temporaire/processus résiduel.

Commande préflight installée :

```powershell
Pharmacy.Deployment.exe preflight --config "$env:ProgramData\LogicielPharmacie\config\pilot.json"
```

Tous les contrôles, y compris `SQL Server Express`, doivent être `PASS`. Un échec de backup/restore, migration, concurrence, disponibilité ou préservation produit un NO-GO.

## Backup Express

Express prend en charge `BACKUP`/`RESTORE`, mais l’automatisation ne doit pas dépendre de SQL Server Agent. Planifier une tâche Windows quotidienne sous une identité autorisée, surveiller son code retour, vérifier régulièrement les hashes et réaliser un restore-test mensuel isolé. Conserver au moins une copie externe chiffrée et déconnectée.
