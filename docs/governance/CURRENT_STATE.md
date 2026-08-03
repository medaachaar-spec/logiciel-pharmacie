# CURRENT_STATE - Logiciel Pharmacie

Date de mise à jour : 2026-08-03

## État fonctionnel et architectural

- Phase 1 ADR : PASS.
- Phase 2 fonctionnelle : PASS.
- Phase 3 modèle de données : PASS.
- Phase 4 UX/UI : PASS.
- Phase 5 code-first : fondation technique Sprint 1, `UI Foundations V0.1` Sprint 2, shell applicatif V0.1 Sprint 3, POS synthétique V0.1 Sprint 4 et continuité POS V0.2 Sprint 5 créés ; noyau Sales Sprint 6, cas d’utilisation/adaptateurs mémoire Sprint 7 puis connexion progressive du POS à cette chaîne au Sprint 8, sans persistance.

## État du développement

- Documentation : INSTALLÉE ; package de gouvernance complet et cinq PDF de référence présents.
- Dépôt réel : `C:\Users\HP\Desktop\Logiciel pharmacie`.
- Branche active : `main`.
- Sprint 0 : `IMPLEMENTED — FOUNDATION READY WITH WARNINGS` après correction de classification Sprint 0.1.
- Git : dépôt local sur le commit initial Sprint 8 `7ba1e83`, zéro remote ; changements Sprint 7 trouvés déjà stagés à l’entrée du Sprint 8 et préservés, changements Sprint 8 laissés visibles sans nouveau `git add`.
- Solution .NET créée : OUI — `Pharmacy.slnx`, six projets .NET 10.
- Code frontend : POS Application Integration V0.1 — shell WPF/Blazor Hybrid, Ventes connecté aux handlers Sales pour catalogue/panier/version, suspensions Desktop restaurées par handlers, TPE ambigu et impression toujours simulés ; sept pages provisoires, diagnostic et UI Lab Debug.
- Code backend : SALES APPLICATION V0.1 — Domain et Application restent inchangés au Sprint 8 ; Infrastructure fournit cinq adaptateurs temporaires en mémoire enregistrés une fois pour le processus Desktop, sans persistance.
- Base SQL Server métier : NON CRÉÉE pour ce projet.
- CI : NON DÉMARRÉE.

## Prérequis détectés au Sprint 0

- Git 2.55.0 et branche `main` : PASS.
- SDK .NET 10 stable `10.0.302` et runtimes 10.0.10 : PASS.
- MSBuild 18.6.11 et WebView2 150.0.4078.105 : PASS.
- Windows 11 Famille x64 : WARNING pour les fondations ; Windows 11 Pro x64 reste obligatoire avant certification du poste pilote.
- Visual Studio / Build Tools : absent — WARNING non bloquant tant que `dotnet` CLI et MSBuild fonctionnent.
- Workloads .NET Desktop/WPF et ASP.NET/web : absents — WARNING non bloquant confirmé par le smoke build Sprint 1 réussi via CLI/MSBuild.
- SQL Server 2025 Express : absent ; édition Developer 17.0.1125.2 détectée — WARNING/différé pour les fondations, BLOCKING avant persistance SQL et production pilote.
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

Effectuer la revue manuelle d’intégration Sprint 8 : ouverture, bandeau Application en mémoire, recherche/scan, panier et totaux Application, concurrence, suspension/reprise, carte synthétique, impression simulée, Diagnostic, UI Lab, clavier et responsive. Ne pas démarrer la persistance sans autorisation explicite. Ne pas démarrer SQL avant disponibilité de SQL Server 2025 Express et traitement contrôlé du TLS. Ne pas certifier le poste pilote avant conformité Windows 11 Pro x64 et SQL Server 2025 Express.

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
