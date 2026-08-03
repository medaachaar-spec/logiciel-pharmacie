# Sprint 6 — Rapport d’exécution Codex

## 1. Statut final

**IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL REGRESSION REVIEW REQUIRED**

## 2. Résumé

Le noyau Sales V0.1 est implémenté dans Domain avec erreurs/résultats explicites, argent MAD exact, identifiants typés, référence, quantité entière, snapshot produit, ligne immuable et agrégat `SaleDraft`. Application expose les trois frontières futures demandées sans implémentation. Le POS V0.2, Infrastructure, les projets et les packages n’ont pas été modifiés.

Les 115 tests initiaux sont conservés ; 68 contrôles Sprint 6 portent le total à 183, tous réussis en Debug et Release.

## 3. Documents consultés

Sources texte lues avant modification :

1. `README.md`, `Directory.Build.props`, `global.json`, `.editorconfig`, `.gitattributes`, `.gitignore` ;
2. les cinq fichiers de `docs/governance/` ;
3. les deux modèles de `docs/templates/` ;
4. `UI_FOUNDATIONS_V0.1.md`, `APP_SHELL_V0.1.md`, `POS_V0.1.md`, `POS_V0.2_CONTINUITY.md` ;
5. les rapports Sprint 4 et Sprint 5 demandés ;
6. le code des six projets et les tests existants pertinents.

Les cinq PDF validés ont été consultés en lecture seule. Les pages pertinentes sur la stack, `decimal`, les identifiants, l’idempotence, le brouillon, les snapshots, les ventes, les contrats matériels, le POS et le workflow code-first ont été extraites puis rendues en images temporaires et inspectées. Aucun PDF n’a été modifié.

## 4. État initial

- branche `main` ;
- working tree propre et aucun fichier stagé ;
- aucun remote ;
- `git diff --check` propre ;
- SDK .NET `10.0.302` ;
- six projets dans `Pharmacy.slnx` ;
- POS V0.2 Sprint 5 présent ;
- baseline Debug : build réussi, 0 avertissement, 0 erreur ; 115/115 tests réussis, 0 ignoré.

## 5. Dernier commit initial

`21dd4a0 feat: add POS continuity and incident recovery workflows`

Le hash a été relevé avant toute modification.

## 6. Architecture créée

```text
Pharmacy.Domain
├── Common : Error, Result, Result<T>
└── Sales  : value objects -> snapshot -> SaleLine -> SaleDraft

Pharmacy.Application -> Pharmacy.Domain
└── Sales  : contrats + modèles minimaux

Pharmacy.Infrastructure : inchangée, aucune implémentation Sales
Pharmacy.Desktop        : inchangé, aucune connexion Sales
```

Les dépendances restent orientées vers l’intérieur. Aucun projet, référence de projet, package ou détail technique n’a été ajouté au Domain.

## 7. Fichiers créés

- `src/Pharmacy.Domain/Common/Error.cs`
- `src/Pharmacy.Domain/Common/Result.cs`
- `src/Pharmacy.Domain/Common/ResultOfT.cs`
- neuf fichiers dans `src/Pharmacy.Domain/Sales/`
- trois contrats dans `src/Pharmacy.Application/Sales/Contracts/`
- cinq modèles dans `src/Pharmacy.Application/Sales/Models/`
- `tests/Pharmacy.UnitTests/SalesCoreTests.cs`
- `tests/Pharmacy.IntegrationTests/SalesArchitectureTests.cs`
- `docs/domain/SALES_CORE_V0.1.md`
- `docs/reports/sprint-06/sales-core-validation.md`
- `docs/reports/sprint-06/codex-report.md`

## 8. Fichiers modifiés

- `README.md`
- `docs/governance/CURRENT_STATE.md`

Aucun fichier Desktop, Infrastructure, projet, solution ou PDF n’a été modifié.

## 9. Error et Result

`Error` contient un code stable et un message français. `Result` et `Result<T>` distinguent succès et échec, interdisent les constructeurs publics incohérents et refusent un échec sans erreur active. `Result<T>.Value` ne masque pas un échec. Les invalidités métier Sales utilisent des résultats ; les exceptions restent réservées aux contrats développeur et états impossibles.

## 10. Money

`Money` stocke exclusivement un `decimal` non négatif en MAD. Il conserve la précision reçue, expose zéro, addition, multiplication par quantité entière, comparaison et égalité par valeur. Aucun `double`, `float`, multi-devise, culture, affichage, arrondi, taxe ou remboursement signé n’est introduit.

## 11. Identifiants et références

`ProductId` encapsule un `long` positif sans génération. `SaleDraftId` encapsule un `Guid` non vide et peut générer un identifiant technique, distinct d’un numéro de vente. `ProductReference` est trimée, obligatoire, limitée localement à 128 caractères et comparée sans sensibilité à la casse. Elle n’est pas assimilée à un code-barres.

## 12. Quantité de vente

`SaleQuantity` utilise un `int` strictement positif. Création, addition, incrément, comparaison et égalité sont disponibles. L’overflow est un échec explicite. Aucune quantité fractionnaire n’est représentable dans son API publique.

## 13. Instantané produit

`SaleProductSnapshot` est immuable et conserve `ProductId`, référence, nom d’affichage, présentation facultative et prix unitaire. Les textes sont normalisés par trim. Aucun objet Product mutable, donnée clinique, marque réelle, lot, taxe, remise ou coût n’est référencé.

## 14. SaleLine

`SaleLine` est immuable. Le prix vient du snapshot, le sous-total est calculé à la demande et `ChangeQuantity` retourne une nouvelle ligne. Il n’existe ni identifiant de base, setter public, champ de total stocké ou logique de disponibilité.

## 15. SaleDraft

`SaleDraft` possède un identifiant et une liste privée exposée par `ReadOnlyCollection`. L’ajout crée une ligne ou fusionne par `ProductId`, le changement de quantité remplace une ligne immuable, la suppression signale l’absence et `Clear` est idempotent. Total, nombre de lignes, quantité totale et état vide sont projetés depuis les lignes.

## 16. Invariants métier

- prix et montants non négatifs ;
- quantité entière strictement positive ;
- un seul `ProductId` par brouillon ;
- snapshot initial conservé lors d’une fusion ;
- sous-totaux et total exacts, zéro à vide ;
- aucune mutation partielle lors d’un échec ;
- aucune modification extérieure de la collection ou des lignes ;
- aucun effet sur stock, lot, paiement, caisse, audit, impression ou persistance.

## 17. Contrats Application

`IProductCatalogReader` couvre identifiant, code et recherche texte. `IStockAvailabilityReader` retourne une disponibilité descriptive pour la quantité demandée, sans réservation ou FEFO. `ISaleFinalizer` représente uniquement la future frontière transactionnelle avec `OperationId`, conformément à l’ADR-005 sur l’idempotence. Les résultats sont structurés et asynchrones ; aucun `IQueryable`, `DbContext`, SQL, chaîne de connexion, type Desktop ou entité EF n’est exposé.

## 18. Éléments volontairement différés

Implémentations Infrastructure, cas d’utilisation, validation finale du request, persistance, transactions, concurrence, entité Product, codes-barres, stock, lots, FEFO, suspension persistée, paiements, caisse, client, remises, taxes, audit, numérotation, retours, annulations, impression et branchement au frontend.

## 19. Packages

Aucun `PackageReference` n’a été ajouté, supprimé ou modifié. Les références directes restent :

- `Microsoft.AspNetCore.Components.WebView.Wpf` 10.0.10 dans Desktop ;
- `xunit.v3.mtp-v2` 3.2.2 dans chacun des deux projets de tests.

## 20. Tests ajoutés

Soixante-huit tests `[Fact]` déterministes et numérotés :

- 5 Result/erreurs ;
- 7 Money ;
- 7 identifiants/référence ;
- 5 quantité ;
- 6 snapshot/ligne ;
- 18 `SaleDraft` ;
- 6 contrats Application ;
- 14 architecture/périmètre.

Les contrôles couvrent notamment l’encapsulation, l’immutabilité, l’overflow sans état partiel, la surface des contrats, les dépendances, l’absence d’Infrastructure Sales, la déconnexion Desktop et la compilation du POS synthétique.

## 21. Commandes exécutées

```powershell
git status --short
git branch --show-current
git log --oneline -n 7
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
dotnet format Pharmacy.slnx --verify-no-changes --no-restore
dotnet format Pharmacy.slnx --verify-no-changes --no-restore --include src/Pharmacy.Domain/Common src/Pharmacy.Domain/Sales src/Pharmacy.Application/Sales tests/Pharmacy.UnitTests/SalesCoreTests.cs tests/Pharmacy.IntegrationTests/SalesArchitectureTests.cs
git diff --cached --name-only
git diff --check
git diff --stat
git ls-files --others --exclude-standard
```

Des commandes PowerShell en lecture seule ont inspecté les sources et l’état de la fenêtre. `pypdf` a extrait le texte et `pypdfium2` a rendu les pages PDF pertinentes dans `tmp/pdfs` pour inspection visuelle temporaire.

## 22. Résultats Debug

- build final : réussite, 0 avertissement, 0 erreur ;
- tests : 183 réussis, 0 échec, 0 ignoré ;
- UnitTests et IntegrationTests réussissent tous deux.

## 23. Résultats Release

- build final : réussite, 0 avertissement, 0 erreur ;
- tests : 183 réussis, 0 échec, 0 ignoré ;
- aucun test désactivé ou conditionné pour obtenir le résultat.

## 24. Non-régression desktop

La commande demandée a lancé un processus `Pharmacy.Desktop` répondant avec fenêtre `Logiciel Pharmacie` et handle non nul. `CloseMainWindow()` a fermé normalement l’application ; le runner s’est terminé et aucun processus résiduel n’est resté. Les tests confirment la compilation du POS synthétique et la présence des routes Diagnostic et UI Lab. Aucun fichier Desktop n’a changé et aucune référence au nouveau namespace Sales n’existe dans ses sources.

Cette validation est technique. Elle ne revendique ni contrôle visuel humain, ni exécution manuelle complète du POS.

## 25. Problèmes rencontrés

1. Le premier build des nouveaux tests utilisait un nom supposé `InfrastructureAssemblyMarker` alors que le dépôt expose `Pharmacy.Infrastructure.AssemblyMarker`. Le test a été corrigé ; les validations complètes ont ensuite réussi.
2. Poppler n’était pas disponible dans le runtime local. Les pages ont été rendues avec `pypdfium2`, déjà fourni, puis inspectées sans modifier les PDF.
3. `dotnet format --verify-no-changes` global signale des espaces et fins de ligne dans `src/Pharmacy.Desktop/AssemblyInfo.cs`, fichier préexistant et hors périmètre. Le contrôle limité à tous les fichiers C# Sprint 6 réussit sans écart ; le fichier Desktop n’a pas été modifié.

## 26. Écarts

Aucun écart fonctionnel ou architectural au périmètre demandé. Trois choix locaux sont explicités : limite provisoire de 128 caractères pour `ProductReference` faute de valeur documentaire, ligne immuable retournée par `ChangeQuantity`, et contrat de finalisation minimal avec `OperationId` justifié par l’ADR. La revue humaine demandée reste volontairement non revendiquée.

## 27. Risques et limites

- `Money` ne modélise pas encore les montants signés des remboursements ;
- un overflow `decimal` reste exceptionnel et aucune précision SQL n’est appliquée ;
- les modèles Application ne constituent pas encore des cas d’utilisation validés ;
- disponibilité et finalisation n’ont aucune implémentation ;
- le POS continue d’utiliser ses modèles synthétiques Desktop ;
- la non-régression visuelle, clavier et fonctionnelle doit être confirmée manuellement.

## 28. Validation manuelle

Exécuter les commandes et les douze contrôles de `sales-core-validation.md`. Vérifier notamment le shell, Ventes, le parcours nominal, suspension/reprise, carte synthétique, impression simulée, Diagnostic, UI Lab, absence d’exception, absence de base et absence de changement visuel involontaire.

Transmettre ensuite ce rapport, les résultats build/test, le total de tests, la confirmation que le POS fonctionne toujours et tout problème observé.

## 29. État Git final

- branche `main` ;
- dernier commit inchangé : `21dd4a0 feat: add POS continuity and incident recovery workflows` ;
- modifications Sprint 6 visibles et non stagées ;
- fichiers stagés : zéro ;
- nouveaux commits : zéro ;
- remotes : zéro ;
- push, changement de branche, réécriture d’historique et modification de PDF : aucun ;
- `git diff --check` : propre.

## 30. Recommandation

Le noyau Sales V0.1 est prêt pour revue de code et non-régression manuelle. Maintenir la Gate ouverte jusqu’à la checklist utilisateur. Ne pas connecter le POS, implémenter Infrastructure ou démarrer la persistance sans un sprint dédié.
