# Gestion du Catalogue UI V0.1

Date : 2026-08-03

Statut : `IMPLEMENTED — MANUAL CATALOG REVIEW REQUIRED`

## Route et objectif

La route `/module/catalog` remplace le placeholder Catalogue par une page Blazor Hybrid fonctionnelle connectée aux handlers Application.

## Liste

La page propose :

- recherche par nom, présentation, référence ou barcode ;
- filtre tous/actifs/inactifs ;
- filtre catégorie ;
- liste limitée à 50 éléments ;
- état loading, état vide et erreur SQL ;
- ouverture au clavier d’une fiche détaillée.

## Fiche produit

La fiche regroupe progressivement :

1. identité et catégorie ;
2. activation/désactivation globale et locale ;
3. codes-barres avec activation ;
4. prix courant et changement dédié ;
5. historique des prix en lecture seule.

La création affiche explicitement qu’aucun stock n’est créé. Les formulaires ne demandent aucune donnée clinique ou réelle.

## Catégories

La section Catégories permet création, modification et activation/désactivation. Aucune action de suppression physique n’est proposée.

## Concurrence et erreurs

Une rowversion périmée affiche : « Ces données ont changé depuis leur ouverture. Rechargez-les et vérifiez les changements avant de recommencer. » Aucun écrasement ou retry automatique n’est réalisé.

Une panne SQL laisse le shell et les outils techniques accessibles. Le Catalogue et le POS annoncent l’indisponibilité, sans exception brute ni catalogue mémoire de secours.

## Anti-double-soumission

`CatalogCoordinator` utilise un verrou non bloquant par scope ; `UiButton` bloque également un second clic pendant son callback. Les contrôles sont désactivés pendant l’opération.

## Accessibilité et responsive

- titres et zones nommées ;
- contrôles natifs avec labels ;
- activation au clavier et Entrée pour la recherche ;
- focus visible hérité du design system ;
- tableaux scrollables accessibles ;
- recomposition en une colonne sous 75 rem ;
- formulaires empilés sous 48 rem ;
- aucune information critique portée uniquement par la couleur.

## POS et Diagnostic

Le POS précise que recherche et prix viennent de SQL, tandis que panier, disponibilité, paiement et impression restent en mémoire. Diagnostic affiche SQL Server, base, connectivité, migration, nombre Demo, stock synthétique et ventes non persistées sans montrer la connexion complète.

## Revue humaine

Vérifier à 1366 × 768 et 1920 × 1080 : recherche, filtres, détail, formulaires, historique, concurrence, panne SQL, clavier, focus, scrolling, double clic et navigation vers POS/Diagnostic/UI Lab.
