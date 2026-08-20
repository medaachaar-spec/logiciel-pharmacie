# Sprint 18 — validation manuelle

Version : `1.0.0-rc.1`. Statut initial : NON EXÉCUTÉ. Environnement obligatoire : poste/VM Windows 11 x64 dédié, SQL Server Express `PHARMACY`, périphériques pilotes. Conserver captures, logs et hashes sans PII.

## Installation

| Contrôle | Attendu | Résultat / preuve |
|---|---|---|
| Hash Setup | identique à `SHA256SUMS.txt` | ☐ PASS ☐ FAIL — |
| Fresh install | préflight + base + 9 migrations + health | ☐ PASS ☐ FAIL — |
| Premier Owner | aucun compte par défaut, récupération hors poste | ☐ PASS ☐ FAIL — |
| Repair | binaires réparés, données/config intactes | ☐ PASS ☐ FAIL — |
| Upgrade fixture | backup vérifié avant migration, données intactes | ☐ PASS ☐ FAIL — |
| Uninstall | app retirée, SQL/config/backups préservés | ☐ PASS ☐ FAIL — |
| Reinstall | reconnexion contrôlée, aucune duplication | ☐ PASS ☐ FAIL — |

## SQL Express et exploitation

| Contrôle | Attendu | Résultat / preuve |
|---|---|---|
| Édition | Express réel, service `MSSQL$PHARMACY` | ☐ PASS ☐ FAIL — |
| Modèle | 0 pending migration/model change | ☐ PASS ☐ FAIL — |
| Backup/verify/hash | fichier non vide et vérifié | ☐ PASS ☐ FAIL — |
| Restore-test | base isolée saine puis supprimée | ☐ PASS ☐ FAIL — |
| Rollback fixture | restauration + anciens binaires compatibles | ☐ PASS ☐ FAIL — |
| Résidus | aucune base temporaire/processus | ☐ PASS ☐ FAIL — |

## Fonctionnel, performance et résilience

Exécuter UAT-01 à UAT-14 dans `PILOT_UAT_PLAN_V1.md`. Mesurer démarrage froid/chaud, recherche catalogue, ajout panier et finalisation vente avec horodatage et données synthétiques. Tester SQL offline, printer offline et fermeture contrôlée ; aucune fausse réussite.

Résultats : UAT ___/14 PASS ; démarrage ___ s ; recherche ___ ms ; panier ___ ms ; vente ___ s.

## Hardware

- [ ] scanner USB HID + suffixe Entrée + clavier réel ;
- [ ] imprimante 80 mm : driver, accents/arabe, coupe, réimpression, offline ;
- [ ] TPE externe : accepté/refusé/annulé, aucune donnée carte ;
- [ ] UPS : branchement, autonomie ou justification, arrêt propre.

## Sécurité et support

- [ ] RBAC/audit/récupération ;
- [ ] package et bundle sans secret/PII ;
- [ ] certificat réel ou acceptation `CODE SIGNING GATE PENDING` ;
- [ ] runbook, rollback, backups et contacts remis.

Décision : ☐ GO PILOTE ☐ NO-GO. Anomalies S1/S2 ouvertes : ___. Pharmacienne : ___ Signature/date : ___. Responsable technique : ___ Signature/date : ___.
