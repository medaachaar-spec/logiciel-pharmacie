# Sprint 4 — Rapport d’exécution Codex

## 1. Statut final

**IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL POS REVIEW REQUIRED**

## 2. Résumé

Le placeholder Ventes a été remplacé par un POS V0.1 exécutable sur la route existante `/module/sales`. Le parcours fonctionne exclusivement dans `Pharmacy.Desktop` avec un catalogue fictif immuable et un `DemoPosSession` scoped en mémoire. Il couvre recherche et scan clavier simulé, panier, calculs `decimal`, paiement espèces, carte avec confirmation TPE opérateur, verrouillage de validation, succès synthétique et nouvelle vente.

Les couches Domain, Application et Infrastructure restent inchangées. Aucun package, serveur, réseau, base, fichier de données ou périphérique n’a été ajouté. Les 49 tests initiaux restent verts ; 26 tests Sprint 4 portent le total à 75 en Debug et Release.

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
15. `docs/ux/APP_SHELL_V0.1.md`
16. `docs/reports/sprint-01/codex-report.md`
17. `docs/reports/sprint-02/codex-report.md`
18. `docs/reports/sprint-03/codex-report.md`
19. `docs/reports/sprint-03/shell-validation.md`

Tout le contenu source de `src/Pharmacy.Desktop`, `tests/Pharmacy.UnitTests` et `tests/Pharmacy.IntegrationTests` a été inspecté.

Les cinq PDF imposés ont été consultés en lecture seule : ADR V1.0, modélisation fonctionnelle Phase 2 V0.1, modèle de données Phase 3 V0.1, UX/UI Phase 4 V0.1 et code-first Phase 5 V0.1. Les pages pertinentes sur WPF/Blazor Hybrid, `decimal`, scanner HID, TPE autonome, panier brouillon, ventes/paiements, navigation clavier, anti-double-validation, responsive et services de démonstration ont été extraites. Une page représentative de chaque PDF a été rendue en PNG temporaire et inspectée. Aucun PDF n’a été modifié.

## 4. État initial

- Branche `main`.
- Working tree propre, aucun fichier stagé.
- Aucun remote.
- `git diff --check` sans erreur.
- SDK .NET `10.0.302`.
- Solution `Pharmacy.slnx` avec six projets.
- Shell Sprint 3, navigation, UI Foundations, Diagnostic et UI Lab présents.
- Baseline Debug : build réussi, 0 avertissement, 0 erreur ; 49/49 tests réussis, 0 ignoré.

## 5. Dernier commit initial

`a160d0b feat: add application shell and synthetic dashboard`

Le hash a été lu avec `git log` avant modification et n’a pas été supposé.

## 6. Architecture du POS

```text
DemoPosPage (/module/sales)
├── recherche / scan clavier simulé
├── DemoCatalogPanel
├── DemoCartPanel
├── DemoPaymentPanel
└── DemoPosSuccess
        ↓
DemoPosSession (DI scoped, mémoire uniquement)
        ↓
DemoCatalog (IReadOnlyList immuable, 10 entrées fictives)
```

Les types `DemoCatalogItem`, `DemoCartLine`, `DemoPosSession`, `DemoPaymentMethod`, `DemoCompletedSale` et les enums associés portent explicitement leur statut de démonstration. Ils ne constituent pas le futur modèle Domain. `App.xaml.cs` enregistre uniquement `DemoPosSession` dans le conteneur Desktop.

Les tests compilent le fichier de modèles POS comme source liée dans IntegrationTests. Cette approche teste les mêmes calculs sans ajouter une référence de projet vers `Pharmacy.Desktop` et conserve exactement le graphe autorisé.

## 7. Fichiers créés

- `src/Pharmacy.Desktop/Components/Pages/DemoPosPage.razor`
- `src/Pharmacy.Desktop/Components/Pos/DemoPosModels.cs`
- `src/Pharmacy.Desktop/Components/Pos/DemoCatalogPanel.razor`
- `src/Pharmacy.Desktop/Components/Pos/DemoCartPanel.razor`
- `src/Pharmacy.Desktop/Components/Pos/DemoPaymentPanel.razor`
- `src/Pharmacy.Desktop/Components/Pos/DemoPosSuccess.razor`
- `src/Pharmacy.Desktop/wwwroot/css/pos.css`
- `tests/Pharmacy.IntegrationTests/DemoPosTests.cs`
- `docs/ux/POS_V0.1.md`
- `docs/reports/sprint-04/pos-validation.md`
- `docs/reports/sprint-04/codex-report.md`

## 8. Fichiers modifiés

- `README.md` : Ventes/POS, limites synthétiques et liens de documentation.
- `docs/governance/CURRENT_STATE.md` : commit réel, état Sprint 4 et prochaine revue.
- `src/Pharmacy.Desktop/App.xaml.cs` : enregistrement scoped de la session POS.
- `src/Pharmacy.Desktop/Components/Navigation/AppNavigationCatalog.cs` : Ventes disponible et description POS.
- `src/Pharmacy.Desktop/_Imports.razor` : namespace des composants POS.
- `src/Pharmacy.Desktop/wwwroot/css/app.css` : import local de `pos.css`.
- `tests/Pharmacy.IntegrationTests/Pharmacy.IntegrationTests.csproj` : source liée du modèle de démonstration, sans ProjectReference ni package.

## 9. Catalogue synthétique

| Code | Article | Présentation | Prix |
|---|---|---|---:|
| DEMO-000001 | Produit Démo A | Boîte 20 unités | 12,50 MAD |
| DEMO-000002 | Produit Démo B | Flacon 100 ml | 18,90 MAD |
| DEMO-000003 | Produit Démo C | Tube 50 g | 24,00 MAD |
| DEMO-000004 | Produit Démo D | Sachet 12 unités | 34,75 MAD |
| DEMO-000005 | Produit Démo E | Boîte 30 unités | 49,90 MAD |
| DEMO-000006 | Article Démo Hygiène A | Flacon 250 ml | 27,50 MAD |
| DEMO-000007 | Article Démo Parapharmacie A | Tube 75 ml | 39,90 MAD |
| DEMO-000008 | Produit Démo F | Boîte 10 unités | 15,25 MAD |
| DEMO-000009 | Article Démo Soin A | Flacon 50 ml | 55,00 MAD |
| DEMO-000010 | Article Démo Accessoire A | Unité | 8,75 MAD |

Les identifiants 1 à 10, codes `DEMO-*`, noms, catégories, prix et disponibilités sont uniques ou déterministes selon le cas. Aucun code EAN, produit, marque, pharmacie ou tiers réel n’est utilisé.

## 10. Recherche et scan simulé

Le champ `Rechercher ou scanner un code` filtre en mémoire code, nom, présentation et catégorie sans tenir compte de la casse. Il reçoit un focus initial raisonnable. Entrée n’ajoute que si la valeur correspond exactement à un code connu ; l’ajout vide ensuite le champ et redemande le focus.

Une valeur inconnue n’altère ni panier ni catalogue et affiche exactement `Aucun article de démonstration trouvé. Aucun produit n’a été créé.` Le bouton d’effacement et l’aide expliquent le comportement local sans réseau.

## 11. Panier

- Premier ajout : nouvelle ligne, quantité 1.
- Ajout répété : fusion et incrément de la ligne.
- Augmentation plafonnée par la disponibilité synthétique.
- Diminution depuis 1 bloquée ; suppression explicitement séparée.
- Sous-total, prix unitaire, quantité et code affichés par ligne.
- `Disponibilité synthétique atteinte` affiché et augmentation désactivée au plafond.
- État vide avec les deux textes imposés.
- `Vider le panier` désactivé à vide et protégé par une confirmation intégrée.
- `Passer au paiement` désactivé à vide.

## 12. Calculs

Tous les montants du catalogue, des lignes, de la session et du résultat utilisent `decimal` ou `decimal?`. Le sous-total vaut quantité × prix unitaire et le total vaut la somme des sous-totaux. L’affichage utilise deux décimales et `MAD` via `DemoMoney`.

Le POS ne calcule aucune taxe, remise, marge, PPV, PPH ou règle d’arrondi métier. Les quantités V0.1 restent des entiers strictement positifs.

## 13. Paiement espèces

Le panneau montre total, champ `Montant reçu`, aide, état d’erreur et monnaie. La saisie accepte virgule ou point, refuse une valeur négative/invalide et conserve un `decimal`. La confirmation reste désactivée tant que le reçu est inférieur au total. La monnaie vaut exactement reçu − total.

## 14. Paiement carte

Le seul mode carte est `Carte — TPE externe simulé`. L’alerte indique `TPE externe — aucune donnée de carte n’est saisie dans le logiciel`. La case `Paiement confirmé sur le TPE de démonstration` est obligatoire avant validation. Aucun champ bancaire sensible ou référence réelle n’existe.

## 15. Écran de succès

La session produit `DEMO-SALE-0001`, puis incrémente le compteur uniquement en mémoire. Le succès affiche total, moyen de paiement, monnaie espèces éventuelle, nombre d’articles/lignes et les rappels `Aucune donnée enregistrée`, `Aucun stock modifié`, `Aucun ticket imprimé`.

`Nouvelle vente` vide panier, recherche, paiement, succès, confirmation et messages. `Retour à l’accueil` réinitialise aussi la session courante avant navigation.

## 16. Accessibilité et clavier

- Labels explicites, `fieldset`/`legend`, boutons et inputs natifs.
- Recherche compatible texte + Entrée d’un futur scanner HID.
- `aria-describedby`, `aria-invalid`, régions `aria-live`, rôles status/alert.
- Noms accessibles détaillés sur les boutons de quantité et suppression.
- Focus demandé sur recherche, conteneur paiement et succès.
- Ordre DOM logique ; confirmation de vidage sans modale ni piège clavier.
- Disabled natif pour quantité, panier vide et paiement incomplet.
- Bouton de confirmation en loading et verrou de session contre le double déclenchement.
- Focus visible et reduced motion hérités des fondations locales.

## 17. Responsive

À largeur suffisante, `pos-layout` répartit catalogue et panier avec environ 62/38. La grille utilise `minmax(0, 1fr)` et des contenus compacts. Sous 1024 px, le panier passe avant le catalogue ; au paiement, le panneau principal reste avant le résumé. Des adaptations à 768 et 576 px empilent recherche, catalogue, actions et résumé.

Le smoke launch 1100 × 700 a rendu le POS sans débordement horizontal visible dans la capture technique. Les cibles 1366 × 768 et 1920 × 1080 restent à vérifier humainement.

## 18. Packages NuGet

Aucun PackageReference n’a été ajouté, supprimé ou modifié. `dotnet list Pharmacy.slnx package` confirme :

- Desktop : `Microsoft.AspNetCore.Components.WebView.Wpf` 10.0.10 ;
- UnitTests et IntegrationTests : `xunit.v3.mtp-v2` 3.2.2 ;
- aucun package direct pour Domain, Application ou Infrastructure.

## 19. Tests ajoutés

Vingt-six tests ont été ajoutés dans `DemoPosTests.cs`. Ils vérifient :

1. catalogue entre 8 et 12 entrées ;
2. codes uniques ;
3. identifiants uniques ;
4. prix/disponibilités positifs ;
5. propriétés financières `decimal` ;
6. création de ligne ;
7. fusion d’un ajout répété ;
8. incrément, sous-total et total ;
9. décrément sans zéro/négatif ;
10. suppression ;
11. total égal à la somme ;
12. panier vide à zéro ;
13. plafond synthétique ;
14. code exact connu ;
15. code inconnu sans création ;
16. recherche insensible à la casse ;
17. espèces insuffisantes refusées ;
18. monnaie exacte ;
19. confirmation TPE obligatoire ;
20. deuxième validation bloquée ;
21. résultat synthétique en mémoire ;
22. réinitialisation complète ;
23. absence de persistance/réseau dans les sources POS ;
24. route Ventes dédiée ;
25. autres modules inchangés ;
26. Diagnostic/UI Lab conservés.

Les tests Sprint 1 à 3 continuent de vérifier URLs/CDN, packages, six projets, graphe de ProjectReference, outils techniques, routes et absence d’infrastructure interdite. Aucun test n’a été désactivé ou ignoré.

## 20. Commandes exécutées

```powershell
git status --short
git branch --show-current
git log --oneline -n 5
git remote -v
git diff --check
dotnet --version
dotnet sln Pharmacy.slnx list
dotnet build Pharmacy.slnx -c Debug
dotnet test Pharmacy.slnx -c Debug --no-build
dotnet build Pharmacy.slnx -c Debug --no-restore
dotnet test Pharmacy.slnx -c Debug --no-build
dotnet restore Pharmacy.slnx
dotnet build Pharmacy.slnx -c Debug --no-restore
dotnet test Pharmacy.slnx -c Debug --no-build
dotnet build Pharmacy.slnx -c Release --no-restore
dotnet test Pharmacy.slnx -c Release --no-build
dotnet run --project src/Pharmacy.Desktop/Pharmacy.Desktop.csproj -c Debug
dotnet list Pharmacy.slnx package
git diff --cached --name-only
git diff --check
git diff --stat
git ls-files --others --exclude-standard
```

Des commandes PowerShell en lecture seule ont aussi inspecté processus, handle de fenêtre, titre, état répondant et enfant WebView2. Des captures techniques temporaires ont été générées sous `tmp/`, répertoire ignoré par Git. Pypdf et Poppler ont servi uniquement à consulter les PDF.

## 21. Résultats Debug

- Restore : tous les projets à jour.
- Build final : réussite, 0 avertissement, 0 erreur.
- Tests : 75 réussis, 0 échec, 0 ignoré.
- Les assemblies UnitTests et IntegrationTests ont été exécutées sous Microsoft Testing Platform x64.

## 22. Résultats Release

- Build final : réussite, 0 avertissement, 0 erreur.
- Tests : 75 réussis, 0 échec, 0 ignoré.
- Aucun package ou référence de projet supplémentaire.

## 23. Lancement desktop

Le commandement exact `dotnet run --project src/Pharmacy.Desktop/Pharmacy.Desktop.csproj -c Debug` a été exécuté.

Observations techniques :

- processus `Pharmacy.Desktop` répondant ;
- fenêtre WPF `Logiciel Pharmacie`, handle non nul, 1100 × 700 ;
- un processus WebView2 enfant direct ;
- accueil et shell rendus ;
- navigation Ventes effectuée ;
- titre `Nouvelle vente`, bandeau de démonstration, recherche et panier vide rendus ;
- saisie clavier `DEMO-000001` + Entrée : une ligne, un article, total 12,50 MAD et confirmation locale ;
- aucun message d’exception visible dans les captures techniques ;
- fermeture par `CloseMainWindow()` réussie, aucun processus Desktop restant ;
- second lancement court terminé avec code runner 0.

Ces constats sont des preuves techniques assistées. Aucune validation visuelle humaine n’est revendiquée.

## 24. Problèmes rencontrés

1. Le premier nouveau test Diagnostic/UI Lab visait `Components/Pages/Diagnostic.razor`, alors que le dépôt utilise `Components/DiagnosticScreen.razor`. Le test a été corrigé ; le code applicatif n’était pas en cause. La passe suivante a donné 75/75.
2. La première commande de lancement a atteint la limite du wrapper après 120 s parce que les captures et contrôles ont duré au-delà de la fenêtre de surveillance. Le processus applicatif avait été fermé sans résidu. Un second lancement identique, contrôlé et fermé immédiatement, est revenu avec code 0.
3. Git affiche l’avis local déjà connu indiquant que `app.css` pourrait être converti de LF à CRLF lors d’une future opération Git. `git diff --check` reste propre et aucun réglage Git n’a été modifié.
4. L’extraction initiale d’un PDF a rencontré un caractère non représentable en CP1252 ; la lecture a été relancée en UTF-8 sans modifier le PDF.

## 25. Écarts

Aucun écart fonctionnel, architectural ou de périmètre volontaire. Le paiement carte nominal demandé a été inclus avec confirmation manuelle TPE, tout en restant purement synthétique. La validation visuelle humaine n’est pas remplacée par les captures techniques.

## 26. Risques et limites

- La qualité visuelle, le contraste réel, le zoom Windows et les résolutions 1366 × 768/1920 × 1080 requièrent l’utilisateur.
- Le parcours clavier complet, Tab/Shift+Tab et le focus après chaque étape nécessitent une revue manuelle.
- Les données et références disparaissent à l’arrêt ; elles ne préfigurent pas les numéros métier finaux.
- Les disponibilités sont des plafonds de démonstration, pas du stock.
- Le TPE est un état d’interface sans intégration ni donnée bancaire.
- Aucun test DOM bUnit, UI Automation complet ou matériel n’a été ajouté.
- Les règles avancées restent explicitement hors périmètre.

## 27. Validation manuelle requise

Exécuter :

```powershell
cd "C:\Users\HP\Desktop\Logiciel pharmacie"
dotnet build .\Pharmacy.slnx -c Debug
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet run --project .\src\Pharmacy.Desktop\Pharmacy.Desktop.csproj -c Debug
git status --short
git diff --stat
```

Vérifier :

- [ ] le POS s’ouvre depuis Ventes ;
- [ ] le bandeau de démonstration est visible ;
- [ ] la recherche fonctionne ;
- [ ] un code exact suivi d’Entrée ajoute l’article ;
- [ ] un code inconnu ne crée rien ;
- [ ] ajouter deux fois fusionne la ligne ;
- [ ] les quantités et sous-totaux sont corrects ;
- [ ] la limite synthétique est respectée ;
- [ ] la suppression fonctionne ;
- [ ] vider le panier demande confirmation ;
- [ ] paiement espèces refuse un montant insuffisant ;
- [ ] la monnaie est correcte ;
- [ ] paiement carte exige la confirmation TPE ;
- [ ] double validation impossible ;
- [ ] succès affiché ;
- [ ] nouvelle vente réinitialise tout ;
- [ ] navigation clavier correcte ;
- [ ] focus visible ;
- [ ] aucun débordement horizontal ;
- [ ] aucune base ou donnée réelle ;
- [ ] aucun message d’exception.

Fournir au minimum quatre captures : POS vide, POS avec deux lignes, espèces avec monnaie, succès. Fournir aussi le résultat manuel d’un paiement carte simulé avant/après confirmation TPE.

## 28. État Git final

- Branche : `main`.
- Dernier commit : `a160d0b feat: add application shell and synthetic dashboard`.
- Working tree : changements Sprint 4 modifiés/non suivis, visibles pour revue.
- Fichiers stagés : 0.
- Remotes : 0.
- Nouveaux commits : 0.
- Push : aucun.
- Changement de branche, remote ou réécriture : aucun.
- PDF modifiés : 0.
- `git diff --check` : propre.

## 29. Recommandation

Le POS V0.1 est prêt pour la revue manuelle. La Gate finale doit rester ouverte jusqu’aux quatre captures, au test carte simulé, au contrôle clavier/focus et aux trois résolutions cibles. Ne pas démarrer le POS avancé, une architecture métier définitive ou la persistance avant décision explicite sur cette revue.
