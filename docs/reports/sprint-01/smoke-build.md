# Sprint 1 — Smoke build WPF/Blazor Hybrid

Date : 2026-08-02

## Environnement

- Windows 11 x64 (`10.0.26200`).
- SDK .NET `10.0.302`, MSBuild `18.6.11`.
- Runtimes .NET, ASP.NET Core et Windows Desktop `10.0.10`.
- Visual Studio et workloads additionnels absents ; non bloquants pour ce smoke build CLI.
- Source NuGet inchangée : `nuget.org` uniquement.

## Solution et projets

La solution native .NET 10 `Pharmacy.slnx` contient exactement :

- `Pharmacy.Domain` — `net10.0` ;
- `Pharmacy.Application` — `net10.0` ;
- `Pharmacy.Infrastructure` — `net10.0` ;
- `Pharmacy.Desktop` — `net10.0-windows10.0.17763.0` ;
- `Pharmacy.UnitTests` — `net10.0` ;
- `Pharmacy.IntegrationTests` — `net10.0`.

## Dépendances

- Application → Domain.
- Infrastructure → Application, Domain.
- Desktop → Application, Infrastructure.
- UnitTests → Domain, Application.
- IntegrationTests → Domain, Application, Infrastructure.
- Domain ne référence aucun autre projet `Pharmacy.*` ; aucun cycle détecté.

## Packages directs

| Projet | Package | Version | Rôle |
|---|---|---:|---|
| Pharmacy.Desktop | Microsoft.AspNetCore.Components.WebView.Wpf | 10.0.10 | Hébergement Blazor dans la fenêtre WPF native |
| Pharmacy.UnitTests | xunit.v3.mtp-v2 | 3.2.2 | xUnit.net v3 avec Microsoft Testing Platform v2 |
| Pharmacy.IntegrationTests | xunit.v3.mtp-v2 | 3.2.2 | xUnit.net v3 avec Microsoft Testing Platform v2 |

Le SDK a restauré implicitement `Microsoft.Windows.SDK.NET.Ref` 10.0.17763.57 pour la cible Windows minimale. Les composants MTP, xUnit, WebView2 et de télémétrie du runner apparaissent uniquement comme dépendances transitives des packages directs autorisés ; aucune télémétrie applicative n’est configurée. Aucun PackageReference supplémentaire n’a été ajouté au dépôt.

## Commandes principales

```powershell
dotnet restore .\Pharmacy.slnx
dotnet build .\Pharmacy.slnx -c Debug --no-restore
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet build .\Pharmacy.slnx -c Release --no-restore
dotnet test .\Pharmacy.slnx -c Release --no-build
dotnet run --project .\src\Pharmacy.Desktop\Pharmacy.Desktop.csproj -c Debug
```

## Résultats Debug

- Restore final : réussite, 0 avertissement, 0 erreur.
- Build : réussite, 0 avertissement, 0 erreur.
- Tests : 12 réussis, 0 échec, 0 ignoré.

## Résultats Release

- Build : réussite, 0 avertissement, 0 erreur.
- Tests : 12 réussis, 0 échec, 0 ignoré.

## Résultat du démarrage

- Processus `Pharmacy.Desktop` démarré et stable après trois secondes.
- Fenêtre WPF native détectée avec le titre `Logiciel Pharmacie` et un handle non nul.
- Six processus `msedgewebview2.exe` descendants détectés, confirmant l’initialisation technique de WebView2.
- Fermeture demandée via `CloseMainWindow()` et terminée normalement.

Ce contrôle est technique. Il ne constitue pas une validation visuelle humaine de la mise en page ni un clic manuel sur le bouton Razor.

## Validation manuelle restante

- Vérifier le rendu de la fenêtre et sa lisibilité à 1366 × 768.
- Vérifier les statuts, informations runtime et trois mentions de périmètre.
- Cliquer sur `Tester l’interactivité` et confirmer la mise à jour sans rechargement.
- Vérifier l’absence de débordement horizontal et de console d’erreur.
- Fournir une capture de la fenêtre pour la revue visuelle.
