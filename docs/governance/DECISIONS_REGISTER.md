# DECISIONS_REGISTER - Logiciel Pharmacie

## Statut des grandes phases

| Phase | Sujet | Statut |
|---|---|---|
| Phase 1 | ADR et socle technique | PASS |
| Phase 2 | Modélisation fonctionnelle complète | PASS |
| Phase 3 | Modèle de données logique | PASS |
| Phase 4 | Parcours utilisateurs et UX/UI | PASS |
| Phase 5 | Plan code-first et workflow Vibe Coding | PLAN VALIDÉ, EXÉCUTION À DÉMARRER |

## ADR validées

### ADR-001 - Local-first

Fonctions critiques disponibles sans Internet. Pas de synchronisation cloud métier obligatoire en V1.

### ADR-002 - Stack

.NET 10, WPF, Blazor Hybrid, monolithe modulaire et services natifs .NET pour Windows.

### ADR-003 - Base de données

SQL Server 2025 Express, montants en `decimal`, migrations versionnées, backup/restore natif et suivi de capacité.

### ADR-004 - Architecture interne

Domain, Application, Infrastructure, Desktop et deux projets de tests. EF Core isolé dans Infrastructure.

### ADR-005 - Persistance et transactions

Une transaction atomique par opération critique. StockMovement et StockBalance mis à jour ensemble. Optimistic concurrency et idempotence ciblée.

### ADR-006 - Sécurité et identité

Authentification locale, rôles et permissions, refus par défaut, mots de passe hachés, verrouillage, réauthentification pour actions sensibles.

### ADR-007 - Logs et observabilité

Logs techniques structurés séparés de l’audit métier. Aucun secret ou donnée sensible. Health checks et support bundle contrôlé.

### ADR-008 - Sauvegarde et continuité

FULL recovery, Full quotidien, Differential toutes les 4 heures, log toutes les 15 minutes, vérification et restauration réelle périodique.

### ADR-009 - Installation et mise à jour

Installateur Windows hors ligne signé, SQL Server Express dédié, WebView2, maintenance, backup avant migration et rollback contrôlé.

### ADR-010 - Matériel

Scanner USB HID, imprimante 80 mm avec pilote Windows, TPE autonome, UPS recommandé et abstractions matérielles isolées.

### ADR-011 - Import et portabilité

XLSX et CSV UTF-8, staging, mapping explicite, validation, prévisualisation, idempotence, import atomique des données critiques et exports ouverts.

### ADR-012 - Tests et release gates

xUnit v3, Microsoft Testing Platform, SQL Server réel pour les tests critiques, CI, restauration réelle, tests d’installation, recette formelle et zéro défaut Blocker/Critical.

## Décisions fonctionnelles majeures

- Un produit est une référence commerciale précise.
- Deux conditionnements différents sont deux produits.
- Plusieurs codes-barres par produit ; un barcode identifie au maximum un produit.
- Créer un produit ne crée pas de stock.
- Vente fractionnée désactivée en V1 tant que le besoin n’est pas confirmé.
- Stock négatif interdit par défaut.
- FEFO proposé avec confirmation humaine ; autre lot possible avec justification.
- Réceptions partielles prises en charge.
- Vente suspendue obligatoire.
- Vente finalisée immutable ; retours et annulations par opérations inverses.
- Caisse séparée des ventes.
- Crédit expliqué par des écritures, jamais par un champ modifiable seul.
- Reporting en lecture seule.

## Décisions UX majeures

- Priorité absolue au POS.
- Clavier et scanner prioritaires.
- Une action principale visible par écran.
- Double activation bloquée.
- Erreur d’impression : la vente reste enregistrée.
- Taille minimale cible : 1366 x 768 ; contrôle en 1920 x 1080.
- États obligatoires : loading, empty, error, denied, degraded et concurrency conflict.

## Amendement Phase 5

Figma n’est pas obligatoire. Le prototype frontend sera construit directement dans le code avec WPF + Blazor Hybrid, données synthétiques, design tokens, UI Lab, captures et itérations pilotées par Codex.

## Décisions techniques locales — Sprint 9

- La première migration physique couvre uniquement le Catalogue et suit les relations validées en Phase 3 : `ProductLocalSettings` matérialise le 1–1 avec `Product`, tandis que les barcodes et historiques de prix référencent directement `Product`.
- Les entités EF restent internes à `Pharmacy.Infrastructure`; le Desktop ne référence ni `DbContext` ni extension de persistance SQL.
- Le projet Infrastructure sert de startup design-time pour `dotnet-ef`, afin que `Microsoft.EntityFrameworkCore.Design` reste privé à Infrastructure.
- Les commandes design-time lisent uniquement `PHARMACY_SQLSERVER_CONNECTION`; les tests réels lisent uniquement `PHARMACY_TEST_SQLSERVER_CONNECTION`. Aucune valeur n’est versionnée ou journalisée.
- Les longueurs physiques `nvarchar` sont des limites techniques de sûreté documentées, et non des obligations pharmacien, fiscales ou légales.
- SQL Server 2025 Developer `17.0.1125.2` constitue l’environnement de validation Sprint 9. La cible pilote demeure SQL Server 2025 Express conformément à l’ADR-003 ; aucun changement d’édition cible n’est décidé.

## Décisions techniques locales — Sprint 10

- Le manque de `Products.IsActive` empêchait l’activation globale exigée par le Catalogue. `CatalogPersistenceEnhancementsV01` ajoute uniquement ce booléen, valeur par défaut active, et un index ; la migration Sprint 9 n’est pas modifiée.
- `ProductLocalSettings.InternalCode` reste la référence Application/Sales. La normalisation barcode effectue trim + majuscules invariantes et conserve les zéros initiaux.
- Les opérations Catalogue multi-entités utilisent un contexte court et un seul `SaveChangesAsync`, transaction EF atomique. Aucun retry automatique n’est effectué après conflit `rowversion`.
- Le prix courant et l’historique append-only sont modifiés dans le même commit. Le modèle validé ne comportant pas de période de fin, aucun ancien historique n’est clôturé artificiellement.
- Le Desktop compose le Catalogue SQL uniquement lorsque la connexion hors dépôt cible `Pharmacy_Development`. En absence de configuration ou en panne, le shell démarre mais aucun fallback vers `InMemoryProductCatalogReader` n’est autorisé.
- La disponibilité stock du POS reste synthétique, calculée et conservée en mémoire à partir du `ProductId` résolu par SQL, sans identifiant codé en dur ni table Stock.
- Les dix produits et quatre catégories Demo proviennent d’un seed manuel idempotent protégé ; les migrations restent sans donnée.

## Décisions techniques locales — Sprint 11

- Le schéma physique Inventory V0.1 se limite à six tables : emplacements, lots, balances, mouvements, sessions et lignes. Les besoins multi-emplacements restent à confirmer sur le terrain, mais un emplacement explicite est requis par le prompt Sprint 11.
- Les quantités Inventory sont des `int` et aucun `ReservedQuantity` n’est ajouté. La vente fractionnée et la réservation restent différées.
- Un mouvement et sa balance sont écrits dans une transaction unique ; les contraintes SQL interdisent les quantités de mouvement non positives, les balances négatives et les incohérences avant/après.
- L’idempotence combine un `OperationId` unique et une empreinte structurelle. Une réutilisation équivalente est stable ; une réutilisation incohérente est un conflit.
- Les lots expirés restent consultables mais sont exclus de la disponibilité POS. FEFO est une proposition descriptive basée sur la date métier injectée, configurable et sans affirmation réglementaire.
- Les sessions utilisent `Draft`, `InProgress`, `Completed`, `Cancelled`. Les états `ReadyForReview`/`Validated` évoqués en Phase 3 sont différés, conformément au périmètre minimal du sprint.
- La disponibilité du POS vient exclusivement de SQL Inventory. Le brouillon, le paiement, les suspensions et l’impression restent en mémoire ; le succès simulé ne consomme aucun stock.
- Le seed/clear Inventory cible des identifiants Demo canoniques exacts pour préserver toute donnée manuelle ou non Demo. Aucune donnée réelle n’est supprimée et aucune migration ne contient de seed.

## Décisions techniques locales — Sprint 12

- Le schéma Purchasing V0.1 reste limité aux cinq tables confirmées. Référencement fournisseur-produit, documents comptables, retours/avoirs, fiscalité, paiements et marges sont différés.
- Les commandes utilisent `Draft`, `Confirmed`, `PartiallyReceived`, `FullyReceived`, `Cancelled`, conformément à Phase 2/3. Le brouillon de réception demeure local au Desktop ; seule une réception `Completed` est persistée.
- La réception directe est autorisée par PUR-008 mais reste signalée comme voie prototype contrôlée, faute d’autorisation métier réelle au Sprint 12.
- Les prix d’achat utilisent `decimal(19,4)`, exception validée de Phase 3, et restent des snapshots. Ils ne modifient pas le prix de vente et ne déclenchent aucun calcul de marge.
- Une ligne de réception pointe directement vers son lot et son mouvement Stock ; aucune table intermédiaire spéculative de ventilation n’est ajoutée en V0.1.
- La finalisation Purchasing étend le journal Inventory avec `PurchaseReceipt` et réalise réception, lot, mouvement, balance et commande dans une transaction Serializable. Les commandes confirmées seules n’ont aucun effet stock.
- L’idempotence repose sur `OperationId` et une empreinte SHA-256 normalisée ; les identifiants techniques dérivés des lots/mouvements sont déterministes. Les conflits métier ou `rowversion` ne sont pas rejoués aveuglément.
- Le seed/clear Purchasing est ciblé sur `DEMO-S12-*`, inverse uniquement son propre mouvement Stock et ne supprime aucun produit Catalogue ou stock extérieur au seed.

## Décisions techniques locales — Sprint 13

- Une différence entre le prix snapshot du brouillon et le prix Catalogue courant bloque la finalisation avec `sales.price.changed` ; le finalizer ne reprix pas silencieusement le panier.
- Les ventes finalisées sont immuables en V0.1 et n’exposent donc pas de `rowversion`. Toute correction future passera par une opération inverse, jamais par une modification de l’historique.
- Une ligne Sales peut consommer plusieurs balances/lots. FEFO classe les dates croissantes, puis les lots sans date et enfin les identifiants techniques ; un lot expiré reste visible mais n’est jamais alloué.
- L’idempotence persiste une empreinte SHA-256 couvrant brouillon, snapshots et paiement. Même opération équivalente recharge la vente ; un sens différent est un conflit explicite.
- Les espèces persistent montant reçu et monnaie ; la carte ne conserve qu’un paiement externe confirmé. Paiement mixte, PAN, CVV/CVC et autres données carte restent hors périmètre.
- Le rollback technique de `SalesPersistenceV01` restitue les quantités des mouvements `Sale` avant de retirer ces mouvements et le schéma Sales, afin de revenir à un état Sprint 12 réconcilié.

## Décisions techniques locales — BRAND-01

- `ReportSettings` reste réservé aux alertes Reporting. L’identité de la pharmacie utilise le singleton `configuration.PharmacyBranding`, avec `rowversion`, UTC et une migration additive unique.
- Le seed contient seulement `Pharmacie Diar Douha` et `#0D9A48`. Le logo initial est livré dans les actifs Desktop ; un remplacement validé PNG/JPEG/WEBP est stocké en SQL jusqu’à 2 Mo afin d’être inclus dans la sauvegarde officielle.
- La lecture de l’identité est locale et pré-authentification. L’écriture est contrôlée côté service par `security.manage_users`; aucune permission, aucun rôle et aucune entrée de navigation ne sont ajoutés.
- Le thème est injecté au runtime par variables CSS. Les variantes et la couleur de texte sont dérivées avec contrôle de contraste, tandis que les couleurs danger, warning, success et information gardent leur signification.
- Une erreur de lecture réinitialise immédiatement le coordinateur sur le branding central par défaut : une ancienne valeur en mémoire n’est jamais présentée comme fraîche.
- Les changements de nom, couleur et métadonnées du logo sont audités dans la même écriture SQL. Le contenu binaire du logo n’entre jamais dans l’audit.
