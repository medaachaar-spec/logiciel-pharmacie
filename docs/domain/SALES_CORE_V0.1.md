# Noyau métier Sales V0.1

Date : 2026-08-03

Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL REGRESSION REVIEW REQUIRED`

## 1. Objectif

Fournir un premier noyau métier de vente fiable, testable et indépendant de l’interface. Cette version représente uniquement un brouillon de vente et les frontières nécessaires à une future orchestration Application.

## 2. Statut V0.1

La V0.1 est implémentée et validée automatiquement. Elle ne constitue ni une vente finalisée, ni une persistance, ni une intégration au POS. La revue manuelle porte uniquement sur la non-régression du Desktop existant.

## 3. Périmètre

Le périmètre comprend les erreurs et résultats explicites, l’argent en MAD, les identifiants typés, la référence produit, la quantité de vente entière, le snapshot produit, la ligne immuable, l’agrégat `SaleDraft` et trois contrats Application non implémentés.

Sont explicitement absents : persistance, finalisation réelle, lots, paiements réels, stock réel, caisse, client, remise, TVA, audit, impression et connexion au POS.

## 4. Architecture

```text
Pharmacy.Domain
├── Common : Error, Result, Result<T>
└── Sales  : value objects, snapshot, SaleLine, SaleDraft

Pharmacy.Application -> Pharmacy.Domain
└── Sales : contrats et modèles minimaux

Pharmacy.Infrastructure : aucune implémentation Sales
Pharmacy.Desktop        : aucune connexion au noyau Sales
```

Domain ne référence aucun autre projet Pharmacy, framework UI, ORM ou détail technique. Application ne référence ni Infrastructure ni Desktop.

## 5. Error et Result

`Error` porte un code technique stable et un message français lisible. `Error.None` représente l’absence d’erreur active. `Result` et `Result<T>` ont des constructeurs privés : un succès ne peut pas porter d’erreur active et un échec ne peut pas être créé sans erreur. Lire `Value` sur un échec provoque une exception de contrat développeur ; les règles métier attendues retournent un échec explicite.

## 6. Money

`Money` conserve exactement un `decimal` non négatif et utilise uniquement la devise `MAD`. `Zero`, addition, multiplication par `SaleQuantity`, comparaison et égalité par valeur sont disponibles. Aucun `double`, `float`, formatage culturel, arrondi fiscal, TVA ou conversion multi-devise n’est présent. La prise en charge de montants signés sera redéfinie avec les retours et écritures inverses.

## 7. Identifiants

`ProductId` encapsule un `long` strictement positif, sans génération Domain. `SaleDraftId` encapsule un `Guid` non vide ; `New()` génère uniquement un identifiant technique de brouillon. Aucun de ces identifiants n’est un numéro de vente visible.

## 8. Référence produit

`ProductReference` impose une valeur non vide, trimée et limitée à 128 caractères. Aucune longueur n’étant fixée dans les documents de référence, 128 est une limite locale de sûreté V0.1 à confirmer lors du dictionnaire de données détaillé. L’égalité est ordinale, insensible à la casse et cohérente avec le hash. Cette référence n’est pas obligatoirement un code-barres et aucun code réel n’est utilisé.

## 9. Quantité

`SaleQuantity` encapsule un `int` strictement positif. La création, l’incrément, l’addition contrôlée, la comparaison et l’égalité sont disponibles. Un dépassement de capacité produit `sales.quantity.overflow`.

V1 : une unité de stock correspond à une unité vendue ; ventes fractionnaires différées.

## 10. Instantané produit

`SaleProductSnapshot` est immuable et contient `ProductId`, `ProductReference`, nom d’affichage obligatoire, présentation facultative et prix unitaire. Il isole la ligne d’une future entité Product mutable. Il ne contient aucune donnée clinique, marque réelle, composition, posologie, lot, expiration, taxe, remise, coût d’achat, PPH ou marge.

## 11. Ligne de vente

`SaleLine` est immuable. Elle expose le snapshot, le produit, la quantité, le prix unitaire et un sous-total calculé par `prix × quantité`. `ChangeQuantity` retourne une nouvelle ligne validée ; aucun état public n’est modifiable et aucune logique de stock n’existe.

## 12. Agrégat SaleDraft

Un brouillon est créé vide avec un `SaleDraftId`. Il possède une liste privée exposée par une vue en lecture seule. `AddItem` crée une ligne ou fusionne la quantité d’un même `ProductId` ; le snapshot de la première ligne reste la référence du brouillon. `ChangeQuantity`, `RemoveItem` et `Clear` retournent un résultat explicite. `LineCount`, `TotalQuantity`, `IsEmpty` et `Total` sont calculés depuis l’état courant.

## 13. Invariants

- aucune quantité n’est nulle, négative ou fractionnaire ;
- aucun montant n’est négatif ou flottant ;
- un seul `ProductId` existe par brouillon ;
- le sous-total n’est jamais stocké séparément ;
- le total est la somme exacte des sous-totaux et vaut zéro à vide ;
- une opération échouée ne laisse aucune mutation partielle ;
- la collection et les lignes ne sont pas modifiables de l’extérieur ;
- le brouillon n’a aucun effet sur stock, paiement, caisse ou persistance.

## 14. Erreurs

Codes V0.1 :

- `sales.money.invalid` ;
- `sales.product.invalid` ;
- `sales.draft.invalid` ;
- `sales.product_reference.invalid` ;
- `sales.product_reference.too_long` ;
- `sales.quantity.invalid` ;
- `sales.quantity.overflow` ;
- `sales.product_name.invalid` ;
- `sales.line.not_found`.

Les codes sont stables ; les messages sont destinés à être traduits ou adaptés par les couches supérieures si nécessaire.

## 15. Contrats Application

`IProductCatalogReader` expose recherche par identifiant, code et texte avec modèles Application et résultats explicites. `IStockAvailabilityReader` décrit la disponibilité pour un produit et une quantité demandée, sans réservation ni mutation. `ISaleFinalizer` marque la future frontière transactionnelle avec `FinalizeSaleRequest`, `FinalizeSaleResult` et un `OperationId` `Guid` justifié par l’ADR-005 sur l’idempotence.

Les interfaces sont asynchrones et acceptent un `CancellationToken`. Elles n’exposent ni `DbContext`, `IQueryable`, SQL, chaîne de connexion, entité EF ou type Desktop.

## 16. Limites

Cette V0.1 ne plafonne pas les montants à une précision SQL et n’applique aucun arrondi. Une surcharge arithmétique `decimal` reste une erreur exceptionnelle de capacité. Les modèles Application définissent des contrats de transport ; leurs validations d’orchestration seront ajoutées avec les cas d’utilisation. La limite de référence de 128 caractères est locale et devra être confirmée.

## 17. Éléments différés

Sont différés : entité Product, codes-barres normalisés, disponibilité persistée, lots et FEFO, suspension persistée, prix revérifié, remises, taxes, client, paiements, caisse, audit, concurrence, transaction, numéro de vente, retour, annulation, impression, EF Core, SQL Server et implémentations Infrastructure.

## 18. Future intégration au frontend

Le POS V0.2 reste entièrement synthétique et inchangé. Une future connexion devra passer par des cas d’utilisation Application, préserver les comportements du POS validés, gérer les erreurs structurées et être autorisée par un sprint dédié. Desktop ne devra jamais accéder directement à `DbContext`.

## 19. Critères de validation

La version est acceptable si Domain reste indépendant, Application ne dépend que des couches autorisées, les 68 contrôles Sprint 6 réussissent avec les 115 tests existants, Debug et Release compilent sans avertissement, aucun package ou SQL n’est ajouté, Infrastructure reste sans Sales, Desktop reste déconnecté et la revue manuelle confirme l’absence de régression visible ou fonctionnelle.
