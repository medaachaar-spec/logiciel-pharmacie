# Purchasing & Receiving UI V0.1

## Page

La route `/module/purchasing` remplace le placeholder par trois zones accessibles : Fournisseurs, Commandes et Réceptions. Elle réutilise la largeur, les cartes, tableaux, ombres, rayons, contrôles et breakpoints de Catalogue/Stock.

## Parcours

- Fournisseurs : recherche, liste, fiche, création/modification et activation logique.
- Commandes : création brouillon, ajout/fusion Catalogue SQL, édition/retrait, total descriptif, confirmation et annulation contrôlée.
- Réceptions : commande facultative, fournisseur, emplacement, BL, lignes, quantité, prix, lot, péremption, aperçu puis confirmation explicite.

Une réception liée précharge les reliquats. Une réception directe affiche un avertissement persistant. Les quantités reçues/restantes et statuts partiel/complet restent visibles.

## Sécurité d’interaction

- `UiButton` neutralise le double clic local ; le coordinateur refuse une seconde opération pendant `IsBusy` ; l’idempotence SQL constitue le dernier garde-fou.
- Une alerte rappelle que la finalisation augmente immédiatement le Stock SQL, que les ventes ne sont pas persistées, qu’aucune marge n’est calculée et que les données Demo sont synthétiques.
- En panne SQL, aucune donnée de secours et aucune mutation partielle.

## Accessibilité et responsive

La page utilise titres hiérarchiques, formulaires/labels natifs, tableaux dans des régions défilables, focus visible hérité, `aria-live`, `aria-busy`, confirmations `alertdialog` et actions au clavier. Les panneaux se recomposent sous 75 rem ; les onglets deviennent verticaux sous 48 rem et les tableaux restent dans leur conteneur sans débordement de page.

Validation humaine requise à 1100×700, 1366×768 et 1920×1080, y compris clavier, focus, états vides/erreur/conflit et confirmation de réception.
