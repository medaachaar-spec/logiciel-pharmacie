# Sprint 5 — Validation de continuité du POS V0.2

Date : 2026-08-03  
Statut : `AUTOMATED CHECKS PASS — MANUAL CONTINUITY REVIEW REQUIRED`

## Résultats automatisés

- Debug : build réussi, 0 avertissement, 0 erreur ; 115/115 tests réussis, 0 ignoré.
- Release : build réussi, 0 avertissement, 0 erreur ; 115/115 tests réussis, 0 ignoré.
- Quarante tests Sprint 5 couvrent exactement les incidents, suspensions, reprise, abandon, carte ambiguë, impression et garde-fous d’architecture demandés.
- Aucun package, projet, ProjectReference, accès SQL/réseau, donnée bancaire, fichier de ticket ou périphérique n’a été ajouté.
- Le lancement desktop a produit un processus et une fenêtre répondants, puis une fermeture propre.

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

- [ ] code inconnu clairement signalé ;
- [ ] aucun produit créé ;
- [ ] limite de disponibilité respectée ;
- [ ] panier intact après erreur ;
- [ ] suspension possible ;
- [ ] référence de suspension visible ;
- [ ] avertissement sur la mémoire temporaire visible ;
- [ ] plusieurs ventes peuvent être suspendues ;
- [ ] reprise correcte ;
- [ ] panier courant jamais écrasé ;
- [ ] suppression d’une suspension confirmée ;
- [ ] abandon du brouillon confirmé ;
- [ ] paiement carte ambigu bloque la validation ;
- [ ] message de vérification TPE clair ;
- [ ] résolution manuelle fonctionnelle ;
- [ ] aucune donnée bancaire demandée ;
- [ ] vente terminée avant impression ;
- [ ] échec d’impression ne supprime pas la vente ;
- [ ] réimpression simulée fonctionne ;
- [ ] nombre de tentatives visible ;
- [ ] double action bloquée ;
- [ ] nouvelle vente réinitialise tous les états ;
- [ ] navigation clavier correcte ;
- [ ] focus visible ;
- [ ] aucun débordement à 1100 × 700, 1366 × 768 et 1920 × 1080 ;
- [ ] aucune exception.

## Captures obligatoires

Fournir au minimum cinq captures :

1. code inconnu avec message et actions ;
2. disponibilité insuffisante avec article et maximum ;
3. liste de plusieurs ventes suspendues ;
4. paiement carte au statut ambigu ;
5. vente terminée avec échec d’impression.

## Confirmations manuelles obligatoires

Confirmer par écrit :

- la reprise correcte d’une vente suspendue ;
- la réussite d’une réimpression après un premier échec ;
- la prévention du double clic sur validation, reprise ou impression.

Ces contrôles relèvent de l’utilisateur. Les captures techniques Codex et les tests automatisés ne constituent pas une validation humaine.
