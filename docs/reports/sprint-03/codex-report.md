# Sprint 3 — Rapport d’exécution Codex

## 1. Statut final

**IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL SHELL REVIEW REQUIRED**

## 2. Résumé

Le shell principal V0.1 a été implémenté dans Pharmacy.Desktop sans fonctionnalité métier ni persistance. Il comprend un layout WPF/Blazor Hybrid routé, une sidebar réductible, une top bar contextuelle, une status bar locale, un catalogue de navigation typé, un accueil entièrement synthétique, un placeholder unique pour huit modules, trois profils techniques de prévisualisation, un composant d’icônes SVG locales et une vue NotFound. Les 22 tests existants restent verts ; 27 tests Sprint 3 portent le total à 49 en Debug et Release.

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
14. `docs/ux/UI_FOUNDATIONS_V0.1.md`
15. `docs/reports/sprint-01/codex-report.md`
16. `docs/reports/sprint-02/codex-report.md`
17. `docs/reports/sprint-02/visual-foundation.md`

Tout le contenu source suivi de `src/Pharmacy.Desktop`, `tests/Pharmacy.UnitTests` et `tests/Pharmacy.IntegrationTests` a été inspecté. Les quatre PDF imposés ont été consultés en lecture seule : ADR V1.0, modélisation fonctionnelle Phase 2 V0.1, UX/UI Phase 4 V0.1 et Code First Phase 5 V0.1. Les pages pertinentes sur la stack, la sécurité, le reporting, les modules, le shell, la navigation, l’administration et le découpage UI-1 ont été extraites, rendues en PNG temporaires et inspectées. Aucun PDF n’a été modifié.

## 4. État initial

- Branche `main`, working tree propre, aucun fichier stagé, aucun remote.
- SDK .NET `10.0.302`.
- Solution `Pharmacy.slnx` avec six projets.
- UI Sprint 2 : diagnostic sur `/`, UI Lab sur `/ui-lab`, barre de développement, cinq CSS locaux et huit composants génériques.
- Baseline Debug : build réussi, 0 avertissement, 0 erreur, 22/22 tests.
- Aucun module métier, type métier, serveur, base ou persistance.

## 5. Dernier commit initial

`391dc3f feat: add code-first UI foundations and UI Lab`

Le hash a été obtenu avec `git log` avant modification et n’a pas été supposé.

## 6. Structure du shell

```text
Components/
├── Layout/
│   ├── AppShellLayout.razor
│   ├── AppSidebar.razor
│   ├── AppTopBar.razor
│   ├── AppStatusBar.razor
│   └── DevelopmentAccessPreview.razor
├── Navigation/
│   ├── AppNavigationItem.cs
│   ├── AppNavigationCatalog.cs
│   └── DevelopmentAccessCatalog.cs
├── Pages/
│   ├── Home.razor
│   ├── HomePresentationModel.cs
│   ├── ModulePlaceholder.razor
│   └── NotFound.razor
└── Ui/UiIcon.razor
```

`RootComponent` utilise `RouteView` avec `AppShellLayout` comme layout par défaut et conserve `FocusOnNavigate`. Le shell assemble sidebar, topbar, contenu principal et statusbar avec un lien d’évitement.

## 7. Fichiers créés

- Cinq composants sous `Components/Layout/`.
- Trois fichiers sous `Components/Navigation/`.
- `Components/Pages/Home.razor` et `HomePresentationModel.cs`.
- `Components/Pages/ModulePlaceholder.razor`.
- `Components/Pages/NotFound.razor`.
- `Components/Ui/UiIcon.razor`.
- `wwwroot/css/shell.css`.
- `tests/Pharmacy.IntegrationTests/AppShellTests.cs`.
- `docs/ux/APP_SHELL_V0.1.md`.
- `docs/reports/sprint-03/shell-validation.md`.
- `docs/reports/sprint-03/codex-report.md`.

## 8. Fichiers modifiés

- `README.md` : démarrage, routes techniques et limites de l’aperçu des accès.
- `docs/governance/CURRENT_STATE.md` : état Sprint 3, commit réel et prochaine revue.
- `Components/RootComponent.razor` : layout routé et NotFound.
- `Components/DiagnosticScreen.razor` : route déplacée de `/` vers `/diagnostic` et titre focalisable.
- `Components/Ui/UiComponentTypes.cs` : noms d’icônes locales.
- `_Imports.razor` : namespaces Layout et Navigation.
- `wwwroot/css/tokens.css` : trois dimensions de shell.
- `wwwroot/css/app.css` : import local de `shell.css`.

## 9. Catalogue de navigation

`AppNavigationCatalog` centralise onze entrées en Debug : neuf produit et deux techniques. Chaque entrée définit identifiant stable, libellé français, route, description, groupe, icône, ordre, état initial, visibilité développement, nature produit/technique, identifiant de module et capacités futures lorsque nécessaire.

Groupes : Activité, Gestion, Pilotage, Configuration et Développement. Les identifiants et routes sont uniques. Les neuf entrées produit sont Accueil, Ventes, Catalogue, Stock, Achats et réceptions, Caisse, Clients et crédit, Rapports et Administration.

## 10. Routes

- `/` et `/home` : Accueil.
- `/module/sales` : Ventes.
- `/module/catalog` : Catalogue.
- `/module/inventory` : Stock.
- `/module/purchasing` : Achats et réceptions.
- `/module/cash` : Caisse.
- `/module/customers` : Clients et crédit.
- `/module/reporting` : Rapports.
- `/module/administration` : Administration.
- `/diagnostic` : diagnostic technique Sprint 1.
- `/ui-lab` : UI Lab Sprint 2.
- route inconnue : `NotFound` avec retour à l’accueil.

## 11. Sidebar

La sidebar contient monogramme local, nom du produit, groupes, liens, badges `Prévu`, état actif, états verrouillés, bouton de réduction et rappel `Aucune donnée réelle`. Le bouton publie `aria-controls` et `aria-expanded`. Les entrées verrouillées sont des boutons désactivés avec cadenas ; elles ne naviguent pas. En compact, les libellés visuels disparaissent, mais les contrôles conservent un nom accessible et un tooltip.

## 12. Top bar

La top bar calcule le titre et la description depuis la route courante. Elle montre `Mode local`, `Session de démonstration`, `Compte local de démonstration` et `Authentification non implémentée`. Aucun faux bouton de compte, d’alerte ou de verrouillage n’a été ajouté.

## 13. Status bar

La status bar affiche Local, Données de démonstration, Base de données non connectée, Debug/Release et `Shell : prêt pour revue`. Les états non configurés sont présentés comme contexte différé, pas comme erreur critique.

## 14. Accueil synthétique

L’accueil porte `Accueil`, `Vue d’ensemble de démonstration` et une alerte `Données entièrement synthétiques`. Le modèle central contient quatre métriques exactes : 24 opérations, 7 alertes, 4 lots et 3 actions. Les quatre accès rapides ouvrent uniquement des placeholders. La liste récente utilise exclusivement `DEMO-VENTE-0001`, `DEMO-RECEPTION-0001` et `DEMO-INVENTAIRE-0001`. L’état système précise application locale, base non connectée, périphériques non configurés et sauvegarde non configurée. Aucun graphique, tendance, pourcentage ou score n’est présent.

## 15. Pages provisoires

Un seul composant `ModulePlaceholder` traite les huit identifiants de module. Il affiche nom, description, badge `Prévu — non implémenté`, limites explicites, capacités futures issues du périmètre validé, retour accueil et action principale désactivée. Il ne charge aucune donnée et n’exécute aucune règle métier.

## 16. Aperçu technique des accès

Trois profils Debug locaux et non persistants sont disponibles : `Démo — accès complet`, `Démo — opérations`, `Démo — lecture`. Le profil complet ouvre toutes les pages. Les profils restreints transforment certaines entrées en boutons verrouillés. Le message exact `Profils de prévisualisation — ne représentent pas les rôles métier finaux` est toujours visible. Cet aperçu ne protège pas les routes et ne constitue ni authentification, ni autorisation, ni sécurité réelle.

## 17. Outils de développement

Diagnostic technique et UI Lab restent accessibles en Debug sous un groupe Développement distinct. Les deux entrées sont ajoutées au catalogue sous `#if DEBUG` et marquées techniques/développement. En Release, elles ne figurent pas dans le catalogue ; leurs routes compilées restent disponibles pour un diagnostic direct documenté.

## 18. Responsive

Le shell utilise une grille `minmax(0, 1fr)`, une sidebar de 15,5 rem, un mode compact de 4,75 rem, des grilles `auto-fit` et du retour à la ligne. La topbar et les panneaux se recomposent sous 70/58/44 rem. La sidebar possède son défilement vertical et le contenu conserve un défilement naturel. La capture technique 1100 × 700 ne montre aucun débordement horizontal ; les quatre métriques sont visibles en mode compact et trois avant le pli en mode étendu.

## 19. Accessibilité

- Éléments sémantiques `aside`, `nav`, `header`, `main`, `footer`, listes et définitions.
- Lien d’évitement et `FocusOnNavigate` vers le `h1` focalisable.
- Focus visible global, état actif avec `aria-current`, bordure et couleur.
- Bouton compact nommé avec état développé/réduit.
- Icônes décoratives SVG `aria-hidden` et contrôles iconiques nommés.
- Verrouillage non activable au clavier ou à la souris.
- Alertes, badges et statuts combinant texte, symbole et couleur.
- Reduced motion conservé et transition du shell neutralisée.

## 20. Packages NuGet

Aucun PackageReference n’a été ajouté, supprimé ou modifié. Desktop conserve uniquement `Microsoft.AspNetCore.Components.WebView.Wpf` 10.0.10. Les deux projets de tests conservent `xunit.v3.mtp-v2` 3.2.2. Aucun package UI, icône, navigateur, serveur, authentification ou persistance n’a été introduit.

## 21. Tests ajoutés

Vingt-sept tests structurels ont été ajoutés dans `AppShellTests.cs`. Ils contrôlent : composants du shell, quatre zones et skip link, catalogue typé, unicité, routes locales, neuf entrées produit, groupes, routes Razor, outils Debug, état compact, verrouillage, topbar, statusbar, synthèse de l’accueil, quatre métriques, quatre accès rapides, références génériques, états système, placeholder unique, profils et avertissement, icônes locales, NotFound, conservation Diagnostic/UI Lab, absence de persistance/serveur, CSS responsive/reduced motion, composition CSS et portabilité des chemins de tests.

Les dix tests Sprint 2 continuent de contrôler les packages, URLs/CDN, composants, graphe des projets et absence de type métier interdit. Aucun test n’a été ignoré ou désactivé.

## 22. Commandes exécutées

```powershell
git status --short
git branch --show-current
git log -n 4 --oneline --decorate
git remote -v
git diff --check
dotnet --version
dotnet sln .\Pharmacy.slnx list
dotnet build .\Pharmacy.slnx -c Debug
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet build .\src\Pharmacy.Desktop\Pharmacy.Desktop.csproj -c Debug --no-restore
dotnet build .\Pharmacy.slnx -c Debug --no-restore
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet restore .\Pharmacy.slnx
dotnet build .\Pharmacy.slnx -c Debug --no-restore
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet build .\Pharmacy.slnx -c Release --no-restore
dotnet test .\Pharmacy.slnx -c Release --no-build
dotnet run --project .\src\Pharmacy.Desktop\Pharmacy.Desktop.csproj -c Debug --no-build
git diff --stat
git diff --check
```

Des contrôles PowerShell en lecture seule ont également inspecté les fichiers, processus, fenêtre et WebView2. Pypdf et Poppler ont servi à extraire/rendre les PDF ; les PNG et captures techniques sont placés sous `tmp/`, ignoré par Git.

## 23. Résultats Debug

- Restore : tous les projets à jour.
- Build final : réussite, 0 avertissement, 0 erreur.
- Tests : 49 réussis, 0 échec, 0 ignoré.
- Assemblies de tests exécutées par Microsoft Testing Platform en x64.

## 24. Résultats Release

- Build final : réussite, 0 avertissement, 0 erreur.
- Tests : 49 réussis, 0 échec, 0 ignoré.
- Les entrées de navigation Développement sont exclues du catalogue par compilation conditionnelle.

## 25. Lancement desktop

- Processus `Pharmacy.Desktop` stable et répondant.
- Fenêtre WPF `Logiciel Pharmacie`, handle non nul, 1100 × 700.
- Processus WebView2 descendant détecté.
- Accueil initial rendu avec sidebar étendue.
- Mode compact rendu avec quatre métriques sur la largeur disponible.
- Placeholder Ventes rendu sans donnée ni exception visible.
- Profil `Démo — lecture` appliqué localement.
- Fermeture normale par `CloseMainWindow()` ; zéro processus Desktop restant.

Les captures ont été inspectées techniquement. Aucune validation visuelle humaine n’est revendiquée.

## 26. Problèmes rencontrés

1. L’extraction Pypdf a rencontré un caractère PDF non représentable par la sortie console Windows CP1252. La sortie a été relancée explicitement en UTF-8 sans modifier le document.
2. `git diff --check` retourne 0 et ne relève aucune erreur, mais Git affiche l’avis local déjà observé au Sprint 2 indiquant que `app.css` et `tokens.css` pourraient être convertis de LF à CRLF lors d’une future opération Git. Aucun réglage Git n’a été modifié.
3. La capture d’un état restreint en sidebar développée n’a pas été retenue comme preuve automatique ; l’état sélectionné et les entrées atténuées ont été observés en mode compact. La revue humaine étendue reste demandée.

## 27. Écarts par rapport au prompt

Aucun écart fonctionnel, architectural ou de périmètre volontaire. Le diagnostic a nécessairement quitté `/` pour `/diagnostic` afin de réserver la route initiale à l’accueil ; il reste accessible et documenté. Les routes techniques restent compilées en Release, mais leurs entrées sont masquées du catalogue comme autorisé. Les preuves de rendu restent qualifiées de techniques, conformément au prompt.

## 28. Risques et limites

- La qualité esthétique perçue, le contraste matériel et les cibles 1366 × 768/1920 × 1080 nécessitent une observation humaine.
- Le parcours clavier et les tooltips du mode compact nécessitent l’utilisateur.
- Les profils ne sont qu’un état de présentation et ne doivent jamais être réutilisés comme sécurité.
- La route actuellement ouverte reste affichée si un profil devient plus restrictif, ce qui souligne volontairement l’absence d’autorisation réelle.
- Les placeholders sont descriptifs ; aucune règle métier n’est validée par ce sprint.
- Aucun test bUnit, Playwright, Selenium ou UI Automation DOM n’est ajouté.
- Windows 11 Pro et SQL Server Express restent des gates différées du poste pilote/persistance.

## 29. Validation manuelle requise

L’utilisateur doit lancer le Desktop Debug, vérifier Accueil, sidebar, topbar, statusbar, ouvrir chaque module, réduire/déployer la sidebar, utiliser Tab/Shift+Tab/Entrée, confirmer le focus, changer les trois profils, ouvrir Diagnostic et UI Lab, redimensionner et contrôler l’absence de débordement horizontal ou de donnée réelle. Les captures minimales attendues sont : Accueil complet, sidebar réduite, placeholder module et profil restreint ; un retour clavier et un avis de qualité visuelle sont également requis.

## 30. État Git final

- Branche : `main`.
- Dernier commit : `391dc3f feat: add code-first UI foundations and UI Lab`.
- Fichiers modifiés/non suivis : changements Sprint 3 listés dans les sections 7 et 8, visibles pour revue.
- Fichiers stagés : 0.
- Nouveaux commits : 0.
- Remotes : 0.
- Push : aucun.
- Changement de branche ou réécriture d’historique : aucun.

## 31. Recommandation

Le shell V0.1 est prêt pour la revue manuelle. Ne pas démarrer un module métier, une authentification réelle ou la persistance avant retour sur les captures, le clavier, les profils et les résolutions cibles, puis décision de Gate explicite.
