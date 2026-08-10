# Sprint 17 — Validation manuelle

Statut initial : **MANUAL OPERATIONS REVIEW REQUIRED — HARDWARE PILOT GATE PENDING**

## Préparation sûre

Utiliser uniquement SQL Server Developer local et `Pharmacy_Development` avec données synthétiques. Vérifier un index Git vide, aucun processus Desktop résiduel et aucun remote. Configurer un dossier de sauvegarde dédié : droits `Modify` pour `NT SERVICE\MSSQLSERVER`, lecture pour l’utilisateur Desktop. Le support externe de test doit être vide, amovible si possible et chiffré ; attester ce chiffrement avec `PHARMACY_BACKUP_EXTERNAL_ENCRYPTED=true` uniquement après contrôle réel.

Créer deux comptes : propriétaire avec les six permissions Sprint 17 et compte restreint sans celles-ci. Ne jamais utiliser une base ou un fichier de production.

## Sauvegarde et reprise

- [ ] 1. Ouvrir « Continuité et périphériques » ; capturer configuration, édition, recovery model et chemins sans chaîne SQL.
- [ ] 2. Créer une sauvegarde complète ; relever nom, taille, statut `VerifyOnlyPassed` et présence d’un SHA-256.
- [ ] 3. Relancer « Vérifier » et comparer le hash.
- [ ] 4. Configurer le support externe chiffré, créer une sauvegarde et vérifier la copie/hash ; retirer le support puis vérifier l’état indisponible.
- [ ] 5. Sans attestation de chiffrement, confirmer que la copie externe n’est pas présentée comme protégée et qu’elle est bloquée.
- [ ] 6. Après réauthentification, lancer « Test restaurer » ; exiger migrations > 0, quatre tables critiques, cleanup PASS et zéro `Pharmacy_RestoreTest_*`.
- [ ] 7. Tester un chemin hors racine et un fichier absent : refus lisible, aucune base créée.
- [ ] 8. Vérifier les tâches Windows Full quotidienne, Diff 4 h, Log 15 min ; sur Express, aucune dépendance SQL Agent.

Captures requises : écran Sauvegardes, résultat de vérification, résultat restore-test et configuration du Planificateur de tâches sans secret.

## Imports

- [ ] 9. Charger l’exemple UTF-8 Catalogue ; vérifier accents/arabe, mapping et hash.
- [ ] 10. Prévisualiser ; vérifier total global, maximum 100 lignes affichées et absence d’écriture SQL.
- [ ] 11. Tester mauvais header, UTF-8 invalide, doublon référence, décimal/date invalide, quantité négative et formule `= + - @` : confirmation bloquée.
- [ ] 12. Tester collision code-barres avec une nouvelle référence : transaction annulée et produit absent.
- [ ] 13. Confirmer un Catalogue valide après réauthentification : `CreateOnly`, rapport et audit.
- [ ] 14. Relancer la même confirmation : résultat idempotent, aucune duplication.
- [ ] 15. Relancer avec un nouvel OperationId : références existantes ignorées, jamais écrasées.
- [ ] 16. Importer un stock initial pour produit sans lots ; vérifier solde + mouvement `Opening` atomiques.
- [ ] 17. Importer un lot/péremption pour produit compatible ; vérifier lot + solde + mouvement.
- [ ] 18. Vérifier qu’un import stock ne crée jamais un produit inconnu et que le PPH n’est pas inventé/persisté.
- [ ] 19. Générer 8 100 lignes avec `New-Sprint17SyntheticImport.ps1` ; vérifier UI répondante, aperçu borné et rapport.

Captures requises : mapping, aperçu valide, erreurs bloquantes, rapport confirmé et Catalogue/Stock après import.

## Périphériques et POS

- [ ] 20. Scanner connu + Enter, inconnu, saisie manuelle et double scan ; vérifier aucune création implicite.
- [ ] 21. Finaliser une vente SQL puis tester ticket simulé succès, échec, retry, sans ticket et réimpression ; aucune vente/paiement doublé.
- [ ] 22. Tester TPE externe accepté/refusé/annulé ; vérifier qu’aucune donnée bancaire n’est capturée.
- [ ] 23. Exécuter le diagnostic avec/sans SQL, support externe et périphériques ; simulateur/manuels/non certifiés doivent être distingués.

Exécuter la checklist physique `docs/hardware/PILOT_HARDWARE_CHECKLIST_V0.1.md`. Scanner, imprimante, TPE et onduleur ne peuvent recevoir `PASS HARDWARE` que sur le poste réel.

## RBAC, accessibilité et responsive

- [ ] Compte restreint : navigation/action refusée et backend refusé même via appel direct.
- [ ] `backup.restore`, `import.execute`, `devices.configure` : réauthentification récente requise.
- [ ] Audit : succès sensibles présents ; aucun secret, contenu complet CSV ou donnée bancaire.
- [ ] Clavier seul : ordre logique, Entrée, focus visible, tableaux scrollables.
- [ ] 1100×700, 1366×768, 1920×1080 : topbar non recouvrante, aucun débordement horizontal, actions accessibles.

## Acceptation

Noter pour chaque ligne `PASS`, `FAIL` ou `NOT TESTED`, joindre les captures et relever les modèles matériels. Toute ligne physique non exécutée maintient : `HARDWARE PILOT GATE PENDING`. Une anomalie SQL, une base temporaire résiduelle, un hash différent ou une double vente bloque la validation.
