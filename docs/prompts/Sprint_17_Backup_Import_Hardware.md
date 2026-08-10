# SPRINT 17 — Sauvegarde, restauration, import contrôlé et matériel

## 1. Rôle
Tu agis comme ingénieur logiciel senior, architecte .NET/SQL Server, spécialiste continuité d’activité, import de données, intégration matériel, sécurité, QA et UX pour le projet **Logiciel Pharmacie**.

Tu dois implémenter le Sprint 17 sans casser les Sprints 0 à 16.

Les opérations Git d’écriture sont désormais faites manuellement par le propriétaire du projet. Tu ne dois donc faire aucun `git add`, commit, push, ajout de remote ou changement de branche.

## 2. Mission
Implémenter la couche d’exploitation nécessaire pour rendre l’application réellement utilisable au quotidien dans la pharmacie pilote :

- sauvegarde SQL contrôlée ;
- vérification des sauvegardes ;
- restauration guidée ;
- stratégie de continuité locale ;
- import catalogue fournisseur CSV/XLSX selon capacités existantes ;
- import stock initial/lots si le modèle le permet proprement ;
- staging, mapping, preview, validation, confirmation ;
- défense contre données malformées et formula injection ;
- journal/audit des imports et sauvegardes ;
- intégration scanner USB HID ;
- abstraction imprimante ticket 80 mm ;
- impression après commit uniquement ;
- réimpression contrôlée ;
- TPE externe/manual confirmation ;
- diagnostic périphériques ;
- comportement dégradé sûr ;
- documentation d’exploitation ;
- tests unitaires, SQL réel, restauration et smokes.

## 3. Sources de vérité
Respecter en priorité :
1. ADR validés ;
2. Phase 2 — modèle fonctionnel ;
3. Phase 3 — modèle de données ;
4. Phase 4 — UX/UI ;
5. Phase 5 — Code First ;
6. code réel Sprints 0 à 16 ;
7. rapports Sprints 9 à 16 ;
8. règles matérielles et d’exploitation déjà validées ;
9. règles du présent prompt.

Ne pas inventer de comportement métier ou de contrainte fournisseur silencieusement.

## 4. État Git attendu
Branche : `main`.

Le dernier commit doit correspondre au Sprint 16, avec un message proche de :
`feat: add reporting dashboards alerts and secure exports`

Le hash exact dépend du commit manuel du propriétaire.

Le working tree doit être propre avant ajout du prompt Sprint 17, à l’exception autorisée :
`docs/prompts/Sprint_17_Backup_Import_Hardware.md`

Avant modification :
- `git status --short`
- `git log --oneline -n 8`
- confirmer branche `main`
- confirmer Sprint 16 en HEAD
- index vide
- aucun changement fonctionnel antérieur
- inspecter migrations, DbContext, services, UI, RBAC, audit, reporting et infrastructure matérielle existante.

Si seul le prompt Sprint 17 est non suivi, continuer.

## 5. Politique Git
NE PAS :
- git add
- git commit
- git push
- git remote add
- créer/changer de branche
- reset --hard
- stash destructif
- checkout/restauration destructive.

À la fin, uniquement rapporter l’état Git en lecture seule.

## 6. Règles déjà VALIDÉES

### 6.1 Environnement
- poste pilote Windows ;
- application local-first ;
- SQL Server 2025 Express prévu pour production/pilote ;
- SQL Server Developer utilisé en développement ;
- fonctions critiques disponibles hors Internet ;
- pas de synchronisation cloud V1.

### 6.2 Sauvegarde
Objectifs validés :
- RPO cible : 15 minutes ;
- RTO cible : 2 heures ;
- sauvegarde complète quotidienne ;
- différentielle toutes les 4 heures ;
- journal toutes les 15 minutes si l’édition/configuration SQL utilisée le permet réellement ;
- checksum/verify/hash ;
- restauration de test mensuelle ;
- copie locale + copie externe chiffrée.

IMPORTANT : vérifier les capacités réelles de SQL Server Express. Ne pas prétendre qu’un mécanisme est supporté s’il ne l’est pas. Documenter clairement toute limitation et proposer l’alternative locale la plus sûre compatible avec le produit.

### 6.3 Données dev/test
Uniquement données synthétiques en développement/test.
Les vraies données doivent être chargées plus tard via onboarding/import contrôlé.

### 6.4 Import fournisseur
Les fichiers fournisseur observés contiennent typiquement des colonnes comme :
- désignation ;
- PPV ;
- PPH ;
- quantité ou référence selon fichier.

Ils peuvent ne pas contenir :
- lot ;
- péremption ;
- code-barres.

Donc :
- ne rien inventer ;
- mapping configurable ;
- preview avant confirmation ;
- importer seulement les champs réellement présents ;
- distinguer données source et réglages locaux ;
- ne pas inventer marge ou règle tarifaire.

### 6.5 Import stock/lots
Autorisé seulement si les données nécessaires existent explicitement.
Pour les opérations affectant balances/lots/crédits/finance :
- import atomique ;
- validation complète avant commit ;
- aucun état partiel.

### 6.6 Scanner
- USB HID ;
- 2D compatible ;
- comportement clavier ;
- suffixe Enter attendu ;
- code inconnu ne crée jamais automatiquement un produit.

### 6.7 Imprimante
- ticket 80 mm ;
- vente commitée avant impression ;
- échec impression ne rollback jamais la vente ;
- réimpression contrôlée.

### 6.8 TPE
TPE externe.
Le logiciel enregistre seulement le mode carte/TPE et la confirmation manuelle.
Jamais de données bancaires sensibles.

### 6.9 UPS
Documenter utilisation/diagnostic, sans intégration spécifique à une marque requise dans ce Sprint.

## 7. Sauvegarde

### 7.1 Service
Créer abstraction Application + implémentation Infrastructure SQL :
- créer sauvegarde ;
- lister sauvegardes connues ;
- vérifier fichier ;
- calculer hash ;
- afficher date/taille/type ;
- préparer copie externe ;
- journaliser résultat.

### 7.2 Chemins
Ne pas hardcoder un chemin utilisateur.
Prévoir dossier local/externe configurables, validation chemin et erreurs lisibles.

### 7.3 Chiffrement externe
Ne pas inventer une crypto maison.
Ne jamais stocker de clé en clair dans repo.
Utiliser mécanisme Windows/.NET sûr si approprié.
Si chiffrement complet n’est pas réalisable proprement sans dépendance risquée, documenter comme gate explicite et livrer au minimum copie contrôlée + hash + vérification.

### 7.4 Vérification
Après backup :
- existence ;
- taille > 0 ;
- checksum SQL si disponible ;
- `RESTORE VERIFYONLY` ou équivalent ;
- hash ;
- statut clair.

### 7.5 Restauration
Action sensible :
- permission ;
- réauthentification récente ;
- confirmation explicite ;
- choix fichier ;
- validation fichier ;
- compatibilité ;
- sauvegarde de sécurité préalable si possible ;
- coordination connexions ;
- restauration ;
- vérification post-restore ;
- reconnexion propre.

Jamais d’écrasement silencieux.

### 7.6 Restore test
Automatiser restauration vers base temporaire isolée :
- restore ;
- vérification migrations ;
- tables critiques ;
- données de contrôle ;
- suppression base temp.
Ne jamais cibler destructivement `Pharmacy_Development`.

## 8. Import contrôlé

### 8.1 Formats
Support requis : CSV UTF-8.
XLSX seulement si capacité sûre déjà disponible ; sinon ne pas ajouter un package externe risqué sans nécessité.

### 8.2 Pipeline
1. choisir fichier ;
2. choisir feuille si XLSX ;
3. staging ;
4. mapping ;
5. normalisation ;
6. validation ;
7. preview ;
8. erreurs/avertissements ;
9. confirmation ;
10. persistance ;
11. audit/rapport.

### 8.3 Mapping
Permettre mapping explicite vers :
- référence ;
- désignation ;
- PPV ;
- PPH ;
- code-barres si présent ;
- catégorie si présente ;
- quantité si type d’import adapté ;
- lot/péremption uniquement si présents.

### 8.4 Validation
Valider :
- lignes vides ;
- doublons ;
- code-barres invalide ;
- montant invalide ;
- quantité négative ;
- date invalide ;
- champs obligatoires ;
- collisions catalogue ;
- taille ;
- encodage ;
- formula injection.

### 8.5 Modes
Séparer clairement :
- catalogue ;
- stock d’ouverture ;
- lots.
Ne pas mélanger implicitement catalogue + stock.

### 8.6 Idempotence
Même import confirmé + même OperationId/fingerprint => pas de doublons.

### 8.7 Rapport
Afficher lignes lues/créées/mises à jour/ignorées/rejetées + raisons, date, acteur et référence fichier minimisée.

## 9. Sécurité fichiers
- pas de path traversal ;
- validation extension/taille ;
- aucun lancement automatique ;
- aucune macro ;
- défense CSV Formula Injection ;
- ne pas faire confiance au nom de fichier ;
- streams sûrs ;
- logs minimisés.

## 10. Scanner USB HID
Créer abstraction testable.
Comportement :
- accumulation rapide ;
- Enter termine ;
- recherche exacte barcode ;
- inconnu => état inconnu, aucune création automatique ;
- saisie manuelle toujours possible ;
- anti-double scan raisonnable.

## 11. Imprimante ticket 80 mm
Créer interface `IReceiptPrinter` ou équivalent.
Ticket basé sur vente SQL persistée :
- référence ;
- date ;
- lignes ;
- quantités ;
- prix ;
- total ;
- paiements ;
- retour si pertinent ;
- infos pharmacie seulement si déjà configurées.

Workflow strict :
`COMMIT SQL -> impression`

Échec impression :
- vente reste finalisée ;
- erreur traçable ;
- réimpression ;
- aucune duplication vente.

Fournir fake/simulator si matériel absent.
Ne pas prétendre à compatibilité matérielle réelle avant test pilote.

## 12. TPE
Conserver abstraction simple :
- succès ;
- échec ;
- annulation ;
- confirmation manuelle ;
- aucune API bancaire réelle ;
- aucune donnée carte.

## 13. Diagnostic matériel
Créer page/section Support/Paramètres avec :
- SQL disponible ;
- chemin backup ;
- support externe ;
- test scanner ;
- imprimante configurée/simulateur ;
- TPE externe ;
- version application ;
- WebView2/runtime si pertinent.

Pas de télémétrie cloud.

## 14. RBAC et audit
Réutiliser/étendre proprement :
- backup.view
- backup.create
- backup.restore
- import.view
- import.execute
- devices.configure

Deny-by-default.

Auditer :
- backup ;
- vérification ;
- restore ;
- import confirmé ;
- configuration matériel ;
- réimpression si utile.

Jamais de secrets ou contenu complet des fichiers.

## 15. UX/UI
Respecter design system.
Pages possibles :
- Sauvegardes
- Imports
- Matériel & diagnostic

États :
loading, ready, in progress, success, warning, error, denied, SQL unavailable, external drive unavailable, invalid file, preview, validation errors.

Responsive :
- 1100x700
- 1366x768
- 1920x1080

## 16. Performance
Fichier fournisseur potentiellement > 8 000 références.
Donc :
- ne pas bloquer UI ;
- preview paginée/virtualisée ;
- batch contrôlé ;
- limites raisonnables ;
- progression si utile.

## 17. Architecture
Respecter Domain/Application/Infrastructure/Desktop/UnitTests/IntegrationTests.
Modules logiques :
- Backup/Recovery
- Import
- Devices/Printing
- Support/Diagnostics

## 18. SQL / EF
Migration seulement si nécessaire pour historique/config/print jobs.
Nom conseillé :
`BackupImportDeviceOperationsV01`

Documenter toute config locale qui ne doit pas être en SQL.

## 19. Tests automatisés

### Backup
- chemin valide/invalide ;
- création ;
- verify ;
- hash ;
- copie ;
- erreur disque ;
- permission ;
- restauration temp DB ;
- cleanup.

### Import
- CSV valide ;
- UTF-8 ;
- accents/arabe ;
- mauvais headers ;
- mapping ;
- doublons ;
- decimals invalides ;
- quantité négative ;
- dates invalides ;
- collision code-barres ;
- preview ;
- confirmation ;
- atomicité ;
- idempotence ;
- formula injection ;
- fichier synthétique de plusieurs milliers de lignes.

### Scanner
- barcode + Enter ;
- inconnu ;
- frappe manuelle ;
- double Enter.

### Printing
- impression après commit ;
- échec n’annule pas vente ;
- réimpression ;
- aucune double vente.

### TPE
- succès/échec/annulation manuels.

### RBAC
Backend + UI.

### SQL réel
Isolation SQL Server Developer ; aucune base temp résiduelle.

### Régression
Tous tests Sprints 0-16 PASS.

### Build
Debug/Release : 0 erreur, 0 warning.

## 20. Validation manuelle à préparer
Guide minimum :
1. backup manuel ;
2. verify ;
3. copie externe de test ;
4. restore vers base temporaire ;
5. erreur chemin ;
6. import CSV catalogue ;
7. preview ;
8. ligne invalide ;
9. collision barcode ;
10. confirmer ;
11. relancer même import ;
12. vérifier catalogue ;
13. import stock si supporté ;
14. scanner connu ;
15. scanner inconnu ;
16. ticket simulé/réel ;
17. panne imprimante ;
18. réimpression ;
19. TPE manuel ;
20. diagnostic ;
21. RBAC ;
22. panne SQL ;
23. responsive.

Pour matériel absent, distinguer :
- PASS simulateur/abstraction
- HARDWARE PILOT GATE PENDING

## 21. Documentation attendue
Créer/mettre à jour :
- `docs/reports/sprint-17/codex-report.md`
- `docs/reports/sprint-17/manual-validation.md`
- `docs/operations/BACKUP_RESTORE_V0.1.md`
- `docs/operations/DISASTER_RECOVERY_V0.1.md`
- `docs/import/IMPORT_PIPELINE_V0.1.md`
- `docs/import/SUPPLIER_CATALOG_MAPPING_V0.1.md`
- `docs/hardware/SCANNER_HID_V0.1.md`
- `docs/hardware/RECEIPT_PRINTER_80MM_V0.1.md`
- `docs/hardware/TPE_EXTERNAL_V0.1.md`
- `docs/hardware/PILOT_HARDWARE_CHECKLIST_V0.1.md`

## 22. Scripts
Préparer validation Sprint 17, restore-test, exemples CSV synthétiques.
Aucun vrai fichier `.bak` ne doit être commité.

## 23. Critères de sortie
PASS technique seulement si :
- Debug/Release propres ;
- tous tests PASS ;
- SQL réel PASS ;
- restore test PASS ;
- import PASS ;
- scanner/printer abstractions PASS ;
- aucun secret ;
- aucune base temp ;
- pending model changes none ;
- Desktop smoke avec/sans SQL PASS.

Le matériel réel peut rester :
`HARDWARE PILOT GATE PENDING`

## 24. Rapport final attendu
Succès sans matériel réel :
`IMPLEMENTED — BACKUP/IMPORT/HARDWARE VALIDATION PASS — MANUAL OPERATIONS REVIEW REQUIRED — HARDWARE PILOT GATE PENDING`

Succès matériel réel testé :
`IMPLEMENTED — BACKUP/IMPORT/HARDWARE VALIDATION PASS — MANUAL OPERATIONS REVIEW REQUIRED`

Blocage :
`BLOCKED — SPRINT 17 IMPLEMENTATION REQUIRES MANUAL DECISION`

Échec :
`FAILED — SPRINT 17 VALIDATION FAILED`

Le rapport doit inclure résumé, migration éventuelle, backup/restore, restore-test, import, formats supportés, scanner, printer, TPE, diagnostic, RBAC, tests Debug/Release, SQL réel, performance import, pending model changes, smokes, scan secrets/PII, Git status lecture seule, limitations, matériel restant et guide manuel.

FIN DU PROMPT SPRINT 17
