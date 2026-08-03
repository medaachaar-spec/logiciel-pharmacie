# Sprint 7 — Validation Sales Application V0.1

Date : 2026-08-03

Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL REGRESSION REVIEW REQUIRED`

## État initial

- branche `main`, working tree propre et aucun fichier stagé ;
- aucun remote et `git diff --check` propre ;
- dernier commit `7ba1e83 feat: add sales domain core and application contracts` ;
- SDK .NET `10.0.302` et six projets ;
- noyau Sales Sprint 6 présent, Infrastructure sans Sales et Desktop déconnecté ;
- baseline Debug : build réussi, 0 avertissement, 0 erreur ; 183/183 tests réussis, 0 ignoré.

## Cas d’utilisation validés

- création et consultation d’un brouillon ;
- recherche vide et textuelle limitée à 25 ;
- produit par identifiant et code exact ;
- ajout par identifiant ou code, fusion et disponibilité cible ;
- changement de quantité, suppression et vidage ;
- erreurs explicites sans état partiel ;
- version initiale 1, incrément réel et conflits optimistes ;
- préparation de toutes les lignes sans mutation ;
- `OperationId` non vide, réutilisation équivalente et conflit incohérent.

## Adaptateurs validés

- `InMemorySaleDraftStore` : snapshots détachés et sauvegarde atomique ;
- `InMemoryProductCatalogReader` : dix références synthétiques déterministes ;
- `InMemoryStockAvailabilityReader` : lecture seule ;
- `InMemorySaleDraftIdGenerator` : `Guid` technique ;
- `InMemorySaleFinalizationPreparationStore` : idempotence structurelle.

Tous sont locaux à leur instance et au processus. Aucun fichier, base, réseau ou état statique global mutable n’est utilisé.

## Tests

Les 183 tests initiaux sont conservés. Cinquante-sept tests Sprint 7 ont été ajoutés :

- 14 tests unitaires Application ;
- 33 tests d’intégration Application → Infrastructure → Domain ;
- 10 tests d’architecture et de périmètre.

Résultat final dans chaque configuration : 240 réussis, 0 échec, 0 ignoré.

## Builds et formatage

- restore : projets à jour ;
- Debug : réussite, 0 avertissement, 0 erreur ;
- Release : réussite, 0 avertissement, 0 erreur ;
- formatage limité à tous les fichiers Sprint 7 : conforme ;
- aucun package, projet ou référence de projet ajouté ou modifié.

## Non-régression Desktop automatisée

La commande de lancement Debug a créé une fenêtre WPF répondante intitulée `Logiciel Pharmacie`, avec handle non nul. `CloseMainWindow()` a fermé normalement l’application, le runner s’est terminé et aucun processus résiduel n’est resté.

Le code Desktop n’a pas été modifié et ne référence aucun namespace Sales Application ou Infrastructure. Cette preuve est technique et ne constitue pas une validation visuelle humaine.

## Limites

- `Prepared` ne signifie pas finalisée, payée, réservée ou persistée ;
- aucun appel à `ISaleFinalizer` ;
- aucune persistance, transaction, base, stock réel, lot, paiement, caisse, audit ou impression réelle ;
- catalogue dupliqué temporairement entre Desktop et Infrastructure ;
- état mémoire perdu à la fermeture ;
- prompt fourni tronqué après l’exemple de concurrence de la section 11.

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
