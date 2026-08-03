# Sales Application V0.1

Date : 2026-08-03

Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL REGRESSION REVIEW REQUIRED`

## 1. Objectif

Orchestrer le noyau Sales V0.1 sans déplacer ses invariants dans Application et sans connecter le POS. Cette couche coordonne catalogue, disponibilité, brouillons versionnés et préparation idempotente d’une future finalisation.

## 2. Frontières

```text
Handlers Application
├── contrats catalogue et disponibilité
├── ISaleDraftStore / ISaleDraftIdGenerator
├── Domain SaleDraft
└── ISaleFinalizationPreparationStore

Infrastructure.InMemory implémente les contrats
Desktop reste déconnecté
```

Application ne dépend ni d’Infrastructure, ni de Desktop, ni d’un ORM. Aucun handler ne contient les calculs de sous-total, total ou fusion déjà portés par Domain.

## 3. Vues immuables

`SaleDraftView` expose identifiant, version, lignes en lecture seule, nombre de lignes, quantité totale, total MAD et état vide. `SaleDraftLineView` expose le snapshot utile au comptoir. `SellableProductView` ajoute disponibilité descriptive et indicateurs de disponibilité/vendabilité. `SaleFinalizationPreparation` copie ses lignes et son empreinte ; aucune collection mutable n’est rendue au demandeur.

## 4. Créer et consulter un brouillon

`CreateSaleDraftHandler` demande un identifiant à `ISaleDraftIdGenerator`, crée un agrégat vide, l’enregistre et retourne une vue à la version 1. Aucun numéro de vente n’est généré. `GetSaleDraftHandler` retourne une vue détachée ou `sales.draft.not_found`.

## 5. Catalogue vendable

`SearchSellableProductsHandler` trim la requête, filtre les références non vendables et limite la réponse à 25 éléments. Une recherche vide retourne volontairement une liste vide et n’appelle aucun adaptateur. Les lectures exactes sont portées par `GetSellableProductByIdHandler` et `GetSellableProductByCodeHandler`.

`SellableProductLookup` représente exclusivement un `ProductId` ou un code exact trimé ; ses constructeurs privés empêchent les combinaisons ambiguës.

## 6. Ajouter et fusionner

`AddProductToSaleDraftHandler` suit l’ordre : charger, vérifier la version, résoudre le produit, contrôler la vendabilité, calculer la quantité cible, lire la disponibilité, appeler `SaleDraft.AddItem`, puis sauvegarder atomiquement. Pour une ligne existante, la disponibilité porte sur `quantité existante + quantité demandée`.

Un échec catalogue, disponibilité, Domain ou concurrence ne modifie jamais le snapshot stocké.

## 7. Modifier, supprimer et vider

`ChangeSaleItemQuantityHandler` exige une quantité Domain strictement positive, refuse une ligne absente, vérifie la disponibilité cible et sauvegarde avec version attendue. Une quantité zéro ne supprime rien.

`RemoveSaleItemHandler` supprime séparément sans lecture de stock. `ClearSaleDraftHandler` incrémente la version lorsqu’il retire des lignes. Sur un brouillon déjà vide, il réussit sans sauvegarde et sans incrément, afin d’éviter une écriture inutile.

## 8. Concurrence optimiste

Chaque commande de mutation porte `ExpectedVersion`. Le store crée à la version 1 et incrémente après chaque modification réelle. Le handler vérifie tôt la version ; le store la revérifie atomiquement au moment de sauvegarder pour couvrir une course concurrente.

```text
Version stockée : 3
Version demandée : 2
→ sales.draft.concurrency_conflict
→ snapshot stocké inchangé
```

## 9. Préparer une finalisation

`PrepareSaleFinalizationHandler` exige un `OperationId` non vide, un brouillon existant, la version attendue, au moins une ligne et la disponibilité de chaque ligne. Il crée un snapshot immuable avec lignes, quantité totale, total, version préparée et statut `Prepared`, puis le confie au store idempotent.

**Prepared ne signifie pas finalisée, payée, réservée ou persistée.**

Le handler ne modifie pas le brouillon, ne réserve rien et n’appelle pas `ISaleFinalizer`.

## 10. Idempotence

L’empreinte structurelle inclut `SaleDraftId`, version, produits triés, quantités, bits décimaux des prix unitaires et bits décimaux du total. Elle ne dépend pas de la culture et n’est pas un hash cryptographique.

- même `OperationId` et même empreinte : même résultat préparé ;
- même `OperationId` et contenu/version/brouillon différent : `sales.finalization.operation_conflict` ;
- `OperationId` différent : préparation indépendante.

## 11. Erreurs Application

- `sales.draft.not_found` ;
- `sales.draft.already_exists` ;
- `sales.draft.version.invalid` ;
- `sales.draft.concurrency_conflict` ;
- `sales.product.not_found` ;
- `sales.product.not_sellable` ;
- `sales.product_code.invalid` ;
- `sales.stock.insufficient_availability` ;
- `sales.draft.empty` ;
- `sales.finalization.operation_id.invalid` ;
- `sales.finalization.operation_conflict`.

Les erreurs Domain sont propagées sans perdre leur code.

## 12. Asynchronisme et annulation

Tous les handlers et contrats susceptibles d’atteindre une frontière technique utilisent `Task` et `CancellationToken`. Aucun `Task.Run`, blocage synchrone, `.Wait()` ou accès à `Task.Result` n’est utilisé.

## 13. Limites et suite

Cette version ne traite ni persistance, transaction SQL, réservation, lot, FEFO, paiement, caisse, client, audit, impression ou finalisation réelle. Le prochain sprint pourra connecter progressivement le POS aux handlers, après validation dédiée, sans lui donner accès aux classes Infrastructure.
