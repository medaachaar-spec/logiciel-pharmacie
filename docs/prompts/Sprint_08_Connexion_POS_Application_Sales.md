# SPRINT 8 — CONNEXION PROGRESSIVE DU POS À APPLICATION SALES V0.1

## Instruction courte à donner à Codex

Place ce fichier dans le dépôt sous :

`docs/prompts/sprint-08-pos-application-integration.md`

Puis donne uniquement cette instruction à Codex :

> Lis intégralement `docs/prompts/sprint-08-pos-application-integration.md`, vérifie que le fichier se termine par `FIN DU PROMPT SPRINT 8`, puis exécute toutes les opérations autorisées. Ne fais aucun commit, push ou ajout de remote.

---

## 1. RÔLE

Tu agis comme ingénieur logiciel senior .NET 10, développeur WPF/Blazor Hybrid, architecte Clean Architecture pragmatique, spécialiste de l’intégration frontend/Application, de la concurrence optimiste, des machines à états POS et des tests automatisés.

Le projet est **Logiciel Pharmacie**. Les fondations, le design system, le shell, le POS synthétique V0.2, le noyau Domain Sales, les cas d’utilisation Application et les adaptateurs Infrastructure en mémoire sont déjà validés.

Ta mission est de connecter progressivement le POS existant à la chaîne réelle :

`Desktop → Application → Infrastructure en mémoire → Domain`

Tu dois inspecter, implémenter, compiler, tester, lancer, documenter et laisser toutes les modifications non stagées. Ne t’arrête pas à un plan.

---

## 2. MISSION

Remplacer, dans le parcours nominal du POS, la logique locale de catalogue et de panier par les cas d’utilisation du Sprint 7.

Le sprint doit permettre :

1. d’enregistrer les adaptateurs Sales en mémoire dans le composition root Desktop ;
2. de créer un brouillon Application à l’ouverture du POS ;
3. de rechercher via les handlers Application ;
4. de retrouver un produit par code exact pour le scan clavier ;
5. d’ajouter, fusionner, modifier, supprimer et vider via les handlers ;
6. d’afficher le panier depuis `SaleDraftView` ;
7. de gérer la version de concurrence optimiste ;
8. de recharger après conflit sans écrasement silencieux ;
9. de conserver temporairement les suspensions dans Desktop à partir d’instantanés Application ;
10. de restaurer une suspension dans un nouveau brouillon Application ;
11. de conserver paiement et impression comme simulations Desktop ;
12. d’utiliser les montants et totaux fournis par Application ;
13. de préserver le rendu et les parcours du POS V0.2 ;
14. de maintenir Diagnostic et UI Lab ;
15. de ne connecter ni SQL Server ni EF Core ;
16. de ne finaliser aucune vente réelle ;
17. de ne créer aucun commit, push ou remote.

Statut attendu :

`IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL POS INTEGRATION REVIEW REQUIRED`

---

## 3. SOURCES DE VÉRITÉ

Lire intégralement :

- `README.md`
- `Directory.Build.props`
- `global.json`
- `.editorconfig`
- `.gitattributes`
- `.gitignore`
- `docs/governance/PROJECT_CONTEXT.md`
- `docs/governance/DECISIONS_REGISTER.md`
- `docs/governance/SCOPE.md`
- `docs/governance/CURRENT_STATE.md`
- `docs/governance/CODEX_WORKFLOW.md`
- `docs/templates/CODEX_PROMPT_TEMPLATE.md`
- `docs/templates/CODEX_REPORT_TEMPLATE.md`
- `docs/domain/SALES_CORE_V0.1.md`
- `docs/application/SALES_USE_CASES_V0.1.md`
- `docs/infrastructure/IN_MEMORY_SALES_ADAPTERS_V0.1.md`
- `docs/ux/POS_V0.1.md`
- `docs/ux/POS_V0.2_CONTINUITY.md`
- `docs/reports/sprint-06/codex-report.md`
- `docs/reports/sprint-07/codex-report.md`
- `docs/reports/sprint-07/sales-application-validation.md`
- le présent fichier jusqu’à `FIN DU PROMPT SPRINT 8`.

Inspecter complètement :

- `src/Pharmacy.Domain`
- `src/Pharmacy.Application`
- `src/Pharmacy.Infrastructure`
- `src/Pharmacy.Desktop`
- `tests/Pharmacy.UnitTests`
- `tests/Pharmacy.IntegrationTests`

Consulter les PDF validés en lecture seule. Ne modifier aucun PDF.

Priorité : présent prompt, décisions validées, état courant, scope, documents de référence, code existant.

En cas de contradiction, ne pas inventer ; bloquer seulement la partie concernée et documenter le conflit.

---

## 4. ÉTAT ACTUEL DU DÉPÔT

Le Sprint 7 est annoncé comme validé et commité. Ne supposer aucun hash.

Exécuter avant modification :

```powershell
git status --short
git branch --show-current
git log --oneline -n 10
git remote -v
git diff --check
dotnet --version
dotnet sln Pharmacy.slnx list
dotnet build Pharmacy.slnx -c Debug
dotnet test Pharmacy.slnx -c Debug --no-build
```

État attendu :

- branche `main` ;
- working tree propre ;
- aucun fichier stagé ;
- aucun remote ;
- six projets ;
- au moins 240 tests réussis ;
- Domain Sales présent ;
- Application Sales présent ;
- cinq adaptateurs en mémoire présents ;
- Desktop encore basé sur le POS synthétique ;
- zéro erreur et zéro avertissement raisonnable.

Si le dépôt n’est pas propre, ne rien supprimer, ne pas reset et bloquer si l’origine des changements n’est pas expliquée.

Le rapport doit indiquer le hash initial, le nombre exact de tests de baseline et les fichiers Desktop responsables du POS actuel.

---

## 5. PRÉREQUIS TECHNIQUES

Respecter le graphe :

- Domain ne dépend d’aucun projet Pharmacy ;
- Application dépend de Domain ;
- Infrastructure dépend d’Application et Domain ;
- Desktop dépend d’Application et Infrastructure.

Le composition root peut connaître Infrastructure. Les composants Razor ne doivent jamais construire directement un store ou adaptateur concret.

Réutiliser l’injection existante. Une extension d’enregistrement comme `AddInMemorySalesAdapters` est autorisée uniquement sans nouveau package.

Après ce sprint :

- `SaleDraftView` est la source de vérité du panier courant ;
- Desktop ne recalcule pas les totaux métier ;
- l’ancien système `Demo*` ne doit plus piloter le catalogue ou le panier nominal ;
- les paiements, impressions, messages UI et suspensions temporaires peuvent rester dans Desktop.

Utiliser l’asynchronisme correctement : `CancellationToken`, aucun `.Result`, `.Wait()` ou `Task.Run` artificiel.

Aucun nouveau package : pas de MediatR, AutoMapper, FluentValidation, Redux, mocking ou framework UI.

---

## 6. PÉRIMÈTRE AUTORISÉ

Modifications autorisées dans :

- `src/Pharmacy.Desktop`
- `src/Pharmacy.Infrastructure`
- `src/Pharmacy.Application` seulement pour une correction minimale démontrée
- `tests/Pharmacy.UnitTests`
- `tests/Pharmacy.IntegrationTests`
- `README.md`
- `docs/governance/CURRENT_STATE.md`

Documents à créer :

- `docs/ux/POS_APPLICATION_INTEGRATION_V0.1.md`
- `docs/application/DESKTOP_SALES_INTEGRATION_V0.1.md`
- `docs/reports/sprint-08/pos-application-integration-validation.md`
- `docs/reports/sprint-08/codex-report.md`

Une couche Desktop testable peut être créée, par exemple `SalesPosCoordinator` ou équivalent. Elle doit appeler les handlers, centraliser la version et traduire les erreurs, sans dupliquer les règles métier.

Aucun nouveau projet.

---

## 7. HORS PÉRIMÈTRE

Interdits :

- SQL Server, EF Core, DbContext, migrations et chaînes de connexion ;
- persistance disque ;
- API HTTP ou serveur web ;
- finalisation réelle ou appel à `ISaleFinalizer` ;
- mouvements ou réservation de stock ;
- lots, péremption, FEFO ;
- caisse, paiement ou TPE réel ;
- impression réelle ou ESC/POS ;
- scanner matériel ;
- remises, TVA, crédit, retours, remboursements ;
- clients, fournisseurs, utilisateurs, rôles, permissions ;
- audit, sauvegarde, import, reporting ;
- données réelles de pharmacie.

Ne pas supprimer massivement les classes `Demo*`. Retirer seulement les branches devenues manifestement inutilisées, avec tests de non-régression.

Ne pas refondre le shell, l’UI Lab, Diagnostic ou la palette.

Ne pas modifier les PDF.

Interdictions Git : aucun `git add`, commit, push, remote, changement de branche ou réécriture d’historique.

---

## 8. ARCHITECTURE D’INTÉGRATION

### Composition root

Configurer une seule chaîne Sales en mémoire pour la durée du processus Desktop. Tous les handlers doivent utiliser les mêmes stores et adaptateurs cohérents.

Documenter les durées de vie choisies.

### Coordinateur Desktop

Créer un coordinateur testable exposant conceptuellement :

- `InitializeAsync`
- `SearchAsync`
- `AddProductAsync`
- `AddExactCodeAsync`
- `ChangeQuantityAsync`
- `RemoveItemAsync`
- `ClearAsync`
- `ReloadDraftAsync`
- `SuspendCurrentDraftAsync`
- `ResumeSuspendedDraftAsync`
- `AbandonCurrentDraftAsync`
- `StartNewDraftAsync`

Il conserve uniquement :

- `SaleDraftId` courant ;
- version courante ;
- dernière `SaleDraftView` ;
- résultats de recherche ;
- états loading ;
- erreurs/messages UI ;
- instantanés de suspensions Desktop ;
- état de paiement et d’impression existant.

Il ne doit jamais :

- calculer le total métier ;
- fusionner les lignes ;
- modifier le panier sans handler ;
- vérifier la disponibilité lui-même ;
- créer directement l’agrégat Domain ;
- utiliser un adaptateur Infrastructure concret.

### Cycle de vie

À l’ouverture de Ventes : initialiser, créer un brouillon si nécessaire, charger la vue, charger la recherche initiale selon le comportement documenté et placer le focus.

À `Nouvelle vente` : réinitialiser paiement/impression, créer un nouveau brouillon Application, remplacer identifiant/version et conserver les suspensions Desktop.

Un brouillon vide abandonné peut rester dans le store mémoire pendant ce sprint ; documenter cette limite.

---

## 9. RÈGLES FONCTIONNELLES

### Recherche et scan

- la recherche passe par Application ;
- aucun filtrage parallèle du catalogue complet dans Desktop ;
- code exact + Entrée utilise la lecture exacte Application puis l’ajout ;
- code inconnu ne crée rien ;
- le brouillon reste inchangé ;
- le focus revient au champ.

### Ajout

Utiliser l’identifiant du brouillon, la version courante, le ProductId et la quantité avec le handler d’ajout.

Après succès : remplacer vue et version.

Après échec : conserver la vue locale, afficher l’erreur et recharger uniquement en cas de conflit de concurrence.

### Quantité

Utiliser le handler de changement de quantité. La quantité zéro ne supprime pas. Les doubles clics doivent être bloqués. Une erreur de disponibilité conserve le panier.

### Suppression et vidage

Passer par les handlers. Conserver la confirmation de vidage. Réinitialiser l’étape de paiement après vidage.

### Concurrence

En cas de version obsolète :

- aucun écrasement ;
- afficher `Le brouillon a été modifié. Son état le plus récent a été rechargé.` ;
- recharger via Application ;
- mettre à jour la version ;
- ne pas relancer automatiquement l’action.

### Disponibilité

Mapper `sales.stock.insufficient` vers un message contextualisé. Ne pas utiliser l’ancienne disponibilité Desktop.

### Totaux

Prix, quantités, sous-totaux, quantité totale et total viennent exclusivement de `SaleDraftView`. Desktop ne fait que le formatage MAD.

---

## 10. UX/UI ET CONTINUITÉ

Conserver la disposition, la densité, les composants, la palette, les libellés, le responsive et les parcours clavier.

Afficher clairement :

`Mode démonstration — moteur Sales Application en mémoire — aucune opération persistée`

Chaque mutation possède un état busy ciblé et bloque les doubles actions.

Mapper explicitement les erreurs principales : brouillon absent, conflit de version, produit absent/non vendable, stock insuffisant et quantité invalide.

Une erreur technique inattendue est journalisée sans donnée sensible et affiche un message sûr sans stack trace.

### Suspension

Les suspensions restent Desktop/en mémoire mais capturent un instantané immuable issu de `SaleDraftView` : référence `DEMO-HOLD-*`, heure, lignes, quantité totale et total.

Après suspension :

1. capturer l’instantané ;
2. créer un nouveau brouillon Application ;
3. n’effacer l’UI courante qu’après succès ;
4. réinitialiser tout paiement incomplet ;
5. rappeler la limite mémoire.

### Reprise

La reprise reconstruit un nouveau brouillon Application :

1. créer un brouillon ;
2. réajouter chaque ligne via les handlers ;
3. conserver la suspension jusqu’au succès complet ;
4. en cas d’échec, conserver la suspension ;
5. supprimer la suspension seulement après restauration complète ;
6. ne jamais écraser un panier courant non vide.

Un brouillon incomplet abandonné peut rester dans le store mémoire après un échec ; documenter cette limite.

### Paiement et impression

Conserver les simulations Desktop. Elles lisent le total et les lignes depuis l’instantané Application.

Ne pas appeler `PrepareSaleFinalizationHandler` ni `ISaleFinalizer`.

Après succès simulé, rappeler : aucune persistance, aucun stock réel, aucun paiement réel et aucun ticket réel.

### Accessibilité

Vérifier 1100×700, 1366×768 et 1920×1080, Tab/Shift+Tab, Entrée/Espace, focus visible, absence de piège clavier et de débordement horizontal.

---

## 11. SÉCURITÉ ET DONNÉES

- uniquement les dix produits synthétiques Infrastructure ;
- aucune donnée personnelle, clinique ou bancaire ;
- aucun secret ;
- aucune chaîne de connexion ;
- aucune écriture disque.

Réutiliser les logs existants pour : initialisation, création du brouillon, conflit de concurrence, erreur inattendue et échec de restauration.

Ne pas journaliser un panier complet futur, des données personnelles ou des secrets.

Les erreurs attendues restent des `Result`. Les exceptions inattendues sont capturées à la frontière UI. Aucun catch vide.

Ne jamais utiliser « last write wins ».

---

## 12. PLAN D’EXÉCUTION

Exécuter dans cet ordre :

1. vérifier que le prompt se termine correctement ;
2. lire les sources ;
3. vérifier Git ;
4. exécuter la baseline ;
5. cartographier le POS actuel ;
6. identifier les sources actuelles du catalogue et du panier ;
7. définir la migration minimale ;
8. configurer l’injection ;
9. créer le coordinateur Desktop ;
10. intégrer création/lecture du brouillon ;
11. intégrer recherche et scan ;
12. intégrer ajout, quantité, suppression et vidage ;
13. intégrer conflits de version ;
14. migrer suspension et reprise ;
15. adapter paiement et impression aux vues Application ;
16. retirer seulement le code réellement obsolète ;
17. ajouter les tests unitaires ;
18. ajouter les tests d’intégration ;
19. vérifier l’architecture ;
20. exécuter Debug ;
21. exécuter Release ;
22. lancer l’application ;
23. produire la documentation ;
24. vérifier le diff ;
25. produire le rapport.

Toute modification Domain ou Application existante doit être minimale, justifiée et testée.

---

## 13. TESTS ET ACCEPTATION

Conserver tous les tests existants.

Ajouter au minimum des tests couvrant :

### Injection et initialisation

1. adaptateurs enregistrés ;
2. store partagé cohérent ;
3. handlers résolus ;
4. aucun composant Razor ne construit un adaptateur concret ;
5. création d’un brouillon Application ;
6. identifiant non vide, version 1 et vue vide ;
7. initialisation répétée sans brouillons actifs involontaires.

### Recherche et scan

8. recherche via Application ;
9. ancien catalogue Desktop non utilisé comme source ;
10. code connu ajoute le bon produit ;
11. code inconnu ne crée rien ;
12. recherche insensible à la casse ;
13. maximum de résultats respecté.

### Panier

14. ajout via handler ;
15. version augmentée ;
16. fusion Domain ;
17. totaux égaux à la vue ;
18. disponibilité insuffisante conserve l’état ;
19. produit inconnu conserve l’état ;
20. double clic bloqué ;
21. quantité augmentée/diminuée ;
22. zéro refusé ;
23. suppression via handler ;
24. vidage via handler ;
25. paiement réinitialisé après vidage.

### Concurrence

26. version obsolète détectée ;
27. aucune écriture forcée ;
28. rechargement du brouillon ;
29. version locale corrigée ;
30. action non relancée automatiquement.

### Suspension et reprise

31. snapshot immuable ;
32. nouveau brouillon après suspension ;
33. panier non effacé avant succès ;
34. plusieurs suspensions distinctes ;
35. reprise via nouveau brouillon ;
36. restauration par handlers ;
37. total restauré exact ;
38. suspension supprimée seulement après succès complet ;
39. échec conserve la suspension ;
40. panier courant non écrasé ;
41. double reprise bloquée.

### Paiement et impression

42. total lu depuis Application ;
43. paiement et impression restent simulés ;
44. aucun appel à `ISaleFinalizer` ;
45. aucun SQL ;
46. nouvelle vente crée un nouveau brouillon ;
47. états paiement/impression réinitialisés.

### Architecture

48. Domain inchangé sauf correction justifiée ;
49. Application inchangée sauf correction justifiée ;
50. aucun type Infrastructure concret dans les composants Razor ;
51. aucun DbContext, EF Core, SQL ou chaîne de connexion ;
52. aucun nouveau ProjectReference ;
53. aucun nouveau package ;
54. Diagnostic et UI Lab accessibles ;
55. autres modules inchangés ;
56. tous les anciens tests passent.

Utiliser des fakes déterministes ou les vrais adaptateurs en mémoire. Aucun framework de mocking supplémentaire ni temporisation fragile.

Commandes obligatoires :

```powershell
dotnet restore Pharmacy.slnx
dotnet build Pharmacy.slnx -c Debug --no-restore
dotnet test Pharmacy.slnx -c Debug --no-build
dotnet build Pharmacy.slnx -c Release --no-restore
dotnet test Pharmacy.slnx -c Release --no-build
```

Puis :

```powershell
dotnet run --project src/Pharmacy.Desktop/Pharmacy.Desktop.csproj -c Debug
```

Vérifier techniquement : shell, Ventes, brouillon Application, catalogue Infrastructure, panier, suspension/reprise, paiement/impression simulés, Diagnostic, UI Lab, absence d’exception et fermeture propre.

Ne pas prétendre avoir effectué la validation visuelle humaine.

Critères de PASS : recherche et panier via Application, versions gérées, conflits récupérables, aucune logique métier parallèle, continuité conservée, aucun SQL/package, builds/tests réussis, application démarrée et aucune action Git interdite.

---

## 14. VALIDATION MANUELLE

Fournir :

```powershell
cd "C:\Users\HP\Desktop\Logiciel pharmacie"
dotnet build .\Pharmacy.slnx -c Debug
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet run --project .\src\Pharmacy.Desktop\Pharmacy.Desktop.csproj -c Debug
git status --short
git diff --stat
```

Checklist :

- [ ] Ventes ouvre le POS ;
- [ ] bandeau Application en mémoire visible ;
- [ ] dix produits synthétiques ;
- [ ] recherche et scan exact ;
- [ ] code inconnu sans création ;
- [ ] ajout, fusion et quantités ;
- [ ] totaux corrects ;
- [ ] disponibilité insuffisante bloquée ;
- [ ] suppression et vidage ;
- [ ] suspension et reprise ;
- [ ] panier courant jamais écrasé ;
- [ ] paiement espèces simulé ;
- [ ] paiement carte ambigu ;
- [ ] impression échouée puis réessai ;
- [ ] nouvelle vente réinitialise ;
- [ ] navigation clavier et focus ;
- [ ] responsive aux trois résolutions ;
- [ ] Diagnostic et UI Lab ;
- [ ] aucune base connectée ;
- [ ] aucune exception.

Demander cinq captures : POS vide avec bandeau, recherche/catalogue, panier avec deux lignes, suspension/reprise, succès avec impression.

Demander aussi le résultat du scan exact, du code inconnu, de la disponibilité insuffisante et de l’anti-double-clic.

Ne pas demander de commit.

---

## 15. RAPPORT FINAL ET GIT

Créer :

- `docs/ux/POS_APPLICATION_INTEGRATION_V0.1.md`
- `docs/application/DESKTOP_SALES_INTEGRATION_V0.1.md`
- `docs/reports/sprint-08/pos-application-integration-validation.md`
- `docs/reports/sprint-08/codex-report.md`

Mettre à jour :

- `README.md`
- `docs/governance/CURRENT_STATE.md`

Le rapport `codex-report.md` doit contenir :

1. statut final ;
2. résumé ;
3. vérification de complétude du prompt ;
4. documents consultés ;
5. état initial et hash ;
6. cartographie du POS avant migration ;
7. architecture d’intégration ;
8. composition root ;
9. coordinateur Desktop ;
10. initialisation ;
11. recherche et scan ;
12. ajout, quantité, suppression, vidage ;
13. source de vérité et totaux ;
14. concurrence ;
15. mapping des erreurs ;
16. suspension et reprise ;
17. paiement et impression ;
18. code Desktop supprimé ou conservé ;
19. accessibilité et responsive ;
20. sécurité et logs ;
21. packages ;
22. tests ajoutés ;
23. commandes ;
24. résultats Debug ;
25. résultats Release ;
26. lancement Desktop ;
27. problèmes et écarts ;
28. risques et limites ;
29. validation manuelle ;
30. état Git ;
31. recommandation.

Statuts autorisés :

- `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL POS INTEGRATION REVIEW REQUIRED`
- `IMPLEMENTED — CORRECTIONS REQUIRED`
- `PARTIALLY IMPLEMENTED`
- `BLOCKED`
- `FAILED`

État Git à indiquer : branche, hash initial, fichiers modifiés/non suivis, fichiers stagés zéro, nouveaux commits zéro, remotes zéro, push absent.

Restrictions absolues :

- NE PAS EXÉCUTER `git add` ;
- NE PAS COMMIT ;
- NE PAS PUSH ;
- NE PAS CRÉER DE REMOTE ;
- NE PAS CHANGER DE BRANCHE ;
- NE PAS RÉÉCRIRE L’HISTORIQUE ;
- LAISSER TOUTES LES MODIFICATIONS VISIBLES POUR REVUE.

FIN DU PROMPT SPRINT 8
