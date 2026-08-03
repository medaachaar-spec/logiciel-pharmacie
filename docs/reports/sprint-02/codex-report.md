# Sprint 2 — Rapport d’exécution Codex

## 1. Statut final

**IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL VISUAL REVIEW REQUIRED**

## 2. Résumé

La première fondation visuelle code-first a été implémentée dans Pharmacy.Desktop sans modifier l’architecture ni introduire de fonctionnalité métier. Le résultat comprend cinq feuilles CSS organisées, des tokens centralisés, huit composants Razor réutilisables, une page UI Lab complète, une navigation technique conservant le diagnostic Sprint 1 et dix nouveaux tests. Les builds et 22 tests passent en Debug et Release.

## 3. Documents consultés

Sources texte lues avant modification :

1. `README.md`
2. `Directory.Build.props`
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
14. `docs/reports/sprint-01/codex-report.md`
15. `docs/reports/sprint-01/smoke-build.md`

Tout le contenu suivi de `src/Pharmacy.Desktop`, `tests/Pharmacy.UnitTests` et `tests/Pharmacy.IntegrationTests` a été inspecté. Les décisions pertinentes des PDF ADR V1.0, UX/UI Phase 4 V0.1 et Code-first Phase 5 V0.1 ont été extraites et rendues localement en lecture seule. Aucun PDF ni ADR n’a été modifié.

## 4. État initial

- Branche `main`, working tree propre, aucun fichier stagé, aucun remote.
- SDK .NET `10.0.302`.
- Une solution SLNX et six projets présents.
- Desktop : WPF, Blazor Hybrid, diagnostic Sprint 1 et un CSS monolithique.
- Tests : 12 tests existants.
- Baseline Debug : build réussi, 0 avertissement/0 erreur, 12/12 tests.
- Baseline Release : build réussi, 0 avertissement/0 erreur, 12/12 tests.

## 5. Dernier commit initial

`ab5c51e feat: establish .NET solution and Blazor Hybrid desktop foundation`

Le hash a été lu avec Git et n’a pas été supposé. `CURRENT_STATE.md` contenait encore une description antérieure du commit Sprint 1 ; cette information a été actualisée sans modifier l’historique.

## 6. Structure UI créée

```text
src/Pharmacy.Desktop/
├── Components/
│   ├── DevelopmentShell.razor
│   ├── Pages/UiLab.razor
│   └── Ui/
│       ├── UiAlert.razor
│       ├── UiBadge.razor
│       ├── UiButton.razor
│       ├── UiCard.razor
│       ├── UiComponentTypes.cs
│       ├── UiEmptyState.razor
│       ├── UiSelect.razor
│       ├── UiSpinner.razor
│       └── UiTextField.razor
└── wwwroot/css/
    ├── tokens.css
    ├── base.css
    ├── components.css
    ├── utilities.css
    └── app.css
```

## 7. Fichiers créés

- `src/Pharmacy.Desktop/Components/DevelopmentShell.razor`
- `src/Pharmacy.Desktop/Components/Pages/UiLab.razor`
- les neuf fichiers sous `Components/Ui/`, dont les huit composants et leurs types communs ;
- `wwwroot/css/tokens.css`, `base.css`, `components.css`, `utilities.css` ;
- `tests/Pharmacy.IntegrationTests/UiFoundationTests.cs` ;
- `docs/ux/UI_FOUNDATIONS_V0.1.md` ;
- `docs/reports/sprint-02/visual-foundation.md` ;
- `docs/reports/sprint-02/codex-report.md`.

## 8. Fichiers modifiés

- `src/Pharmacy.Desktop/_Imports.razor` : namespaces composants/routing.
- `src/Pharmacy.Desktop/Components/RootComponent.razor` : routeur et shell.
- `src/Pharmacy.Desktop/Components/DiagnosticScreen.razor` : route `/` et intégration au shell, contenu Sprint 1 conservé.
- `src/Pharmacy.Desktop/wwwroot/css/app.css` : composition et imports CSS locaux.
- `README.md` : accès Diagnostic/UI Lab.
- `docs/governance/CURRENT_STATE.md` : état Sprint 2 et commit réel.

## 9. Design tokens

`tokens.css` centralise :

- palette fonctionnelle teal/ardoise et surfaces sémantiques ;
- typographie système et niveaux de texte ;
- espacements 4, 8, 12, 16, 24, 32 et 48 px ;
- rayons, bordures et ombre légère ;
- hauteurs de contrôles 36, 42 et 48 px ;
- largeur maximale, gouttières et largeur minimale de carte ;
- transitions, z-index et anneau de focus.

Les autres feuilles consomment les variables et ne définissent aucune palette parallèle.

## 10. Composants Razor

- `UiButton` : Primary/Secondary/Tertiary/Danger, Small/Medium/Large, disabled, loading, type HTML, callback et verrou anti-double activation.
- `UiBadge` : Neutral/Information/Success/Warning/Danger avec symbole non dépendant de la couleur.
- `UiCard` : titre/sous-titre/footer facultatifs et deux densités.
- `UiAlert` : quatre variantes, indicateur textuel, rôles ARIA et fermeture facultative.
- `UiTextField` : identifiant explicite, label, binding, aide, erreur, required, disabled, readonly et types contrôlés.
- `UiSelect` : options techniques neutres, binding, placeholder, aide, erreur, disabled et required.
- `UiSpinner` : Small/Medium, label accessible et animation réduite.
- `UiEmptyState` : contenu générique, action facultative et illustration CSS locale.

## 11. UI Lab

La route `/ui-lab` affiche `UI Lab — Fondations visuelles V0.1`, clairement identifiée comme outil interne. Elle contient toutes les sections exigées : introduction, palette, typographie, boutons et états, badges, alertes, cartes, sept champs, quatre sélecteurs, chargements, état vide et grille responsive. Les valeurs visibles sont exclusivement synthétiques et neutres.

Deux interactions locales sont prévues : compteur de clic et simulation loading de 700 ms. Le composant désactive l’action pendant le callback et empêche une seconde activation.

## 12. Navigation technique

Le `Router` Blazor charge :

- `/` → diagnostic Sprint 1 ;
- `/ui-lab` → UI Lab.

`DevelopmentShell` fournit une barre sobre, deux `NavLink`, état actif, lien d’évitement et contenu principal. Aucun serveur HTTP, port, sidebar métier ou menu fonctionnel n’est ajouté.

## 13. Accessibilité

- HTML sémantique avec `nav`, `main`, `section`, `article`, `label`, `button`, `input` et `select`.
- Focus `:focus-visible` global et renforcé sur les contrôles.
- Navigation Tab native et focus du titre après changement de route.
- Labels liés par `for`/`id` ; aides et erreurs liées avec `aria-describedby`.
- `aria-invalid`, `aria-busy`, `aria-live`, `status` et `alert` selon l’état.
- Texte ou symbole pour chaque statut sémantique.
- Disabled lisible, loading annoncé et double activation bloquée.
- `prefers-reduced-motion: reduce` réduit animations et transitions.

## 14. Responsive

- Conteneur maximum et gouttières fluides.
- Grilles `auto-fit/minmax` et éléments `min-width: 0`.
- Breakpoints approximatifs 900 px et 700 px pour passer de quatre/trois colonnes à deux puis une.
- Retour à la ligne des textes longs et aucun composant à largeur fixe causant un débordement.
- Capture technique 1100 × 700 inspectée : diagnostic et UI Lab lisibles, aucune barre horizontale.
- Les observations humaines à 1366 × 768 et 1920 × 1080 restent obligatoires.

## 15. Packages NuGet

Aucun PackageReference n’a été ajouté, supprimé ou modifié pendant le Sprint 2.

Packages directs conservés :

- Desktop : `Microsoft.AspNetCore.Components.WebView.Wpf` 10.0.10 ;
- UnitTests et IntegrationTests : `xunit.v3.mtp-v2` 3.2.2.

Aucun package UI, CSS, icône, navigateur ou télémétrie applicative n’a été introduit.

## 16. Tests ajoutés

Dix tests techniques ont été ajoutés dans `UiFoundationTests.cs` :

1. présence des cinq fichiers CSS ;
2. présence des tokens essentiels ;
3. absence d’URL HTTP/HTTPS ;
4. absence de référence CDN ;
5. présence des huit composants ;
6. chargement de l’assembly Desktop ;
7. graphe exact des références de projets ;
8. absence de package UI externe ;
9. absence de type métier interdit dans les sources Sprint 2 ;
10. conservation des trois marqueurs d’architecture.

La racine est résolue par remontée jusqu’à `Pharmacy.slnx`, sans chemin machine absolu.

## 17. Commandes exécutées

```powershell
git status --short
git branch --show-current
git log --oneline -n 3
git diff --check
git remote -v
dotnet --version
dotnet sln .\Pharmacy.slnx list
dotnet build .\Pharmacy.slnx -c Debug
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet build .\Pharmacy.slnx -c Release --no-restore
dotnet test .\Pharmacy.slnx -c Release --no-build
dotnet restore .\Pharmacy.slnx
dotnet build .\Pharmacy.slnx -c Debug --no-restore
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet build .\Pharmacy.slnx -c Release --no-restore
dotnet test .\Pharmacy.slnx -c Release --no-build
dotnet run --project .\src\Pharmacy.Desktop\Pharmacy.Desktop.csproj -c Debug
```

Des contrôles PowerShell en lecture seule ont également inspecté les fichiers, packages, processus, fenêtre et captures temporaires.

## 18. Résultats Debug

- Restore : tous les projets à jour, réussite.
- Build final : réussite, 0 erreur, 0 avertissement.
- Tests : 22 réussis, 0 échec, 0 ignoré.
- Les deux assemblies de tests ont été exécutées par Microsoft Testing Platform en x64.

## 19. Résultats Release

- Build final : réussite, 0 erreur, 0 avertissement.
- Tests : 22 réussis, 0 échec, 0 ignoré.
- Aucun test n’a été désactivé ou contourné.

## 20. Résultat du lancement desktop

- Processus `Pharmacy.Desktop` stable.
- Fenêtre WPF `Logiciel Pharmacie` créée avec handle non nul en 1100 × 700.
- Processus WebView2 enfant détecté.
- Diagnostic Sprint 1 rendu avec la nouvelle navigation.
- Lien UI Lab activé par un clic local ciblé ; titre, état actif et palette de la route rendus.
- Défilement vertical de l’UI Lab vérifié sans débordement horizontal.
- Fermeture normale via `CloseMainWindow()` ; aucun processus Desktop laissé actif.

Ces preuves sont techniques et assistées par captures. Elles ne sont pas présentées comme validation visuelle humaine.

## 21. Problèmes rencontrés

1. Le wrapper `pdftoppm.cmd` fourni par le runtime pointait vers un sous-chemin Poppler absent. L’exécutable Poppler réellement fourni dans le même runtime a été utilisé directement, sans installation ni modification du système.
2. WebView2 n’a pas exposé son DOM HTML à UI Automation dans cette session. Le processus et la fenêtre restaient opérationnels. La navigation a été vérifiée par clic écran ciblé et capture ; l’automatisation détaillée du bouton n’est pas revendiquée.
3. Une tentative de défilement clavier est restée sur le diagnostic et a été écartée comme preuve. Une navigation et un défilement à la molette ultérieurs ont confirmé la route UI Lab.
4. `git diff --check` retourne le code 0 et aucune erreur d’espace, mais Git affiche un avis local indiquant que `app.css` passera de LF à CRLF lors d’une prochaine opération Git. Aucun réglage Git ni `.gitattributes` hors périmètre n’a été modifié.

## 22. Écarts par rapport au prompt

Aucun écart de périmètre ou d’architecture. L’absence d’invocation UI Automation du bouton est une limite de l’outil de contrôle, pas une fonctionnalité supprimée : le composant et son interaction sont compilés, testés structurellement et laissés à la validation manuelle explicitement demandée.

## 23. Risques et limites

- La qualité esthétique perçue, le contraste selon l’écran et le comportement exact à 1366 × 768/1920 × 1080 nécessitent l’utilisateur.
- La double activation et le loading doivent être essayés dans la fenêtre réelle malgré leur verrou logiciel.
- L’UI Lab est une fondation provisoire, pas le shell final de production.
- La compatibilité avec la mise à l’échelle Windows du poste pilote reste à tester.
- Aucun test de navigateur, bUnit, Playwright ou Selenium n’est présent par décision de sprint.

## 24. Validation manuelle requise

L’utilisateur doit ouvrir Diagnostic puis UI Lab, parcourir tous les contrôles avec Tab/Shift+Tab, activer les boutons avec Entrée, tester la simulation loading et le double clic, observer disabled/error/success, redimensionner la fenêtre et fournir les captures demandées. Le Gate visuel reste ouvert jusqu’à ce retour.

## 25. État Git final

- Branche : `main`.
- Dernier commit : `ab5c51e feat: establish .NET solution and Blazor Hybrid desktop foundation`.
- Changements : fichiers suivis modifiés et nouveaux fichiers Sprint 2 non suivis, tous visibles pour revue.
- Fichiers stagés : 0.
- Nouveau commit Sprint 2 : 0.
- Remotes : 0.
- Push : aucun.
- Changement de branche ou réécriture d’historique : aucun.

## 26. Recommandation

Le résultat est prêt pour la revue visuelle manuelle du Sprint 2. Valider les captures, le clavier, les états et les deux résolutions avant de prononcer le Gate. Ne pas démarrer un écran métier ou figer l’identité graphique commerciale avant cette validation.
