# Runbook support pilote V1

## Triage initial

1. Noter heure, version, utilisateur/rôle, écran, opération et résultat attendu ; ne jamais demander mot de passe, code de récupération, donnée carte ou export client complet.
2. Classer : S1 perte/corruption/sécurité ; S2 vente/caisse/stock bloqué ; S3 fonction dégradée ; S4 cosmétique.
3. En S1/S2 : arrêter les écritures, ne pas réinstaller ni restaurer sans décision, préserver logs et backup.
4. Vérifier espace disque, service `MSSQL$PHARMACY`, WebView2, processus et `health`.

```powershell
$tool = "$env:ProgramFiles\Logiciel Pharmacie\deployment\Pharmacy.Deployment.exe"
$config = "$env:ProgramData\LogicielPharmacie\config\pilot.json"
& $tool health --config $config
& $tool support-bundle --config $config --output "$env:ProgramData\LogicielPharmacie\support"
```

Le bundle contient version, OS, architecture, nom de base et santé ; serveur/connexion sont masqués. Relire le JSON avant transfert.

## Pannes courantes

- Application refuse de démarrer : exécuter `health`; si migrations pending, suivre upgrade contrôlé, jamais migration au startup.
- SQL inaccessible : confirmer service, identité Windows et configuration ; ne pas basculer vers une base de secours improvisée.
- WebView2 absent : installer Evergreen depuis Microsoft puis relancer.
- Vente/ticket : vérifier l’historique SQL avant toute nouvelle finalisation ; réimprimer une vente persistée, ne pas recréer le paiement.
- Backup : vérifier droits du service SQL sur `%ProgramData%\LogicielPharmacie\backups`, espace, `RESTORE VERIFYONLY` et hash.
- Scanner : Notepad, suffixe Entrée, disposition clavier ; aucune installation driver propriétaire si HID standard.
- TPE : aucune donnée carte dans l’application ; réconcilier le ticket TPE externe avec la vente.

## Sauvegarde et restauration

Backup manuel depuis Operations avec Owner réauthentifié. Conserver hash et copie externe chiffrée. Restore-test sur base temporaire uniquement. Rollback de base pilote exclusivement selon `UPGRADE_ROLLBACK_V1.md`, avec validation métier et perte post-backup explicitée.

## Escalade

Transmettre : identifiant incident, version/hash, sévérité, timeline, étapes, résultat `health`, bundle relu, logs pertinents et référence backup. Exclure secrets, PII non nécessaires, fichier de base et backup brut. Clôturer avec cause, correction, tests et validation utilisateur.
