# Sécurité locale, RBAC et audit V0.1

## Portée

Le Sprint 15 introduit une authentification strictement locale, un modèle multi-utilisateur et multi-rôle, une autorisation deny-by-default et un audit SQL distinct des logs techniques. Il n’introduit ni identité cloud, ni accès distant, ni télémétrie.

La pharmacie pilote peut utiliser le rôle système `OWNER`. L’usage partagé de ce compte empêche d’attribuer individuellement les actions aux deux exploitants : cette limite organisationnelle est assumée pour le pilote, tandis que le schéma accepte plusieurs comptes et plusieurs rôles.

## Règles

- Mot de passe : 15 à 128 caractères, haché par `PasswordHasher<TUser>` Identity V3 à 210 000 itérations ; les mots de passe courants explicitement connus sont refusés.
- Verrouillage compte : cinq échecs entraînent cinq minutes de verrouillage ; le message reste neutre.
- Session : conservée uniquement en mémoire, verrouillée après dix minutes sans activité ; les brouillons et suspensions persistantes ne sont pas supprimés.
- Réauthentification : fenêtre technique prudente de cinq minutes pour ajustement/finalisation de stock, prix, crédit, réception, retour/remboursement et administration de sécurité. Aucune durée métier n’étant définie dans les ADR, cette valeur est une décision technique V0.1.
- Récupération : huit codes aléatoires cryptographiques, affichés une fois, stockés uniquement sous SHA-256, valables un an et utilisables une seule fois.
- Autorisation : une session absente/verrouillée, une permission absente ou une réauthentification expirée produit un refus Application même si l’appel contourne l’interface.

## Permissions

Les 20 codes de `SecurityPermissions` sont la source applicative. La migration les insère comme données de référence et affecte tous les codes au rôle système `OWNER`. Aucun utilisateur ni secret n’est seedé.

## Audit

Les tables `audit.AuditEvents` et `audit.AuditEventDetails` sont séparées des événements d’authentification. L’API publique d’audit n’expose aucune suppression ou modification. Les événements de bootstrap et d’administration sont enregistrés dans la transaction de l’action ; les événements métier instrumentés sont ajoutés immédiatement après la transaction métier V0.1.

Chaque événement conserve au plus : type, module, ressource, identifiant, action, acteur, poste local haché, résultat, UTC, OperationId/corrélation et code de raison non sensible. Mots de passe, codes de récupération, données carte et notes client sont interdits.

Cette implémentation s’aligne de manière pragmatique sur OWASP ASVS 5 L2 ; elle ne constitue pas une certification.
