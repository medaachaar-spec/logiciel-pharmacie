# SPRINT 14 — Caisse simple, clients fidèles, crédit, suspensions persistantes et retours/remboursements

## 1. Rôle
Tu agis comme ingénieur logiciel senior, architecte .NET/SQL Server, analyste métier, QA, sécurité et intégration pour le projet **Logiciel Pharmacie**.

Tu dois implémenter le Sprint 14 dans le dépôt existant sans casser les Sprints 0 à 13.

## 2. Mission
Implémenter un Sprint 14 production-oriented couvrant :

- gestion minimale des clients ;
- autorisation explicite au crédit pour certains clients fidèles ;
- vente totalement ou partiellement à crédit ;
- règlements ultérieurs partiels ou complets ;
- historique immuable des créances et règlements ;
- suspensions de ventes persistantes ;
- reprise d’une vente suspendue après redémarrage ;
- retours contrôlés d’articles issus d’une vente persistée ;
- remboursement comme mode de compensation du retour accepté ;
- journal simple des flux espèces liés aux ventes, remboursements et règlements de crédit ;
- intégration SQL transactionnelle cohérente avec les Sprints précédents ;
- UI Desktop cohérente avec le design system existant ;
- tests automatisés et validation SQL réelle.

Aucun commit, push, ajout de remote ou changement de branche n’est autorisé.

## 3. Sources de vérité
Respecter en priorité :

1. ADR validés du projet ;
2. dossier Phase 2 — Modélisation fonctionnelle ;
3. dossier Phase 3 — Modèle de données ;
4. dossier Phase 4 — UX/UI ;
5. dossier Phase 5 — Code First / Vibe Coding ;
6. architecture et conventions réellement présentes dans le dépôt ;
7. rapports Sprints 9 à 13 ;
8. les règles métier ci-dessous, explicitement validées.

Ne pas inventer une règle métier pour combler un manque.

## 4. État du dépôt attendu avant démarrage
Branche : `main`

HEAD attendu :
`13bfb85501ba876df4178788db4eb4e3f8d3c546`
Message :
`feat: add persistent SQL sales finalization and FEFO stock consumption`

Le working tree doit être propre au démarrage.

Commits récents attendus :
- Sprint 13 : `13bfb85`
- Sprint 12 : `383de33`
- Sprint 11 : `a46ffb7`
- Sprint 10 : `58e7315`
- Sprint 9 : `e9a5fcf`
- Sprint 8 : `49c2ff2`

Aucun remote ne doit être ajouté.

## 5. Prérequis à vérifier
Avant toute modification :

- `git status`
- `git log --oneline -n 8`
- vérifier le HEAD attendu ;
- vérifier que l’index est vide ;
- identifier les projets Domain/Application/Infrastructure/Desktop/UnitTests/IntegrationTests ;
- inspecter les implémentations existantes de Sales, Inventory et Purchasing ;
- inspecter les patterns Result/Error, Money, OperationId, rowversion, transactions, idempotence, logging, validation et UI ;
- inspecter les migrations EF Core existantes ;
- vérifier la connexion SQL Server via `PHARMACY_SQLSERVER_CONNECTION` ;
- vérifier l’absence de migrations EF en attente avant le Sprint 14.

Si le dépôt n’est pas dans cet état, arrêter et rapporter précisément l’écart avant de modifier quoi que ce soit.

## 6. Règles métier VALIDÉES — NE PAS MODIFIER

### 6.1 Ouverture de caisse
La pharmacie pilote **ne souhaite pas saisir de fond de caisse à l’ouverture**.

Donc :
- ne pas imposer une session d’ouverture avec montant initial ;
- ne pas créer un workflow obligatoire `OpenCashSession` ;
- ne pas bloquer les ventes parce qu’une caisse n’a pas été “ouverte”.

### 6.2 Fermeture de caisse
La pharmacie pilote **ne souhaite pas faire un comptage de caisse à la fermeture**.

Donc :
- ne pas imposer de comptage réel ;
- ne pas calculer d’écart théorique/réel obligatoire ;
- ne pas créer un workflow de clôture de caisse obligatoire.

### 6.3 Crédit client
Le crédit est autorisé, mais **uniquement pour certains clients fidèles**.

Règle :
- un client doit exister ;
- un indicateur explicite `CreditAllowed` ou équivalent doit être activé par un opérateur autorisé ;
- le logiciel ne décide jamais lui-même qu’un client est fidèle ;
- les clients sans autorisation crédit ne peuvent pas utiliser le crédit.

### 6.4 Paiement partiel + crédit
Une vente peut être partiellement payée immédiatement et partiellement laissée à crédit.

Exemple :
- total : 300 MAD
- payé maintenant : 100 MAD
- crédit : 200 MAD

Le système doit empêcher :
- montant payé > total ;
- crédit négatif ;
- crédit sans client ;
- crédit pour un client non autorisé.

### 6.5 Plafond de crédit
Il n’existe **aucun plafond de crédit validé**.

Donc :
- ne pas inventer de limite ;
- ne pas bloquer selon un seuil arbitraire ;
- ne pas ajouter de scoring ou solvabilité automatique.

### 6.6 Règlement d’une dette
Le remboursement progressif est autorisé.

Exemple :
- dette : 500 MAD
- règlement : 100 MAD
- solde restant : 400 MAD

Les règlements doivent être :
- partiels ou complets ;
- persistés ;
- traçables ;
- immuables ;
- corrigés par écriture inverse si nécessaire, jamais par suppression ou écrasement de l’historique.

### 6.7 Retours
La pharmacie accepte les retours.

IMPORTANT :
- le logiciel ne doit pas affirmer une règle légale générale ;
- il doit uniquement offrir un workflow contrôlé ;
- l’opérateur décide si le retour est accepté ;
- le retour doit toujours être rattaché à la vente d’origine ;
- aucun retour supérieur à la quantité effectivement vendue et non déjà retournée.

### 6.8 Compensation du retour
Lorsqu’un retour est accepté, la compensation retenue est le **remboursement**.

Ne pas implémenter dans ce sprint :
- avoir ;
- échange automatique ;
- portefeuille client.

### 6.9 TPE
Le TPE reste externe.

Le logiciel peut mémoriser :
- mode de paiement = carte/TPE ;
- confirmation manuelle du succès.

Le logiciel ne doit stocker :
- aucun numéro de carte ;
- aucun cryptogramme ;
- aucune donnée bancaire sensible.

## 7. Périmètre fonctionnel autorisé

### 7.1 Clients
Créer un module minimal permettant :
- créer un client ;
- modifier les informations autorisées ;
- activer/désactiver ;
- activer/désactiver l’autorisation de crédit ;
- rechercher par nom/téléphone/référence ;
- consulter le solde de crédit ;
- consulter l’historique financier.

Données minimales :
- identifiant technique ;
- référence métier locale ;
- nom ;
- téléphone facultatif ;
- note courte facultative ;
- actif/inactif ;
- crédit autorisé oui/non ;
- timestamps ;
- rowversion sur les données mutables.

Pas de données cliniques.

### 7.2 Créances client
Créer un modèle de dette basé sur des écritures financières immuables.

Préférer un journal de mouvements plutôt qu’un simple solde écrasable.

Le solde client doit être dérivable/recalculable à partir des écritures.

Types conceptuels possibles :
- `CreditSale`
- `CreditPayment`
- `CreditAdjustmentIncrease`
- `CreditAdjustmentDecrease`
- `RefundImpact` si nécessaire

Les noms exacts doivent suivre les conventions du dépôt.

### 7.3 Vente à crédit
Étendre le finalizer SQL du Sprint 13 sans casser son atomicité.

Scénarios :
- paiement intégral espèces ;
- paiement intégral carte ;
- paiement partiel immédiat + crédit ;
- éventuellement paiement intégral crédit si cohérent avec l’architecture existante.

Une vente avec crédit doit :
- exiger un client ;
- vérifier `CreditAllowed`;
- créer la vente ;
- créer les lignes ;
- créer les allocations FEFO ;
- décrémenter le stock ;
- créer les mouvements stock ;
- créer les paiements immédiats ;
- créer l’écriture de créance ;
- tout persister dans une transaction SQL unique.

Aucun état partiel si la transaction échoue.

### 7.4 Règlements de crédit
Permettre :
- rechercher un client ;
- afficher dette actuelle ;
- saisir montant à régler ;
- choisir mode de règlement supporté ;
- refuser montant <= 0 ;
- refuser montant > dette sauf règle explicite déjà présente ;
- créer une écriture immuable ;
- mettre à jour les vues/projections de solde ;
- assurer idempotence de l’opération ;
- protéger le double clic.

### 7.5 Caisse simple
Pas de sessions ouverture/fermeture obligatoires.

Créer uniquement ce qui est nécessaire pour tracer les flux espèces :
- encaissement espèces d’une vente ;
- remboursement espèces ;
- règlement espèces d’une dette client.

Le journal doit être append-only / immuable.

Ne pas inventer :
- fond de caisse ;
- clôture ;
- comptage ;
- écarts ;
- dépenses diverses ;
- retraits manuels ;
- dépôts bancaires.

Si le modèle existant impose un module `cash`, l’implémenter de façon minimale et compatible, sans workflow d’ouverture/fermeture.

### 7.6 Suspensions persistantes
Remplacer/compléter les suspensions en mémoire par une persistance SQL.

Exigences :
- suspendre un brouillon ;
- fermer l’application ;
- relancer ;
- retrouver les suspensions ;
- reprendre une suspension ;
- empêcher la reprise simultanée incohérente ;
- abandon contrôlé ;
- aucun impact stock tant que la vente n’est pas finalisée ;
- idempotence ;
- version/concurrence ;
- aucune duplication de lignes.

### 7.7 Retours
Implémenter un workflow de retour rattaché à une vente finalisée.

Exigences :
- rechercher/ouvrir vente d’origine ;
- sélectionner ligne ;
- quantité retournée > 0 ;
- quantité retournée cumulée <= quantité vendue ;
- empêcher double retour du même reliquat ;
- conserver prix de vente historique pour calcul de remboursement ;
- ne jamais modifier la vente originale ;
- créer une entité/transaction de retour séparée ;
- créer les écritures inverses nécessaires ;
- journaliser.

### 7.8 Réintégration stock lors d’un retour
Ne pas inventer automatiquement qu’un produit retourné est revendable.

Le workflow doit imposer une décision explicite de destination du retour, au minimum :
- `RestockApproved`
- `NotRestockable`

Si `RestockApproved` :
- réintégrer via mouvement de stock inverse contrôlé ;
- rattacher au produit et, si possible et cohérent, au lot d’origine ;
- sinon appliquer la règle documentée la plus sûre existante dans le modèle de données.

Si `NotRestockable` :
- remboursement possible ;
- aucune augmentation du stock vendable.

Ne pas créer un lot anonyme silencieusement.

### 7.9 Remboursement
Le retour accepté doit générer un remboursement.

Pour une vente :
- espèces : remboursement espèces si retenu par le workflow ;
- carte/TPE : ne jamais simuler un remboursement bancaire réel ; enregistrer seulement que le remboursement doit être traité/confirmé via le TPE externe, selon le pattern du projet ;
- vente à crédit : si une partie du montant correspondant est encore due, le retour doit réduire la créance de façon cohérente avant d’envisager un décaissement.

Ce dernier point doit être implémenté avec prudence :
- ne jamais rembourser plus que ce qui a réellement été payé ;
- ne jamais laisser une dette incohérente ;
- si l’architecture actuelle ne permet pas de le garantir proprement, implémenter un cas supporté minimal et documenter explicitement le reste comme hors périmètre plutôt que d’inventer.

## 8. Hors périmètre explicite
Ne pas implémenter :

- ouverture/fermeture de caisse obligatoire ;
- fond de caisse ;
- comptage de caisse ;
- écarts de caisse ;
- plafond automatique de crédit ;
- scoring client ;
- intérêts/pénalités ;
- échéancier de crédit complexe ;
- dossier médical ;
- ordonnance ;
- diagnostic ;
- allergies ;
- historique clinique ;
- fidélité à points ;
- avoir ;
- échange automatique ;
- wallet ;
- paiement bancaire intégré ;
- stockage de données carte ;
- multi-devises ;
- facturation réglementaire avancée ;
- comptabilité générale ;
- fiscalité automatisée ;
- suppression physique des ventes, retours ou écritures financières ;
- synchronisation cloud.

## 9. Architecture attendue
Respecter l’architecture modulaire existante :

- `Pharmacy.Domain`
- `Pharmacy.Application`
- `Pharmacy.Infrastructure`
- `Pharmacy.Desktop`
- tests unitaires
- tests d’intégration

Dépendances :
- Domain : aucune dépendance vers Infrastructure/Desktop ;
- Application -> Domain ;
- Infrastructure -> Application + Domain ;
- Desktop -> Application + Infrastructure.

Créer/étendre les modules logiques :
- Customers
- Credit
- Cash minimal
- Sales/SuspendedSales
- Returns

Éviter les dépendances circulaires.

## 10. Données et SQL Server

### 10.1 EF Core
Utiliser EF Core et SQL Server selon les conventions existantes.

### 10.2 Migration
Créer une migration additive Sprint 14.

Nom conseillé :
`CashCustomersCreditReturnsPersistenceV01`

Adapter le nom si les conventions du dépôt indiquent mieux.

### 10.3 Schémas
Réutiliser les schémas Phase 3 s’ils existent.

À défaut, architecture cohérente possible :
- `customers`
- `cash`
- `sales`

Ne pas multiplier les schémas inutilement.

### 10.4 Contraintes
Prévoir :
- PK explicites ;
- FK explicites ;
- index utiles ;
- check constraints ;
- unique constraints nécessaires ;
- `decimal` pour l’argent ;
- `datetime2` cohérent ;
- `rowversion` pour entités mutables ;
- `NO ACTION`/restrict pour les historiques ;
- append-only pour journaux financiers et retours finalisés.

### 10.5 Argent
Aucun `float`/`double` pour l’argent.

Utiliser les primitives/Value Objects Money déjà existants.

### 10.6 Transactions
Pour les workflows critiques :
- `READ COMMITTED` par défaut ;
- transaction SQL explicite ;
- audit/écritures financières/stock dans la même transaction lorsque nécessaire ;
- rollback total sur échec.

### 10.7 Idempotence
Toute opération de finalisation doit avoir un `OperationId` ou mécanisme équivalent persistant :
- finalisation vente crédit ;
- règlement crédit ;
- reprise/abandon suspension si nécessaire ;
- retour ;
- remboursement.

## 11. Sécurité et confidentialité
Même si le Sprint 15 fera l’authentification/RBAC complet :

- préparer des boundaries d’autorisation propres ;
- ne pas stocker de secret ;
- pas de PII dans les logs ;
- téléphone client seulement si utile ;
- ne pas journaliser de contenu sensible inutile ;
- aucune donnée bancaire ;
- erreurs utilisateur sans stack trace ;
- erreurs techniques dans logs locaux selon Serilog existant.

## 12. UX/UI
Respecter strictement le design system existant.

Pages/sections attendues selon architecture réelle :

### Clients
- liste/recherche ;
- détail ;
- autorisation crédit ;
- solde ;
- historique.

### Crédit
Peut être intégré à Clients plutôt qu’une page séparée si plus cohérent.

### Ventes
Étendre le POS pour :
- sélectionner un client ;
- afficher clairement si crédit autorisé ;
- permettre paiement partiel + crédit ;
- montrer montant payé maintenant ;
- montrer montant crédité ;
- bloquer validation si règle invalide.

### Suspensions
- liste persistante ;
- date/heure ;
- montant ;
- client éventuel ;
- reprendre ;
- abandonner.

### Historique ventes
Ajouter action de retour depuis une vente éligible.

### Retours
- lignes d’origine ;
- vendu ;
- déjà retourné ;
- retournable ;
- quantité demandée ;
- montant remboursement ;
- décision réintégration stock.

Tous les états :
- loading ;
- data ;
- empty ;
- validation error ;
- SQL unavailable ;
- concurrency conflict ;
- operation in progress ;
- success ;
- denied-ready pour futur Sprint 15.

Responsive minimum :
- 1100x700
- 1366x768
- 1920x1080

Aucun débordement horizontal non voulu.

## 13. Plan d’exécution recommandé
1. Audit dépôt et prérequis.
2. Lire docs métier/architecture pertinentes.
3. Concevoir modèle Domain/Application minimal.
4. Concevoir schéma physique Sprint 14.
5. Implémenter EF entities/configurations.
6. Créer migration.
7. Implémenter persistence SQL.
8. Implémenter crédit client.
9. Étendre finalisation Sales pour crédit partiel/intégral.
10. Implémenter règlements.
11. Implémenter suspensions persistantes.
12. Implémenter retours/remboursements.
13. Implémenter journal cash minimal.
14. Connecter Desktop.
15. Ajouter tests unitaires.
16. Ajouter tests SQL réels.
17. Ajouter scripts validation/clear/seed ciblés.
18. Tester migration up/down/reapply si procédure existante.
19. Builds Debug/Release.
20. Smoke Desktop avec SQL.
21. Smoke Desktop sans SQL.
22. Vérifier aucun process/temp DB résiduel.
23. Produire documentation et rapport.

## 14. Tests et critères d’acceptation

### 14.1 Clients
Tester :
- création ;
- modification ;
- désactivation ;
- autorisation crédit ;
- recherche ;
- concurrence rowversion.

### 14.2 Vente crédit
Tester :
- client autorisé + crédit ;
- client non autorisé -> refus ;
- aucun client + crédit -> refus ;
- paiement partiel + crédit ;
- paiement intégral crédit si supporté ;
- montant payé > total -> refus ;
- double validation -> une seule vente/créance ;
- rollback complet sur erreur.

### 14.3 Règlements
Tester :
- paiement partiel ;
- paiement complet ;
- montant > solde -> refus ;
- double clic ;
- idempotence ;
- historique immuable ;
- solde correct après plusieurs paiements.

### 14.4 Suspensions
Tester :
- suspension ;
- fermeture app ;
- relance ;
- reprise ;
- abandon ;
- double reprise ;
- aucune modification de stock avant finalisation.

### 14.5 Retours
Tester :
- retour partiel ;
- retour complet ;
- quantité > disponible -> refus ;
- double retour -> refus ;
- remboursement correct ;
- vente originale inchangée ;
- stock réintégré uniquement si explicitement approuvé ;
- non-restockable -> stock inchangé ;
- double clic -> une seule opération.

### 14.6 Cash minimal
Tester :
- vente espèces -> écriture cash ;
- règlement espèces -> écriture cash ;
- remboursement espèces -> écriture cash négative ;
- carte -> aucune donnée bancaire ;
- cohérence références sources.

### 14.7 SQL réel
Tests d’intégration obligatoires sur SQL Server réel :
- migration ;
- contraintes ;
- transactions ;
- idempotence ;
- concurrence ;
- rollback ;
- persistance après redémarrage ;
- absence de pending model changes.

### 14.8 Régression
Tous les tests Sprints 0 à 13 doivent rester PASS.

### 14.9 Build
Debug et Release :
- 0 erreur ;
- 0 avertissement.

## 15. Validation manuelle à préparer
Créer un guide manuel détaillé couvrant au minimum :

1. créer client sans crédit ;
2. tenter vente crédit -> refus ;
3. autoriser crédit ;
4. vente partiellement payée + crédit ;
5. vérifier stock décrémenté ;
6. vérifier dette client ;
7. règlement partiel ;
8. redémarrage ;
9. dette persistée ;
10. suspendre vente ;
11. redémarrer ;
12. reprendre suspension ;
13. finaliser ;
14. retour partiel avec restock ;
15. retour partiel sans restock ;
16. remboursement ;
17. tentative retour excessif ;
18. double clic retour ;
19. panne SQL ;
20. vérification SSMS.

Demander des captures :
- fiche client ;
- POS avec paiement + crédit ;
- historique crédit ;
- suspension persistante ;
- retour ;
- stock après retour ;
- historique vente/retour ;
- SSMS.

## 16. Documentation attendue
Créer/mettre à jour :

- `docs/reports/sprint-14/codex-report.md`
- `docs/reports/sprint-14/manual-validation.md`
- documentation physique Sprint 14 sous `docs/database/` selon conventions repo ;
- documentation des transactions de crédit/retour ;
- documentation de la caisse simple ;
- documentation des suspensions persistantes.

Le rapport doit distinguer :
- implémenté ;
- testé automatiquement ;
- testé SQL ;
- à valider manuellement ;
- hors périmètre ;
- limitations connues.

## 17. Git — interdictions strictes
Pendant ce Sprint :

NE PAS :
- `git add`
- `git commit`
- `git push`
- ajouter un remote
- changer de branche
- créer une branche
- faire `reset --hard`
- stash destructif
- checkout/restauration destructive.

À la fin :
- fournir `git status --short`
- fournir HEAD
- confirmer index vide
- confirmer aucun remote ajouté
- confirmer aucun commit/push.

## 18. Rapport final attendu
Le statut final doit utiliser exactement une des formes suivantes :

Succès technique :
`IMPLEMENTED — SQL CASH/CREDIT/RETURNS VALIDATION PASS — MANUAL REVIEW REQUIRED`

Blocage :
`BLOCKED — SPRINT 14 IMPLEMENTATION REQUIRES MANUAL DECISION`

Échec :
`FAILED — SPRINT 14 VALIDATION FAILED`

Le rapport doit inclure :
- résumé ;
- fichiers principaux ;
- migration ;
- tables créées/modifiées ;
- workflows ;
- tests ;
- nombre total tests Debug/Release ;
- résultats SQL ;
- smoke Desktop ;
- pending migrations/model changes ;
- secrets scan ;
- Git status ;
- limitations ;
- guide de validation humaine.

FIN DU PROMPT SPRINT 14
