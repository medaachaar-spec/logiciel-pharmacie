# Schéma physique Sales V0.1

Date : 2026-08-10

Migration : `20260810103858_SalesPersistenceV01`

## Vue d’ensemble

Le schéma `sales` contient cinq tables. Les clés techniques sont `bigint identity`, les montants sont `decimal(19,4)`, les instants UTC `datetime2(7)` et les textes sont bornés. Toutes les suppressions référentielles sont `NO ACTION`.

| Table | Rôle | Contraintes et index principaux |
| --- | --- | --- |
| `sales.Sales` | en-tête finalisé immuable | référence et `OperationId` uniques, date/id indexés, total et compteurs positifs/cohérents |
| `sales.SaleLines` | snapshots produit/prix | FK vente/produit, quantité positive, total = prix × quantité |
| `sales.SaleLineAllocations` | ventilation par balance/lot | FK ligne/balance/lot, quantité positive, couple ligne/balance unique |
| `sales.Payments` | paiement final | type, montant, confirmation et cohérence espèces/carte ; `OperationId` unique |
| `sales.SaleOperations` | registre idempotent | `OperationId` alternate key, empreinte, statut, résultat unique et dates cohérentes |

## Relations

- une vente possède une ou plusieurs lignes et exactement un paiement en V0.1 ;
- une ligne référence le produit courant tout en conservant référence, nom, présentation, prix et total en snapshot ;
- une allocation référence la balance consommée et éventuellement son lot ;
- une opération pointe vers sa vente résultat et la vente référence son `OperationId` ;
- les mouvements Inventory portent `MovementType = Sale`, la référence persistée et le lot consommé.

Les cycles entre vente et opération sont créés avec des FK ajoutées après création des tables. Aucune cascade ne peut supprimer un historique de vente ou de stock.

## Paiement et sécurité

Les seuls modes sont `Cash` et `ExternalCard`. La base conserve montant, montant reçu/monnaie pour espèces et confirmation booléenne pour carte. Aucun PAN, numéro de carte, titulaire, date d’expiration, CVV/CVC, piste ou jeton bancaire n’existe.

## Migration et rollback

`Up` ajoute `Sale` à la contrainte des types de mouvement puis crée les cinq tables, index et FK. `Down` restitue d’abord aux balances les quantités des mouvements `Sale`, supprime ces mouvements, retire le schéma Sales et restaure la contrainte Inventory antérieure. Le rollback et la réapplication sont couverts sur SQL Server réel.

La migration ne contient aucune vente seedée. Le script idempotent est `database/scripts/005_sales_persistence_v01_idempotent.sql`.
