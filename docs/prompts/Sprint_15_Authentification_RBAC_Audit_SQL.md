# SPRINT 15 — Authentification locale, rôles, permissions et audit sécurisé

## 1. Rôle
Tu agis comme ingénieur logiciel senior, architecte .NET/SQL Server, spécialiste sécurité applicative, analyste métier, QA et intégrateur pour le projet **Logiciel Pharmacie**.

Tu dois implémenter le Sprint 15 dans le dépôt existant sans casser les Sprints 0 à 14.

## 2. Mission
Implémenter une couche de sécurité locale prête pour production couvrant :

- authentification locale ;
- utilisateurs ;
- rôles ;
- permissions granulaires ;
- deny-by-default ;
- compte partagé pilote ;
- architecture prête pour multi-utilisateurs futurs ;
- mots de passe hashés ;
- verrouillage après échecs ;
- verrouillage après inactivité ;
- réauthentification pour opérations sensibles ;
- changement de mot de passe ;
- récupération contrôlée ;
- codes de récupération ;
- audit applicatif sécurisé ;
- protection des pages et actions Desktop ;
- administration minimale des utilisateurs/permissions ;
- intégration SQL ;
- tests unitaires, d’intégration SQL réelle et tests d’architecture.

Aucun commit, push, ajout de remote ou changement de branche n’est autorisé.

## 3. Sources de vérité
Respecter en priorité :

1. ADR du projet ;
2. dossier Phase 2 — Modélisation fonctionnelle ;
3. dossier Phase 3 — Modèle de données ;
4. dossier Phase 4 — UX/UI ;
5. dossier Phase 5 — Code First / Vibe Coding ;
6. architecture réellement présente dans le dépôt ;
7. rapports Sprints 9 à 14 ;
8. règles de sécurité déjà validées ci-dessous.

Ne pas inventer de règle métier ni affaiblir un contrôle de sécurité pour simplifier l’implémentation.

## 4. État attendu du dépôt
Branche : `main`

HEAD attendu :
`4054e5e3fd1b7d62c8e532ad6d587cf610d80338`

Message :
`feat: add customers credit cash returns and persistent suspensions`

Working tree propre.
Index vide.
Aucun remote.

Avant toute modification :
- `git status`
- `git log --oneline -n 8`
- vérifier HEAD ;
- vérifier index vide ;
- vérifier aucun fichier non suivi inattendu ;
- inspecter architecture existante ;
- inspecter les patterns Result/Error, Money, OperationId, rowversion, EF, transactions, Serilog, Desktop shell, navigation et pages.

Le seul fichier prompt Sprint 15 pourra être considéré comme exception autorisée s’il apparaît non suivi dans `docs/prompts/`.

## 5. Prérequis techniques
Vérifier :

- .NET 10 ;
- SQL Server Developer local ;
- EF Core SQL Server ;
- aucune migration en attente avant Sprint 15 ;
- builds Debug/Release ;
- tests existants ;
- variables de connexion existantes ;
- stratégie actuelle de logging/audit ;
- absence de secret dans le dépôt ;
- Desktop fonctionnel avec SQL.

## 6. Règles de sécurité VALIDÉES

### 6.1 Authentification locale
L’application utilise une authentification locale.

Pas d’Azure AD, OAuth, cloud identity ou service externe dans ce sprint.

### 6.2 Compte pilote
La pharmacie pilote utilise un **compte partagé propriétaire**.

Limitation connue :
- pas d’attribution individuelle réelle des actions entre les deux exploitants du pilote ;
- cette limitation doit être documentée ;
- l’architecture doit malgré tout être multi-utilisateur/multi-rôle pour le futur.

### 6.3 Mot de passe
Règles validées :
- longueur minimale : 15 caractères ;
- longueur maximale : 128 caractères ;
- ne pas imposer de règles arbitraires de composition si elles ne sont pas nécessaires ;
- utiliser `PasswordHasher<TUser>` ou mécanisme .NET équivalent éprouvé ;
- ne jamais stocker de mot de passe en clair ;
- ne jamais logger le mot de passe.

### 6.4 Verrouillage
Après 5 échecs de connexion :
- verrouiller le compte pendant 5 minutes ;
- éviter l’énumération d’utilisateurs ;
- message utilisateur neutre.

### 6.5 Inactivité
Après 10 minutes d’inactivité :
- verrouiller la session applicative ;
- revenir à un écran de déverrouillage/authentification ;
- ne pas tuer brutalement les processus métier ;
- les brouillons/suspensions doivent rester cohérents.

### 6.6 Réauthentification
Exiger une réauthentification récente pour les opérations sensibles.

Exemples :
- ajustement de stock ;
- finalisation inventaire ;
- changement de prix ;
- autorisation/désautorisation crédit ;
- retour/remboursement ;
- gestion utilisateurs/rôles ;
- récupération/changement de mot de passe sensible ;
- restauration/sauvegarde sensible si l’architecture y fait déjà référence.

Prévoir une fenêtre de réauthentification courte et documentée ; ne pas inventer une durée si une valeur existe dans les ADR. Si aucune valeur n’est définie, choisir une valeur technique prudente et la documenter comme décision technique, pas règle métier.

### 6.7 Récupération
Prévoir des codes de récupération locaux.

Exigences :
- générés de manière cryptographiquement sûre ;
- stockés hashés ou protégés ;
- utilisables une seule fois ;
- jamais loggés ;
- affichage initial contrôlé ;
- protection locale via DPAPI si nécessaire pour certains secrets machine.

### 6.8 RBAC
Deny-by-default.

Un utilisateur ne peut effectuer une action que si une permission explicite l’autorise.

### 6.9 Audit
L’audit doit :
- être distinct des logs techniques ;
- être append-only ;
- être écrit dans la même transaction que l’action critique quand pertinent ;
- éviter secrets et données personnelles inutiles ;
- tracer acteur, action, ressource, identifiant métier/technique utile, résultat, date UTC et corrélation ;
- ne pas enregistrer mots de passe, codes de récupération, données carte ou contenu sensible inutile.

### 6.10 OWASP
S’aligner sur OWASP ASVS 5 niveau L2 de manière adaptée à cette application locale, sans prétendre à une certification.

## 7. Périmètre fonctionnel autorisé

### 7.1 Utilisateurs
Créer :
- identifiant ;
- login normalisé ;
- nom d’affichage ;
- statut actif/inactif ;
- hash mot de passe ;
- compte verrouillé jusqu’à ;
- compteur d’échecs ;
- timestamps ;
- rowversion ;
- marqueurs nécessaires pour changement/récupération.

### 7.2 Rôles
Créer :
- rôles ;
- permissions ;
- relations UserRoles ;
- RolePermissions ;
- contraintes d’unicité ;
- seed minimal contrôlé.

### 7.3 Compte propriétaire pilote
Préparer un rôle de type `Owner` ou équivalent cohérent avec le dépôt.

Il doit posséder les permissions nécessaires à la pharmacie pilote.

Ne pas hardcoder un mot de passe dans :
- code ;
- migration ;
- seed ;
- script SQL ;
- documentation ;
- tests non isolés.

La création du premier compte doit passer par un bootstrap contrôlé.

### 7.4 Permissions minimales
Prévoir au minimum des permissions cohérentes du type :

- `sales.create`
- `sales.view_history`
- `sales.return`
- `sales.refund`
- `inventory.view`
- `inventory.adjust`
- `inventory.finalize_count`
- `purchasing.view`
- `purchasing.receive`
- `catalog.view`
- `catalog.manage`
- `catalog.change_price`
- `customers.view`
- `customers.manage`
- `customers.manage_credit`
- `cash.view`
- `security.manage_users`
- `security.manage_roles`
- `audit.view`
- `reports.view`

Adapter les noms aux conventions existantes ; ne pas créer de doublons sémantiques.

### 7.5 Administration
Créer une UI minimale pour :
- lister utilisateurs ;
- créer utilisateur ;
- activer/désactiver ;
- affecter rôles ;
- réinitialisation contrôlée ;
- consulter rôles/permissions ;
- ne pas permettre de supprimer physiquement les comptes ayant un historique.

### 7.6 Authentification Desktop
Ajouter :
- écran Login ;
- état authentifié ;
- logout ;
- verrouillage après inactivité ;
- déverrouillage ;
- affichage utilisateur/rôle dans shell si cohérent ;
- protection navigation/pages/actions.

### 7.7 Autorisation UI + backend
Ne pas se contenter de cacher des boutons.

Chaque action critique doit être protégée côté Application/Infrastructure/service avec vérification permission.

L’UI peut masquer/désactiver, mais le backend doit refuser aussi.

### 7.8 Audit
Auditer au minimum :
- login succès/échec sans exposer secret ;
- logout ;
- verrouillage/déverrouillage ;
- changement de mot de passe ;
- utilisateur créé/désactivé ;
- rôle affecté/retiré ;
- permission sensible modifiée ;
- ajustement stock ;
- inventaire finalisé ;
- changement de prix ;
- réception ;
- vente finalisée ;
- crédit autorisé/désautorisé ;
- règlement crédit ;
- retour/remboursement.

Réutiliser les mécanismes transactionnels existants lorsque possible.

## 8. Hors périmètre explicite
Ne pas implémenter :

- MFA cloud ;
- SSO ;
- Azure AD ;
- OAuth ;
- authentification biométrique ;
- identité mobile ;
- sync cloud ;
- accès distant ;
- stockage de secrets dans repo ;
- collecte télémétrique cloud ;
- dossier clinique ;
- chiffrement complet de toute la base si non validé ;
- certification officielle OWASP ;
- politique de rotation périodique arbitraire du mot de passe ;
- questions secrètes.

## 9. Architecture attendue
Respecter :

- Domain sans dépendance infra ;
- Application -> Domain ;
- Infrastructure -> Application + Domain ;
- Desktop -> Application + Infrastructure.

Créer/étendre modules :
- Security
- Administration
- Audit

Favoriser :
- interfaces Application ;
- implémentations Infrastructure ;
- services d’autorisation centralisés ;
- contexte utilisateur abstrait ;
- horloge injectable ;
- générateur de codes injectable ;
- tests déterministes.

## 10. SQL Server / EF Core

### 10.1 Migration
Créer une migration additive Sprint 15.

Nom conseillé :
`SecurityRbacAuditPersistenceV01`

### 10.2 Schémas
Réutiliser les schémas Phase 3 si présents.

Sinon structure cohérente :
- `security`
- `audit`
- `administration` si nécessaire

### 10.3 Contraintes
Prévoir :
- PK/FK explicites ;
- unique index login normalisé ;
- unique permissions ;
- unique rôle ;
- contraintes sur statuts ;
- rowversion sur entités mutables ;
- append-only audit ;
- restrict/no action sur historiques ;
- UTC pour timestamps.

### 10.4 Bootstrap
Prévoir une procédure sûre pour le premier compte :
- jamais de mot de passe dans migration ;
- bootstrap seulement si aucun utilisateur ;
- mot de passe fourni interactif/config externe contrôlée ;
- documentation claire.

## 11. Logging et protection des données
Respecter le système ILogger/Serilog existant.

Interdictions logs :
- mot de passe ;
- récupération ;
- token secret ;
- carte bancaire ;
- notes client inutiles ;
- PII non nécessaire.

Audit ≠ log technique.

Conserver corrélation sans exposer secrets.

## 12. UX/UI
Respecter le design system existant.

Écrans attendus :
- Login ;
- verrouillage/déverrouillage ;
- Profil/session minimal ;
- Administration utilisateurs ;
- rôles/permissions ;
- audit viewer read-only.

États :
- loading ;
- invalid credentials ;
- locked ;
- disabled ;
- expired reauth ;
- denied ;
- SQL unavailable ;
- concurrency conflict ;
- success.

Responsive :
- 1100×700
- 1366×768
- 1920×1080

Clavier :
- tab order ;
- Enter login ;
- focus visible ;
- aucune information sensible persistée dans l’UI après logout.

## 13. Plan d’exécution
1. Audit dépôt.
2. Lire ADR/docs sécurité.
3. Concevoir modèle Domain/Application.
4. Concevoir schéma Security/Audit.
5. EF entities/configurations.
6. Migration.
7. Stores SQL.
8. Password hashing.
9. Login/lockout.
10. CurrentUser/session context.
11. Inactivity lock.
12. RBAC.
13. Reauth.
14. Recovery codes.
15. Audit persistence.
16. Instrumenter workflows critiques.
17. UI Login/Admin/Audit.
18. Tests unitaires.
19. Tests intégration SQL.
20. Tests architecture.
21. Scripts validation/seed/bootstrap.
22. Builds Debug/Release.
23. Smoke Desktop avec SQL.
24. Smoke sans SQL.
25. Vérifier pending migrations.
26. Vérifier secrets/PII.
27. Documentation/rapport.

## 14. Tests et critères d’acceptation

### Auth
Tester :
- login valide ;
- mot de passe invalide ;
- utilisateur inconnu même message neutre ;
- 5 échecs -> lock 5 min ;
- login pendant lock refusé ;
- login après expiration lock ;
- utilisateur désactivé ;
- mot de passe >=15 ;
- longueur >128 refusée ;
- hash jamais égal au mot de passe.

### Session
Tester :
- logout ;
- inactivité 10 min -> verrouillage ;
- déverrouillage ;
- état métier non corrompu.

### RBAC
Tester :
- permission présente -> autorisé ;
- absente -> refus ;
- deny-by-default ;
- UI cachée/désactivée ;
- backend refuse malgré appel direct.

### Reauth
Tester :
- opération sensible sans reauth récente -> refus/challenge ;
- après reauth -> autorisé ;
- mauvais mot de passe -> refus.

### Recovery
Tester :
- génération ;
- usage unique ;
- second usage refusé ;
- pas de code en clair en DB après stockage ;
- aucun log.

### Audit
Tester :
- action critique produit audit ;
- échec important produit audit adapté ;
- audit transactionnel ;
- append-only ;
- aucun secret/PII interdite.

### Régression
Tous tests Sprints 0–14 restent PASS.

### SQL réel
Tests réels obligatoires sur SQL Server Developer avec isolation/base temporaire selon pattern repo.

### Build
Debug/Release :
- 0 erreur ;
- 0 warning.

## 15. Validation manuelle à préparer
Créer guide couvrant :
1. bootstrap propriétaire ;
2. login correct ;
3. mauvais password x5 ;
4. lockout ;
5. déverrouillage après période contrôlée/test ;
6. logout/login ;
7. changement mot de passe ;
8. création second utilisateur test ;
9. rôle limité ;
10. accès refusé Stock adjustment ;
11. accès autorisé Ventes ;
12. changement rôle ;
13. reauth opération sensible ;
14. inactivité ;
15. recovery code ;
16. audit viewer ;
17. SQL outage ;
18. redémarrage app ;
19. vérification SSMS ;
20. vérification aucune donnée sensible.

## 16. Documentation attendue
Créer/mettre à jour :
- `docs/reports/sprint-15/codex-report.md`
- `docs/reports/sprint-15/manual-validation.md`
- documentation schéma security/audit ;
- documentation bootstrap premier compte ;
- documentation RBAC ;
- documentation lockout/inactivity/reauth ;
- documentation recovery ;
- documentation audit.

## 17. Git — interdictions strictes
NE PAS :
- git add ;
- commit ;
- push ;
- remote ;
- branch ;
- reset --hard ;
- stash destructif ;
- checkout destructif.

À la fin :
- `git status --short`
- HEAD
- index vide
- aucun remote
- aucun commit/push.

## 18. Rapport final attendu
Succès :
`IMPLEMENTED — SQL SECURITY/RBAC/AUDIT VALIDATION PASS — MANUAL SECURITY REVIEW REQUIRED`

Blocage :
`BLOCKED — SPRINT 15 IMPLEMENTATION REQUIRES MANUAL DECISION`

Échec :
`FAILED — SPRINT 15 VALIDATION FAILED`

Le rapport doit inclure :
- résumé ;
- migration ;
- tables ;
- bootstrap ;
- RBAC ;
- login/lockout/inactivity ;
- reauth ;
- recovery ;
- audit ;
- tests Debug/Release ;
- SQL réel ;
- pending migrations ;
- smoke Desktop ;
- secrets/PII scan ;
- Git status ;
- limites connues ;
- guide manuel.

FIN DU PROMPT SPRINT 15
