# Désinstallation et réinstallation pilote V1

## Avant désinstallation

Créer et vérifier un backup, copier le backup vers un support externe chiffré, noter la version et arrêter l’application.

## Désinstallation

Utiliser Applications installées > Logiciel Pharmacie > Désinstaller. Sont retirés : binaires, raccourcis et entrée de désinstallation. Sont explicitement préservés :

- base SQL `Pharmacy` ;
- instance SQL `PHARMACY` ;
- `%ProgramData%\LogicielPharmacie\config` ;
- `%ProgramData%\LogicielPharmacie\backups` ;
- bundles support.

Le désinstalleur n’exécute ni `DROP DATABASE`, ni suppression de configuration, ni désinstallation SQL/WebView2.

## Réinstallation / réparation

Relancer le même Setup pour réparer ou un Setup plus récent pour upgrader. La même `AppId` assure l’identité stable du produit. `pilot.json` n’est pas écrasé. L’orchestrateur se reconnecte à la base, applique uniquement les migrations nécessaires après backup, puis vérifie le schéma. Aucun Owner, rôle ou jeu de données métier n’est dupliqué.

Après réinstallation : version, `health`, connexion Owner, données historiques, backup et périphériques. Une suppression définitive de la base/configuration est une opération séparée, hors désinstalleur, nécessitant décision écrite et preuve de backup.
