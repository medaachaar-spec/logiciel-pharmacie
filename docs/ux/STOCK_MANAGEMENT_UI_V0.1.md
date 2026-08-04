# Stock Management UI V0.1

Date : 2026-08-04  
Statut : `IMPLEMENTED — MANUAL VISUAL REVIEW REQUIRED`

## Page

La route `/module/inventory` remplace le placeholder Stock. Elle présente un résumé compact, une recherche produit/référence/code-barres, un filtre d’emplacement, les états disponible/épuisé/expiré, le détail des balances et un historique de mouvements.

Les lots vendables et expirés sont visuellement séparés. Les expirés restent visibles et sont explicitement non vendables. La proposition FEFO est descriptive et rappelle qu’elle ne consomme rien.

## Actions sensibles

Une ouverture ou un ajustement exige un solde sélectionné, une quantité et un motif. Une confirmation précède l’écriture. La baisse est présentée comme sensible et le moteur refuse un résultat négatif. Le `rowversion` lu avec la balance est envoyé à la commande ; un conflit demande un rechargement.

Une session d’inventaire est créée/démarrée pour un emplacement, affiche le théorique capturé, accepte des comptages entiers et ne rend la finalisation disponible que lorsque toutes les lignes sont comptées. Finalisation et annulation ont des confirmations séparées.

## États et accessibilité

La page couvre chargement, vide, succès, erreur, indisponibilité SQL et conflit. Les contrôles sont natifs ou issus du UI Lab, les tables défilent au clavier, les dialogues ont un rôle explicite, les boutons bloquent la double activation et la mise en page passe de deux colonnes à une sous 704 px.

## POS et limites

Le POS affiche que Catalogue et disponibilité viennent de SQL, mais que vente, paiement et impression restent simulés. Un succès POS ne décrémente pas le stock. La revue humaine doit encore vérifier focus, clavier/scanner et rendu aux résolutions 1366×768 et 1920×1080.
