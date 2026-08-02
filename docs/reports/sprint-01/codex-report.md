# Sprint 1 — Rapport d’exécution Codex

## 1. Statut final

**IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL UI REVIEW REQUIRED**

## 2. Résumé du travail

La fondation .NET 10 a été créée sans fonctionnalité métier : solution SLNX, six projets, références descendantes contrôlées, hôte WPF avec Blazor Hybrid local, écran diagnostique interactif et douze tests d’architecture sous xUnit.net v3 avec Microsoft Testing Platform. Restore, builds Debug/Release, tests Debug/Release et lancement technique du Desktop ont réussi.

## 3. Documents consultés

Les quinze sources texte obligatoires ont été lues avant modification :

1. `README.md`
2. `MANIFEST.txt`
3. `global.json`
4. `.editorconfig`
5. `.gitattributes`
6. `.gitignore`
7. `docs/governance/PROJECT_CONTEXT.md`
8. `docs/governance/DECISIONS_REGISTER.md`
9. `docs/governance/SCOPE.md`
10. `docs/governance/CURRENT_STATE.md`
11. `docs/governance/CODEX_WORKFLOW.md`
12. `docs/templates/CODEX_PROMPT_TEMPLATE.md`
13. `docs/templates/CODEX_REPORT_TEMPLATE.md`
14. `docs/reports/sprint-00/codex-report.md`
15. `docs/reports/sprint-00-1/codex-report.md`

Les décisions pertinentes des trois PDF imposés ont également été consultées en lecture seule : ADR, conception UX/UI Phase 4 et plan Code First Phase 5. Aucun PDF ni ADR n’a été modifié.

## 4. État initial observé

- Racine : `C:\Users\HP\Desktop\Logiciel pharmacie`.
- Branche : `main`.
- Commit initial : `9b8da35 chore: initialise project governance and envuroment audit`.
- Working tree : propre.
- Remotes : zéro.
- SDK : .NET `10.0.302` ; runtimes 10.0.10 ; MSBuild 18.6.11.
- Modèles `sln`, `classlib`, `wpf` et `xunit` disponibles localement.
- Source NuGet : `nuget.org`, inchangée.
- Solution/projet applicatif préexistant : aucun.

## 5. Structure finale de la solution

```text
Pharmacy.slnx
Directory.Build.props
src/
├── Pharmacy.Domain/
├── Pharmacy.Application/
├── Pharmacy.Infrastructure/
└── Pharmacy.Desktop/
tests/
├── Pharmacy.UnitTests/
└── Pharmacy.IntegrationTests/
docs/reports/sprint-01/
├── smoke-build.md
└── codex-report.md
```

Un seul fichier solution existe : `Pharmacy.slnx`. Aucun fichier `.sln` parallèle n’a été créé.

## 6. Fichiers créés

- Racine : `Pharmacy.slnx`, `Directory.Build.props`.
- Domain : projet et `AssemblyMarker.cs`.
- Application : projet et `AssemblyMarker.cs`.
- Infrastructure : projet et `AssemblyMarker.cs`.
- Desktop : projet, `App.xaml`, `App.xaml.cs`, `AssemblyInfo.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `_Imports.razor`, `Components/RootComponent.razor`, `Components/DiagnosticScreen.razor`, `wwwroot/index.html`, `wwwroot/css/app.css`.
- Tests : deux projets et leurs fichiers `FoundationArchitectureTests.cs` / `FoundationIntegrationTests.cs`.
- Rapports : `docs/reports/sprint-01/smoke-build.md` et le présent fichier.

Tous les `Class1.cs` générés ont été supprimés. Les répertoires `bin` et `obj` sont ignorés par la configuration Git existante.

## 7. Fichiers modifiés

- `global.json` : ajout du runner `Microsoft.Testing.Platform`, SDK et politique de roll-forward préservés.
- `README.md` : ajout exclusif des commandes restore/build/test/run.
- `docs/governance/CURRENT_STATE.md` : état Sprint 1, résultats et prochaine revue.

`.gitignore`, les PDF, les ADR, les rapports historiques et le script d’environnement n’ont pas été modifiés.

## 8. Projets et frameworks

| Projet | Framework | Type |
|---|---|---|
| Pharmacy.Domain | net10.0 | Bibliothèque |
| Pharmacy.Application | net10.0 | Bibliothèque |
| Pharmacy.Infrastructure | net10.0 | Bibliothèque |
| Pharmacy.Desktop | net10.0-windows10.0.17763.0 | WinExe WPF, SDK Razor |
| Pharmacy.UnitTests | net10.0 | Exécutable de tests MTP |
| Pharmacy.IntegrationTests | net10.0 | Exécutable de tests MTP |

`Directory.Build.props` active Nullable, ImplicitUsings, le build déterministe, désactive la documentation XML et traite les avertissements comme des erreurs au niveau 4.

## 9. Graphe de dépendances

- Domain → aucune couche `Pharmacy.*`.
- Application → Domain.
- Infrastructure → Application, Domain.
- Desktop → Application, Infrastructure.
- UnitTests → Domain, Application.
- IntegrationTests → Domain, Application, Infrastructure.

Les références ont été contrôlées par `dotnet list ... reference` et par tests de réflexion. Aucun cycle ni référence interdite n’a été détecté.

## 10. Packages NuGet

| Nom | Version exacte | Projet consommateur | Justification |
|---|---:|---|---|
| Microsoft.AspNetCore.Components.WebView.Wpf | 10.0.10 | Pharmacy.Desktop | Héberger les composants Razor dans un `BlazorWebView` WPF local |
| xunit.v3.mtp-v2 | 3.2.2 | Pharmacy.UnitTests | xUnit.net v3 avec Microsoft Testing Platform v2 |
| xunit.v3.mtp-v2 | 3.2.2 | Pharmacy.IntegrationTests | xUnit.net v3 avec Microsoft Testing Platform v2 |

La version 3.2.2 est la dernière stable résolue ; les versions 4.0 disponibles sont des préversions et n’ont pas été utilisées. L’inventaire transitif confirme les composants attendus de WebView/DI, `Microsoft.Web.WebView2` 1.0.3179.45, xUnit v3 3.2.2 et Microsoft Testing Platform 2.0.2. `Microsoft.Testing.Extensions.Telemetry` 2.0.2 et `Microsoft.ApplicationInsights` 2.23.0 sont des dépendances transitives du runner de test explicitement autorisé ; elles ne sont ni référencées par Desktop, ni configurées, ni utilisées comme télémétrie applicative. Le SDK a restauré implicitement `Microsoft.Windows.SDK.NET.Ref` 10.0.17763.57 pour compiler le TFM Windows minimal. Aucun autre PackageReference direct, package preview ou runner VSTest historique n’a été ajouté.

## 11. Implémentation WPF/Blazor Hybrid

- Projet Desktop basé sur `Microsoft.NET.Sdk.Razor`, `WinExe`, WPF actif.
- `ServiceCollection` construite au démarrage, `AddWpfBlazorWebView()` enregistrée et provider fourni au contrôle.
- `AddBlazorWebViewDeveloperTools()` compilée uniquement en DEBUG.
- Fenêtre WPF native 1100 × 700, minimum 840 × 580, contenant le `BlazorWebView`.
- Gestion des exceptions Dispatcher : détail technique en DEBUG, message générique hors DEBUG.
- Ressources HTML/CSS intégralement locales et script `_framework/blazor.webview.js`.
- Aucun serveur HTTP, port, CDN ou ressource visuelle externe.

## 12. Écran technique créé

L’écran affiche le titre `Logiciel Pharmacie`, le sous-titre `Fondations techniques — Sprint 1`, les états WPF/Blazor Hybrid/WebView2, la version .NET, l’architecture, le système et la configuration Debug/Release. Il porte les trois mentions de limite imposées. Le bouton `Tester l’interactivité` incrémente un compteur Razor et met à jour une région `aria-live` sans rechargement.

Le CSS local utilise les polices système, un fond neutre, des cartes simples, des focus visibles, un contraste lisible, des grilles responsives et aucune largeur imposant un défilement horizontal.

## 13. Tests ajoutés

Six tests unitaires vérifient le chargement de Domain/Application, leurs références autorisées/interdites et l’existence des marqueurs. Six tests d’intégration technique vérifient le chargement et les références d’Infrastructure, l’absence de Desktop, l’absence de cycle et une liste volontairement bornée de noms d’entités métier interdites.

Total : 12 tests réels, aucun test vide, ignoré ou connecté à SQL Server.

## 14. Commandes exécutées

Contrôles initiaux et inventaires :

```powershell
git status --short
git branch --show-current
git log --oneline -n 1
git remote -v
dotnet --version
dotnet --info
dotnet new list
dotnet nuget list source
dotnet package search xunit.v3.mtp-v2 --exact-match --format json
dotnet sln .\Pharmacy.slnx list
dotnet list <projet> reference
```

Génération et validation :

```powershell
dotnet new sln -n Pharmacy -f slnx
dotnet new classlib ...
dotnet new wpf ...
dotnet sln .\Pharmacy.slnx add ...
dotnet restore .\Pharmacy.slnx
dotnet build .\Pharmacy.slnx -c Debug --no-restore
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet build .\Pharmacy.slnx -c Release --no-restore
dotnet test .\Pharmacy.slnx -c Release --no-build
dotnet run --project .\src\Pharmacy.Desktop\Pharmacy.Desktop.csproj -c Debug
```

## 15. Résultats restore/build/test

| Contrôle | Debug | Release |
|---|---|---|
| Restore solution | Réussite, 0 avertissement, 0 erreur | Actifs identiques réutilisés |
| Build | Réussite, 0 avertissement, 0 erreur | Réussite, 0 avertissement, 0 erreur |
| Tests | 12 réussis, 0 échec, 0 ignoré | 12 réussis, 0 échec, 0 ignoré |

Microsoft Testing Platform a exécuté directement les deux assemblies de tests en x64. Aucun fallback projet-par-projet n’a été nécessaire.

## 16. Résultat du lancement desktop

Le lancement Debug a produit le processus `Pharmacy.Desktop` (PID de contrôle 15392), stable après trois secondes. Le handle de fenêtre était non nul (`1441854`), le titre était `Logiciel Pharmacie` et six processus `msedgewebview2.exe` descendants ont confirmé l’initialisation technique du contrôle. `CloseMainWindow()` a été accepté et le processus s’est fermé normalement dans le délai prévu.

Il s’agit d’une preuve technique de démarrage, de fenêtre et de WebView2. Aucune validation visuelle humaine ni clic réel sur le bouton n’est revendiqué.

## 17. Problèmes rencontrés et corrections

1. La première restauration a dépassé le délai de 124 secondes pendant la récupération initiale des packages. Les seuls processus restore/MSBuild lancés par cette opération ont été arrêtés, puis la restauration a été relancée avec verbosité normale. Elle a réussi en 2 min 7 s ; le temps était concentré sur le téléchargement initial de `Microsoft.Windows.SDK.NET.Ref` 10.0.17763.57.
2. La valeur textuelle `latest` de `WarningLevel` n’était pas acceptée par la tâche C# du SDK. Elle a été corrigée en niveau numérique `4`, après quoi les builds ont produit zéro avertissement.
3. Le premier script PowerShell de contrôle de lancement contenait une apostrophe typographique interprétée comme délimiteur. Le parseur a échoué avant tout démarrage ; les messages ont été normalisés puis le contrôle a réussi.

## 18. Écarts par rapport au prompt

Aucun écart fonctionnel ou architectural volontaire. La restauration finale a ajouté `--disable-parallel --verbosity normal` uniquement pour rendre observable le téléchargement initial lent ; la commande de base au niveau SLNX a bien réussi. La revue visuelle reste volontairement non déclarée, conformément au prompt.

## 19. Risques et limites

- La mise en page, le contraste perçu, l’absence visuelle de débordement et l’usage à 1366 × 768 requièrent une observation humaine.
- Le bouton Razor est implémenté et compilé, mais son clic dans la fenêtre réelle reste à valider manuellement.
- Windows 11 Pro et SQL Server Express restent des gates différées du poste pilote/persistance, sans impact sur ce socle.
- Aucun comportement métier, stockage, journalisation structurée ou packaging n’est présent par conception.

## 20. Validation manuelle requise

Exécuter l’application en Debug, vérifier le titre/sous-titre et les trois statuts, puis cliquer plusieurs fois sur `Tester l’interactivité`. Confirmer la mise à jour du compteur sans rechargement, la lisibilité, le focus clavier, l’absence de débordement horizontal et de message d’erreur. Une capture de la fenêtre est demandée pour la revue visuelle.

## 21. État Git final

- Branche : `main`.
- Commit initial `9b8da35` : toujours présent et inchangé.
- Changements Sprint 1 : suivis modifiés et nouveaux fichiers non suivis, laissés visibles pour revue.
- Fichiers stagés : 0.
- Commits créés pendant le Sprint 1 : 0.
- Remotes : 0.
- Push : aucun.
- Changement de branche ou réécriture d’historique : aucun.

## 22. Recommandation

Le socle est prêt pour revue manuelle de l’interface. Valider la fenêtre et le bouton à partir des commandes et de la checklist fournies avant de clôturer le gate UX du Sprint 1. Ne démarrer aucun module métier ou travail de persistance avant un sprint dédié.
