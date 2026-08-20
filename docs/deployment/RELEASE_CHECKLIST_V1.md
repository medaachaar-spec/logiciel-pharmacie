# Checklist Release Candidate V1

Cocher avec preuve datée ; une case critique vide signifie NO-GO.

## Build et artefacts

- [ ] version unique `1.0.0-rc.1` affichée dans l’application et l’outil ;
- [ ] Debug build/tests PASS ;
- [ ] Release build/tests PASS ;
- [ ] publish `win-x64` self-contained, non trimmed, non single-file ;
- [ ] Setup compilé, manifest et SHA-256 vérifiés ;
- [ ] package sans secret, PII, `.bak`, `.mdf`, logs, dump, certificat privé ;
- [ ] `CODE SIGNING GATE PENDING` accepté formellement ou binaire signé avec certificat réel.

## Base et exploitation

- [ ] SQL Express réel PASS ;
- [ ] fresh DB, 9 migrations, 0 pending/model changes ;
- [ ] aucun utilisateur/mot de passe/donnée métier seedé ;
- [ ] backup pré-upgrade + verify + hash ;
- [ ] rollback restauré sur cible isolée ;
- [ ] uninstall/reinstall préserve SQL/config ;
- [ ] support bundle expurgé ;
- [ ] planification backup et copie externe opérationnelles.

## Fonctionnel, sécurité, matériel

- [ ] E2E 1–9 PASS ;
- [ ] Owner bootstrap/récupération PASS ;
- [ ] RBAC et audit PASS ;
- [ ] erreurs SQL/printer sans faux succès ;
- [ ] scanner, imprimante 80 mm, TPE externe et UPS évalués ;
- [ ] UAT pharmacienne critique PASS ;
- [ ] runbook support et contacts remis.

Décision : [ ] GO PILOTE  [ ] NO-GO. Décideur : ______ Date : ______ Version/hash : ______ Motif : ______
