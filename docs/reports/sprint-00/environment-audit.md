# Sprint 0 — Audit de l’environnement

Date de l’audit : 2026-08-02  
Dépôt : `C:\Users\HP\Desktop\Logiciel pharmacie`  
Commande reproductible : `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-Environment.ps1`  
Classification corrigée au Sprint 0.1 : 2026-08-02  
Code de sortie observé : `1` — étape de fondation prête avec avertissements.

## Synthèse

L’initialisation du dépôt et l’audit du Sprint 0 sont implémentés. La correction 0.1 sépare désormais la disponibilité des fondations, de la persistance SQL et du poste pilote au lieu d’appliquer un verdict global unique.

| Niveau | Verdict | Motif principal |
|---|---|---|
| `FoundationReadiness` | **READY WITH WARNINGS** | Le SDK .NET 10, les runtimes, MSBuild et WebView2 permettent le Sprint 1. Windows Famille et l’absence de Visual Studio/workloads restent à surveiller jusqu’au smoke build réel. |
| `PersistenceReadiness` | **REMEDIATION REQUIRED** | SQL Server 2025 Express est requis avant le sprint de persistance ; l’édition Developer détectée ne remplace pas la cible validée. |
| `PilotProductionReadiness` | **REMEDIATION REQUIRED** | Windows 11 Pro x64 et SQL Server 2025 Express restent obligatoires avant certification du poste pilote. |

- `FoundationReadiness = READY WITH WARNINGS`
- `PersistenceReadiness = REMEDIATION REQUIRED`
- `PilotProductionReadiness = REMEDIATION REQUIRED`

La requête SQL locale de métadonnées réussit avec l’authentification Windows après une exception TLS cliente temporaire `-C`; la validation TLS standard échoue car le certificat local n’est pas approuvé. Ce point est différé au sprint de persistance : aucun `TrustServerCertificate` permanent et aucune désactivation globale de TLS ne sont autorisés.

Le prompt 0.1 indiquait environ 16,15 Go libres. La réexécution locale a détecté environ 32 Go, désormais au-dessus de la recommandation de 25 Go avant les sprints SQL, installation et sauvegarde. Le contrôle reste dynamique et repassera en `WARNING` sous 25 Go.

## État initial observé

- Le dossier contenait uniquement `README.md`, `MANIFEST.txt` et le package documentaire sous `docs/`.
- Les neuf sources Markdown/texte obligatoires étaient présentes.
- Les cinq PDF officiels étaient présents et lisibles.
- Aucun dossier `.git` n’existait.
- Aucun `.sln`, `.slnx`, `.csproj`, `.cs`, `.razor`, `.xaml` ou `.sql` applicatif n’existait.
- Aucune base métier, sauvegarde, donnée réelle ou donnée sensible n’a été créée ou lue.

## Résultats détaillés

| Composant | Exigence | Détecté | Statut | Bloquant | Preuve/commande | Action recommandée |
|---|---|---|---|---|---|---|
| Windows et architecture | Windows 11 x64 pour les fondations ; Pro obligatoire avant certification pilote | Windows 11 Famille Unilingue, version 10.0.26200, build 26200, x64 | WARNING | NON pour Sprint 1 | `Get-CimInstance Win32_OperatingSystem`, `Win32_ComputerSystem` | Continuer les fondations ; utiliser ou mettre à niveau vers Windows 11 Pro x64 avant certification du poste pilote. |
| PowerShell | Obligatoire — PowerShell 5.1 ou ultérieur | Windows PowerShell 5.1.26100.8972 ; PowerShell Core 7.6.3 | PASS | NON | `$PSVersionTable.PSVersion` | Aucune. |
| Espace disque | Recommandé — au moins 25 Go avant SQL, installation et sauvegarde ; plancher local 10 Go | Environ 32 Go libres sur `C:` lors de la réexécution 0.1 | PASS | NON | `Get-PSDrive C` | Maintenir au moins 25 Go libres avant les sprints concernés. |
| Git | Obligatoire | Git 2.55.0.windows.3 | PASS | NON | `git --version` | Aucune. |
| Dépôt Git | Obligatoire | Dépôt local initialisé | PASS | NON | `git rev-parse --is-inside-work-tree` | Aucune. |
| Branche | Obligatoire — branche connue, `main` pour un nouveau dépôt | `main` | PASS | NON | `git branch --show-current` | Aucune. |
| Documentation | Obligatoire | 10/10 documents attendus présents | PASS | NON | `Test-Path` sur la liste officielle | Aucune. |
| PDF de référence | Obligatoire | 5/5 PDF présents | PASS | NON | `Get-ChildItem docs\reference\*.pdf` | Aucune. |
| SDK .NET | Obligatoire | SDK `10.0.302` | PASS | NON | `dotnet --list-sdks` | Aucune. |
| SDK .NET 10 stable | Obligatoire | `10.0.302`, stable | PASS | NON | `dotnet --list-sdks` et exclusion des versions preview/RC | Aucune. |
| Runtimes .NET | Obligatoire — .NET, ASP.NET Core et Windows Desktop 10 | `Microsoft.NETCore.App 10.0.10`, `Microsoft.AspNetCore.App 10.0.10`, `Microsoft.WindowsDesktop.App 10.0.10` | PASS | NON | `dotnet --list-runtimes` | Aucune. |
| Visual Studio / Build Tools | Recommandé pour l’IDE ; `dotnet` CLI et MSBuild suffisent pour évaluer les fondations | Non détecté ; `vswhere` ne retourne que SSMS | WARNING | NON pour Sprint 1 | `vswhere -all -products * -format json` avec filtrage des produits IDE/Build Tools | Continuer avec `dotnet` CLI ; réévaluer uniquement si création, restore ou smoke build WPF/Blazor Hybrid échoue. |
| Workload .NET Desktop/WPF | Recommandé pour l’IDE ; preuve réelle par smoke build Sprint 1 | Non détecté | WARNING | NON pour Sprint 1 | `vswhere -requires Microsoft.VisualStudio.Workload.ManagedDesktop` | Réévaluer si le smoke build WPF/Blazor Hybrid l’exige. |
| Workload ASP.NET/web | Recommandé pour l’IDE ; preuve réelle par smoke build Sprint 1 | Non détecté | WARNING | NON pour Sprint 1 | `vswhere -requires Microsoft.VisualStudio.Workload.NetWeb` | Réévaluer si le smoke build WPF/Blazor Hybrid l’exige. |
| MSBuild | Obligatoire | `18.6.11.33009` via le SDK .NET | PASS | NON | `dotnet msbuild -version -nologo` | Aucune. |
| WebView2 Runtime | Obligatoire | `150.0.4078.105` | PASS | NON | Exécutable standard EdgeWebView et version du fichier | Aucune. |
| Services SQL Server | Différé pour les fondations ; requis pour persistance et pilote | `MSSQLSERVER`, Running, démarrage Auto | PASS | NON pour Sprint 1 | `Get-CimInstance Win32_Service` | Aucune modification de service pendant l’audit. |
| SQL Server 2025 Express | Différé pour les fondations ; obligatoire avant persistance SQL et production pilote | SQL Server 2025 `17.0.1125.2`, Standard Developer Edition 64 bits ; aucune édition Express | WARNING | NON pour Sprint 1 ; OUI avant persistance/pilote | Registre d’instances SQL et `SERVERPROPERTY` | Continuer les fondations ; installer SQL Server 2025 Express avant le sprint de persistance, sans créer de base pendant cet audit. |
| Requête locale SQL | Vérification SQL/TLS différée | Métadonnées accessibles par authentification Windows avec `sqlcmd -C`; validation TLS standard en échec | WARNING | NON pour Sprint 1 | Requête `SERVERPROPERTY` en lecture seule, délai 5 secondes | Traiter la confiance du certificat au sprint de persistance ; ne pas persister `TrustServerCertificate` et ne pas désactiver TLS globalement. |
| `sqlcmd` | Recommandé | Version 17.0.1000.7 | PASS | NON | `sqlcmd -?` | Aucune. |
| SSMS | Facultatif | SQL Server Management Studio 22, version d’installation 22.8.12009.203 | PASS | NON | `vswhere`, produit `Microsoft.VisualStudio.Product.Ssms` | Aucune. |
| `global.json` | Obligatoire quand un SDK .NET 10 stable existe | `10.0.302`, `latestPatch`, `allowPrerelease: false` | PASS | NON | `Get-Content global.json`, `dotnet --list-sdks` | Aucune. |
| Fichiers racine du Sprint 0 | Obligatoire | `.gitignore`, `.gitattributes`, `.editorconfig` et script présents | PASS | NON | `Test-Path` | Aucune. |
| Fichiers sensibles locaux | Obligatoire — aucun secret/base/backup exposé | Aucun nom de fichier sensible détecté ; aucun contenu sensible lu | PASS | NON | Scan des noms uniquement, `.git` exclu | Aucune. |
| Protections `.gitignore` | Obligatoire | 22/22 cas fictifs conformes : 16 ignorés, 6 visibles | PASS | NON | `git check-ignore --no-index --quiet` | Aucune. |
| État Git final | Obligatoire | `main`, zéro commit, zéro remote, working tree visible pour revue | PASS | NON | `git status --short`, `git rev-list --count --all`, `git remote` | Ne pas effectuer de `git add`, commit ou push avant validation explicite. |
| Artefacts applicatifs Sprint 0 | Obligatoire — aucun code ou solution | Aucun `.sln`, `.slnx`, `.csproj`, `.cs`, `.razor`, `.xaml` ou `.sql` | PASS | NON | Inventaire récursif par extension | Aucune. |

## Conclusion

Le dépôt est correctement préparé et **prêt pour le Sprint 1 de fondation avec avertissements**. Le smoke build du Sprint 1 devra fournir la preuve réelle de création, restauration et compilation WPF/Blazor Hybrid via `dotnet` CLI/MSBuild. La persistance SQL et la certification du poste pilote restent en remédiation. Aucun outil n’a été installé et aucun paramètre Windows, service, registre, pare-feu ou stratégie PowerShell n’a été modifié.
