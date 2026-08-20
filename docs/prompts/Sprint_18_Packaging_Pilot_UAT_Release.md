# SPRINT 18 — Packaging Windows, SQL Server Express, installation pilote, UAT et préparation de livraison v1.0

## 1. Rôle
Tu agis comme ingénieur logiciel senior, architecte .NET/Windows/SQL Server, spécialiste packaging, déploiement local, sécurité, migration, QA, exploitation et recette utilisateur pour le projet **Logiciel Pharmacie**.

Tu dois transformer l’application validée des Sprints 0 à 17 en une **Release Candidate installable et exploitable sur le poste pilote**, sans casser les fonctionnalités existantes.

Les opérations Git d’écriture sont réalisées manuellement par le propriétaire du projet. Tu ne dois donc faire aucun `git add`, commit, push, ajout de remote ou changement de branche.

## 2. Mission
Implémenter et valider :

- publication Windows `win-x64` ;
- packaging/installateur Windows ;
- préflight machine ;
- stratégie SQL Server Express pour le pilote ;
- configuration locale sécurisée ;
- application contrôlée des migrations ;
- bootstrap premier compte propriétaire ;
- préservation des données pendant upgrade/réparation/désinstallation ;
- health checks post-installation ;
- procédure d’upgrade et rollback ;
- sauvegarde automatique avant migration/upgrade ;
- configuration WebView2/runtime nécessaire ;
- préparation scanner/imprimante/TPE/UPS ;
- tests end-to-end sur environnement propre ;
- validation installation/désinstallation/réinstallation ;
- tests de restauration ;
- UAT / recette pilote structurée ;
- documentation utilisateur, support et exploitation ;
- génération d’une Release Candidate v1.0 ;
- checklist des derniers gates physiques et organisationnels.

Ne prétends jamais que le produit est “production ready” ou “final v1.0” tant que les gates explicitement requis ne sont pas réellement passés.

## 3. Sources de vérité
Respecter, dans cet ordre :

1. ADR validés du projet ;
2. dossiers Phase 2, 3, 4 et 5 ;
3. architecture et code réels du dépôt ;
4. migrations et rapports Sprints 9 à 17 ;
5. documentation Backup/Restore, Import et Hardware du Sprint 17 ;
6. règles sécurité/RBAC/audit du Sprint 15 ;
7. règles reporting du Sprint 16 ;
8. contraintes et décisions du présent prompt.

Ne pas inventer une dépendance, une règle métier ou une exigence légale non validée.

## 4. État Git attendu
Branche : `main`.

Le dernier commit doit correspondre au Sprint 17, avec un message proche de :
`feat: add backup restore imports and hardware integration`

Le hash exact dépend du commit manuel effectué par le propriétaire.

Le working tree doit être propre avant ajout du prompt Sprint 18, à l’exception autorisée :
`docs/prompts/Sprint_18_Packaging_Pilot_UAT_Release.md`

Avant toute modification :
- `git status --short`
- `git log --oneline -n 10`
- confirmer `main`
- confirmer Sprint 17 en HEAD
- index vide
- aucun changement fonctionnel antérieur
- inventorier projets, migrations, scripts, docs, assets, configuration, publish profiles et tests.

Si seul le prompt Sprint 18 est non suivi, continuer.

## 5. Politique Git
INTERDIT :
- git add
- git commit
- git push
- git remote add
- création/changement de branche
- reset --hard
- stash destructif
- checkout/restauration destructive.

À la fin, uniquement rapporter Git en lecture seule :
- branche
- HEAD
- `git status --short`
- index vide
- aucun remote ajouté
- aucun commit/push.

## 6. Cible pilote validée
Cible principale :
- Windows 11 x64 ;
- cible pilote prévue : Windows 11 Pro x64 ;
- application locale-first ;
- .NET 10 ;
- WPF + Blazor Hybrid/WebView2 ;
- SQL Server Express prévu pour le pilote/production locale ;
- SQL Server Developer uniquement en développement/test ;
- mono-poste pilote ;
- architecture prête pour évolution multi-utilisateur ;
- scanner USB HID ;
- imprimante ticket 80 mm ;
- TPE externe/manual confirmation ;
- UPS recommandé ;
- pas de cloud sync V1.

IMPORTANT :
Ne pas supposer que la machine cible possède déjà les runtimes ou SQL nécessaires.
Le packaging doit détecter clairement les prérequis et choisir une stratégie documentée.

## 7. Release Candidate et version
Préparer une version de type :
`1.0.0-rc.1`

ou convention équivalente cohérente avec le dépôt.

Ne pas déclarer `1.0.0 final` automatiquement.

Créer une source unique de version si le dépôt n’en possède pas déjà une.

Afficher la version :
- dans l’application ;
- dans diagnostic/support ;
- dans les métadonnées de build/installateur lorsque possible.

## 8. Publication Windows

### 8.1 Publish
Créer une publication reproductible :
- `win-x64`
- self-contained si cohérent et validé
- Release
- symbols/debug non distribués sauf package support séparé
- trimming uniquement si déjà prouvé compatible ; sinon ne pas l’activer par optimisation arbitraire
- single-file uniquement si compatible avec WPF/Blazor Hybrid/WebView2 et testé ; sinon privilégier stabilité.

### 8.2 Sortie
Créer une structure claire :
- app
- config
- logs
- data/config local
- backup path configurable
- support bundle si existant.

Ne pas placer données utilisateur modifiables dans un emplacement écrasé par les upgrades.

## 9. Installateur Windows

### 9.1 Technologie
Utiliser une technologie de packaging déjà disponible ou raisonnablement maintenable dans le dépôt.

Ne pas ajouter une chaîne d’outils exotique sans nécessité.

Le résultat doit fournir un EXE/MSI ou équivalent installable cohérent pour Windows.

### 9.2 Fonctionnalités
L’installateur doit :
- vérifier architecture x64 ;
- vérifier version Windows minimale supportée ;
- vérifier espace disque ;
- vérifier droits requis ;
- détecter WebView2 ;
- détecter SQL Server/instance configurée ;
- détecter configuration existante ;
- préserver données et configuration lors d’un upgrade ;
- proposer réparation/désinstallation sûre ;
- ne pas supprimer la base par défaut à la désinstallation.

### 9.3 Signature
Si un certificat de signature de code réel n’est pas disponible :
- ne pas générer de faux certificat “production” ;
- permettre une build/test non signée ;
- documenter `CODE SIGNING GATE PENDING`.

Si un certificat valide est disponible, prévoir la procédure de signature.

## 10. SQL Server Express — gate pilote

### 10.1 Validation réelle
Vérifier localement les prérequis et capacités réellement utilisés par le produit.

Le Sprint doit tester explicitement une instance **SQL Server Express compatible**, pas seulement Developer, avant de déclarer le gate Express PASS.

Si Express n’est pas installé sur la machine de développement :
- ne pas modifier destructivement Developer ;
- documenter l’installation requise ;
- préparer automation/preflight ;
- statut `SQL EXPRESS PILOT GATE PENDING` jusqu’au test réel.

### 10.2 Instance
Définir une convention d’instance pilote cohérente, par exemple :
`PHARMACY`

ou instance Express existante si choisie.

Ne pas hardcoder `localhost` partout.

La chaîne de connexion doit être générée/configurée localement de manière sûre.

### 10.3 Base
Base pilote :
`Pharmacy`

ou nom cohérent distinct de `Pharmacy_Development`.

Ne pas embarquer de données synthétiques de démonstration dans la base pilote finale, sauf choix explicite d’un mode Demo distinct.

### 10.4 Création/migration
Le déploiement doit :
1. détecter instance ;
2. vérifier connectivité ;
3. créer base si onboarding neuf ;
4. appliquer migrations contrôlées ;
5. vérifier résultat ;
6. bootstrap propriétaire si aucun utilisateur ;
7. exécuter health check.

Pas de migration silencieuse à chaque démarrage de l’application si l’ADR l’interdit.

## 11. Upgrade / migrations

### 11.1 Pré-upgrade
Avant toute migration d’une base existante :
- vérifier version ;
- backup ;
- verify backup ;
- hash ;
- espace disque ;
- connexion exclusive ou stratégie sûre ;
- enregistrer résultat d’upgrade.

### 11.2 Upgrade
- appliquer migrations attendues uniquement ;
- timeout raisonnable ;
- rollback transactionnel quand possible ;
- message clair si échec ;
- application ne doit pas démarrer sur schéma incompatible.

### 11.3 Rollback
Ne pas promettre un downgrade EF automatique dangereux.

Prévoir stratégie réaliste :
- restauration du backup pré-upgrade ;
- réinstallation de la version précédente ;
- vérification.

Documenter précisément.

## 12. Configuration locale sécurisée
Ne pas mettre secrets dans :
- appsettings commité ;
- source ;
- installateur ;
- scripts publics.

Prévoir stockage config local :
- chaîne SQL sans mot de passe si Integrated Security/local Windows auth est le choix pilote ;
- chemins backup/import ;
- périphériques ;
- paramètres d’alerte déjà persistés selon architecture ;
- informations pharmacie non sensibles si prévues.

Utiliser DPAPI ou mécanisme Windows approprié pour secrets locaux réellement nécessaires.

## 13. Bootstrap propriétaire
Sur installation neuve :
- aucun utilisateur seedé ;
- aucun mot de passe par défaut ;
- workflow bootstrap explicite ;
- mot de passe 15–128 ;
- récupération/codes selon Sprint 15 ;
- Owner role/permissions ;
- audit de création ;
- empêcher second bootstrap une fois un utilisateur existant.

Créer un guide de conservation sûre des codes de récupération.

## 14. Données de départ
Prévoir onboarding contrôlé :

### Installation neuve
- base vide métier ;
- références techniques/permissions nécessaires uniquement ;
- aucun faux stock ;
- aucune fausse vente ;
- aucun faux client.

### Import réel
Utiliser pipeline Sprint 17 :
- catalogue fournisseur ;
- stock initial ;
- lots/péremptions uniquement si disponibles.

Ne pas importer automatiquement des données réelles sans preview/confirmation.

## 15. Backup opérationnel pilote
Configurer une stratégie compatible avec l’édition SQL réellement déployée.

Le logiciel doit au minimum permettre :
- backup manuel ;
- vérification ;
- copie externe ;
- restore-test ;
- rappel/documentation de fréquence.

Si la fréquence automatique cible nécessite Windows Task Scheduler ou un service :
- implémenter seulement si stable et clairement maintenable ;
- sinon fournir scripts/tâches documentées à installer explicitement.

Ne pas faire de promesse RPO 15 min si la configuration réelle ne l’atteint pas.

## 16. Hardware pilot gate

### 16.1 Scanner
Préparer checklist :
- USB HID détecté ;
- scan produit connu ;
- inconnu ;
- double scan ;
- saisie clavier ;
- focus POS.

### 16.2 Imprimante 80 mm
Checklist réelle :
- driver installé ;
- imprimante sélectionnée ;
- ticket test ;
- vente réelle synthétique ;
- panne/offline ;
- réimpression ;
- caractères français/arabe si nécessaires ;
- largeur/coupe si matériel supporte.

### 16.3 TPE
Checklist :
- TPE indépendant ;
- confirmation manuelle ;
- succès/échec/annulation ;
- aucune donnée carte.

### 16.4 UPS
Checklist :
- PC + écran + équipement critique branchés ;
- test de coupure contrôlé seulement si sûr ;
- procédure arrêt propre documentée.

Si matériel réel indisponible :
`HARDWARE PILOT GATE PENDING`

et ne pas simuler un PASS physique.

## 17. End-to-end tests
Créer/exécuter des scénarios E2E représentatifs sur environnement de test propre.

### E2E 1 — Onboarding
- install app ;
- SQL ;
- migrations ;
- bootstrap owner ;
- login ;
- catalogue vide ;
- import synthétique ;
- stock initial synthétique.

### E2E 2 — Achat
- fournisseur ;
- commande ;
- réception ;
- lot ;
- stock augmenté.

### E2E 3 — Vente
- scan/recherche ;
- panier ;
- paiement espèces ;
- FEFO ;
- stock décrémenté ;
- ticket.

### E2E 4 — Crédit
- client fidèle ;
- vente partielle + crédit ;
- règlement ;
- reporting.

### E2E 5 — Retour
- retour ;
- remboursement ;
- restock ou not restockable ;
- audit/reporting.

### E2E 6 — Inventaire
- session ;
- comptage ;
- finalisation ;
- stock.

### E2E 7 — Backup/restore
- backup ;
- verify ;
- restore temp ;
- checks.

### E2E 8 — RBAC
- Owner ;
- utilisateur limité ;
- permission denied ;
- reauth.

### E2E 9 — Panne
- SQL indisponible ;
- printer offline ;
- recovery comportement sûr.

## 18. Tests d’installation
Tester au minimum :

### Fresh install
- environnement propre autant que possible ;
- installation ;
- démarrage ;
- bootstrap ;
- fonctionnement.

### Repair
- réparer installation ;
- données conservées.

### Upgrade
- fixture/version précédente installable ;
- base existante ;
- backup ;
- upgrade ;
- migration ;
- données conservées.

### Uninstall
- application retirée ;
- données SQL non supprimées silencieusement ;
- réinstallation possible.

### Reinstall
- reconnexion à la base conservée contrôlée ;
- aucune duplication seed/bootstrap.

## 19. Performance pilote
Mesurer raisonnablement :
- démarrage application ;
- login ;
- recherche produit ;
- scan ;
- ajout panier ;
- finalisation vente ;
- ouverture dashboard ;
- import ~8 000 références synthétiques ;
- backup ;
- taille DB.

Ne pas inventer SLO officiel.
Documenter résultats observés et environnement.

## 20. Sécurité Release
Vérifier :
- aucun secret/PII dans package ;
- pas de fichiers dev ;
- pas de chaînes Developer hardcodées ;
- pas de DB synthétique accidentelle ;
- RBAC actif ;
- audit actif ;
- logs minimisés ;
- réauth restauration/import sensibles ;
- recovery codes protégés ;
- export sécurisé ;
- pas de données carte ;
- dépendances connues listées.

## 21. Support bundle
Si possible, fournir une fonction/support script produisant un bundle sans données sensibles :
- version application ;
- état environnement ;
- erreurs/logs récents filtrés ;
- migrations ;
- SQL version/édition ;
- device status ;
- aucune donnée métier complète ;
- aucun secret.

## 22. Documentation utilisateur
Créer guides simples :
- premier démarrage ;
- connexion ;
- vente ;
- crédit ;
- retour ;
- stock ;
- réception ;
- inventaire ;
- reporting ;
- backup ;
- import ;
- scanner ;
- imprimante ;
- panne SQL ;
- récupération.

## 23. Documentation installation/support
Créer/mettre à jour :
- `docs/reports/sprint-18/codex-report.md`
- `docs/reports/sprint-18/manual-validation.md`
- `docs/deployment/INSTALLATION_PILOT_V1.md`
- `docs/deployment/UPGRADE_ROLLBACK_V1.md`
- `docs/deployment/UNINSTALL_REINSTALL_V1.md`
- `docs/deployment/SQL_EXPRESS_PILOT_V1.md`
- `docs/deployment/RELEASE_CHECKLIST_V1.md`
- `docs/deployment/PILOT_UAT_PLAN_V1.md`
- `docs/deployment/PILOT_GO_LIVE_CHECKLIST_V1.md`
- `docs/support/SUPPORT_RUNBOOK_V1.md`
- `docs/user/USER_GUIDE_V1.md`

## 24. UAT — recette utilisateur
Préparer un protocole que la pharmacienne peut exécuter.

Scénarios minimum :
1. se connecter ;
2. rechercher/scanner produit ;
3. créer produit si nécessaire ;
4. recevoir livraison ;
5. vérifier stock/lot/péremption ;
6. vendre espèces ;
7. vendre carte/TPE ;
8. vendre à crédit client autorisé ;
9. régler crédit ;
10. suspendre/reprendre ;
11. retourner/rembourser ;
12. inventaire ;
13. reporting ;
14. export ;
15. backup ;
16. impression/réimpression ;
17. redémarrage ;
18. panne simulée simple.

Pour chaque scénario :
- prérequis ;
- actions ;
- attendu ;
- PASS/FAIL ;
- commentaire ;
- capture éventuelle.

## 25. Critères GO / NO-GO
Définir clairement.

### GO pilote uniquement si :
- build/tests automatiques PASS ;
- SQL réel PASS ;
- package installé ;
- migration PASS ;
- backup/restore PASS ;
- aucun défaut critique/majeur ouvert ;
- sécurité/RBAC PASS ;
- UAT critique PASS ;
- hardware requis réellement PASS ou explicitement non nécessaire au pilote initial ;
- procédure support/rollback prête.

### NO-GO si :
- risque perte données ;
- restore non vérifié ;
- ventes/stock incohérents ;
- auth contournable ;
- double transaction ;
- données sensibles exposées ;
- migration non fiable ;
- package non reproductible ;
- matériel indispensable non fonctionnel.

## 26. Tests automatisés
Tous les tests existants Sprints 0–17 doivent rester PASS.

Ajouter tests nécessaires pour :
- versioning ;
- configuration ;
- preflight ;
- migration orchestrator ;
- install/upgrade logic testable ;
- bootstrap ;
- package file inclusion/exclusion ;
- support bundle ;
- data preservation.

Debug/Release :
- 0 erreur
- 0 warning.

SQL réel :
- Developer tests existants ;
- Express test obligatoire pour lever `SQL EXPRESS PILOT GATE PENDING`.

## 27. Smokes
Exécuter :
- Desktop avec SQL ;
- sans SQL ;
- published app ;
- après fresh install ;
- après upgrade ;
- après repair si possible.

Aucun processus résiduel.

## 28. Validation manuelle à préparer
Créer guide Sprint 18 couvrant :
- installateur ;
- fresh install ;
- bootstrap ;
- login ;
- import ;
- achat ;
- vente ;
- crédit ;
- retour ;
- reporting ;
- backup ;
- restore ;
- scanner ;
- printer ;
- TPE ;
- upgrade ;
- repair ;
- uninstall ;
- reinstall ;
- rollback ;
- UAT.

## 29. Livrables Release Candidate
Créer dans un dossier de sortie NON commité si binaire lourd :
- installateur RC ;
- hashes SHA-256 ;
- manifest version ;
- release notes ;
- checklist ;
- éventuellement portable diagnostic package si prévu.

Ne jamais commiter :
- `.bak`
- DB réelle
- secrets
- certificats privés
- gros binaires build si politique repo les exclut.

## 30. Statuts finaux autorisés

### Tous gates techniques + Express passent, matériel réel encore absent
`IMPLEMENTED — RELEASE CANDIDATE VALIDATION PASS — MANUAL PILOT UAT REQUIRED — HARDWARE PILOT GATE PENDING`

### Express réel non testé
`IMPLEMENTED — RELEASE CANDIDATE VALIDATION PASS — SQL EXPRESS PILOT GATE PENDING — MANUAL PILOT UAT REQUIRED`

### Tout y compris matériel/UAT passe
`IMPLEMENTED — PILOT RELEASE V1.0 VALIDATION PASS — GO-LIVE REVIEW REQUIRED`

### Blocage
`BLOCKED — SPRINT 18 REQUIRES PILOT ENVIRONMENT OR MANUAL DECISION`

### Échec
`FAILED — SPRINT 18 VALIDATION FAILED`

Ne jamais utiliser `FINAL PRODUCTION RELEASE` sans GO-LIVE humain explicite.

## 31. Rapport final attendu
Le rapport doit inclure :
- version RC ;
- packaging choisi ;
- publish ;
- installer ;
- code-signing status ;
- SQL Express status ;
- migrations ;
- bootstrap ;
- configuration ;
- backup pré-upgrade ;
- fresh install ;
- upgrade ;
- repair ;
- uninstall/reinstall ;
- rollback ;
- E2E ;
- performance observée ;
- security release scan ;
- hardware gates ;
- UAT status ;
- GO/NO-GO blockers ;
- tests Debug/Release ;
- SQL Developer/Express ;
- pending model changes ;
- smokes ;
- Git lecture seule ;
- chemins des livrables locaux ;
- documentation produite.

FIN DU PROMPT SPRINT 18
