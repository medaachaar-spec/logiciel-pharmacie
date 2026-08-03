# Sprint 6 — Validation du noyau Sales V0.1

Date : 2026-08-03

Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL REGRESSION REVIEW REQUIRED`

## État initial

- branche `main` et working tree propre ;
- aucun fichier stagé, aucun remote et `git diff --check` propre ;
- dernier commit `21dd4a0 feat: add POS continuity and incident recovery workflows` ;
- SDK .NET `10.0.302` et six projets dans `Pharmacy.slnx` ;
- POS V0.2 synthétique présent ;
- baseline Debug : build réussi, 0 avertissement, 0 erreur ; 115/115 tests réussis, 0 ignoré.

## Structure créée

- `Pharmacy.Domain/Common` : `Error`, `Result`, `Result<T>` ;
- `Pharmacy.Domain/Sales` : `Money`, identifiants, référence, quantité, snapshot, ligne, brouillon et erreurs ;
- `Pharmacy.Application/Sales/Contracts` : catalogue, disponibilité, finalisation ;
- `Pharmacy.Application/Sales/Models` : cinq modèles minimaux ;
- tests : `SalesCoreTests` et `SalesArchitectureTests` ;
- documentation : spécification Domain et rapports Sprint 6.

## Invariants validés

- argent `decimal`, MAD, non négatif, sans arrondi caché ;
- identifiants et référence invalides refusés ;
- quantité entière strictement positive ;
- snapshot et ligne immuables ;
- collection du brouillon en lecture seule ;
- unicité par `ProductId` et fusion des quantités ;
- sous-totaux et total calculés exactement ;
- erreurs structurées et absence de mutation partielle ;
- aucun stock, lot, paiement, caisse, audit ou effet technique.

## Contrats Application

- `IProductCatalogReader` : identifiant, code et recherche textuelle ;
- `IStockAvailabilityReader` : disponibilité descriptive pour une quantité demandée ;
- `ISaleFinalizer` : future frontière transactionnelle avec `OperationId` idempotent ;
- aucune implémentation, entité EF, requête, SQL, chaîne de connexion ou type Desktop.

## Tests

Les 115 tests initiaux sont conservés. Soixante-huit `[Fact]` Sprint 6 numérotés couvrent exactement les catégories demandées : 48 contrôles Domain, 6 contrôles de contrats Application et 14 contrôles d’architecture/périmètre.

Résultat final dans chaque configuration : 183 réussis, 0 échec, 0 ignoré.

## Builds

- restore : projets à jour ;
- Debug : réussite, 0 avertissement, 0 erreur ;
- Release : réussite, 0 avertissement, 0 erreur ;
- aucun `PackageReference` ajouté, supprimé ou modifié.

## Non-régression desktop automatisée

La commande de lancement Debug a créé le processus `Pharmacy.Desktop`, une fenêtre répondante intitulée `Logiciel Pharmacie` avec handle non nul, puis `CloseMainWindow()` a produit une fermeture normale. Le runner s’est terminé et aucun processus résiduel n’est resté.

Les tests confirment que le POS synthétique compile et que les routes Diagnostic/UI Lab restent présentes. Les sources Desktop n’utilisent aucun type du nouveau noyau. Cette preuve est technique et ne constitue pas une validation visuelle humaine.

## Limites

- aucune persistance ou base connectée ;
- aucune finalisation réelle ;
- aucune gestion de lot, stock, paiement, caisse, audit ou impression réelle ;
- aucune connexion du noyau au POS ;
- longueur maximale de référence 128 à confirmer ;
- validation visuelle et fonctionnelle manuelle restante.

## Commandes de revue manuelle

```powershell
cd "C:\Users\HP\Desktop\Logiciel pharmacie"
dotnet build .\Pharmacy.slnx -c Debug
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet run --project .\src\Pharmacy.Desktop\Pharmacy.Desktop.csproj -c Debug
git status --short
git diff --stat
```

## Checklist manuelle restante

- [ ] l’application s’ouvre ;
- [ ] le shell est inchangé visuellement ;
- [ ] le module Ventes reste accessible ;
- [ ] le POS nominal fonctionne encore ;
- [ ] la suspension et la reprise fonctionnent encore ;
- [ ] le paiement carte synthétique fonctionne encore ;
- [ ] l’impression simulée fonctionne encore ;
- [ ] Diagnostic reste accessible ;
- [ ] UI Lab reste accessible ;
- [ ] aucun message d’exception ;
- [ ] aucune base de données connectée ;
- [ ] aucun changement visuel involontaire.

Après la revue, transmettre le rapport, les résultats build/test, le total de tests, la confirmation que le POS fonctionne toujours et tout problème observé.
