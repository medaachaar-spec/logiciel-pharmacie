# Installation pilote V1

Version ciblée : `1.0.0-rc.1` — Windows 11 x64 — mono-poste — SQL Server Express nommé `PHARMACY`.

## Prérequis bloquants

- compte Windows administrateur pendant l’installation ;
- au moins 2 Gio libres sur le volume de sauvegarde ;
- Microsoft Edge WebView2 Evergreen installé ;
- SQL Server Express installé comme instance nommée `PHARMACY`, moteur démarré ;
- authentification Windows active et compte d’installation autorisé à créer la base et appliquer les migrations ;
- aucune instance `Pharmacy.Desktop.exe` en cours.

SQL Express et WebView2 doivent provenir de Microsoft. Le Setup RC ne télécharge rien et ne contourne aucun prérequis.

## Artefact

`artifacts/sprint-18/1.0.0-rc.1/installer/Logiciel-Pharmacie-1.0.0-rc.1-win-x64-Setup.exe`

SHA-256 attendu : lire `artifacts/sprint-18/1.0.0-rc.1/SHA256SUMS.txt`, puis comparer avec :

```powershell
Get-FileHash .\Logiciel-Pharmacie-1.0.0-rc.1-win-x64-Setup.exe -Algorithm SHA256
```

L’artefact est volontairement non signé : `CODE SIGNING GATE PENDING`. Ne pas ignorer un avertissement SmartScreen sur un poste pilote sans vérifier le hash et la provenance.

## Installation contrôlée

1. Sauvegarder toute base pilote existante et la vérifier.
2. Fermer l’application.
3. Exécuter le Setup en administrateur.
4. Le Setup vérifie Windows x64 et WebView2, installe les binaires, préserve ou crée la configuration, donne au service `MSSQL$PHARMACY` l’accès au dossier de backup, exécute le préflight puis `install-or-upgrade`.
5. Toute base existante nécessitant une migration reçoit d’abord une sauvegarde `COPY_ONLY` vérifiée et hashée.
6. Le démarrage de l’application est refusé si le schéma reste incompatible. L’application n’applique jamais de migration au démarrage.
7. Ouvrir Diagnostic et vérifier version `1.0.0-rc.1`, connectivité et migrations.

## Configuration locale

Fichier : `%ProgramData%\LogicielPharmacie\config\pilot.json`.

```json
{
  "ServerInstance": ".\\PHARMACY",
  "DatabaseName": "Pharmacy",
  "BackupDirectory": "%ProgramData%\\LogicielPharmacie\\backups"
}
```

Cette configuration ne contient ni login ni mot de passe. L’application construit une connexion chiffrée avec sécurité Windows intégrée. Toute clé JSON inconnue est rejetée. Limiter l’écriture aux administrateurs ; les utilisateurs de l’application ont seulement besoin de lecture.

## Premier démarrage

La base métier est vide. Seules les références techniques et permissions des migrations existent. Aucun utilisateur ni mot de passe par défaut n’est seedé. L’écran de bootstrap demande la création du premier Owner ; conserver hors poste les codes de récupération affichés une seule fois. Voir `docs/security/OWNER_BOOTSTRAP_AND_RECOVERY_V0.1.md`.

## Contrôles après installation

```powershell
& "$env:ProgramFiles\Logiciel Pharmacie\deployment\Pharmacy.Deployment.exe" health `
  --config "$env:ProgramData\LogicielPharmacie\config\pilot.json"
```

Attendu : `Passed=true`, 9 migrations appliquées, 0 pending, 5 tables critiques. Vérifier ensuite connexion Owner, vente synthétique, backup/verify et arrêt propre. Ne pas importer de données réelles avant GO pilote.

## Échec

Conserver le log Inno Setup, ne pas relancer en boucle et ne jamais supprimer la base. Générer un bundle support, consulter `SUPPORT_RUNBOOK_V1.md`, puis appliquer le rollback documenté si une migration a commencé.
