# Sprint 12 — Purchasing validation

Statut : `IMPLEMENTED — SQL PURCHASING VALIDATION PASS — MANUAL RECEIVING REVIEW REQUIRED`

## Validation automatisée

- `dotnet tool restore` et `dotnet restore Pharmacy.slnx` : PASS.
- build Debug : PASS, 0 avertissement, 0 erreur.
- tests Debug : 336/336 PASS, 0 échec, 0 ignoré.
- build Release : PASS, 0 avertissement, 0 erreur.
- tests Release : 336/336 PASS, 0 échec, 0 ignoré.
- migration `20260804214802_PurchasingPersistenceV01` : appliquée ; base à jour.
- `has-pending-model-changes` : aucun changement en attente.
- seed : deux passages successifs sans duplication.
- verify : PASS — 4 fournisseurs Demo, 2 commandes, 1 réception.
- clear ciblé puis reseed : PASS ; Catalogue et stocks extérieurs préservés.
- test SQL réel : PASS ; réception partielle/complète/directe, contraintes, concurrence, idempotence, rollback et prix de vente inchangé.
- bases temporaires `Pharmacy_Sprint12_Test_*` résiduelles : 0.
- smoke Desktop avec SQL et sans configuration SQL : fenêtre répondante, WebView2 chargé, sortie 0.
- secrets/packages/diff : aucun credential actif, aucun package nouveau, `git diff --check` propre.

## Guide de validation manuelle

1. Ouvrir Fournisseurs, créer un fournisseur `DEMO-S12-MANUAL-*`, puis le modifier.
2. Créer une commande avec deux lignes Catalogue et la confirmer ; vérifier que Stock ne change pas.
3. Préparer une réception partielle avec BL unique, lot et péremption, puis confirmer.
4. Dans Stock, vérifier lot, balance augmentée et mouvement `PurchaseReceipt`.
5. Réceptionner le reliquat et vérifier `FullyReceived`.
6. Retenter le même BL puis un double clic : aucun second mouvement.
7. Tester une réception directe et lire l’avertissement.
8. Couper SQL : page/Diagnostic doivent signaler la panne sans fallback ni écriture.
9. Recontrôler Catalogue, Stock et POS ; une vente simulée ne décrémente toujours pas le stock.

Captures demandées : liste fournisseurs, commande brouillon, commande confirmée, réception partielle, réception terminée, lot/mouvement dans Stock, vérification SSMS et état SQL indisponible.
