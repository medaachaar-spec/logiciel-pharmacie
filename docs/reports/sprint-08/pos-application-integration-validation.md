# Sprint 8 — Validation POS Application Integration V0.1

Date : 2026-08-03

Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL POS INTEGRATION REVIEW REQUIRED`

## Prompt

Le fichier demandé `docs/prompts/sprint-08-pos-application-integration.md` n’existe pas sous ce nom. Le fichier présent et lu intégralement est `docs/prompts/Sprint_08_Connexion_POS_Application_Sales.md` : 648 lignes, 20 664 caractères, dernière ligne non vide exactement `FIN DU PROMPT SPRINT 8`.

## Baseline

- branche `main` ;
- hash initial `7ba1e83 feat: add sales domain core and application contracts` ;
- aucun remote ;
- six projets et SDK .NET `10.0.302` ;
- build Debug : 0 avertissement, 0 erreur ;
- tests : 240 réussis, 0 échec, 0 ignoré ;
- les fichiers Sprint 7 étaient déjà stagés avant toute opération Sprint 8 ; cet index utilisateur a été préservé.

## Résultats automatisés

- 31 nouveaux tests Sprint 8 : 5 unitaires et 26 intégration/architecture ;
- Debug : build réussi, 0 avertissement, 0 erreur ; 271/271 tests réussis ;
- Release : build réussi, 0 avertissement, 0 erreur ; 271/271 tests réussis ;
- formatage ciblé : conforme ;
- aucun test ignoré ou désactivé ;
- aucun package, projet ou `ProjectReference` ajouté ;
- Domain et Application existante non modifiés par le Sprint 8 ;
- aucun SQL, EF Core, fichier de données, réseau, finalisation ou `ISaleFinalizer`.

## Lancement Desktop

`dotnet run --project src/Pharmacy.Desktop/Pharmacy.Desktop.csproj -c Debug --no-build` a démarré une fenêtre répondante intitulée `Logiciel Pharmacie`, avec handle non nul. La fermeture par `CloseMainWindow()` a réussi, le runner a retourné 0, stderr est vide et aucun processus résiduel n’est resté.

L’arbre HTML interne de WebView2 n’était pas exposé à UI Automation pendant ce smoke masqué. Les routes et le branchement Ventes sont validés par compilation/tests, mais aucune revue visuelle ou interactive humaine n’est revendiquée.

## Commandes de validation manuelle

```powershell
cd "C:\Users\HP\Desktop\Logiciel pharmacie"
dotnet build .\Pharmacy.slnx -c Debug
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet run --project .\src\Pharmacy.Desktop\Pharmacy.Desktop.csproj -c Debug
git status --short
git diff --stat
```

## Checklist manuelle

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
- [ ] navigation Tab/Shift+Tab, Entrée/Espace et focus visible ;
- [ ] responsive à 1100×700, 1366×768 et 1920×1080 sans débordement horizontal ;
- [ ] Diagnostic et UI Lab ;
- [ ] aucune base connectée ;
- [ ] aucune exception.

## Captures et résultats à transmettre

Fournir cinq captures :

1. POS vide avec bandeau ;
2. recherche/catalogue ;
3. panier avec deux lignes ;
4. suspension/reprise ;
5. succès avec impression.

Transmettre aussi le résultat du scan exact, du code inconnu, de la disponibilité insuffisante et de l’anti-double-clic, ainsi que le total de tests et tout problème observé. Aucun commit n’est demandé.

