# POS Application Integration V0.1

Date : 2026-08-03

Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL POS INTEGRATION REVIEW REQUIRED`

## 1. Objectif

Le parcours nominal du POS utilise désormais la chaîne `Desktop → Application → Infrastructure en mémoire → Domain`. Le rendu et les simulations de continuité du POS V0.2 sont conservés, mais le catalogue et le panier affichés ne sont plus pilotés par `DemoCatalog` ou les lignes locales de `DemoPosSession`.

## 2. Bandeau et limites visibles

Le POS affiche :

`Mode démonstration — moteur Sales Application en mémoire — aucune opération persistée`

Les dix références `DEMO-*`, brouillons Application et suspensions Desktop disparaissent à la fermeture. Aucun stock réel, paiement réel, ticket physique, fichier ou enregistrement SQL n’est produit.

## 3. Initialisation

À la première ouverture de Ventes, le coordinateur crée un brouillon Application à la version 1, charge sa vue vide, puis recherche `DEMO` via `SearchSellableProductsHandler` afin de conserver l’affichage initial des dix produits synthétiques. Une initialisation répétée réutilise le brouillon actif et ne crée pas de brouillon involontaire.

## 4. Recherche et scan

Chaque texte de recherche est transmis à Application. Une recherche visuellement vide recharge le catalogue synthétique initial avec la requête documentée `DEMO` ; aucun filtrage parallèle du catalogue complet n’existe dans Razor.

Entrée dans le champ utilise `GetSellableProductByCodeHandler`, puis ajoute le `ProductId` obtenu avec `AddProductToSaleDraftHandler`. Un code inconnu conserve le brouillon et affiche qu’aucun produit n’a été créé. Le focus revient au champ de scan.

## 5. Panier et source de vérité

`SaleDraftView` fournit exclusivement :

- les lignes et snapshots produit ;
- la quantité de chaque ligne ;
- le prix unitaire et le sous-total ;
- le nombre de lignes et la quantité totale ;
- le total MAD ;
- l’identifiant et la version du brouillon.

Desktop formate les montants avec `DemoMoney`, sans recalculer les totaux métier. Ajout, fusion, changement de quantité, suppression et vidage utilisent leurs handlers Application. La quantité zéro reste invalide et ne supprime jamais une ligne.

## 6. États busy et double activation

Le coordinateur possède un verrou ciblé pour les mutations de panier et un verrou séparé pour les opérations de continuité. Les contrôles sont désactivés pendant l’action et `UiButton` conserve sa protection interne contre un second clic. Une deuxième opération concurrente retourne un état busy sans atteindre le handler.

## 7. Disponibilité et erreurs

La disponibilité affichée vient de `SellableProductView`. Elle sert à l’information, pas à une validation locale. Le handler Application reste l’unique décideur lors d’une mutation.

Le mapping couvre brouillon absent, conflit, produit absent, produit non vendable, code invalide, stock insuffisant, quantité invalide et ligne absente. Un refus de disponibilité affiche le produit, la quantité demandée, la quantité disponible et confirme que le panier antérieur est conservé.

Une exception inattendue est capturée dans la frontière Desktop, journalisée via `Trace` avec seulement le nom de l’opération et le type d’exception, puis traduite en message sûr sans stack trace.

## 8. Concurrence optimiste

Chaque commande porte `CurrentDraft.Version`. Si Application retourne `sales.draft.concurrency_conflict`, le coordinateur :

1. n’écrase rien ;
2. recharge le brouillon avec `GetSaleDraftHandler` ;
3. remplace la vue et la version locales ;
4. affiche `Le brouillon a été modifié. Son état le plus récent a été rechargé.` ;
5. ne relance pas l’action en échec.

## 9. Suspension

Une suspension copie les lignes, la quantité totale et le total d’un `SaleDraftView` dans un snapshot Desktop immuable `DEMO-HOLD-*`. Le coordinateur crée d’abord un nouveau brouillon Application ; l’interface courante n’est remplacée qu’après ce succès. Le paiement incomplet est réinitialisé.

Les suspensions restent limitées au processus. Le brouillon Application désormais abandonné reste dans le store mémoire, limite acceptée pour cette version.

## 10. Reprise

Une reprise refuse d’écraser un panier non vide. Après choix explicite de l’opérateur, elle :

1. crée un nouveau brouillon Application ;
2. réajoute chaque snapshot par `AddProductToSaleDraftHandler` ;
3. conserve la suspension pendant toute la reconstruction ;
4. publie la nouvelle vue uniquement après restauration complète ;
5. supprime la suspension seulement après succès.

En cas d’échec, la suspension et la vue affichée sont conservées. Un brouillon de restauration partiel peut rester dans le store mémoire et disparaît avec le processus.

## 11. Paiement et impression

Les machines à états espèces/carte, TPE ambigu et impression restent Desktop. Le résultat simulé copie les lignes, le total, la quantité et la version du `SaleDraftView` courant. Aucun appel n’est fait à `PrepareSaleFinalizationHandler` ou `ISaleFinalizer`.

Après succès simulé : aucune persistance, aucun stock réel, aucun paiement réel et aucun ticket réel.

## 12. Accessibilité et responsive

Les composants conservent contrôles natifs, labels, régions live, `aria-busy`, confirmation unique, Escape non destructif, focus visible et retour du focus au scan. Les mutations désactivent leurs contrôles pendant l’action.

Les points de rupture existants restent : disposition colonne à 72 rem environ, actions empilées sous 48 rem et contenu pleine largeur sous 36 rem. La revue humaine reste requise à 1100×700, 1366×768 et 1920×1080, avec Tab, Shift+Tab, Entrée, Espace, focus visible et absence de débordement horizontal.

## 13. Code historique conservé

Les classes `DemoCatalog`, `DemoCartLine`, `DemoPosSession` et `DemoSuspendedSaleStore` sont conservées pour éviter une suppression massive et préserver les tests historiques. Elles ne sont plus enregistrées dans le composition root et aucun composant Razor nominal ne les utilise comme source catalogue ou panier.

