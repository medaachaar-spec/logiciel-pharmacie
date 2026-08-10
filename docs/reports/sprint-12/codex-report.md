# Rapport Codex — Sprint 12

Statut : `IMPLEMENTED — SQL PURCHASING VALIDATION PASS — MANUAL RECEIVING REVIEW REQUIRED`

## 1. Complétude du prompt

`docs/prompts/Sprint_12_Fournisseurs_Achats_Receptions_SQL.md` a été lu intégralement : 930 lignes, 22 345 octets, SHA-256 `591346A96F7FF9284C494D076B5CE6BBB3953BF4B9F1B22E9E3FC9EFF29B6027`. La dernière ligne non vide est exactement `FIN DU PROMPT SPRINT 12`.

Les sources gouvernance, Persistence/Catalogue/Inventory, rapports Sprint 10/11, migrations et cinq PDF Phase 1 à 5 ont été inspectés. Les pages pertinentes des PDF ont aussi été rendues visuellement. La réception directe est confirmée par PUR-008 ; les statuts de commande retenus viennent de Phase 2/3.

## 2. État initial

- branche `main` ; HEAD `a46ffb7` ; aucun remote ; index vide ; prompt Sprint 12 seul fichier non suivi ;
- solution de six projets .NET 10, outils EF 10.0.10 sans package nouveau ;
- baseline Debug : build 0 avertissement/erreur, 321/321 tests ;
- SQL Server Developer 17.0.1125.2, compatibilité 170, trois migrations appliquées avant Sprint 12 ;
- Catalogue et Inventory présents, schéma `purchasing` absent.

## 3. Modèle et schéma

Le schéma `purchasing` contient exactement : `Suppliers`, `PurchaseOrders`, `PurchaseOrderLines`, `GoodsReceipts`, `GoodsReceiptLines`. Les PK sont `bigint IDENTITY`, les FK `NO ACTION`, les instants `datetime2(7)` UTC, péremptions/dates document `date`, quantités `int`, prix d’achat `decimal(19,4)`. Quatre entités mutables portent une `rowversion`.

Aucune table spéculative de référence fournisseur-produit, document comptable, ventilation de lot, retour, avoir, taxe, paiement ou marge n’a été créée. Le diagramme et la matrice de traçabilité se trouvent dans les documents Database V0.1.

## 4. Fournisseurs

Recherche/liste/détail, création, modification et activation logique sont connectés de Desktop à SQL via les handlers Application. Code unique, nom requis et concurrence optimiste sont validés. Une suppression SQL d’un fournisseur référencé est refusée par FK.

Erreurs stables confirmées : introuvable, code dupliqué, inactif et conflit de concurrence. Aucune donnée bancaire ou fiscale n’est modélisée.

## 5. Commandes d’achat

États : `Draft`, `Confirmed`, `PartiallyReceived`, `FullyReceived`, `Cancelled`. Un brouillon accepte ajout/fusion, modification et retrait de lignes ; une commande vide est refusée à la confirmation. La confirmation fige les lignes et ne crée aucun lot, mouvement ou stock. L’annulation est contrôlée et interdite après réception.

Les snapshots code/nom et le prix d’achat facultatif sont conservés. Les quantités reçues/restantes et le total descriptif sont projetés sans calcul de marge. Le `rowversion` de commande est explicitement touché lors d’une mutation de ligne, y compris lorsque l’horloge retourne le même instant.

## 6. Réceptions, lots et péremptions

Réceptions liées, partielles, complètes et directes sont fonctionnelles. Une réception de commande vérifie le fournisseur, le statut, la ligne, le produit et le reliquat cumulé. Le BL est unique par fournisseur. Une réception terminée est append-only.

Un lot existant doit être actif, appartenir au produit et satisfaire son suivi lot/péremption. Un nouveau lot est créé ou réutilisé par produit/numéro avec péremption compatible. Une péremption absente lorsqu’elle est requise ou antérieure à la date métier est refusée. Les produits sans lot utilisent la balance sans lot.

## 7. Prix d’achat

Le prix reçu est un snapshot non négatif sur la ligne de réception. Le scénario SQL confirme que `catalog.ProductLocalSettings.CurrentSalePrice` reste inchangé. Aucun prix de vente, historique Catalogue, taxe ou marge n’est calculé automatiquement.

## 8. Transaction, mouvements et balances

`SqlPurchasingStore` utilise une stratégie EF, un contexte court et une transaction `Serializable`. Validation, lot, réception/lignes, mouvement, balance, quantités reçues et statut de commande sont committés ensemble. Les échecs quantité, lot, péremption, BL, concurrence ou FK ne laissent aucune écriture partielle.

Le nouveau mouvement append-only `PurchaseReceipt` est entrant et couvert par la contrainte de direction Inventory. Chaque ligne reçoit un mouvement unique et une FK directe vers son lot facultatif. Les balances locales ajoutées dans la même transaction sont réutilisées afin d’éviter un doublon sur plusieurs lignes identiques.

## 9. Idempotence et concurrence

Une réception utilise `OperationId` unique et empreinte SHA-256 normalisée. Même identifiant/contenu retourne le même résultat sans second mouvement ni seconde augmentation ; contenu différent retourne `purchasing.receipt.operation_conflict`. Les identifiants techniques dérivés sont déterministes.

Les conflits `rowversion` fournisseur/commande sont prouvés sur SQL réel. Aucun retry aveugle n’est appliqué aux erreurs métier ou de concurrence. Le Desktop bloque les doubles soumissions dans `UiButton`, dans le coordinateur `IsBusy`, puis dans SQL.

## 10. Migration et scripts

Migration additive : `20260804214802_PurchasingPersistenceV01`. Elle crée cinq tables et étend seulement la contrainte de direction Inventory pour `PurchaseReceipt`. Les trois migrations antérieures sont inchangées. Le script idempotent `004_purchasing_persistence_v01_idempotent.sql` ne contient ni seed ni secret.

`database update` : base déjà à jour. `migrations list` : quatre migrations. `has-pending-model-changes` : aucun changement.

## 11. Seed, clear et vérification

Le seed transactionnel manuel crée quatre fournisseurs `DEMO-S12-*`, deux commandes et une réception partielle sur des produits `DEMO-*`. Deux passages successifs ne dupliquent rien. Le verify final retourne PASS avec 4 fournisseurs, 2 commandes et 1 réception.

Le clear ciblé inverse son propre mouvement/balance, supprime ses lignes dans l’ordre FK et préserve Catalogue/Stock extérieur. Un premier essai a révélé les options SET exigées par les index filtrés ; `QUOTED_IDENTIFIER` et les options ANSI ont été ajoutés. Le cycle clear → reseed → verify a ensuite réussi.

## 12. Desktop et diagnostic

`/module/purchasing` remplace le placeholder. Trois zones couvrent fournisseurs, commandes et réceptions. La page réutilise les surfaces/tokens Catalogue, contient loading/vide/erreur/SQL indisponible/concurrence/duplicate BL/partiel/terminé, régions de tableaux défilables, labels natifs, `aria-live`, `aria-busy` et confirmations `alertdialog`.

Les avertissements visibles rappellent l’augmentation immédiate du Stock, les ventes non persistées, l’absence de marge et les données Demo. Le Diagnostic expose connectivité, migrations, compteurs et effet Stock sans connexion ni détail SQL sensible.

Smokes Release avec SQL puis sans configuration : fenêtre `Logiciel Pharmacie` répondante, handle valide, enfant WebView2 présent, fermeture normale code 0 dans les deux cas.

## 13. Tests et gates

- tool restore/restore : PASS ;
- Debug : build 0 avertissement/erreur, tests 336/336 PASS, 0 ignoré ;
- Release : build 0 avertissement/erreur, tests 336/336 PASS, 0 ignoré ;
- test SQL réel isolé : fournisseurs, unique/concurrence, fusion/retrait/confirmation/annulation, partiel/complet/direct, dépassement, fournisseur/produit absents, lot/péremption, duplicate BL, idempotence, stock, snapshot prix, FK et diagnostic ;
- base temporaire supprimée en `finally` ; zéro base `Pharmacy_Sprint12_Test_*` résiduelle ;
- `git diff --check` : propre.

## 14. Sécurité

Aucun package, projet ou référence de projet ajouté. Aucun secret, token, mot de passe, connexion authentifiée, donnée patient, export ou donnée réelle. Le scan spécialisé ne trouve `Password=`/`Pwd=` que dans le prompt historique et les assertions négatives existantes. Les adresses de seed utilisent `example.invalid` et les numéros sont explicitement synthétiques.

## 15. Écarts et validation manuelle

SQL Server Developer valide le sprint ; SQL Server 2025 Express et le certificat TLS approuvé restent des gates pilote. L’autorisation métier réelle de la réception directe n’est pas encore implémentée : l’UI la présente comme voie prototype contrôlée. Aucun retour fournisseur, avoir, document comptable ou paiement n’est inclus.

La revue humaine reste requise aux résolutions 1100×700, 1366×768 et 1920×1080, clavier/focus inclus. Le guide et les huit captures demandées sont détaillés dans `purchasing-validation.md`.

## 16. État Git final

- branche : `main` ; HEAD initial/final : `a46ffb7` ;
- 17 fichiers suivis modifiés et 41 nouveaux fichiers non suivis, tous visibles pour revue ;
- index/staging : 0 fichier ; commits Sprint 12 : 0 ;
- remotes : 0 ; push : aucun ; changement de branche : aucun ;
- aucun `git add`, commit, push, remote, stash, reset ou réécriture d’historique exécuté.
