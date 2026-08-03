# Sprint 4 — Validation du POS V0.1

Date : 2026-08-03  
Statut : `AUTOMATED CHECKS PASS — MANUAL POS REVIEW REQUIRED`

## Périmètre automatisé

- Route Ventes dédiée `/module/sales` et navigation marquée disponible.
- Catalogue immuable de dix articles fictifs avec codes/identifiants uniques et prix `decimal` positifs.
- Recherche nom/code insensible à la casse et ajout par code exact.
- Panier : fusion, quantité, minimum, plafond synthétique, suppression, vidage et totaux.
- Paiement espèces : montant insuffisant refusé et monnaie exacte.
- Paiement carte : confirmation TPE opérateur obligatoire.
- Verrouillage d’une seconde validation et résultat `DEMO-SALE-*` en mémoire.
- Réinitialisation complète d’une nouvelle vente.
- Absence d’accès persistant/réseau dans le POS, d’URL externe et de nouveau package.
- Conservation des sept placeholders, de Diagnostic et de l’UI Lab.
- Graphe des six projets inchangé.

La dernière passe automatisée détaillée et le lancement desktop sont consignés dans `codex-report.md`.

## Commandes de revue

```powershell
cd "C:\Users\HP\Desktop\Logiciel pharmacie"
dotnet build .\Pharmacy.slnx -c Debug
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet run --project .\src\Pharmacy.Desktop\Pharmacy.Desktop.csproj -c Debug
git status --short
git diff --stat
```

## Checklist manuelle

- [ ] le POS s’ouvre depuis Ventes ;
- [ ] le bandeau de démonstration est visible ;
- [ ] la recherche fonctionne ;
- [ ] un code exact suivi d’Entrée ajoute l’article ;
- [ ] un code inconnu ne crée rien ;
- [ ] ajouter deux fois fusionne la ligne ;
- [ ] les quantités et sous-totaux sont corrects ;
- [ ] la limite synthétique est respectée ;
- [ ] la suppression fonctionne ;
- [ ] vider le panier demande confirmation ;
- [ ] paiement espèces refuse un montant insuffisant ;
- [ ] la monnaie est correcte ;
- [ ] paiement carte exige la confirmation TPE ;
- [ ] double validation impossible ;
- [ ] succès affiché ;
- [ ] nouvelle vente réinitialise tout ;
- [ ] navigation clavier correcte ;
- [ ] focus visible ;
- [ ] aucun débordement horizontal à 1100 × 700, 1366 × 768 et 1920 × 1080 ;
- [ ] aucune base ou donnée réelle ;
- [ ] aucun message d’exception.

## Captures et résultat demandés

Fournir au minimum :

1. POS avec panier vide ;
2. POS avec au moins deux lignes ;
3. paiement espèces avec monnaie calculée ;
4. écran de succès.

Fournir également le résultat manuel d’un paiement carte simulé : sélection Carte, vérification du bouton désactivé avant confirmation TPE, confirmation, validation et succès.

Ces vérifications relèvent de l’utilisateur. Le contrôle technique Codex ne constitue pas une validation visuelle humaine.
