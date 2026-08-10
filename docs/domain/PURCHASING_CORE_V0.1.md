# Purchasing Core V0.1

## Objet

Le noyau Purchasing du Sprint 12 formalise uniquement les invariants partagés par les commandes d’achat et les réceptions. Il ne dépend ni d’EF Core, ni de SQL Server, ni du Desktop.

## États validés

- Commande : `Draft`, `Confirmed`, `PartiallyReceived`, `FullyReceived`, `Cancelled`.
- Réception persistée : `Completed` uniquement. Le brouillon de saisie reste local au Desktop et ne devient jamais un faux état métier persistant.
- Type de réception : `PurchaseOrder` ou `Direct`. La réception directe est autorisée par PUR-008 et signalée explicitement dans l’interface.

Une commande `Draft` est modifiable et n’a aucun effet stock. La confirmation exige au moins une ligne, fige les lignes et ne touche toujours pas au stock. Les statuts partiel/complet sont calculés à partir des quantités reçues cumulées.

## Invariants

- quantité entière strictement positive ;
- prix d’achat non négatif ;
- reçu cumulé inférieur ou égal au commandé ;
- finalisation unique et transactionnelle ;
- réception terminée append-only ;
- aucune marge, fiscalité, remise, paiement fournisseur ou donnée bancaire.

Les erreurs publiques sont stables sous le préfixe `purchasing.`. Les conflits de concurrence et d’idempotence sont distingués des erreurs de validation et de l’indisponibilité SQL.

## Intégration Stock

`StockMovementType.PurchaseReceipt` est un mouvement entrant. Une réception validée crée exactement un mouvement par ligne et actualise la balance associée dans la même transaction. Une commande confirmée seule ne crée ni lot, ni mouvement, ni balance.
