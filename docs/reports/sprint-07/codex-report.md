# Sprint 7 — Rapport d’exécution Codex

## 1. Statut final

**IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL REGRESSION REVIEW REQUIRED**

## 2. Résumé

Sales Application V0.1 orchestre maintenant la création/lecture des brouillons, le catalogue vendable, les mutations avec disponibilité et concurrence optimiste, ainsi qu’une préparation idempotente de finalisation. Cinq adaptateurs temporaires en mémoire implémentent les frontières techniques. Domain et Desktop restent inchangés.

Les 183 tests initiaux sont conservés ; 57 contrôles Sprint 7 portent le total à 240, tous réussis en Debug et Release.

## 3. Documents consultés

Les 19 sources texte demandées ont été relues ou vérifiées, notamment gouvernance, spécification Domain Sprint 6, UX POS et rapports Sprint 5/6. Les sources des six projets et des deux projets de tests ont été cartographiées ; les contrats et implémentations Sales ont été inspectés intégralement.

Les cinq PDF validés ont été consultés en lecture seule. Les pages sur l’ADR-005, le panier brouillon, la vérification finale, les GUID d’opération, `RowVersion`, le POS et le workflow code-first ont été extraites, rendues et inspectées. Aucun PDF n’a été modifié.

## 4. État initial

- branche `main` ;
- working tree propre, aucun fichier stagé ;
- aucun remote ;
- `git diff --check` propre ;
- SDK .NET `10.0.302` ;
- six projets ;
- Domain Sales et contrats Sprint 6 présents ;
- Infrastructure sans Sales et Desktop déconnecté ;
- baseline Debug : build réussi, 0 avertissement, 0 erreur ; 183/183 tests réussis.

## 5. Dernier commit initial

`7ba1e83 feat: add sales domain core and application contracts`

Le hash a été relevé avant toute modification.

## 6. Architecture Application

```text
Handlers Application
├── IProductCatalogReader
├── IStockAvailabilityReader
├── ISaleDraftStore
├── ISaleDraftIdGenerator
├── Domain SaleDraft
└── ISaleFinalizationPreparationStore
        ↓
Infrastructure.Sales.InMemory
```

Application orchestre, Domain conserve les invariants, Infrastructure implémente les frontières, Desktop demeure déconnecté. Aucun handler ne dépend d’une classe Infrastructure.

## 7. Fichiers créés

- 27 fichiers sous `src/Pharmacy.Application/Sales/` ;
- 5 adaptateurs sous `src/Pharmacy.Infrastructure/Sales/InMemory/` ;
- 3 fichiers de tests Sprint 7 ;
- `docs/application/SALES_USE_CASES_V0.1.md` ;
- `docs/infrastructure/IN_MEMORY_SALES_ADAPTERS_V0.1.md` ;
- `docs/reports/sprint-07/sales-application-validation.md` ;
- `docs/reports/sprint-07/codex-report.md`.

## 8. Fichiers modifiés

- `README.md` ;
- `docs/governance/CURRENT_STATE.md` ;
- `src/Pharmacy.Application/Sales/Models/SellableProductSnapshot.cs` pour l’indicateur `IsSellable` ;
- `tests/Pharmacy.IntegrationTests/SalesArchitectureTests.cs` pour autoriser uniquement les nouveaux adaptateurs mémoire.

Aucun fichier Domain, Desktop, projet, solution ou PDF n’a été modifié.

## 9. Modèles de lecture

`SaleDraftView`, `SaleDraftLineView`, `SellableProductView` et `SaleFinalizationPreparation` exposent des données immuables. Les constructeurs copient les collections reçues ; les propriétés sont en lecture seule ou `init` pour les records de valeur. Aucun agrégat mutable n’est retourné par les handlers.

## 10. Création et consultation

`CreateSaleDraftHandler` utilise `ISaleDraftIdGenerator`, crée un brouillon vide et le stocke à la version 1. `GetSaleDraftHandler` retourne une vue reconstruite ou `sales.draft.not_found`. Les tests fournissent aussi un générateur déterministe.

## 11. Catalogue et recherche

La recherche trim la requête, retourne une liste vide sans appel technique lorsque le texte est vide, filtre les produits non vendables et plafonne à 25. Les lectures par `ProductId` et code exact sont distinctes. `SellableProductLookup` empêche de porter simultanément identifiant et code.

## 12. Ajout et fusion

L’ajout charge le brouillon, vérifie la version, résout le produit, contrôle sa vendabilité, calcule la quantité cible, vérifie la disponibilité, appelle Domain puis sauvegarde. Une fusion contrôle `existant + demandé`, pas seulement l’incrément. Tout échec conserve le snapshot stocké.

## 13. Modification, suppression et vidage

Le changement de quantité exige une ligne existante et une disponibilité suffisante. La quantité zéro reste impossible grâce à `SaleQuantity`. La suppression est distincte et ne consulte pas le stock. Le vidage incrémente la version lorsqu’il change l’état ; à vide il réussit sans sauvegarde ni incrément.

## 14. Concurrence optimiste

Chaque mutation porte `ExpectedVersion`. Le handler contrôle la version avant les lectures inutiles et le store la revérifie atomiquement lors de la sauvegarde. Un conflit retourne `sales.draft.concurrency_conflict` sans mutation. Un test de deux sauvegardes concurrentes prouve qu’une seule gagne.

## 15. Préparation de finalisation

`PrepareSaleFinalizationHandler` valide l’`OperationId`, le brouillon, la version, l’état non vide et la disponibilité de chaque ligne. Il crée un snapshot `Prepared` sans modifier le brouillon, sans réserver de stock et sans appeler `ISaleFinalizer`.

**Prepared ne signifie pas finalisée, payée, réservée ou persistée.**

## 16. Idempotence

L’empreinte structurelle contient brouillon, version, produits, quantités, prix unitaires et total, avec représentation exacte des bits `decimal`. Même identifiant et même empreinte retournent la même préparation. Toute réutilisation incohérente retourne `sales.finalization.operation_conflict`.

## 17. Store de brouillons

`InMemorySaleDraftStore` utilise un dictionnaire et un verrou privés d’instance. Il stocke des snapshots, reconstruit un agrégat à chaque lecture et remplace atomiquement le snapshot lors d’une sauvegarde valide. Les modifications d’un agrégat chargé ne touchent pas le store avant sauvegarde.

## 18. Adaptateurs synthétiques

Le catalogue contient dix références `DEMO-000001` à `DEMO-000010`, neutres et déterministes. La recherche couvre référence, nom et présentation sans sensibilité à la casse. Les disponibilités correspondent au POS synthétique actuel et restent en lecture seule. Le générateur utilise `Guid.NewGuid()`.

Le catalogue Desktop sera supprimé après connexion complète au Sprint suivant.

## 19. Sécurité et périmètre

Aucun produit réel, client, secret, fichier de données, état global mutable, réseau, HTTP, SQL, EF Core, `DbContext`, migration, repository générique, lot, FEFO, mouvement, paiement, caisse, audit ou impression n’a été introduit. Aucun `Task.Run`, `.Wait()` ou accès bloquant à une tâche n’est présent.

## 20. Packages et projets

Aucun `PackageReference`, projet ou `ProjectReference` n’a été ajouté, supprimé ou modifié. La solution conserve ses six projets et les trois packages directs déjà validés.

## 21. Tests ajoutés

57 tests déterministes :

- 14 tests unitaires Application ;
- 33 tests d’intégration Application → Infrastructure → Domain ;
- 10 tests d’architecture/périmètre.

Ils couvrent recherche, modèles immuables, produits absents/non vendables, disponibilité, fusion cible, versions, états partiels, snapshots détachés, course concurrente, préparation, idempotence, annulation et frontières techniques.

## 22. Commandes exécutées

```powershell
git status --short
git branch --show-current
git log --oneline -n 8
git remote -v
git diff --check
dotnet --version
dotnet sln Pharmacy.slnx list
dotnet build Pharmacy.slnx -c Debug
dotnet test Pharmacy.slnx -c Debug --no-build
dotnet restore Pharmacy.slnx
dotnet build Pharmacy.slnx -c Debug --no-restore
dotnet test Pharmacy.slnx -c Debug --no-build
dotnet build Pharmacy.slnx -c Release --no-restore
dotnet test Pharmacy.slnx -c Release --no-build
dotnet run --project src/Pharmacy.Desktop/Pharmacy.Desktop.csproj -c Debug
dotnet format Pharmacy.slnx --verify-no-changes --no-restore --include <fichiers Sprint 7>
git diff --cached --name-only
git diff --check
git diff --stat
git ls-files --others --exclude-standard
```

`pypdf` et `pypdfium2` ont servi à la consultation en lecture seule des PDF et à leurs rendus temporaires.

## 23. Résultats Debug

- build final : réussite, 0 avertissement, 0 erreur ;
- tests : 240 réussis, 0 échec, 0 ignoré ;
- formatage Sprint 7 : conforme.

## 24. Résultats Release

- build final : réussite, 0 avertissement, 0 erreur ;
- tests : 240 réussis, 0 échec, 0 ignoré ;
- aucun test désactivé.

## 25. Non-régression Desktop

La commande demandée a lancé un processus `Pharmacy.Desktop` répondant, avec fenêtre `Logiciel Pharmacie` et handle non nul. `CloseMainWindow()` a fermé normalement la fenêtre ; le runner s’est terminé et aucun processus résiduel n’est resté. Aucun fichier Desktop n’a changé et ses sources ne référencent pas la nouvelle chaîne Sales.

Cette validation est technique et ne revendique aucune revue visuelle humaine du POS.

## 26. Problèmes rencontrés

1. La pièce jointe Sprint 7 se termine au milieu de la section 11, après l’exemple de concurrence. Les sections finales éventuelles ne sont pas disponibles ; les gates et rapports ont repris les conventions validées des sprints précédents.
2. xUnit1051 a exigé la propagation du token de test sur chaque appel asynchrone. Tous les tests utilisent maintenant le token du runner, sans désactiver l’analyseur.
3. Deux assertions initiales d’architecture confondaient champs de lambdas générés et cache métier, ainsi que setters `init` et setters mutables. Elles ont été resserrées sur les adaptateurs publics et la mutabilité après construction.
4. Poppler n’était pas disponible ; `pypdfium2`, déjà installé, a rendu les pages pertinentes en lecture seule.

## 27. Écarts

Aucun écart fonctionnel ou architectural au texte disponible. La recherche vide retourne explicitement une liste vide. Le vidage à vide réussit sans incrément. L’empreinte est structurelle et non cryptographique. La structure exacte des sections de rapport et l’éventuelle liste de tests imposée après la section 11 n’ont pas pu être reproduites car la pièce jointe est tronquée.

## 28. Risques et limites

- tout état disparaît à la fermeture ;
- le catalogue reste temporairement dupliqué avec Desktop ;
- les disponibilités ne sont pas un stock ;
- les verrous mémoire ne remplacent pas une transaction SQL ;
- aucune finalisation réelle ou réservation n’existe ;
- les adaptateurs devront être remplacés derrière les contrats ;
- la non-régression visuelle et fonctionnelle doit être confirmée manuellement.

## 29. État Git final

- branche `main` ;
- dernier commit inchangé : `7ba1e83 feat: add sales domain core and application contracts` ;
- modifications Sprint 7 visibles et non stagées ;
- staging : zéro ;
- nouveaux commits : zéro ;
- remotes : zéro ;
- push, changement de branche, réécriture d’historique et modification de PDF : aucun ;
- `git diff --check` : propre.

## 30. Recommandation

Sales Application V0.1 est prête pour revue de code et non-régression manuelle. Maintenir la Gate ouverte jusqu’à la checklist utilisateur. Ne pas connecter le POS ou démarrer la persistance sans un sprint dédié et une pièce jointe complète.
