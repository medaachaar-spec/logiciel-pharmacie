# CODEX_WORKFLOW - Règles officielles de Vibe Coding

## 1. Rôles

- ChatGPT : co-ingénieur, architecte, manager du travail, auteur des prompts et responsable des Gates.
- Codex : implémentation autonome limitée au sprint, tests, vérifications et rapport.
- Utilisateur : exécution locale, validation manuelle et transmission du rapport.
- Pharmacie pilote : validation terrain avant production.

## 2. Cycle obligatoire

1. Lire le prompt du sprint et les sources de vérité.
2. Inspecter le dépôt sans modifier.
3. Vérifier les prérequis.
4. Présenter un plan d’exécution court.
5. Exécuter réellement le travail ; ne pas s’arrêter au plan.
6. Compiler et tester.
7. Corriger les erreurs dans le périmètre.
8. Vérifier le diff et l’état Git.
9. Produire le rapport obligatoire.
10. Attendre la validation humaine.

## 3. Priorité documentaire

1. Prompt actuel.
2. `DECISIONS_REGISTER.md`.
3. `CURRENT_STATE.md`.
4. `SCOPE.md`.
5. PDF dans `docs/reference/`.
6. Code existant.

Une contradiction doit être signalée. Codex ne choisit pas arbitrairement.

## 4. Structure obligatoire des prompts

1. Rôle.
2. Mission du sprint.
3. Sources de vérité.
4. État actuel du dépôt.
5. Prérequis.
6. Périmètre autorisé.
7. Hors périmètre.
8. Contraintes d’architecture.
9. Règles fonctionnelles.
10. Exigences UX/UI ou techniques.
11. Sécurité et données.
12. Plan d’exécution.
13. Tests et critères d’acceptation.
14. Validation manuelle.
15. Rapport final et restrictions Git.

## 5. Restrictions Git

- Ne pas commit.
- Ne pas push.
- Ne pas changer de branche.
- Ne pas réécrire l’historique.
- Ne pas supprimer des fichiers hors périmètre.
- Afficher l’état Git final dans le rapport.

Un commit n’est créé qu’après décision explicite de ChatGPT et de l’utilisateur.

## 6. Restrictions d’architecture

- Domain ne dépend d’aucun autre projet du produit.
- Application ne dépend pas d’Infrastructure ni de Desktop.
- Infrastructure contient EF Core, SQL Server, fichiers et matériels.
- Desktop ne doit pas utiliser directement DbContext.
- Pas de microservice, cloud obligatoire, event sourcing ou repository générique sans décision validée.
- Pas d’auto-migration de production au démarrage.

## 7. Données et sécurité

- Données synthétiques uniquement en développement et test.
- Aucun secret, mot de passe, clé ou chaîne sensible dans Git.
- Aucun numéro de carte, CVV ou donnée clinique.
- Permissions vérifiées dans Application.
- Les logs ne contiennent pas de données sensibles.

## 8. Règle de blocage

Lorsqu’une information manque :

1. arrêter uniquement la partie concernée ;
2. continuer les parties indépendantes ;
3. documenter précisément le blocage ;
4. ne pas inventer de règle métier.

## 9. Statuts autorisés dans le rapport

- `IMPLEMENTED - AUTOMATED CHECKS PASS`
- `IMPLEMENTED - MANUAL REVIEW REQUIRED`
- `PARTIALLY IMPLEMENTED`
- `BLOCKED`
- `FAILED`

Codex ne prononce pas le Gate définitif du sprint.

## 10. Validation frontend

- Lancer l’application desktop réelle.
- Tester 1366 x 768 et 1920 x 1080.
- Vérifier clavier, focus, loading, disabled, empty, error et access denied.
- Vérifier qu’une double activation ne duplique pas une opération.
- Fournir des captures lorsque le sprint modifie le rendu.

## 11. Validation backend

- Tests unitaires et intégration avec SQL Server réel pour les comportements critiques.
- Vérifier transaction, rollback, concurrence, idempotence, permissions et invariants.
- Aucun test critique ignoré.

## 12. Rapport final

Le rapport doit suivre le modèle `docs/templates/CODEX_REPORT_TEMPLATE.md` et être enregistré dans `docs/reports/sprint-XX/codex-report.md` lorsque le sprint le demande.
