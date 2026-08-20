# Identité configurable de la pharmacie V0.1

## Périmètre

L’identité affichée par Pharmacy Desktop est constituée du nom de la pharmacie, de sa couleur principale et de son logo. Elle est lisible avant authentification pour construire l’écran de connexion, mais sa modification exige la permission backend `security.manage_users`.

## Valeurs initiales

- nom : `Pharmacie Diar Douha` ;
- couleur : `#0D9A48` ;
- logo : `wwwroot/branding/pharmacie-diar-douha-logo.png`.

Ces valeurs sont centralisées. Le logo livré sert de repli tant qu’aucun remplacement n’a été enregistré.

## Persistance

`configuration.PharmacyBranding` est un singleton SQL (`Id = 1`). Il contient le nom, la couleur, le contenu éventuel du logo personnalisé, son MIME, son nom de fichier, `UpdatedAtUtc` et un `rowversion`.

Le logo personnalisé est limité à 2 Mo. Les seuls formats acceptés sont PNG, JPEG et WEBP, après contrôle cohérent de l’extension, du MIME et de la signature binaire. SVG utilisateur est refusé.

## Chargement et fallback

Le `BrandingCoordinator` charge le singleton avant le workflow d’authentification. Une lecture réussie remplace le snapshot courant. Avant chaque nouvelle lecture, le coordinateur revient aux valeurs initiales ; une panne ne peut donc pas laisser croire qu’un ancien snapshot est frais. Le Shell continue de fonctionner avec le fallback et un message contrôlé.

## Thème

La couleur validée alimente les variables :

- `--pharmacy-primary` ;
- `--pharmacy-primary-dark` ;
- `--pharmacy-primary-active` ;
- `--pharmacy-primary-soft` ;
- `--pharmacy-primary-border` ;
- `--pharmacy-on-primary` ;
- `--pharmacy-focus`.

Les variantes sont dérivées de façon déterministe. Le texte sur la couleur principale choisit la meilleure variante sombre ou claire, et le focus conserve au moins un contraste non textuel de 3:1 sur blanc. Les tokens fonctionnels danger, warning, success et information ne sont pas remappés.

## Concurrence, audit et sauvegarde

Une mise à jour fournit le `rowversion` lu ; une écriture concurrente est refusée sans last-write-wins. L’audit enregistre l’acteur, l’UTC, l’opération et les champs nom/couleur/logo modifiés. Pour le logo, seule la métadonnée MIME/taille est auditée.

Le contenu personnalisé étant dans SQL, les sauvegardes officielles l’incluent. Le restore-test vérifie la table critique et retourne le nom, la couleur et le SHA-256 du logo restauré avant de supprimer sa base temporaire.
