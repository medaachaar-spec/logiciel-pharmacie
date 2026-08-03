# Sprint 2 — Visual Foundation

Date : 2026-08-03  
Version : `UI Foundations V0.1`  
Statut : provisoire, revue visuelle humaine requise

## Objectif

Traduire le wireframing validé en un design system code-first léger, exécutable dans WPF/Blazor Hybrid, sans écran métier, donnée réelle, serveur web ou ressource distante.

## Composants

- `UiButton` : quatre variantes, trois tailles, disabled, loading, type HTML et verrou anti-double activation.
- `UiBadge` : cinq variantes avec symbole et texte.
- `UiCard` : titre, sous-titre, contenu, footer et densités Comfortable/Compact.
- `UiAlert` : quatre variantes, rôles ARIA et fermeture facultative.
- `UiTextField` : label, binding, aide, erreur, required, disabled, readonly et types contrôlés.
- `UiSelect` : options génériques, binding, placeholder, aide, erreur, disabled et required.
- `UiSpinner` : tailles Small/Medium, label accessible et reduced motion.
- `UiEmptyState` : titre, description, action facultative et illustration CSS locale.

## Fichiers CSS

| Fichier | Responsabilité |
|---|---|
| `tokens.css` | Palette, typographie, espacements, rayons, ombres, contrôles, transitions, z-index et focus |
| `base.css` | Reset minimal, HTML global, typographie, focus et reduced motion |
| `components.css` | Navigation de développement et huit composants UI |
| `utilities.css` | Quelques primitives de stack, cluster et grille |
| `app.css` | Composition du diagnostic et de l’UI Lab, imports locaux et breakpoints |

## Routes et pages

- `/` : diagnostic technique Sprint 1 conservé.
- `/ui-lab` : `UI Lab — Fondations visuelles V0.1`.
- `DevelopmentShell` : barre sobre, lien d’évitement, navigation clavier et état actif.

L’UI Lab présente introduction, palette, typographie, boutons, badges, alertes, cartes, champs, sélecteurs, chargement, état vide et grille responsive.

## Builds

- Baseline Debug : réussite, 0 avertissement, 0 erreur.
- Baseline Release : réussite, 0 avertissement, 0 erreur.
- Final Debug : réussite, 0 avertissement, 0 erreur.
- Final Release : réussite, 0 avertissement, 0 erreur.

## Tests

- 12 tests Sprint 1 conservés.
- 10 tests Sprint 2 ajoutés.
- Debug : 22 réussis, 0 échec, 0 ignoré.
- Release : 22 réussis, 0 échec, 0 ignoré.

Les nouveaux contrôles vérifient les cinq feuilles CSS, les tokens, l’absence d’URL externe/CDN, les huit composants, le chargement Desktop, le graphe de projets, l’absence de package UI, l’absence de type métier et les marqueurs d’architecture.

## Lancement

- `dotnet run` Debug exécuté sur l’application réelle.
- Processus `Pharmacy.Desktop` stable et fenêtre `Logiciel Pharmacie` créée en 1100 × 700.
- WebView2 détecté comme processus enfant.
- Diagnostic affiché et conservé.
- Navigation vers UI Lab confirmée par clic local et rendu de la route.
- Application fermée normalement par `CloseMainWindow()`.

Des captures techniques temporaires, ignorées par Git, ont été inspectées pendant l’exécution. Elles ne remplacent pas la validation humaine.

## Limites

- WebView2 n’a pas exposé son DOM dans l’arbre UI Automation de cette session ; l’invocation automatisée des boutons n’a donc pas été utilisée comme preuve.
- Le contrôle visuel réalisé par Codex couvre la fenêtre 1100 × 700 et la navigation. Les cibles 1366 × 768 et 1920 × 1080 restent à observer sur le poste utilisateur.
- La simulation loading est implémentée, compilée et protégée contre la double activation ; son ressenti visuel reste manuel.
- Aucun écran métier, mode sombre, logo définitif ou shell de production n’est inclus.

## Validation visuelle restante

- Capturer l’UI Lab complet à une résolution cible.
- Activer la simulation loading et capturer son résultat.
- Tester Tab, Shift+Tab, Entrée et le focus visible.
- Vérifier disabled, error, alertes et labels.
- Redimensionner à 1366 × 768 puis 1920 × 1080.
- Confirmer absence de débordement horizontal et de ressource Internet.
