# Sprint 5 — Rapport d’exécution Codex

## 1. Statut final

**IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL CONTINUITY REVIEW REQUIRED**

## 2. Résumé

Le POS V0.1 a été étendu en POS V0.2 entièrement synthétique. Le parcours traite maintenant code inconnu, plafond de disponibilité, suspension/reprise/suppression, abandon du brouillon, paiement carte non réalisé ou ambigu, résolution manuelle et impression post-vente réussie/échouée avec réessai.

Toute la logique reste dans `Pharmacy.Desktop`, en mémoire du processus. Aucun package, projet, modèle Domain, persistance, réseau, donnée bancaire, stock réel, imprimante ou fichier de ticket n’a été introduit. Les 75 tests initiaux sont conservés et 40 tests Sprint 5 portent le total à 115.

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
16. `docs/ux/POS_V0.1.md`
17. `docs/reports/sprint-03/codex-report.md`
18. `docs/reports/sprint-04/codex-report.md`
19. `docs/reports/sprint-04/pos-validation.md`

Tout `src/Pharmacy.Desktop`, `tests/Pharmacy.UnitTests`, `tests/Pharmacy.IntegrationTests` et les tests POS ont été inspectés.

Les cinq PDF imposés ont été consultés en lecture seule. Les pages sur idempotence, matériel, suspension, TPE, impression post-commit, `PaymentAttempt`, `PrintJob`, clavier, anti-double-action et mode dégradé ont été extraites. Une page représentative de chaque PDF a été rendue en image temporaire et inspectée. Aucun PDF n’a été modifié.

## 4. État initial

- branche `main` ;
- working tree propre, aucun fichier stagé ;
- aucun remote ;
- `git diff --check` propre ;
- SDK .NET `10.0.302` ;
- six projets dans `Pharmacy.slnx` ;
- POS V0.1 Sprint 4 présent ;
- baseline Debug : build réussi, 0 avertissement, 0 erreur ; 75/75 tests réussis, 0 ignoré.

## 5. Dernier commit initial

`38dc3d0 feat: add synthetic POS nominal sales workflow`

Le hash a été lu avant modification et n’a pas été supposé.

## 6. Architecture des états

```text
DemoPosPage
├── incidents code/disponibilité
├── confirmation unique (suspension, abandon, reprise, suppression, TPE)
├── DemoSuspendedSalesPanel
├── DemoPaymentPanel
└── DemoPosSuccess / Ticket
        ↓
DemoPosSession (scoped)
├── brouillon / paiement / résultat
├── DemoSuspendedSaleStore (scoped, mémoire)
└── DemoTicketPrintService (scoped, mémoire)
```

`DemoSuspendedSale` stocke uniquement des snapshots immuables de lignes, quantités et heure locale. `DemoCardPaymentStatus` sépare cinq états explicites. `DemoTicketPrintService` est indépendant du résultat de vente et protège le démarrage concurrent d’une tentative. Les sources sont liées dans IntegrationTests sans ajouter de ProjectReference vers Desktop.

## 7. Fichiers créés

- `src/Pharmacy.Desktop/Components/Pos/DemoSuspendedSaleStore.cs`
- `src/Pharmacy.Desktop/Components/Pos/DemoTicketPrintService.cs`
- `src/Pharmacy.Desktop/Components/Pos/DemoSuspendedSalesPanel.razor`
- `tests/Pharmacy.IntegrationTests/DemoPosContinuityTests.cs`
- `docs/ux/POS_V0.2_CONTINUITY.md`
- `docs/reports/sprint-05/pos-continuity-validation.md`
- `docs/reports/sprint-05/codex-report.md`

## 8. Fichiers modifiés

- `README.md`
- `docs/governance/CURRENT_STATE.md`
- `src/Pharmacy.Desktop/App.xaml.cs`
- `src/Pharmacy.Desktop/Components/Pages/DemoPosPage.razor`
- `src/Pharmacy.Desktop/Components/Pos/DemoPosModels.cs`
- `src/Pharmacy.Desktop/Components/Pos/DemoCartPanel.razor`
- `src/Pharmacy.Desktop/Components/Pos/DemoPaymentPanel.razor`
- `src/Pharmacy.Desktop/Components/Pos/DemoPosSuccess.razor`
- `src/Pharmacy.Desktop/wwwroot/css/pos.css`
- `tests/Pharmacy.IntegrationTests/Pharmacy.IntegrationTests.csproj`

## 9. Code inconnu

Entrée sur un code exact inconnu ne crée aucune ligne et ne modifie ni catalogue, ni panier, ni total. L’incident adjacent affiche exactement `Code inconnu` et `Aucun article de démonstration ne correspond à ce code. Aucun produit n’a été créé.` Les deux actions autorisées nettoient la recherche et replacent le focus.

## 10. Disponibilité insuffisante

`AddItem` et `Increase` retournent `AvailabilityReached` sans mutation au plafond. L’alerte adjacente au panier affiche `Quantité indisponible dans le catalogue de démonstration.`, le nom de l’article, le maximum synthétique et la conservation du panier existant.

## 11. Suspension

`Suspendre la vente` est désactivé à vide, pendant une validation ou après succès. Une confirmation annonce que le paiement ne sera pas conservé et que la donnée disparaîtra à la fermeture. Le store génère `DEMO-HOLD-0001`, puis des références distinctes, avec heure locale et snapshots du panier. Le POS courant revient ensuite à zéro.

## 12. Reprise

Une reprise à panier vide restaure les lignes et retire la suspension. À panier non vide, `ResumeSuspended` retourne `CurrentCartNotEmpty` sans mutation ; l’interface impose un choix entre retour, suspension du panier courant ou abandon explicite. Les suppressions sont confirmées et isolées.

## 13. Abandon du brouillon

`Abandonner le brouillon` n’est disponible que pour un brouillon non vide hors traitement. La confirmation détaille panier et paiement supprimés. L’action remet panier/paiement à zéro, ne produit aucun `DemoCompletedSale`, conserve les autres suspensions et redonne le focus à la recherche.

## 14. Paiement carte ambigu

La machine expose `NotStarted`, `AwaitingConfirmation`, `Confirmed`, `NotCompleted` et `Ambiguous`. Après lancement de la vérification TPE simulée, les trois résultats sont explicites. L’ambiguïté bloque `CanComplete`, ne déclenche aucune seconde tentative et affiche le message de vérification imposé. Confirmer, déclarer non réalisé ou revenir au choix exige une confirmation supplémentaire. Aucun champ bancaire n’existe.

## 15. Impression simulée

Le bloc `Ticket` n’existe qu’après la vente terminée. Les états sont `NotRequested`, `InProgress`, `Printed` et `Failed`. En Debug, le choix succès/échec est étiqueté `Simulateur technique`. Le bouton imposé ne contacte aucun périphérique et ne génère aucun document.

## 16. Réimpression

Après échec, l’écran conserve `Vente terminée`, la référence, le total et `Ticket non imprimé`. Une nouvelle tentative utilise le même résultat de vente, incrémente le compteur et met à jour l’heure. Elle peut réussir ou échouer selon le simulateur. `Continuer sans ticket` ne touche pas à la vente.

## 17. Anti-double-action

- validation : verrou `completionInProgress` et bouton loading ;
- suspension/reprise/abandon : verrou local et opérations synchrones atomiques en mémoire ;
- suppression : la seconde opération ne retrouve plus la référence ;
- résolution TPE : transitions valides uniquement depuis `Ambiguous` ;
- impression/réimpression : `TryBegin` refuse `InProgress` et `Printed` ;
- composants `UiButton` : verrou interne pendant chaque callback.

## 18. Accessibilité

Les incidents sont placés près de la recherche ou du panier. Alertes, badges, champs, boutons et état vide réutilisent le design system. Les textes portent le sens sans dépendre de la couleur. La confirmation unique utilise `alertdialog`, titre accessible, focus programmatique et boutons `type=button`. Escape annule sans perte ; Entrée ne déclenche pas implicitement une action destructive. Les focus recherche/paiement/succès/confirmation sont replacés selon l’état.

## 19. Responsive

La grille large reste active à 1366 × 768 et 1920 × 1080. Sous 72 rem, donc à 1100 × 700, catalogue et panier sont empilés. Sous 768 px, confirmation, paiement et en-têtes secondaires s’empilent ; sous 576 px, actions et synthèses passent en pleine largeur. `minmax(0, 1fr)`, flex-wrap et les tokens existants préviennent le débordement horizontal.

## 20. Packages

Aucun PackageReference n’a été ajouté, supprimé ou modifié. La solution conserve trois références directes : WebView WPF dans Desktop et xUnit dans chacun des deux projets de tests. Aucun package de paiement, impression, UI, SQL ou réseau n’a été ajouté.

## 21. Tests ajoutés

Quarante tests déterministes dans `DemoPosContinuityTests.cs` couvrent exactement :

- 5 contrôles code inconnu/disponibilité ;
- 7 contrôles suspension ;
- 5 contrôles reprise/suppression ;
- 3 contrôles abandon ;
- 5 contrôles carte ambiguë/données bancaires/absence de relance ;
- 6 contrôles impression/réimpression/compteur/concurrence/reset ;
- 9 contrôles architecture, packages, routes et modules.

Ils appellent directement les machines d’états sans temporisation. Les 26 tests POS Sprint 4 et les 49 tests antérieurs restent présents.

## 22. Commandes exécutées

```powershell
git status --short
git branch --show-current
git log --oneline -n 6
git remote -v
git diff --check
dotnet --version
dotnet sln Pharmacy.slnx list
dotnet build Pharmacy.slnx -c Debug
dotnet test Pharmacy.slnx -c Debug --no-build
dotnet restore Pharmacy.slnx
dotnet build Pharmacy.slnx -c Debug --no-restore
dotnet test Pharmacy.slnx -c Debug --no-build
dotnet build Pharmacy.slnx -c Release --no-restore
dotnet test Pharmacy.slnx -c Release --no-build
dotnet run --project src/Pharmacy.Desktop/Pharmacy.Desktop.csproj -c Debug
git diff --cached --name-only
git diff --check
git diff --stat
git ls-files --others --exclude-standard
```

Des commandes PowerShell en lecture seule ont inspecté processus, titre, handle, état répondant et fermeture. Pypdf/Poppler ont servi à la consultation des PDF et `PrintWindow` à des captures techniques temporaires hors dépôt.

## 23. Résultats Debug

- restore : projets à jour ;
- build : réussite, 0 avertissement, 0 erreur ;
- tests : 115 réussis, 0 échec, 0 ignoré.

## 24. Résultats Release

- build : réussite, 0 avertissement, 0 erreur ;
- tests : 115 réussis, 0 échec, 0 ignoré ;
- simulateur technique d’issue d’impression masqué en Release par compilation conditionnelle.

## 25. Lancement desktop

La commande exacte demandée a été exécutée. Le processus `Pharmacy.Desktop` était répondant, avec fenêtre `Logiciel Pharmacie` et handle non nul. Le shell, le POS, l’incident Code inconnu, les confirmations suspension/abandon, une liste de plusieurs suspensions et le conflit de reprise ont été rendus dans la fenêtre compilée. Les vues TPE ambigu et Ticket sont compilées par Razor et leurs transitions sont couvertes par les tests automatisés.

La seconde exécution a été fermée avec `CloseMainWindow()` ; le runner s’est terminé avec le code 0 et aucun processus Desktop résiduel. Ces preuves sont techniques : aucune validation visuelle humaine n’est revendiquée.

## 26. Problèmes rencontrés

1. Le wrapper `pdftoppm.cmd` fourni par l’environnement pointait vers un chemin interne absent. L’exécutable Poppler livré a été appelé directement, en lecture seule, pour rendre les cinq pages.
2. Le premier `dotnet run` a dépassé la limite de 120 secondes du wrapper pendant les contrôles interactifs ; la fenêtre avait été fermée proprement. Un second lancement identique, avec limite étendue, a été fermé normalement et le runner a rendu le code 0.
3. L’automatisation souris/clavier du WebView est sensible au focus du bureau. Elle a suffi pour les écrans de continuité observés, mais ne remplace pas la checklist humaine TPE/impression.
4. La première capture du conflit de reprise à 1366 px a montré un texte trop comprimé et des actions trop larges. Le flex du dialogue a été corrigé avec largeurs flexibles, retour à la ligne et empilement sous 72 rem ; les builds et tests ont été repassés après correction.
5. Git signale localement que `pos.css` pourrait être converti de LF à CRLF lors d’une future opération Git. `git diff --check` reste propre et aucun réglage Git n’a été modifié.

## 27. Écarts

Aucun écart fonctionnel ou architectural volontaire. La recherche saisie n’est pas conservée dans une suspension : elle est traitée comme filtre transitoire non utile à la reprise, conformément à la clause « seulement si justifié ». La validation visuelle humaine des états TPE ambigu, impression et des trois résolutions reste volontairement hors des affirmations automatisées.

## 28. Risques et limites

- toutes les données disparaissent au redémarrage ;
- les disponibilités ne sont pas du stock ;
- aucune transaction ou idempotence persistante n’existe encore ;
- le TPE et l’impression sont des simulateurs sans matériel ;
- l’heure locale dépend du poste ;
- la qualité visuelle, le zoom Windows, Tab/Shift+Tab et les trois résolutions exigent une revue humaine ;
- les futurs modèles Domain ne doivent pas réutiliser implicitement ces types de démonstration.

## 29. Validation manuelle

Exécuter les commandes et la checklist complètes dans `pos-continuity-validation.md`. Fournir cinq captures : code inconnu, disponibilité insuffisante, ventes suspendues, carte ambiguë et vente terminée avec impression échouée. Confirmer aussi la reprise, une réimpression réussie après échec et la prévention du double clic.

## 30. État Git final

- branche : `main` ;
- dernier commit initial : `38dc3d0 feat: add synthetic POS nominal sales workflow` ;
- fichiers modifiés/non suivis : changements Sprint 5 visibles pour revue ;
- fichiers stagés : zéro ;
- nouveaux commits : zéro ;
- remotes : zéro ;
- push : absent ;
- changement de branche, réécriture et modification des PDF : aucun ;
- `git diff --check` : propre.

## 31. Recommandation

Le POS V0.2 est prêt pour la revue manuelle de continuité. La Gate doit rester ouverte jusqu’aux cinq captures, aux trois confirmations manuelles et au contrôle clavier/focus/responsive. Ne pas démarrer la persistance, le matériel réel ou un autre module métier avant le prochain prompt autorisé.
