# Sprint 0 — Rapport d’exécution Codex

## 1. Statut final

**IMPLEMENTED — FOUNDATION READY WITH WARNINGS**

Le périmètre autorisé du Sprint 0 est entièrement implémenté et vérifié. Après correction de classification au Sprint 0.1, le dépôt est prêt pour le Sprint 1 de fondation avec avertissements. La persistance SQL et la production pilote restent en remédiation.

- `FoundationReadiness = READY WITH WARNINGS`
- `PersistenceReadiness = REMEDIATION REQUIRED`
- `PilotProductionReadiness = REMEDIATION REQUIRED`

## 2. Résumé du travail

- Lecture des sources de vérité et consultation des deux PDF prioritaires.
- Inventaire initial du dossier, des fichiers cachés, de Git et des artefacts applicatifs.
- Initialisation locale de Git sur `main`, sans commit, `git add`, remote ni push.
- Audit non destructif de Windows, PowerShell, disque, Git, .NET, Visual Studio, MSBuild, WebView2 et SQL Server.
- Création des protections racine, de `global.json` et du script d’audit réexécutable.
- Exécution et correction du script sous Windows PowerShell 5.1.
- Validation non destructive de 22 chemins fictifs contre `.gitignore`.
- Création du rapport d’environnement et mise à jour de l’état courant.
- Confirmation qu’aucune solution, application, base métier ou donnée réelle n’a été créée.
- Correction Sprint 0.1 : séparation des verdicts fondation, persistance et production pilote sans modifier les ADR.

## 3. Documents consultés

1. `README.md`
2. `MANIFEST.txt`
3. `docs/governance/PROJECT_CONTEXT.md`
4. `docs/governance/DECISIONS_REGISTER.md`
5. `docs/governance/SCOPE.md`
6. `docs/governance/CURRENT_STATE.md`
7. `docs/governance/CODEX_WORKFLOW.md`
8. `docs/templates/CODEX_PROMPT_TEMPLATE.md`
9. `docs/templates/CODEX_REPORT_TEMPLATE.md`
10. `docs/reference/01_Dossier_ADR_Logiciel_Pharmacie_V1.0.pdf`
11. `docs/reference/05_Dossier_Phase_5_Code_First_Vibe_Coding_V0.1.pdf`

La présence des cinq PDF officiels de `docs/reference` a été vérifiée. Aucun PDF n’a été modifié.

## 4. État initial observé

- Racine : `C:\Users\HP\Desktop\Logiciel pharmacie`.
- Contenu initial : `README.md`, `MANIFEST.txt` et le package `docs/` de 12 fichiers.
- Dépôt Git : absent.
- Branche : inexistante avant initialisation.
- Code applicatif et solution : absents.
- Base métier du projet : absente et non créée.
- État initial des outils : inconnu avant l’audit.
- Aucun fichier `AGENTS.md` local n’était présent.

## 5. Fichiers créés

- `.gitignore`
- `.gitattributes`
- `.editorconfig`
- `global.json`
- `scripts/Verify-Environment.ps1`
- `docs/reports/sprint-00/environment-audit.md`
- `docs/reports/sprint-00/codex-report.md`

Le dossier interne `.git/` a également été créé par `git init -b main .`.

## 6. Fichiers modifiés

- `docs/governance/CURRENT_STATE.md` — mise à jour factuelle du statut du Sprint 0, de Git, des prérequis, des readiness et de la prochaine étape.
- Au Sprint 0.1 : `scripts/Verify-Environment.ps1`, `docs/reports/sprint-00/environment-audit.md` et le présent rapport ont été corrigés uniquement sur la classification et les messages associés.

Aucun autre document officiel, ADR, PDF ou modèle n’a été modifié.

## 7. Audit de l’environnement

Résultat automatisé après correction Sprint 0.1 : `PASS=20`, `WARNING=6`, `MISSING=0`, `BLOCKING=0`. Code de sortie pour l’étape courante de fondation : `1`.

Éléments conformes principaux : Git 2.55, branche `main`, SDK .NET `10.0.302`, runtimes .NET 10.0.10, MSBuild 18.6.11, WebView2 150.0.4078.105, `sqlcmd` 17.0.1000.7 et SSMS 22.

Windows Famille x64, l’absence de Visual Studio/workloads et l’absence de SQL Server Express sont des avertissements pour les fondations. Le SDK .NET 10, les runtimes et MSBuild constituent la preuve technique disponible avant le smoke build du Sprint 1.

SQL Server 2025 Express reste bloquant avant la persistance SQL. Windows 11 Pro x64 et SQL Server 2025 Express restent bloquants avant la certification du poste pilote.

Le détail et les actions recommandées sont dans `docs/reports/sprint-00/environment-audit.md`.

## 8. Initialisation Git

- Commande : `git init -b main .`.
- Résultat : dépôt vide initialisé localement sur `main`.
- Historique : aucun commit.
- Index : aucun `git add` exécuté.
- Remotes : aucun.
- Changement de branche : aucun après l’initialisation nécessaire.

## 9. Protections du dépôt

`.gitignore` protège les sorties `bin/` et `obj/`, Visual Studio, tests, couverture, logs, temporaires, `.env`, configurations locales sensibles, certificats, clés, fichiers SQL Server, backups, packages, publications, installateurs et diagnostics.

Les migrations EF Core futures, scripts SQL versionnés, documents Markdown, configurations non sensibles et données synthétiques restent visibles. Les 22 cas fictifs testés avec `git check-ignore --no-index` ont tous produit le résultat attendu.

`.gitattributes` normalise les textes, fixe les fins de ligne, prépare PowerShell en CRLF et marque PDF, images, archives, bases et installateurs comme binaires. Git LFS n’a pas été configuré.

`.editorconfig` impose UTF-8, une nouvelle ligne finale, les espaces finaux supprimés hors Markdown et des règles minimales pour PowerShell, JSON, XML, YAML et C# futur.

## 10. Commandes exécutées

Commandes principales, toutes locales et non destructives hors initialisation Git et création des fichiers autorisés :

```powershell
Get-Content -LiteralPath <document> -Raw
Get-ChildItem -Force
Get-ChildItem -Recurse
git --version
git init -b main .
git branch --show-current
git status --short --untracked-files=all
git rev-list --count --all
git remote
dotnet --version
dotnet --list-sdks
dotnet --list-runtimes
dotnet workload list
dotnet msbuild -version -nologo
vswhere -all -products * -format json
vswhere -requires Microsoft.VisualStudio.Workload.ManagedDesktop
vswhere -requires Microsoft.VisualStudio.Workload.NetWeb
Get-CimInstance Win32_OperatingSystem
Get-CimInstance Win32_ComputerSystem
Get-CimInstance Win32_Service
Get-PSDrive C
sqlcmd -?
sqlcmd -S . -E -l 5 -Q <requête SERVERPROPERTY en lecture seule>
sqlcmd -S . -E -C -l 5 -Q <requête SERVERPROPERTY en lecture seule>
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-Environment.ps1
git check-ignore --no-index --quiet -- <chemin fictif>
```

Le texte des deux PDF prioritaires a été extrait localement en lecture seule avec `pypdf`. Aucune commande réseau, installation ou élévation n’a été exécutée.

## 11. Résultats des vérifications

- Script PowerShell : syntaxe valide et exécution complète sous Windows PowerShell 5.1.
- Code de sortie : `1`, cohérent avec une fondation prête avec avertissements.
- `FoundationReadiness = READY WITH WARNINGS`.
- `PersistenceReadiness = REMEDIATION REQUIRED`.
- `PilotProductionReadiness = REMEDIATION REQUIRED`.
- Documentation : complète.
- PDF : 5/5 présents.
- `global.json` : version réellement installée, stable, reproductible.
- `.gitignore` : 22/22 cas de test conformes.
- Fichiers sensibles : aucun nom suspect détecté ; aucun contenu sensible lu.
- Artefacts applicatifs : zéro.
- Base ou backup du projet : zéro.
- Commit créé : zéro.
- Remote créé : zéro.
- Push : aucun.

## 12. Prérequis manquants ou incomplets

1. **Windows 11 Pro x64** : différé jusqu’à la certification du poste pilote ; Windows 11 Famille x64 ne bloque pas les fondations.
2. **Visual Studio ou Build Tools** : absent et non bloquant tant que `dotnet` CLI/MSBuild fonctionnent ; réévaluation au smoke build Sprint 1.
3. **Workloads .NET Desktop/WPF et ASP.NET/web** : absents et non bloquants pour Sprint 1 ; preuve réelle attendue par création, restore et compilation.
4. **SQL Server 2025 Express** : absent ; non bloquant pour les fondations, mais bloquant avant persistance SQL et production pilote.
5. **Confiance TLS SQL locale** : avertissement différé au sprint de persistance ; aucun `TrustServerCertificate` permanent ni désactivation globale de TLS.
6. **Espace disque** : environ 32 Go lors de la réexécution 0.1, au-dessus des 25 Go recommandés ; le prompt 0.1 rapportait auparavant environ 16,15 Go.

## 13. Problèmes rencontrés

- Deux premières commandes exploratoires PowerShell contenaient des constructions de pipeline invalides ; elles ont échoué au parseur avant toute action et ont été remplacées par des blocs indépendants.
- Le premier essai de lecture des PDF a rencontré l’encodage console CP1252 sur une puce typographique ; l’extraction a été relancée en UTF-8.
- Windows PowerShell 5.1 promouvait la sortie d’erreur native de `sqlcmd` en exception sous le mode global `Stop`. L’enveloppe native du script capture désormais sortie et code sans interrompre l’audit.
- Windows PowerShell 5.1 traite différemment `Get-ChildItem -LiteralPath -Include` et les tableaux JSON racine. Les deux cas ont été corrigés et retestés.
- Aucun défaut du script ne reste ouvert.

## 14. Écarts par rapport au prompt

Aucun écart d’exécution volontaire.

Une contradiction documentaire limitée a été détectée : le PDF de Phase 5 décrit SPRINT-0 comme l’initialisation du dépôt **et de la solution**, alors que le prompt actuel interdit explicitement toute solution ou tout projet .NET. Conformément à l’ordre de priorité, le prompt actuel a été appliqué : aucune `.sln`, `.slnx` ou `.csproj` n’a été créée. Cette contradiction ne bloque pas les autres livrables du Sprint 0.

Le statut utilisé dans ce rapport suit la liste spécifique imposée par le prompt actuel, prioritaire sur le modèle générique.

## 15. Risques et limites

- La conformité Windows 11 Pro doit être confirmée avant certification du poste pilote.
- L’absence de Visual Studio et des workloads reste un risque à réévaluer seulement si le smoke build WPF/Blazor Hybrid via `dotnet` CLI/MSBuild échoue.
- L’édition SQL Developer ne remplace pas SQL Server Express pour le sprint de persistance ni pour le projet pilote.
- La confiance TLS SQL locale doit être traitée proprement ; `-C` n’a été utilisé que pour la requête de métadonnées de ce processus.
- L’espace disque peut devenir insuffisant avec Visual Studio, SQL Express, packages et builds.
- L’audit ne prouve aucune compatibilité matérielle future et ne crée volontairement aucune base métier.
- Les fichiers du dépôt sont tous non suivis parce que le dépôt vient d’être initialisé et qu’aucun commit n’est autorisé.

## 16. Validation manuelle requise

Exécuter exactement depuis la racine :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-Environment.ps1
Get-Content .\docs\reports\sprint-00\environment-audit.md -Encoding UTF8
git status --short
git diff -- scripts\Verify-Environment.ps1 docs\governance\CURRENT_STATE.md docs\reports\sprint-00
```

Vérifier manuellement :

- qu’aucun `.sln` ou `.slnx` n’existe ;
- qu’aucun `.csproj` n’existe ;
- qu’aucun fichier `.cs`, `.razor`, `.xaml` ou `.sql` applicatif n’existe ;
- qu’aucune application n’a encore été développée ;
- qu’aucune base métier n’a été créée ;
- qu’aucun commit n’a été réalisé ;
- que `FoundationReadiness = READY WITH WARNINGS` ;
- que le script retourne `1` pour l’environnement courant ;
- que les verdicts persistance et production pilote restent `REMEDIATION REQUIRED` ;
- qu’aucune installation, aucun commit et aucun push n’ont été réalisés.

Ne pas effectuer de commit à cette étape.

## 17. État Git final

- Branche : `main`.
- Working tree : volontairement non propre pour revue ; tous les documents initiaux et fichiers Sprint 0 sont non suivis dans ce dépôt nouvellement initialisé.
- Fichiers Sprint 0 non suivis : `.editorconfig`, `.gitattributes`, `.gitignore`, `global.json`, `scripts/Verify-Environment.ps1`, `docs/reports/sprint-00/environment-audit.md` et `docs/reports/sprint-00/codex-report.md`.
- Fichier de gouvernance mis à jour mais encore non suivi : `docs/governance/CURRENT_STATE.md`.
- Autres fichiers non suivis : package documentaire initial décrit par `MANIFEST.txt`.
- Nombre de commits créés pendant le sprint : **0**.
- `git add` exécuté : **NON**.
- Push réalisé : **NON**.
- Remote ajouté : **NON** ; nombre final de remotes : **0**.

## 18. Recommandation

Démarrer le Sprint 1 de fondation selon son prompt dédié. Utiliser le smoke build réel pour confirmer création, restore et compilation WPF/Blazor Hybrid avec `dotnet` CLI/MSBuild. Ne pas démarrer le sprint de persistance avant disponibilité de SQL Server 2025 Express. Ne pas certifier le poste pilote avant Windows 11 Pro x64, SQL Server 2025 Express et traitement contrôlé du TLS local.
