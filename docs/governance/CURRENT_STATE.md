# CURRENT_STATE - Logiciel Pharmacie

Date de mise à jour : 2026-08-10

## État fonctionnel et architectural

- Phase 1 ADR : PASS.
- Phase 2 fonctionnelle : PASS.
- Phase 3 modèle de données : PASS.
- Phase 4 UX/UI : PASS.
- Phase 5 code-first : fondations Sprint 1 à 5, Sales Sprint 6 à 8, fondation SQL Sprint 9, Catalogue Sprint 10, Inventory Sprint 11, Purchasing Sprint 12 et ventes persistantes Sprint 13.

## État du développement

- Documentation : INSTALLÉE ; package de gouvernance complet et cinq PDF de référence présents.
- Dépôt réel : `C:\Users\HP\Desktop\Logiciel pharmacie`.
- Branche active : `main`.
- Sprint 0 : `IMPLEMENTED — FOUNDATION READY WITH WARNINGS` après correction de classification Sprint 0.1.
- Git : dépôt local sur `a46ffb7`, zéro remote ; changements Sprints 12 et 13 laissés hors index pour revue, aucun commit ou push.
- Solution .NET créée : OUI — `Pharmacy.slnx`, six projets .NET 10.
- Code frontend : pages Catalogue, Stock et Achats/réceptions SQL fonctionnelles ; POS connecté au Catalogue, au stock et à la finalisation Sales SQL avec historique récent ; Diagnostic SQL sûr ; quatre autres modules restent provisoires, UI Lab disponible en Debug.
- Code backend : noyaux/handlers Catalogue, Inventory, Purchasing et Sales ; finalizer/lecteur Sales SQL ; seuls brouillons, suspensions et impressions restent en mémoire.
- Base SQL Server métier : `Pharmacy_Development` sur SQL Server 2025 Developer, compatibilité 170, cinq migrations appliquées ; seeds Catalogue/Inventory/Purchasing synthétiques et zéro vente seedée ; cible Express non encore validée.
- CI : NON DÉMARRÉE.

## Prérequis détectés au Sprint 0

- Git 2.55.0 et branche `main` : PASS.
- SDK .NET 10 stable `10.0.302` et runtimes 10.0.10 : PASS.
- MSBuild 18.6.11 et WebView2 150.0.4078.105 : PASS.
- Windows 11 Famille x64 : WARNING pour les fondations ; Windows 11 Pro x64 reste obligatoire avant certification du poste pilote.
- Visual Studio / Build Tools : absent — WARNING non bloquant tant que `dotnet` CLI et MSBuild fonctionnent.
- Workloads .NET Desktop/WPF et ASP.NET/web : absents — WARNING non bloquant confirmé par le smoke build Sprint 1 réussi via CLI/MSBuild.
- SQL Server 2025 Express : absent ; édition Developer 17.0.1125.2 validée pour la fondation SQL Sprint 9 — gate Express toujours requis avant production pilote.
- Espace libre : environ 32 Go lors de la réexécution 0.1 — PASS par rapport aux 25 Go recommandés ; la mesure manuelle antérieure était d’environ 16,15 Go.
- Confiance TLS SQL locale : validation standard en échec — WARNING différé au sprint de persistance ; aucune exception permanente autorisée.

## Readiness après correction Sprint 0.1

- `FoundationReadiness = READY WITH WARNINGS`.
- `PersistenceReadiness = REMEDIATION REQUIRED`.
- `PilotProductionReadiness = REMEDIATION REQUIRED`.

## Fichiers du Sprint 0

- Protections racine : `.gitignore`, `.gitattributes`, `.editorconfig`.
- SDK reproductible : `global.json` épinglé sur `10.0.302` stable.
- Audit : `scripts/Verify-Environment.ps1`.
- Rapports : `docs/reports/sprint-00/environment-audit.md` et `codex-report.md`.
- Aucun code applicatif, solution, projet, migration ou base métier créé.

## Prochaine étape autorisée

Effectuer les revues manuelles Sprints 12 et 13 dans SSMS et Desktop, notamment FEFO multi-lots, paiements, historique, double clic, prix changé, panne SQL et impression post-commit. Recontrôler Catalogue/Stock/Purchasing. Traiter la confiance TLS et valider SQL Server 2025 Express avant toute certification pilote.

## Mise à jour après le Sprint 15

- Statut : `IMPLEMENTED — SQL SECURITY/RBAC/AUDIT VALIDATION PASS — MANUAL SECURITY REVIEW REQUIRED` après validation automatisée finale ; la revue humaine reste obligatoire.
- Sécurité : authentification locale, Owner bootstrap sans secret seedé, PasswordHasher Identity V3, lockout 5 échecs/5 minutes, session mémoire verrouillée après 10 minutes et recovery codes hashés à usage unique.
- Autorisation : 20 permissions granulaires, deny-by-default dans Application, réauthentification technique de 5 minutes sur actions sensibles, navigation Desktop filtrée et pages Administration/Audit protégées.
- SQL : migration additive `SecurityRbacAuditPersistenceV01`, sept tables `security`, deux tables `audit`, aucun compte ni mot de passe dans migration/scripts.
- Audit : authentification, administration et principaux workflows métier instrumentés ; secrets, codes, cartes et notes client interdits.
- Limite pilote : le compte propriétaire partagé ne permet pas d’attribution individuelle entre les deux exploitants.
- Git : changements Sprint 15 laissés hors index pour revue ; aucun commit, push, remote ou changement de branche.

## Mise à jour après le Sprint 17

- Statut : `IMPLEMENTED — BACKUP/IMPORT/HARDWARE VALIDATION PASS — MANUAL OPERATIONS REVIEW REQUIRED — HARDWARE PILOT GATE PENDING` sous réserve de la revue humaine décrite dans le rapport Sprint 17.
- Continuité : sauvegardes SQL Full/Diff/Log avec CHECKSUM, VERIFYONLY, SHA-256, copie externe sur volume chiffré attesté et restore-test vers base temporaire supprimée.
- Import : CSV UTF-8 contrôlé, mapping/prévisualisation obligatoires, Catalogue CreateOnly, stock initial/lots atomiques et idempotence par OperationId/fingerprint ; XLSX non annoncé.
- Matériel : buffer scanner HID, abstraction ticket 80 mm connectée après commit SQL, TPE externe manuel et diagnostic local ; aucun matériel physique certifié.
- SQL : migration `BackupImportDeviceOperationsV01`, deux tables `operations`, six permissions nouvelles, neuf migrations appliquées à `Pharmacy_Development` et aucun pending model change attendu.
- Exploitation : SQL Server Express reste sans Agent ; automatisation documentée via Planificateur de tâches Windows. Cible pilote Express et validation matérielle restent obligatoires.
- Git : modifications Sprint 17 laissées hors index ; aucun add, commit, push, remote ou changement de branche autorisé pendant le sprint.

## Interdictions actuelles

- Ne pas implémenter de fonctionnalité métier hors d’un sprint explicitement autorisé.
- Ne pas utiliser de données réelles.
- Ne pas créer de migration de production.
- Ne pas commit/push sans validation explicite.
- Ne pas changer la stack ou les ADR.

## Mise à jour après le Sprint 0.1

- Statut : correction de classification implémentée ; fondations prêtes avec avertissements.
- Vérification principale : `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-Environment.ps1`.
- Résultat automatisé corrigé : 20 PASS, 6 WARNING, 0 MISSING, 0 BLOCKING techniques globaux ; code de sortie fondation 1.
- Test `.gitignore` : 22/22 chemins fictifs conformes.
- Risques différés : édition Windows pour le pilote, Visual Studio/workloads à confirmer par smoke build, SQL Express pour la persistance/pilote et TLS local au sprint de persistance.
- Working tree : fichiers initiaux et Sprint 0 visibles pour revue, aucun `git add`, commit ou push.
- Prochaine action unique : Sprint 1 de fondation avec smoke build ; aucune persistance SQL ni certification pilote à ce stade.

## Mise à jour après le Sprint 1

- Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL UI REVIEW REQUIRED`.
- Solution : un fichier `Pharmacy.slnx` et six projets ciblant les frameworks imposés.
- Architecture : graphe Domain → aucune couche ; Application → Domain ; Infrastructure → Application et Domain ; Desktop → Application et Infrastructure.
- Desktop : WPF natif, Blazor Hybrid local, WebView2, DI et écran de diagnostic sans serveur HTTP ni ressource externe.
- Packages directs : `Microsoft.AspNetCore.Components.WebView.Wpf` 10.0.10 et `xunit.v3.mtp-v2` 3.2.2.
- Validation Debug : build réussi avec 0 avertissement et 0 erreur ; 12/12 tests réussis.
- Validation Release : build réussi avec 0 avertissement et 0 erreur ; 12/12 tests réussis.
- Démarrage : processus stable, fenêtre WPF et WebView2 détectés, fermeture normale ; revue visuelle humaine encore requise.
- Aucun module métier, base de données, fichier SQL, secret, remote, commit ou push ajouté.

## Mise à jour après le Sprint 2

- Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL VISUAL REVIEW REQUIRED`.
- Design system : `UI Foundations V0.1`, provisoire et code-first, centralisé dans cinq fichiers CSS locaux.
- Composants : `UiButton`, `UiBadge`, `UiCard`, `UiAlert`, `UiTextField`, `UiSelect`, `UiSpinner` et `UiEmptyState`.
- Navigation : diagnostic Sprint 1 conservé sur `/` et UI Lab disponible sur `/ui-lab` via une barre de développement.
- Accessibilité : HTML sémantique, labels, ARIA, focus visible, parcours Tab, états textuels et reduced motion prévus.
- Validation Debug : build réussi avec 0 avertissement et 0 erreur ; 22/22 tests réussis.
- Validation Release : build réussi avec 0 avertissement et 0 erreur ; 22/22 tests réussis.
- Démarrage : fenêtre WPF, WebView2 et navigation UI Lab vérifiés techniquement ; fermeture normale.
- Packages : aucun nouveau PackageReference ajouté.
- Revue restante : contrôle humain complet des états, du clavier et du responsive à 1366 × 768 et 1920 × 1080.
- Aucun écran ou type métier, base, SQL, API, serveur, ressource distante, remote, commit ou push ajouté.

## Mise à jour après le Sprint 3

- Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL SHELL REVIEW REQUIRED`.
- Shell : `AppShellLayout`, sidebar réductible, topbar contextuelle, statusbar locale et lien d’évitement.
- Navigation : catalogue typé central de neuf entrées produit et deux outils techniques visibles uniquement dans le groupe Développement en Debug.
- Routes : accueil sur `/` et `/home`, huit modules sur un placeholder paramétré, diagnostic sur `/diagnostic`, UI Lab sur `/ui-lab` et vue NotFound claire.
- Accueil : quatre indicateurs, quatre accès rapides, trois références récentes génériques et quatre états système, tous explicitement synthétiques.
- Aperçu des accès : trois profils locaux non persistants qui modifient uniquement la présentation ; aucune authentification, autorisation ou sécurité réelle.
- Accessibilité : navigation sémantique, état actif, verrouillage non navigable, noms accessibles, focus visible, icônes décoratives et reduced motion.
- Validation Debug et Release : build réussi avec 0 avertissement et 0 erreur ; 49/49 tests réussis dans chaque configuration.
- Lancement : fenêtre WPF 1100 × 700, WebView2, accueil, mode compact et page provisoire vérifiés techniquement ; fermeture normale.
- Packages : aucun nouveau PackageReference ajouté.
- Revue restante : contrôle humain du rendu, du clavier et du responsive à 1366 × 768 et 1920 × 1080.
- Aucun module métier, type métier, base, SQL, API, serveur, ressource distante, remote, commit ou push ajouté.

## Mise à jour après le Sprint 4

- Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL POS REVIEW REQUIRED`.
- Ventes : `/module/sales` ouvre désormais `Nouvelle vente`, tandis que les sept autres modules restent sur le placeholder existant.
- État : `DemoPosSession` scoped, catalogue central immuable de dix articles fictifs, panier et résultat uniquement en mémoire dans `Pharmacy.Desktop`.
- Recherche : nom/code insensibles à la casse, ajout par code exact suivi d’Entrée, aucun ajout pour un code inconnu et aucun appel réseau.
- Panier : fusion des lignes, quantités entières, minimum 1, plafond de disponibilité synthétique, suppression explicite, vidage confirmé et calculs exclusivement `decimal`.
- Paiements : espèces avec montant reçu/monnaie et carte avec confirmation d’un TPE externe simulé ; aucune donnée bancaire collectée.
- Succès : référence `DEMO-SALE-*` en mémoire, rappels explicites d’absence d’enregistrement, de modification de stock et d’impression.
- Accessibilité : labels, HTML sémantique, messages live, focus initial/replacé, contrôles natifs et état loading bloquant la double validation.
- Responsive : grille catalogue/panier à largeur suffisante, recomposition verticale sous 1024 px et styles sans ressource externe.
- Validation finale Debug et Release : builds réussis avec 0 avertissement et 0 erreur ; 75/75 tests réussis dans chaque configuration, aucun test ignoré.
- Lancement : fenêtre WPF 1100 × 700, WebView2, route Ventes et ajout clavier `DEMO-000001` vérifiés techniquement ; fermeture propre et runner final code 0.
- Packages : aucun nouveau PackageReference ; graphe de six projets inchangé.
- Architecture métier définitive, base, SQL, API, serveur, ressource distante, périphérique réel, remote, commit et push : aucun ajout.

## Mise à jour après le Sprint 5

- Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL CONTINUITY REVIEW REQUIRED`.
- Incidents : code inconnu sans création et refus explicite d’une quantité au-delà de la disponibilité synthétique, sans altérer le panier.
- Continuité : suspensions `DEMO-HOLD-*` en mémoire, liste temporaire, reprise sans écrasement silencieux, suppression confirmée et abandon contrôlé du brouillon.
- Carte : états `NotStarted`, `AwaitingConfirmation`, `Confirmed`, `NotCompleted` et `Ambiguous` ; validation bloquée tant que le statut est incertain et résolution manuelle confirmée.
- Impression : vente terminée avant le ticket, états non demandé/en cours/imprimé/échec, nouvelle tentative avec compteur et protection contre les doubles clics ; aucun périphérique ou fichier utilisé.
- Accessibilité : incidents adjacents, confirmations uniques, Escape non destructif, boutons natifs, focus replacé et textes indépendants de la couleur.
- Responsive : disposition en colonne à 1100 px et grille à 1366/1920 px ; revue humaine encore requise aux trois résolutions.
- Validation finale Debug et Release : builds réussis avec 0 avertissement et 0 erreur ; 115/115 tests réussis dans chaque configuration, aucun test ignoré.
- Lancement : processus et fenêtre WPF répondants, POS/incident/suspensions/conflit de reprise rendus, fermeture propre ; TPE ambigu et impression restent à valider visuellement par l’utilisateur.
- Packages : aucun nouveau PackageReference ; six projets et graphe de références inchangés.
- Architecture métier définitive, base, SQL, fichier de ticket, réseau, donnée bancaire, périphérique réel, remote, commit et push : aucun ajout.

## Mise à jour après le Sprint 6

- Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL REGRESSION REVIEW REQUIRED`.
- Domain : `Error`, `Result`, `Result<T>`, `Money` MAD en `decimal`, identifiants typés, référence produit, quantité entière, snapshot immuable, `SaleLine` immuable et agrégat `SaleDraft` encapsulé.
- Brouillon : ajout, fusion par `ProductId`, changement de quantité, suppression, vidage, sous-totaux et total exacts ; échecs explicites sans mutation partielle.
- Application : contrats `IProductCatalogReader`, `IStockAvailabilityReader` et `ISaleFinalizer`, avec modèles minimaux et `OperationId` idempotent ; aucune implémentation.
- Architecture : Domain reste indépendant ; Application ne dépend que de Domain ; Infrastructure reste sans Sales ; Desktop reste déconnecté du nouveau noyau.
- Validation Debug et Release : builds réussis avec 0 avertissement et 0 erreur ; 183/183 tests réussis dans chaque configuration, aucun test ignoré.
- Tests : 68 contrôles Sprint 6 numérotés ajoutés aux 115 tests existants.
- Lancement : processus et fenêtre WPF répondants, titre `Logiciel Pharmacie`, fermeture normale et aucun processus résiduel ; revue visuelle et fonctionnelle humaine encore requise.
- Packages : aucun `PackageReference` ajouté, supprimé ou modifié ; six projets inchangés.
- Persistance, EF Core, SQL, lots, paiements, stock réel, caisse, audit, impression réelle, réseau, remote, commit et push : aucun ajout.

## Mise à jour après le Sprint 7

- Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL REGRESSION REVIEW REQUIRED`.
- Application : création/lecture de brouillon, recherche catalogue, lecture par identifiant/code, ajout/fusion, changement de quantité, suppression, vidage et préparation de finalisation.
- Modèles : vues immuables de brouillon, ligne, produit vendable et préparation ; aucune collection mutable exposée.
- Concurrence : version initiale 1, version attendue obligatoire, incrément après mutation réelle et erreur `sales.draft.concurrency_conflict` sans état partiel.
- Idempotence : préparation par `OperationId` non vide et empreinte structurelle ; réutilisation équivalente stable, réutilisation incohérente refusée.
- Infrastructure : stores de brouillons/préparations, catalogue de dix produits synthétiques, disponibilités déterministes et générateur de `Guid`, tous locaux au processus et thread-safe.
- Préparation : `Prepared` ne signifie pas finalisée, payée, réservée ou persistée ; aucun appel à `ISaleFinalizer`.
- Architecture : Domain inchangé ; Desktop inchangé et déconnecté ; aucune implémentation de finalisation réelle.
- Validation Debug et Release : builds réussis avec 0 avertissement et 0 erreur ; 240/240 tests réussis, aucun test ignoré.
- Tests : 57 contrôles Sprint 7 ajoutés aux 183 tests existants.
- Lancement : processus et fenêtre WPF répondants, titre `Logiciel Pharmacie`, fermeture normale et aucun processus résiduel ; revue humaine encore requise.
- Packages, projets et références : aucun ajout ou changement.
- EF Core, SQL, fichier de données, réseau, lot, stock réel, paiement, caisse, audit, impression réelle, remote, commit et push : aucun ajout.

## Mise à jour après le Sprint 8

- Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL POS INTEGRATION REVIEW REQUIRED`.
- Composition root : les cinq adaptateurs Sales mémoire sont des singletons de processus ; les handlers sont transients et `SalesPosCoordinator` est scoped au frontend.
- POS : `SaleDraftView` alimente désormais lignes, quantités, prix, sous-totaux et total ; le catalogue et le scan exact passent par les handlers Application.
- Concurrence : chaque mutation utilise la version courante ; un conflit recharge la vue récente, affiche un message explicite et ne rejoue jamais l’action.
- Continuité : les suspensions restent des snapshots immuables Desktop ; la reprise crée un nouveau brouillon et réajoute les lignes par handlers avant de supprimer le snapshot.
- Paiement et impression : simulations Desktop conservées, alimentées par l’instantané Application ; aucun appel à `PrepareSaleFinalizationHandler` ou `ISaleFinalizer`.
- Validation Debug et Release : builds réussis avec 0 avertissement et 0 erreur ; 271/271 tests réussis, aucun test ignoré.
- Tests : 31 contrôles Sprint 8 ajoutés aux 240 tests de baseline — 5 unitaires et 26 intégration/architecture.
- Lancement : fenêtre WPF répondante, titre `Logiciel Pharmacie`, fermeture normale, runner code 0, aucun stderr ni processus résiduel ; revue visuelle et interactive humaine requise.
- Packages, projets et références : aucun package, projet ou `ProjectReference` ajouté ; trois liens de compilation de sources Desktop testables ajoutés dans les projets de tests.
- Domain, Application existante et Infrastructure existante : aucun fichier modifié au Sprint 8 ; SQL, EF Core, fichier, réseau, stock réel, paiement réel, impression réelle, remote, commit et push : aucun ajout.

## Mise à jour après le Sprint 9

- Statut : `IMPLEMENTED — SQL SERVER DEVELOPER VALIDATION PASS — EXPRESS ENVIRONMENT GATE PENDING`.
- SQL Server : instance par défaut `MED-AMINE`, version `17.0.1125.2`, édition Developer, moteur 17 ; connexion TLS standard refusée par chaîne non approuvée et exception de confiance limitée au développement local.
- Outils : `Microsoft.EntityFrameworkCore.SqlServer` et `Design` 10.0.10 ; `dotnet-ef` local 10.0.10 ; aucun preview.
- Persistence : `PharmacyDbContext`, factory design-time par variable d’environnement, options SQL Server niveau 170, retry et migrations explicites ; aucun logging sensible.
- Catalogue : cinq tables vides dans le schéma `catalog` — catégories, produits, paramètres locaux, barcodes et historiques de prix.
- Schéma : PK `bigint`, prix `decimal(19,4)`, seuils `decimal(18,3)`, instants `datetime2(7)`, textes bornés et quatre `rowversion`.
- Intégrité : quatre FK `NO ACTION`, unicité catégorie/référence interne/barcode, un barcode principal actif, prix/seuils non négatifs et dates cohérentes.
- Migration : `InitialCatalogPersistenceV01`, snapshot et script idempotent générés ; `Up`, `Down`, double application du script, rollback vers `0` et réapplication validés.
- Base développement : `Pharmacy_Development` créée, migration unique appliquée, zéro donnée Catalogue et aucune migration en attente.
- Tests SQL réels : bases `Pharmacy_Sprint09_Test_*` créées et supprimées dans `finally` ; zéro base temporaire résiduelle.
- Validation Debug et Release : builds réussis avec 0 avertissement et 0 erreur ; 283/283 tests réussis, aucun test ignoré.
- Non-régression Desktop : fenêtre répondante, titre `Logiciel Pharmacie`, fermeture propre, code runner 0, stderr vide et aucun processus résiduel sans variable SQL.
- Architecture : Domain/Application/Desktop inchangés par la fondation ; POS toujours en mémoire ; aucune implémentation SQL Sales ou migration automatique.
- Sécurité : aucun secret, mot de passe, compte SQL ou connexion réelle versionné ; scan spécialisé sans détection active.
- Git : branche `main`, HEAD initial `49c2ff2`, index vide, zéro nouveau commit, remote ou push.

## Mise à jour après le Sprint 10

- Statut : `IMPLEMENTED — SQL CATALOG VALIDATION PASS — MANUAL CATALOG REVIEW REQUIRED`.
- Application : contrats spécifiques, modèles de liste/détail immuables, handlers produits/catégories, rowversions Base64 et dix erreurs structurées.
- Infrastructure : `SqlProductCatalogReader`, store transactionnel, diagnostic sûr et disponibilité synthétique dynamique sans `ProductId` codé en dur.
- Migration : `CatalogPersistenceEnhancementsV01` ajoute seulement `Products.IsActive` et son index ; migration Sprint 9 inchangée et aucune migration automatique.
- SQL : migration appliquée à `Pharmacy_Development`, seed manuel exécuté deux fois sans duplication, dix produits Demo, quatre catégories, dix barcodes et dix historiques initiaux.
- POS : recherche exacte/résultats/prix depuis SQL ; brouillon Sales, stock, paiements, suspensions et impression toujours en mémoire ; aucune vente persistée.
- Desktop : Catalogue fonctionnel, filtres, détail, création/modification, activation, barcodes, prix/historique, catégories, états indisponible/loading/empty et anti-double-soumission.
- Panne SQL : shell et outils techniques démarrent, Catalogue/POS signalent l’indisponibilité, aucun fallback mémoire.
- Tests : 300 contrôles après ajout de 17 tests Sprint 10 ; SQL Server réel, transactions, rollback, concurrence et base temporaire supprimée.
- Gate restant : revue manuelle Catalogue/POS/Diagnostic, validation Express et remédiation du certificat TLS local.
- Git : branche `main`, HEAD initial `e9a5fcf`, index vide, zéro nouveau commit, remote ou push.

## Mise à jour après le Sprint 11

- Statut : `IMPLEMENTED — SQL INVENTORY VALIDATION PASS — MANUAL STOCK REVIEW REQUIRED`.
- Domain/Application : quantités entières, lot/péremption, états, erreurs stables, contrats spécifiques, handlers et date métier injectée, sans EF Core.
- Persistence : six tables `inventory`, PK `bigint`, quantités `int`, expiration `date`, instants `datetime2(7)`, neuf rowversions Catalogue/Inventory, FK `NO ACTION` et journal append-only.
- Transactions : mouvement et balance atomiques ; démarrage/finalisation d’inventaire sous stratégie EF + transaction ; stock négatif refusé.
- Idempotence/concurrence : `OperationId` + empreinte et `rowversion` Base64 ; répétition cohérente stable, conflit explicite sans retry aveugle.
- Lots/POS : expirés visibles mais exclus ; FEFO descriptif ; disponibilité POS SQL ; aucune réservation/décrémentation lors d’une vente simulée.
- Desktop : page Stock fonctionnelle et responsive, ajustements confirmés, historique, sessions/comptages/finalisation ; Diagnostic Inventory sans secret.
- SQL : migration `InventoryPersistenceV01` appliquée, aucune migration en attente ; seed exécuté deux fois, un emplacement, cinq lots, douze balances et douze mouvements Opening sans duplication.
- Tests : scénario SQL réel isolé et tests Domain/Application/architecture ; rollback/réapplication de l’ensemble des migrations conservé.
- Écart : la vérification Sprint 10 historique voit onze références sous collation insensible à la casse à cause de `demo-manual-001`, donnée manuelle inactive préservée ; aucun clear large n’a été exécuté.
- Gate restant : captures/revue manuelle Stock/POS/Diagnostic, SQL Server Express et certificat TLS local.
- Git : branche `main`, HEAD initial `58e7315`, index vide, zéro nouveau commit, remote ou push.

## Mise à jour après le Sprint 12

- Statut : `IMPLEMENTED — SQL PURCHASING VALIDATION PASS — MANUAL RECEIVING REVIEW REQUIRED`.
- Domain/Application : statuts et erreurs Purchasing, commandes/vues/contrats/handlers spécifiques, sans EF Core ni repository générique.
- Persistence : cinq tables `purchasing`, quatre `rowversion`, prix d’achat `decimal(19,4)`, quantités `int`, FK `NO ACTION`, BL unique par fournisseur et réception append-only.
- Transactions : finalisation sous stratégie EF + isolation Serializable ; réception, lot, mouvement `PurchaseReceipt`, balance et statut de commande dans un commit unique.
- Idempotence/concurrence : `OperationId` + empreinte SHA-256, identifiants dérivés déterministes et tokens Base64 ; replay équivalent stable, contenu différent refusé.
- Desktop : fournisseurs, commandes et réceptions liés/directes sur `/module/purchasing`, avertissements visibles, confirmation, anti-double-soumission et Diagnostic Purchasing.
- SQL : migration `PurchasingPersistenceV01` appliquée ; seed double, verify, clear ciblé et reseed réussis avec quatre fournisseurs, deux commandes et une réception Demo.
- Sécurité : aucune donnée bancaire/fiscale, marge, paiement fournisseur, connexion ou donnée réelle ; prix de vente inchangé.
- Gate restant : captures/revue manuelle Achats/Stock/SSMS/panne SQL, SQL Server Express et certificat TLS local.
- Git : branche `main`, HEAD initial `a46ffb7`, index vide, zéro nouveau commit, remote ou push.

## Mise à jour après le Sprint 13

- Statut : `IMPLEMENTED — SQL SALES VALIDATION PASS — MANUAL POS REVIEW REQUIRED`.
- Application : `FinalizeSaleHandler`, paiements espèces/carte externe, lectures par identifiant/récentes et erreurs stables, sans dépendance Infrastructure.
- Persistence : cinq tables `sales`, snapshots produit/prix, allocations par balance/lot, paiement et registre idempotent ; FK `NO ACTION` et montants `decimal(19,4)`.
- Transaction : produits/prix/stock revalidés, FEFO multi-lots, lots expirés exclus, mouvement `Sale`, balance et opération complétée dans un commit Serializable.
- Idempotence/concurrence : SHA-256 par opération, replay identique stable, contenu différent refusé, `rowversion` et conflits SQL convertis sans last-write-wins.
- POS : référence persistée, historique SQL récent, panier conservé sur panne/prix/stock ; double validation bloquée et impression toujours post-commit simulée.
- SQL : migration `SalesPersistenceV01` appliquée à `Pharmacy_Development`, cinq migrations au total, aucun changement modèle en attente, vérifications S9 à S13 réussies, zéro vente seedée.
- Tests : SQL Server réel avec vente espèces FEFO, carte, refus atomiques, idempotence, contraintes, rollback et réapplication ; base temporaire supprimée.
- Gate restant : revue manuelle POS/Stock/SSMS, SQL Server Express et certificat TLS local.
- Git : branche `main`, HEAD initial `a46ffb7`, index vide, zéro nouveau commit, remote ou push ; changements Sprints 12 et 13 visibles pour revue.
