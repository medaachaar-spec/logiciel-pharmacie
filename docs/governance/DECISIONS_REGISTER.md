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
