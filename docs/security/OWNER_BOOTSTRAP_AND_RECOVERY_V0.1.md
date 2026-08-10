# Bootstrap propriétaire et récupération V0.1

## Premier démarrage

1. Appliquer les migrations EF avec une connexion administrateur contrôlée.
2. Définir `PHARMACY_SQLSERVER_CONNECTION` uniquement dans l’environnement du processus Desktop.
3. Lancer l’application. Si `security.UserAccounts` est vide, l’écran de bootstrap apparaît.
4. Saisir le login, le nom affiché et un mot de passe de 15 à 128 caractères.
5. Enregistrer les huit codes affichés dans un support hors ligne contrôlé, puis fermer l’affichage.
6. Se connecter avec le compte créé.

Le bootstrap utilise une transaction sérialisable et refuse toute seconde exécution dès qu’un utilisateur existe. Aucun mot de passe par défaut n’existe dans le code, la migration, les scripts ou cette documentation.

## Récupération

Depuis l’écran de connexion, saisir le login, un code et un nouveau mot de passe. Le code est atomiquement marqué utilisé avant validation. Un second usage échoue. Une régénération depuis Administration invalide tous les codes actifs antérieurs et exige une réauthentification récente.

Ne jamais copier les codes dans un ticket, log, capture publique ou dépôt Git. DPAPI n’est pas utilisé en V0.1 car aucun code n’est persisté côté poste : seul son hash SQL subsiste.
