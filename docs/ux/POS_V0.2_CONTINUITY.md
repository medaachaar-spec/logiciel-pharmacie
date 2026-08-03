# POS V0.2 — Incidents et continuité de vente

Date : 2026-08-03  
Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL CONTINUITY REVIEW REQUIRED`

## 1. Statut V0.2

Le POS V0.2 étend le vertical slice synthétique V0.1 sans créer de modèle métier définitif. Toute la logique reste dans `Pharmacy.Desktop`, en mémoire du processus, avec des articles et références `DEMO-*`. Les couches Domain, Application et Infrastructure restent inchangées.

## 2. Machine à états synthétique

```text
Panier vide
  └─ ajout → Brouillon
               ├─ suspendre → Suspension en mémoire → reprendre → Brouillon
               ├─ abandonner → Panier vide
               └─ paiement
                    ├─ espèces complètes → validation → Vente terminée
                    └─ carte NotStarted
                         → AwaitingConfirmation
                         ├─ Confirmed → validation → Vente terminée
                         ├─ NotCompleted → retour au choix
                         └─ Ambiguous → résolution manuelle confirmée

Vente terminée
  └─ Ticket NotRequested → InProgress → Printed | Failed
                                         Failed → nouvelle tentative | suite sans ticket
```

La vente et l’impression ont des états séparés. Une suspension ne contient pas de paiement. Une impression échouée ne modifie jamais le résultat de vente.

## 3. Code inconnu

Entrée sur un code sans correspondance affiche `Code inconnu` et le message : `Aucun article de démonstration ne correspond à ce code. Aucun produit n’a été créé.` Aucun article, ligne ou total n’est modifié. Les actions effacent la recherche ou reviennent au catalogue, puis replacent le focus sur le champ.

## 4. Disponibilité insuffisante

Une tentative au-delà du plafond de catalogue retourne `AvailabilityReached`. Le message adjacent au panier précise qu’il s’agit du catalogue de démonstration, affiche l’article et la quantité maximale. La quantité précédente, les autres lignes et tous les totaux restent intacts.

## 5. Suspension

`Suspendre la vente` est disponible uniquement pour un brouillon non vide hors validation. Une confirmation annonce la perte de tout état de paiement et la durée de vie limitée au processus. Le store produit des références uniques `DEMO-HOLD-0001`, `DEMO-HOLD-0002`, etc., avec heure locale, snapshots des lignes/quantités et total calculé.

Après suspension, panier et paiement courants sont remis à zéro. Le texte de recherche n’est pas conservé : il s’agit d’un filtre transitoire, pas d’un contexte utile à la reprise.

## 6. Reprise

Une reprise depuis un POS vide restaure les lignes/quantités et retire atomiquement l’élément de la liste en mémoire. Si un panier existe, aucune donnée n’est écrasée : une confirmation unique propose de revenir au panier, de le suspendre avant reprise ou de l’abandonner explicitement avant reprise.

La suppression d’une suspension exige aussi une confirmation et n’affecte aucune autre suspension.

## 7. Abandon

`Abandonner le brouillon` efface uniquement le panier courant et son paiement simulé après confirmation. Aucun résultat de vente, stock ou paiement réel n’est créé. Les suspensions existantes restent disponibles. Le POS revient à l’état vide avec focus de recherche.

## 8. Paiement carte ambigu

Les états sont :

- `NotStarted` : vérification non lancée ;
- `AwaitingConfirmation` : choix opérateur attendu ;
- `Confirmed` : vente validable ;
- `NotCompleted` : vente non validable ;
- `Ambiguous` : vérification externe obligatoire, sans relance automatique.

L’état ambigu affiche `Statut du paiement incertain` et l’instruction de vérifier le TPE externe. Chacune des trois sorties — confirmer, déclarer non réalisé, revenir au choix — passe par une confirmation supplémentaire. Aucun champ bancaire n’existe.

## 9. Impression

Le bloc `Ticket` apparaît seulement après création du `DemoCompletedSale`. Ses états sont `NotRequested`, `InProgress`, `Printed` et `Failed`. En Debug, un sélecteur clairement technique choisit le résultat simulé. Aucun appel imprimante, ESC/POS, document, PDF ou fichier n’est produit.

## 10. Réimpression

Après échec, `Réessayer l’impression` réutilise la même référence et le même résultat de vente. Chaque démarrage autorisé incrémente le compteur et met à jour l’heure locale. Un second démarrage concurrent est rejeté. `Continuer sans ticket` conserve la vente et marque explicitement la décision de poursuivre.

## 11. Règles clavier

- recherche focalisée au démarrage, après incident, suspension, reprise, abandon et nouvelle vente ;
- Entrée réservée à l’ajout d’un code exact dans la recherche ;
- boutons critiques de type `button`, sans soumission implicite ;
- Escape ferme la confirmation courante sans exécuter l’action ;
- une seule confirmation rendue à la fois ;
- focus visible hérité des fondations UI et ordre DOM logique.

## 12. États responsive

- 1920 × 1080 : catalogue et panier en grille large ;
- 1366 × 768 : grille comptoir compacte ;
- 1100 × 700 : basculement en colonne sous 72 rem, panier et action principale conservés dans le flux ;
- sous 768 px : confirmations et actions empilées ;
- sous 576 px : recherche, résumé et actions en colonne pleine largeur.

Les conteneurs utilisent `minmax(0, 1fr)`, flex-wrap et aucune largeur fixe susceptible de créer un débordement horizontal.

## 13. Limites de la mémoire

Suspensions, références, paiement, résultat et tentatives d’impression disparaissent à la fermeture de l’application. Il n’existe ni fichier, registre, JSON, SQL, API ni serveur. Les plafonds affichés ne représentent pas un stock réel.

## 14. Éléments différés

Persistance, transactions métier, SQL Server, stock/mouvements/lots, caisse, crédit, retours, annulations finalisées, audit, scanner, TPE et imprimante réels restent hors périmètre. Les états synthétiques ne constituent pas les futures entités Domain.

## 15. Critères de validation humaine

La revue doit confirmer les cinq captures imposées, les trois résolutions cibles, le clavier/focus, l’absence de débordement aux trois résolutions et la lisibilité des conséquences avant chaque action destructive. Le détail exécutable se trouve dans `docs/reports/sprint-05/pos-continuity-validation.md`.
