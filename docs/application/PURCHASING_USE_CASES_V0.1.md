# Purchasing Use Cases V0.1

## Architecture

`Pharmacy.Application.Purchasing` expose des commandes, vues immuables et contrats spécifiques. Application dépend seulement de Domain ; aucun type EF, `DbContext`, `IQueryable` ou repository générique n’est exposé.

## Fournisseurs

- rechercher/lister, lire un détail ;
- créer et modifier code, nom et contacts facultatifs ;
- activer/désactiver avec `rowversion` ;
- refuser code dupliqué, état périmé et fournisseur absent/inactif.

La suppression physique n’est pas un cas d’utilisation.

## Commandes

- créer un brouillon pour un fournisseur actif ;
- ajouter une ligne Catalogue ou fusionner le même produit ;
- modifier/supprimer une ligne du brouillon ;
- confirmer une commande non vide ;
- annuler un brouillon ou une commande confirmée sans réception ;
- lire quantités commandées, reçues et restantes ainsi qu’un total descriptif.

Les commandes confirmées sont immuables. Le prix est un coût d’achat descriptif et ne modifie pas le prix de vente.

## Réceptions

`FinalizeGoodsReceiptCommand` porte un `OperationId`, le fournisseur, la commande facultative, l’emplacement, le BL, la date document, le `rowversion` attendu de la commande et les lignes. Une ligne indique produit, ligne de commande facultative, quantité, prix snapshot, lot existant ou nouveau et péremption.

La finalisation vérifie toute la requête avant écriture. Même `OperationId` et même empreinte retournent la réception existante ; un contenu différent retourne `purchasing.receipt.operation_conflict`. Le Desktop garde le même `OperationId` pendant une tentative pour neutraliser double clic et réponse ambiguë.

## Diagnostics

Les lectures exposent fournisseurs, commandes, réceptions, produits achetables et un diagnostic sûr. En panne SQL, `purchasing.receipt.database_unavailable` bloque les mutations et aucun fallback mémoire n’est autorisé.
