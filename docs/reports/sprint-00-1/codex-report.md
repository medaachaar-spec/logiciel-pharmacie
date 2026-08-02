# Sprint 0.1 — Rapport de correction

## 1. Statut final

**IMPLEMENTED — FOUNDATION READY WITH WARNINGS**

- `FoundationReadiness = READY WITH WARNINGS`
- `PersistenceReadiness = REMEDIATION REQUIRED`
- `PilotProductionReadiness = REMEDIATION REQUIRED`

## 2. Résumé

La classification du Sprint 0 a été corrigée sans supprimer aucun contrôle technique. Le script distingue désormais la readiness des fondations, de la persistance SQL et du poste pilote. Son code de sortie dépend de l’étape courante de fondation : l’environnement actuel retourne `1`, car les fondations sont prêtes avec avertissements.

Aucune ADR, installation, configuration Windows, base de données, donnée réelle ou donnée sensible n’a été modifiée ou créée.

## 3. Documents consultés

1. `docs/governance/PROJECT_CONTEXT.md`
2. `docs/governance/DECISIONS_REGISTER.md`
3. `docs/governance/SCOPE.md`
4. `docs/governance/CURRENT_STATE.md`
5. `docs/governance/CODEX_WORKFLOW.md`
6. `docs/reports/sprint-00/environment-audit.md`
7. `docs/reports/sprint-00/codex-report.md`
8. `scripts/Verify-Environment.ps1`
9. `docs/reference/01_Dossier_ADR_Logiciel_Pharmacie_V1.0.pdf`
10. `docs/reference/05_Dossier_Phase_5_Code_First_Vibe_Coding_V0.1.pdf`

Les PDF ont été consultés en lecture seule. Aucune ADR ni référence PDF n’a été modifiée.

## 4. Classification précédente

Le script appliquait un verdict global unique :

- Windows 11 Famille x64 : `BLOCKING` ;
- Visual Studio absent : `BLOCKING` ;
- workloads Visual Studio absents : `BLOCKING` ;
- SQL Server 2025 Express absent : `BLOCKING` ;
- verdict final : environnement bloqué pour la création de la solution ;
- code de sortie : `2`.

Cette classification mélangeait les exigences immédiates des fondations avec celles, différées, de la persistance SQL et du poste pilote.

## 5. Classification corrigée

| Élément | Fondations | Persistance SQL | Production pilote |
|---|---|---|---|
| Windows 11 Famille x64 | WARNING, non bloquant | WARNING | BLOCKING jusqu’à Windows 11 Pro x64 |
| Visual Studio absent | WARNING, non bloquant tant que CLI/MSBuild fonctionnent | WARNING | Sans impact direct sur le poste publié |
| Workloads Visual Studio absents | WARNING ; preuve par smoke build Sprint 1 | WARNING | Sans impact direct sur le poste publié |
| SQL Server 2025 Express absent | WARNING/différé | BLOCKING | BLOCKING |
| Certificat SQL local | Sans impact sur les fondations | WARNING à traiter au sprint de persistance | WARNING avant validation finale |
| Espace disque | Seuil dynamique : PASS à environ 32 Go | Maintenir au moins 25 Go | Maintenir au moins 25 Go |

La mesure manuelle jointe indiquait environ 16,15 Go, ce qui aurait produit `WARNING`. La réexécution a détecté environ 32 Go, donc `PASS` avec le seuil recommandé de 25 Go.

## 6. Fichiers modifiés

- `scripts/Verify-Environment.ps1`
- `docs/reports/sprint-00/environment-audit.md`
- `docs/reports/sprint-00/codex-report.md`
- `docs/governance/CURRENT_STATE.md`

Fichier créé :

- `docs/reports/sprint-00-1/codex-report.md`

Aucun autre fichier n’a été modifié ou créé hors du périmètre autorisé.

## 7. Changements du script

- Conservation des 27 composants de contrôle existants, y compris le contrôle conditionnel d’erreur d’audit.
- Ajout des impacts `FoundationImpact`, `PersistenceImpact` et `PilotImpact` à chaque résultat.
- Ajout d’un calcul indépendant pour les trois readiness.
- Reclassification de Windows Famille, Visual Studio, workloads et SQL Express sans affaiblir les exigences finales.
- Seuil recommandé d’espace libre porté à 25 Go avant SQL, installation et sauvegarde ; plancher de sécurité de 10 Go conservé.
- Avertissement TLS différé au sprint de persistance, sans confiance permanente ni désactivation globale.
- Suppression du verdict global trompeur au profit des trois lignes explicites.
- Codes de sortie : `0` fondation prête sans avertissement, `1` fondation prête avec avertissements, `2` fondation réellement bloquée.

## 8. Commandes exécutées

```powershell
Get-Content -LiteralPath <source> -Raw
git branch --show-current
git status --short --untracked-files=all
git rev-list --count --all
git remote
Get-PSDrive C
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-Environment.ps1
pwsh -NoProfile -File .\scripts\Verify-Environment.ps1
[System.Management.Automation.Language.Parser]::ParseFile(...)
```

Les pages pertinentes des deux PDF ont été extraites localement avec `pypdf` en lecture seule. Aucune commande réseau, installation ou élévation n’a été exécutée.

## 9. Résultats

- Analyse syntaxique PowerShell 5.1 : 0 erreur.
- Analyse syntaxique PowerShell 7.6 : 0 erreur.
- Exécution Windows PowerShell 5.1 : code `1`.
- Exécution PowerShell 7.6 : code `1`.
- Résumé technique : `PASS=20`, `WARNING=6`, `MISSING=0`, `BLOCKING=0`.
- `FoundationReadiness = READY WITH WARNINGS`.
- `PersistenceReadiness = REMEDIATION REQUIRED`.
- `PilotProductionReadiness = REMEDIATION REQUIRED`.
- Branche : `main` ; commits : `0` ; remotes : `0`.
- Code applicatif, solution, projet ou SQL applicatif : `0` fichier.
- Installation, base, backup, secret, commit et push : aucun.

## 10. Validation manuelle

Exécuter depuis la racine :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-Environment.ps1
Get-Content .\docs\reports\sprint-00\environment-audit.md -Encoding UTF8
git status --short
git diff -- scripts\Verify-Environment.ps1 docs\governance\CURRENT_STATE.md docs\reports\sprint-00
```

Vérifier :

- `FoundationReadiness = READY WITH WARNINGS` ;
- aucun `.sln`, `.slnx`, `.csproj`, `.cs`, `.razor`, `.xaml` ou `.sql` applicatif ;
- aucune installation ;
- aucun commit ;
- aucun push.

Le dépôt n’a encore aucun commit et ses fichiers sont non suivis ; `git diff` peut donc rester vide. `git status --short` et la lecture directe des quatre fichiers constituent la revue effective à ce stade.

## 11. Écarts

Aucun écart volontaire par rapport au périmètre ou aux verdicts attendus.

Écart factuel documenté : le prompt indique environ 16,15 Go libres, tandis que la mesure de réexécution indique environ 32 Go. Le script ne fige pas cette valeur et applique dynamiquement la recommandation de 25 Go.

## 12. Risques différés

- Le smoke build WPF/Blazor Hybrid du Sprint 1 doit confirmer que `dotnet` CLI/MSBuild suffisent sur cette machine.
- Visual Studio et ses workloads devront être réévalués uniquement si création, restore ou compilation échoue.
- SQL Server 2025 Express doit être disponible avant le sprint de persistance.
- Windows 11 Pro x64 et SQL Server 2025 Express restent obligatoires avant certification du poste pilote.
- La confiance TLS SQL doit être traitée au sprint de persistance sans exception permanente ni affaiblissement global.
- Aucune readiness de production ne peut être déduite du seul succès de compilation futur.

## 13. État Git

- Branche : `main`.
- Working tree : volontairement non propre pour revue.
- Fichiers du dépôt : non suivis, aucun `git add` exécuté.
- Commits créés : **0**.
- Push réalisé : **NON**.
- Remote créé : **NON** ; nombre de remotes : **0**.
- Changement de branche : **NON**.

## 14. Recommandation

Autoriser le Sprint 1 de fondation selon son prompt dédié et exiger comme preuve un smoke build réel de création, restore et compilation WPF/Blazor Hybrid. Maintenir séparément les gates : aucune persistance SQL avant SQL Server 2025 Express, et aucune certification du poste pilote avant Windows 11 Pro x64, SQL Server 2025 Express et traitement contrôlé du TLS local.
