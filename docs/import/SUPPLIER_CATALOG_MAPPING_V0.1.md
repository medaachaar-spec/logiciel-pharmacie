# Mapping catalogue fournisseur V0.1

## Exemple supporté

Le fichier synthétique de référence est `database/examples/sprint17_supplier_catalog_utf8.csv` :

| Source | Cible | Règle |
|---|---|---|
| `Reference` | `ProductLocalSettings.InternalCode` | obligatoire, unique dans le fichier, création seulement |
| `Designation` | `Products.Name` | obligatoire, 256 caractères maximum |
| `PrixVente` | prix courant + historique | décimal invariant ou français, positif ; absent = produit non vendable |
| `PrixAchat` | aperçu seulement | non persisté tant qu’une règle PPH cible n’est pas confirmée |
| `CodeBarres` | `ProductBarcodes` | 8 à 14 chiffres, normalisé et globalement unique |
| `Categorie` | `ProductCategories.Code` | code existant ; sinon catégorie par défaut explicite |

Le mapping par défaut de l’écran utilise `Reference`, `Designation`, `PrixVente`, `CodeBarres`, `Categorie` et la catégorie `GEN`. Adapter les valeurs au fournisseur avant prévisualisation ; aucun profil fournisseur permanent n’est créé en V0.1.

## Nombres, dates et textes

- Décimaux : `19.95` ou `19,95` lorsque le séparateur CSV ne rend pas la valeur ambiguë.
- Dates de lot : `yyyy-MM-dd` ou `dd/MM/yyyy`.
- Texte : UTF-8, accents et arabe acceptés.
- Formules : toute cellule dont le premier caractère non blanc vaut `=`, `+`, `-` ou `@` est bloquée.
- Les guillemets CSV doubles sont supportés ; un guillemet non fermé invalide le fichier.

## Contrôle fournisseur

Avant confirmation, faire valider : sémantique du prix, devise MAD, unité de quantité, politique de lots, catégorie, codes-barres et absence de données patient. Un PPH ou une marge ne doit jamais être déduit du PPV. Conserver le rapport et le SHA-256 comme preuve, pas le fichier réel dans Git.
