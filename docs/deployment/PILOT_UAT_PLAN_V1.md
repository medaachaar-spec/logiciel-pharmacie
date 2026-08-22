# Plan UAT pilote V1

Exécutante principale : pharmacienne pilote. Un support observe sans saisir à sa place. Utiliser uniquement des données synthétiques identifiables puis les nettoyer selon procédure.

Pour chaque scénario renseigner : date, version/hash, rôle, données, étapes, attendu, résultat réel, PASS/FAIL, capture/référence incident, commentaire et signature.

| ID | Scénario | Attendu critique | Résultat |
|---|---|---|---|
| UAT-01 | Fresh install et bootstrap Owner | base créée, aucun compte par défaut, récupération conservée | ☐ PASS ☐ FAIL |
| UAT-02 | Catalogue/import fournisseur | aperçu, validation, import contrôlé, erreurs lisibles | ☐ PASS ☐ FAIL |
| UAT-03 | Fournisseur, commande, réception | lots/stock/mouvements cohérents, réception idempotente | ☐ PASS ☐ FAIL |
| UAT-04 | Vente scan/recherche/cash | FEFO, stock décrémenté et vente persistée ; aucun ticket lorsque l’impression est désactivée | ☐ PASS ☐ FAIL |
| UAT-05 | Crédit client et règlement | plafond, dette et cash journal cohérents | ☐ PASS ☐ FAIL |
| UAT-06 | Retour/remboursement | restock approuvé ou non restockable respecté, audit présent | ☐ PASS ☐ FAIL |
| UAT-07 | Inventaire | comptage, écart, finalisation et mouvement cohérents | ☐ PASS ☐ FAIL |
| UAT-08 | RBAC | utilisateur limité bloqué, Owner autorisé, audit sans secret | ☐ PASS ☐ FAIL |
| UAT-09 | Reporting/export | chiffres réconciliés, export protégé contre formules | ☐ PASS ☐ FAIL |
| UAT-10 | Suspension/reprise | panier retrouvé après redémarrage sans duplication | ☐ PASS ☐ FAIL |
| UAT-11 | SQL indisponible | blocage explicite, aucune fausse réussite/perte silencieuse | ☐ PASS ☐ FAIL |
| UAT-12 | Imprimante/TPE indisponible | imprimante non requise si désactivée ; si activée, vente persistée identifiable et réimpression possible ; aucun PAN stocké | ☐ PASS ☐ FAIL |
| UAT-13 | Backup/restore-test | backup vérifié/hashé, restauration isolée saine | ☐ PASS ☐ FAIL |
| UAT-14 | Repair/uninstall/reinstall | données et configuration conservées | ☐ PASS ☐ FAIL |

Critères d’arrêt immédiat : perte/corruption, vente ou paiement incohérent, stock négatif injustifié, contournement RBAC, secret/PII exposé, restore impossible. Toute anomalie reçoit sévérité S1–S4, étapes, preuve et propriétaire. GO seulement si aucun S1/S2 ouvert et tous les scénarios critiques PASS.
