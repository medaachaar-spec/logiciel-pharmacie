# Sprint 15 — Validation manuelle sécurité

Préconditions : base de développement migrée, sauvegarde disponible, données synthétiques uniquement, compte propriétaire de test. Ne jamais utiliser une base de production.

1. Base vide : lancer Desktop et vérifier le bootstrap propriétaire sans compte/mot de passe prédéfini.
2. Créer le propriétaire avec un mot de passe de test d’au moins 15 caractères ; conserver les codes hors ligne.
3. Vérifier un login correct et l’affichage du nom/rôle dans la topbar.
4. Vérifier que login inconnu et mot de passe faux donnent le même message.
5. Répéter cinq mauvais mots de passe et constater le verrouillage neutre.
6. Pendant les cinq minutes, confirmer que le bon mot de passe reste refusé ; après expiration contrôlée, confirmer le succès.
7. Verrouiller manuellement, puis déverrouiller sans perdre un brouillon/suspension.
8. Faire logout puis login et vérifier qu’aucun champ mot de passe ne reste affiché.
9. Créer un second utilisateur synthétique ; vérifier `MustChangePassword` dans SQL sans lire le hash.
10. Affecter un rôle limité préparé pour la revue et confirmer qu’un ajustement Stock est refusé côté backend.
11. Donner `sales.create` et confirmer qu’une vente est autorisée.
12. Changer le rôle, se reconnecter et vérifier la nouvelle navigation.
13. Lancer un changement de prix sans réauth récente, puis après réauthentification correcte.
14. Simuler dix minutes d’inactivité et vérifier l’écran de verrouillage.
15. Utiliser un code de récupération, puis confirmer que le second usage est refusé.
16. Ouvrir le journal d’audit avec `audit.view` et vérifier acteur/action/ressource/résultat/UTC/corrélation.
17. Couper SQL Server : vérifier un message non sensible et le blocage des opérations, puis restaurer le service.
18. Redémarrer Desktop : vérifier que la session mémoire est perdue mais pas les comptes, rôles, brouillons persistants ou audits.
19. Dans SSMS, vérifier les 7 tables `security`, les 2 tables `audit`, les 20 permissions, les index uniques et les `rowversion`.
20. Rechercher dans SQL/logs/UI : aucun mot de passe, code de récupération en clair, donnée carte, chaîne de connexion ou note client inutile.

Captures demandées : bootstrap sans données sensibles, login/lock, administration, accès refusé, audit viewer. Masquer tous les identifiants locaux avant partage.

Résultat manuel : **à compléter par le valideur**.
