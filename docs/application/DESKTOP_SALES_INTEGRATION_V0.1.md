# Intégration Desktop vers Sales Application V0.1

Date : 2026-08-03

Statut : `IN-MEMORY APPLICATION INTEGRATION — AUTOMATED CHECKS PASS`

## 1. Chaîne d’exécution

```text
Composants Razor POS
        ↓
SalesPosCoordinator (Desktop, scoped)
        ↓
Handlers Sales Application (transient)
        ↓
Contrats Application
        ↓
Adaptateurs Infrastructure en mémoire (singleton)
        ↓
SaleDraft Domain
```

Razor injecte uniquement `SalesPosCoordinator`. Il ne construit ni store, ni adaptateur, ni agrégat Domain.

## 2. Composition root

`AddInMemorySalesAdapters` enregistre une chaîne cohérente pour tout le processus Desktop :

- `IProductCatalogReader` → `InMemoryProductCatalogReader`, singleton ;
- `IStockAvailabilityReader` → `InMemoryStockAvailabilityReader`, singleton ;
- `ISaleDraftStore` → `InMemorySaleDraftStore`, singleton ;
- `ISaleDraftIdGenerator` → `InMemorySaleDraftIdGenerator`, singleton ;
- `ISaleFinalizationPreparationStore` → `InMemorySaleFinalizationPreparationStore`, singleton ;
- handlers Sprint 7, transients ;
- `DemoTicketPrintService` et `SalesPosCoordinator`, scoped.

Les singletons garantissent que tous les handlers résolus voient les mêmes brouillons et préparations pendant le processus. `ISaleFinalizer` n’est pas enregistré.

## 3. Responsabilités du coordinateur

Le coordinateur conserve uniquement l’identifiant/version/vue courants, les résultats de recherche, les indicateurs busy, les messages UI, les suspensions Desktop et les états simulés de paiement/impression.

Il expose `InitializeAsync`, `SearchAsync`, `AddProductAsync`, `AddExactCodeAsync`, `ChangeQuantityAsync`, `RemoveItemAsync`, `ClearAsync`, `ReloadDraftAsync`, `SuspendCurrentDraftAsync`, `ResumeSuspendedDraftAsync`, `AbandonCurrentDraftAsync` et `StartNewDraftAsync`.

Il ne fusionne pas les lignes, ne calcule pas les totaux métier, ne vérifie pas la disponibilité comme règle, ne crée pas d’agrégat Domain et ne dépend d’aucun type Infrastructure concret.

## 4. Handlers utilisés

- `CreateSaleDraftHandler` : création du brouillon initial, après suspension, à la reprise et pour Nouvelle vente ;
- `SearchSellableProductsHandler` : catalogue initial et recherche ;
- `GetSellableProductByCodeHandler` : scan exact ;
- `AddProductToSaleDraftHandler` : ajout, fusion et reconstruction de suspension ;
- `ChangeSaleItemQuantityHandler` : quantité cible strictement positive ;
- `RemoveSaleItemHandler` : suppression explicite ;
- `ClearSaleDraftHandler` : vidage/abandon ;
- `GetSaleDraftHandler` : rechargement après conflit.

`PrepareSaleFinalizationHandler` est résolu par le composition root pour compléter la chaîne Sprint 7 mais n’est jamais injecté ou appelé par le POS.

## 5. Gestion de version

Après chaque succès, le coordinateur remplace intégralement `CurrentDraft` par la vue retournée. La version attendue suivante est donc toujours celle du dernier résultat Application. Un conflit provoque uniquement une lecture du brouillon courant ; aucun retry automatique ou last-write-wins n’existe.

## 6. Mapping d’erreurs

`SalesPosErrorMapper` est une fonction Desktop pure, testée unitairement. Les codes techniques restent stables tandis que les messages exposés au comptoir sont sûrs et actionnables. Le détail technique des exceptions inattendues n’est jamais affiché.

## 7. Asynchronisme et annulation

Tous les appels Application reçoivent le `CancellationToken` de la page. Aucun `.Result`, `.Wait()`, `GetAwaiter().GetResult()`, `Task.Run` ou temporisation de test fragile n’est introduit. Les délais existants de 180 ms restent limités aux simulations visuelles de paiement et d’impression.

## 8. Tests

Les tests unitaires vérifient le mapping d’erreurs. Les tests d’intégration instancient les vrais handlers avec les vrais adaptateurs mémoire et couvrent initialisation, catalogue, scan, panier, disponibilité, concurrence, suspensions, reprise, paiement et impression. Les tests d’architecture inspectent le composition root, Razor, les dépendances et les interdictions SQL/package.

## 9. Limites

- aucun état ne survit au processus ;
- une suspension n’est pas persistée dans Application ;
- un brouillon vide ou partiellement reconstruit peut rester dans le store mémoire ;
- disponibilité et catalogue sont synthétiques ;
- aucune réservation, finalisation, transaction SQL, vente, paiement ou impression réelle ;
- les classes `Demo*` historiques restent dans le code mais ne pilotent plus le parcours nominal.

