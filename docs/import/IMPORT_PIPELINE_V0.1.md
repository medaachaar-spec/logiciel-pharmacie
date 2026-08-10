# Pipeline d’import contrôlé V0.1

## Formats et limites

La V0.1 accepte uniquement `.csv` UTF-8 strict (BOM facultatif), séparé par point-virgule, virgule ou tabulation. Taille maximale : 25 Mio ; 100 000 lignes ; aperçu limité aux 100 premières lignes. XLSX n’est pas annoncé : aucun parseur XLSX sûr et maintenu n’était déjà présent et aucun package n’a été ajouté. Les macros ne sont donc jamais exécutées.

Le nom doit être un nom de fichier simple, sans chemin. Toute cellule commençant par `=`, `+`, `-` ou `@` est rejetée pour empêcher l’injection de formule. Les exports futurs doivent également neutraliser ces préfixes.

## Étapes obligatoires

1. Sélection locale du fichier.
2. Choix explicite du mode : Catalogue, Stock initial sans lot, ou Lots.
3. Mapping explicite des en-têtes et valeurs par défaut.
4. Décodage UTF-8, parsing CSV cité et calcul SHA-256.
5. Validation de toutes les lignes ; aucune persistance pendant la prévisualisation.
6. Aperçu borné, compteur global et liste d’erreurs/avertissements.
7. Réauthentification récente et confirmation humaine.
8. Transaction SQL `Serializable`, écriture atomique et historique `operations.ImportJobs`.
9. Rapport créé/ignoré/rejeté et audit.

Le parsing CPU de l’aperçu et de la confirmation s’exécute hors du thread UI. L’écriture reste asynchrone. L’essai automatisé couvre 8 100 références synthétiques.

## Modes

### Catalogue — CreateOnly

- Une référence existante est ignorée ; elle n’est jamais écrasée silencieusement.
- La catégorie doit exister via la colonne ou une valeur par défaut explicite.
- Un code-barres existant ou dupliqué bloque la transaction.
- Le PPV absent crée un produit non vendable au prix technique zéro ; aucune marge n’est inventée.
- Le PPH fournisseur apparaît dans l’aperçu mais n’est pas persisté : le modèle cible ne possède pas de champ confirmé adapté.

### Stock initial sans lot

- Le produit et l’emplacement doivent exister.
- Aucun produit Catalogue n’est créé implicitement.
- Produit avec suivi de lots : mode refusé.
- Quantité strictement positive.
- Un stock initial déjà positif est ignoré, sans cumul silencieux.
- Création atomique d’un solde et d’un mouvement `Opening`.

### Lots

- Produit, emplacement, lot et quantité sont obligatoires.
- Le produit doit avoir le suivi de lots ; la péremption est obligatoire si `TracksExpiry=true`.
- Un lot existant est ignoré ; aucune fusion automatique.
- Lot, solde et mouvement `Opening` sont créés dans une transaction unique.

## Atomicité et idempotence

La confirmation porte un `OperationId` stable conservé par le coordinateur UI après prévisualisation. Le fingerprint combine OperationId, SHA-256, mode et mapping. Rejouer exactement la même opération retourne le rapport existant ; réutiliser l’OperationId avec un autre contenu renvoie un conflit. Une validation ou collision SQL annule toute la transaction.

## Erreurs attendues

Les erreurs ont un code stable : fichier/UTF-8 invalide, mapping invalide, référence/nom requis, formule, duplication fichier, code-barres invalide/collision, nombre/date/quantité invalide, catégorie/produit/emplacement introuvable, mode lot incohérent, conflit d’idempotence ou de persistance. Aucun contenu complet du fichier n’est écrit dans l’audit.
