# POS — ventes persistantes V0.1

Date : 2026-08-10

Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL POS REVIEW REQUIRED`

## Parcours nominal

Le POS conserve sa recherche Catalogue SQL et son brouillon Application. À la validation, il utilise `FinalizeSaleHandler`; l’écran de succès n’apparaît qu’après commit et affiche la référence `VTE-*` persistée. Le bouton Nouvelle vente active un brouillon précréé distinct.

## Paiement

Les espèces exigent un montant reçu suffisant et affichent la monnaie calculée. La carte représente uniquement un TPE externe : l’utilisateur confirme le succès, sans saisir de donnée bancaire. Les états non confirmé ou ambigu bloquent la finalisation.

## Incidents

- stock devenu insuffisant : vente refusée, panier conservé ;
- prix changé : vente refusée, panier conservé pour rechargement explicite ;
- conflit de stock : message sûr et nouvelle tentative manuelle ;
- SQL indisponible : aucun faux succès, panier conservé ;
- double clic : la finalisation déjà en cours bloque un second appel ;
- impression en échec : la vente et le stock restent committés.

Le même `OperationId` est conservé pendant les nouvelles tentatives d’une validation donnée. Le changer de paiement ou de panier annule cette tentative et crée une nouvelle opération.

## Impression

Le service de ticket reste une simulation Desktop. Il n’est préparé qu’après retour persistant du finalizer. Réessayer ou continuer sans ticket n’appelle jamais la finalisation et ne redécrémente donc pas le stock.

## Historique minimal

La page expose un panneau repliable des ventes récentes avec référence, date UTC, nombre de lignes, quantité, total, paiement et statut. La lecture vient de `ISalesReader`, pas du résultat local de la session.

## Continuité encore locale

Les suspensions/reprises de paniers, le brouillon actif et l’état d’impression disparaissent avec le processus. Cette limite est signalée dans l’alerte du POS.

## Accessibilité et design system

La page réutilise les badges, alertes, états vides et tables existants. Les boutons natifs conservent focus visible et blocage désactivé ; l’historique utilise `details/summary` au clavier et la grille reste sans débordement horizontal aux points de rupture existants.
