# Sprint 3 — Validation technique du shell

Date : 2026-08-03  
Statut : `AUTOMATED CHECKS PASS — MANUAL SHELL REVIEW REQUIRED`

## Environnement

- Dépôt : `C:\Users\HP\Desktop\Logiciel pharmacie`.
- Branche : `main`.
- SDK : .NET `10.0.302`.
- Solution : `Pharmacy.slnx`, six projets.
- Remotes : aucun.
- État initial : propre, aucun fichier stagé, `git diff --check` sans erreur.
- Baseline Debug : build 0 avertissement/0 erreur, 22/22 tests.

## Dernier commit initial

`391dc3f feat: add code-first UI foundations and UI Lab`

Le hash a été lu avec Git avant toute modification.

## Structure du shell

```text
AppShellLayout
├── AppSidebar
│   └── catalogue typé + états disponible/prévu/verrouillé
├── AppTopBar
│   └── DevelopmentAccessPreview (Debug)
├── contenu routé
└── AppStatusBar
```

Le layout comprend un lien d’évitement et conserve les composants/tokens Sprint 2.

## Routes

- `/` et `/home` : accueil synthétique.
- `/module/{ModuleId}` : composant provisoire unique pour huit modules.
- `/diagnostic` : diagnostic Sprint 1.
- `/ui-lab` : UI Lab Sprint 2.
- route inconnue : vue NotFound claire.

## Catalogue

Le catalogue central contient neuf entrées produit : Accueil, Ventes, Catalogue, Stock, Achats et réceptions, Caisse, Clients et crédit, Rapports, Administration. Deux entrées techniques supplémentaires sont compilées dans le catalogue Debug : Diagnostic technique et UI Lab.

Chaque entrée porte identifiant, libellé, route, description, groupe, icône, ordre, état, visibilité de développement et nature produit/technique. Les routes sont locales et uniques.

## Profils techniques

- `Démo — accès complet` : toutes les pages navigables.
- `Démo — opérations` : rapports et administration verrouillés.
- `Démo — lecture` : accueil, catalogue, stock, rapports et outils techniques navigables ; opérations d’écriture présentées comme verrouillées.

Ces profils sont une prévisualisation locale non persistante. Ils ne représentent pas les rôles finaux, ne protègent aucune route et ne constituent pas une autorisation.

## Tests

- 22 tests Sprint 1/2 conservés.
- 27 tests Sprint 3 ajoutés.
- Total Debug : 49 réussis, 0 échec, 0 ignoré.
- Total Release : 49 réussis, 0 échec, 0 ignoré.

Les contrôles couvrent structure du shell, catalogue typé, identifiants/routes uniques, groupes, routes Razor, outils Debug, compact, verrouillage, topbar, statusbar, accueil synthétique, métriques, accès rapides, références de démonstration, états système, placeholder, profils, icônes, NotFound, Diagnostic/UI Lab, absence de persistance/serveur, CSS responsive et chemins de tests portables. Les contrôles Sprint 2 continuent de vérifier packages, URLs externes, graphe des projets et absence de type métier interdit.

## Builds

| Contrôle | Résultat |
|---|---|
| Restore solution | réussite |
| Build Debug | réussite, 0 avertissement, 0 erreur |
| Tests Debug | 49/49 |
| Build Release | réussite, 0 avertissement, 0 erreur |
| Tests Release | 49/49 |

## Lancement

Le Desktop Debug a été lancé avec `dotnet run --no-build`. La fenêtre `Logiciel Pharmacie` a été créée en 1100 × 700, est restée répondante et possédait un processus WebView2 descendant. L’accueil, le mode compact, le placeholder Ventes et le profil `Démo — lecture` ont été observés par captures techniques. Aucun message d’erreur Blazor n’était visible. La fermeture par `CloseMainWindow()` a réussi et aucun processus Desktop n’est resté actif.

Ces observations sont des preuves techniques assistées ; elles ne remplacent pas une validation visuelle humaine.

## Limites

- Aucun DOM WebView2 ou navigateur de test n’est instrumenté.
- Les routes sont vérifiées structurellement et les routes Accueil/Ventes ont été parcourues techniquement ; le parcours manuel de chaque module reste requis.
- Le contrôle technique est effectué à 1100 × 700 ; les cibles 1366 × 768 et 1920 × 1080 restent humaines.
- L’aperçu des accès n’est pas un mécanisme de sécurité.
- Les valeurs sont statiques et synthétiques ; aucune fonctionnalité métier, base ou périphérique n’est connecté.

## Revue manuelle restante

Vérifier le shell complet à 1366 × 768 et 1920 × 1080, ouvrir chaque placeholder, réduire la sidebar, parcourir au clavier, contrôler le focus, essayer les deux profils restreints, confirmer Diagnostic/UI Lab en Debug et vérifier l’absence de débordement horizontal ou de donnée réelle. Fournir au minimum les captures Accueil complet, sidebar réduite, placeholder et profil restreint, ainsi qu’un retour clavier et qualité visuelle.
