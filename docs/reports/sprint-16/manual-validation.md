# Sprint 16 — Validation manuelle Reporting

Statut : **MANUAL REPORTING REVIEW REQUIRED**

## Préconditions

Utiliser uniquement `Pharmacy_Development` avec des données synthétiques, un compte possédant les permissions attendues et une fenêtre Desktop à 1100×700, 1366×768 puis 1920×1080. Ne jamais utiliser une base de production.

## Scénarios

1. Ouvrir Rapports et vérifier titre, badge SQL, période UTC et date de génération.
2. Comparer ventes brutes et nombre de ventes avec les ventes SQL de la période.
3. Vérifier qu’un retour est séparé du brut et retranché une seule fois du net documenté.
4. Vérifier la ventilation des paiements par moyen avec les paiements confirmés.
5. Tester une période sans donnée : zéros explicites et états vides, sans erreur.
6. Vérifier qu’un produit sans balance apparaît en rupture et qu’aucun produit n’est doublé.
7. Vérifier qu’un lot périmé n’entre pas dans le stock vendable.
8. Activer le stock bas et comparer un produit à son `MinimumStock` configuré.
9. Désactiver le stock bas et vérifier que le compteur sous seuil revient à zéro sans modifier le stock.
10. Tenter d’activer la péremption sans horizon : l’enregistrement doit être refusé.
11. Définir un horizon explicite, vérifier les bornes 0 et horizon incluses et un lot au-delà exclu.
12. Vérifier commandes et réceptions, dont quantité et prix d’achat historiques.
13. Vérifier crédit généré, paiements et solde actuel sur un client synthétique.
14. Vérifier cash : ventes espèces, paiements crédit et remboursements restent distincts.
15. Exporter chaque CSV et contrôler BOM UTF-8, en-têtes, dates et décimaux.
16. Avec une valeur synthétique commençant par `=`, `+`, `-` ou `@`, vérifier l’apostrophe de neutralisation.
17. Tester un utilisateur sans `reports.view`, puis sans `reports.export`, puis sans `reports.manage_settings`.
18. Couper SQL Server : erreur contrôlée, shell encore utilisable, aucun faux indicateur.
19. Redémarrer l’application : paramètres persistés, rapports recalculés, aucun état mémoire supposé.
20. Vérifier clavier, focus visible, lecture des tables, absence de débordement et contenu non masqué par la topbar aux trois résolutions.

## Captures obligatoires

- Dashboard complet avec filtres et date de génération ;
- rapport Ventes avec un retour ;
- Stock et péremptions avec seuil/horizon visibles ;
- Achats/réceptions ;
- Clients/crédit et caisse ;
- fichier CSV ouvert montrant les en-têtes et la neutralisation ;
- état « accès refusé ».

Consigner pour chaque scénario : compte/rôle, période, données synthétiques utilisées, résultat attendu, résultat observé et capture associée.
