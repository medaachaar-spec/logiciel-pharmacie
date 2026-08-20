# Guide utilisateur pilote V1

## Première ouverture

Vérifier que la barre d’état et Diagnostic affichent `1.0.0-rc.1`. À la première installation, créer le compte Propriétaire avec une phrase secrète unique. Il n’existe aucun mot de passe par défaut. Imprimer ou recopier les codes de récupération et les conserver hors du PC.

## Routine quotidienne

1. Ouvrir avec son propre compte, jamais celui d’un collègue.
2. Vérifier l’état SQL et les alertes.
3. Réceptionner les achats avec lot/péremption exacts.
4. Pour une vente, scanner ou rechercher, vérifier quantité/prix/client, choisir cash/TPE externe/crédit, puis confirmer une seule fois.
5. Pour TPE : attendre le résultat du terminal puis reporter accepté/refusé/annulé ; l’application ne collecte aucune donnée carte.
6. Réimprimer depuis l’historique si nécessaire ; ne jamais recréer une vente persistée.
7. Réaliser retours, crédit, inventaires et exports avec le rôle autorisé.
8. Fermer l’application proprement en fin de journée.

## Bonnes pratiques

- une session par personne ; verrouiller le poste en s’absentant ;
- vérifier le client avant crédit/remboursement ;
- ne pas modifier directement SQL ou les fichiers ProgramData ;
- ne pas mettre de données réelles dans un export de test ;
- conserver le backup quotidien et sa copie externe chiffrée ;
- signaler immédiatement toute différence vente/caisse/stock.

## Si quelque chose ne marche pas

Lire le message sans répéter l’opération. Si SQL est indisponible ou si une confirmation reste incertaine, arrêter et contacter le support : ne pas promettre au client que l’opération est enregistrée. Pour imprimante offline, vérifier d’abord que la vente figure dans l’historique puis réimprimer. Ne communiquer au support ni mot de passe, ni code de récupération, ni donnée carte.
