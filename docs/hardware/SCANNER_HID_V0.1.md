# Scanner USB HID V0.1

## Contrat

Le scanner est traité comme un clavier USB HID sans SDK de marque. Configuration recommandée : chiffres bruts, suffixe `Enter`, aucun préfixe, disposition clavier identique au poste. La saisie manuelle utilise le même champ.

`HidScannerBuffer` accepte les caractères non contrôlés, réinitialise une séquence si l’intervalle dépasse 120 ms et termine sur Entrée. Le même code terminé dans les 500 ms est supprimé comme double scan. Une référence inconnue produit un état explicite et ne crée jamais un produit.

## Validation pilote

1. Scanner un code connu et vérifier un seul ajout.
2. Scanner deux fois rapidement et vérifier la protection contre le doublon involontaire.
3. Scanner un code inconnu et confirmer l’absence de création Catalogue.
4. Saisir le même code au clavier puis Entrée.
5. Vérifier focus visible, tabulation, clavier numérique et comportement à 1100×700.
6. Tester déconnexion/reconnexion USB et mise en veille.

L’état logiciel « Mode clavier » prouve uniquement l’abstraction. La compatibilité du scanner physique reste `HARDWARE PILOT GATE PENDING` jusqu’à exécution de cette liste.
