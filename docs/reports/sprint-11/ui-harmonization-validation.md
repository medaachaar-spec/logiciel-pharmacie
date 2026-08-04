# Sprint 11 — Validation de l’harmonisation visuelle Stock

## Statut

`IMPLEMENTED — STOCK UI HARMONIZED — MANUAL VISUAL REVIEW REQUIRED`

## Périmètre

Cette correction modifie uniquement le rendu Desktop de la page Stock, sa feuille de styles et un test structurel associé. Aucun contrat Application, calcul de stock, lot, mouvement, inventaire, requête SQL, `DbContext`, seed, script ou migration n’a été modifié.

## Sources de vérité consultées

- `docs/ux/UI_FOUNDATIONS_V0.1.md` ;
- `docs/ux/APP_SHELL_V0.1.md` ;
- `docs/ux/CATALOG_MANAGEMENT_UI_V0.1.md` ;
- rapports Sprint 10 et Sprint 11 ;
- composants `UiCard`, `UiAlert`, `UiBadge`, `UiButton`, `UiEmptyState`, `UiSelect`, `UiSpinner` et `UiTextField` ;
- structure et styles de `CatalogPage.razor`, `catalog.css`, `InventoryPage.razor`, `inventory.css`, `tokens.css`, `components.css` et règles de contenu du shell.

## État Git préalable

- branche `main`, HEAD `58e7315` ;
- index vide ;
- aucun remote ;
- travail Sprint 11 toujours modifié ou non suivi, sans staging ;
- aucune commande `git add`, aucun commit, push, changement de branche ou ajout de remote effectué.

## Corrections réalisées

| Attente | Validation |
|---|---|
| Titre et densité | Titre ramené à `Stock SQL`, avec la même structure `page-header`, le même token de titre et le même rythme que Catalogue. |
| Quatre indicateurs | Quatre composants `UiCard` compacts, rayons et ombre du design system, grille 4 colonnes puis 2×2 sous 1200 px et une colonne sous 768 px. |
| Recherche, filtres et actions | Carte partagée `catalog-toolbar`, grille `catalog-filter-grid`, contrôles `Ui*` de hauteur standard et actions regroupées. |
| Résumé produit | Carte principale `catalog-panel`, en-tête `catalog-section-heading`, compteur de résultats et état vide `UiEmptyState`. |
| Tableau | Conteneur `catalog-table-scroll`, table `catalog-table`, sélection `catalog-row--selected`, boutons standard et défilement horizontal limité au tableau. |
| Détail | Carte toujours présente, état d’invitation avant sélection, badges de quantités, sous-panneaux cohérents et historique basé sur le tableau Catalogue. |
| Ajustement et inventaire | Surfaces, espacements, champs, confirmations et grilles harmonisés avec les tokens existants ; aucune logique de soumission modifiée. |
| SQL et ventes | Badge `SQL actif — ventes non persistées` conservé et avertissement visible précisant que les ventes ne consomment aucun stock. |
| Topbar et largeur | Flux normal sous la topbar, largeur `page-content` commune, `min-width: 0` sur les grilles et surfaces, aucun débordement horizontal global observé. |
| États et accessibilité | Loading dans une carte, erreurs via `UiAlert`, vides via `UiEmptyState`, régions de table focusables, labels et focus visible hérités du design system. |

## Contrôles de tokens et responsive

Les 26 propriétés personnalisées utilisées par `inventory.css` sont toutes déclarées dans `tokens.css`. Les anciens alias inexistants (`--radius-lg`, `--radius-md`, `--color-brand-*`, `--color-text-muted`, `--space-5`) ont été retirés. Aucune couleur hexadécimale locale ni nouveau token n’a été ajouté.

Breakpoints contrôlés :

- au-dessus de 1200 px : quatre cartes statistiques et grilles larges ;
- à 1200 px et moins : cartes statistiques et filtres en deux colonnes ;
- à 1024 px et moins : détail, ajustement et inventaire sur une colonne ;
- à 768 px et moins : cartes, filtres et en-têtes sur une colonne ;
- toutes les tables restent dans un conteneur local `overflow: auto`.

## Validation automatisée

Commandes exécutées avec une connexion SQL de test limitée au processus :

```text
dotnet build Pharmacy.slnx -c Debug
dotnet test Pharmacy.slnx -c Debug --no-build
dotnet build Pharmacy.slnx -c Release
dotnet test Pharmacy.slnx -c Release --no-build
```

Résultats :

- Debug : build réussi, 0 avertissement, 0 erreur ; 321/321 tests réussis ;
- Release : build réussi, 0 avertissement, 0 erreur ; 321/321 tests réussis ;
- aucun nouveau package ;
- test structurel ajouté pour les primitives Catalogue, les quatre cartes, les états, le scroll local, les breakpoints et l’absence de tokens obsolètes.

## Smoke runtime

Le binaire Desktop Debug a été lancé avec la base locale existante `Pharmacy_Development`. Le processus est resté répondant avec le titre `Logiciel Pharmacie`. La page Stock a chargé les données déjà présentes et une fiche produit a été ouverte en lecture seule ; aucune création, modification, finalisation ou autre opération d’écriture n’a été déclenchée.

Une inspection assistée à 1100 px a confirmé le haut de page, la grille 2×2 des indicateurs, la recherche, la liste, le détail, l’ajustement, FEFO et le début des inventaires sans débordement global. Une comparaison Catalogue/Stock à cette même largeur a aussi confirmé la largeur de contenu, le titre, les alertes et les surfaces partagées. Elle ne remplace pas la validation visuelle humaine multi-résolution.

## Revue visuelle manuelle requise

Merci de fournir ou valider au minimum, avec Stock et Catalogue à la même largeur :

1. une capture du haut de la page Stock ;
2. une capture du tableau et du panneau de détail ;
3. une capture d’un ajustement ou d’un inventaire, sans nécessairement confirmer l’opération ;
4. une capture comparative Stock/Catalogue à largeur identique.

La revue doit couvrir 1100×700, 1366×768 et 1920×1080, le parcours clavier, le focus visible, les états loading/vide/erreur et l’absence de contenu masqué par la topbar.

## Git final de la correction

Branche `main`, HEAD `58e7315`, index vide, aucun remote et aucun commit/push. Toutes les modifications restent visibles pour revue.
