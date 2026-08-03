# UI Foundations V0.1

Date : 2026-08-03  
Statut : **provisoire — validation visuelle humaine requise**

Ce document décrit la première fondation visuelle code-first de Logiciel Pharmacie. Il ne constitue pas une identité graphique commerciale définitivement approuvée et ne valide aucun écran métier.

## 1. Principes visuels

- Interface claire, professionnelle, dense sans être compacte au détriment de la lecture.
- Fond général froid très clair et surfaces blanches.
- Texte principal ardoise bleu-noir, texte secondaire moins dominant mais contrasté.
- Teal sombre pour l’action principale et la navigation active.
- Vert réservé au succès, ambre à l’avertissement, rouge à l’erreur ou au danger, bleu à l’information.
- Bordures discrètes, ombres légères et rayons modérés.
- Une information importante combine texte, symbole et couleur ; la couleur ne porte jamais seule le sens.
- Aucun gradient décoratif, glassmorphism, effet marketing ou dépendance visuelle externe.

## 2. Palette fonctionnelle

Toutes les valeurs sont définies dans `wwwroot/css/tokens.css`.

| Fonction | Token principal | Valeur V0.1 | Usage |
|---|---|---:|---|
| Accent | `--color-accent` | `#0f5f5a` | Action principale, navigation active |
| Accent hover | `--color-accent-hover` | `#0a4c48` | Survol action principale |
| Accent subtil | `--color-accent-subtle` | `#e5f4f1` | Fond de sélection léger |
| Texte principal | `--color-text-primary` | `#10242e` | Titres et contenu prioritaire |
| Texte secondaire | `--color-text-secondary` | `#52636c` | Aide et métadonnées |
| Fond application | `--color-app-background` | `#eef3f5` | Arrière-plan général |
| Surface | `--color-surface` | `#ffffff` | Cartes et contrôles |
| Succès | `--color-success` | `#176c47` | Confirmation et état valide |
| Information | `--color-information` | `#155d96` | Information neutre |
| Avertissement | `--color-warning` | `#7a4b00` | Vérification requise |
| Danger | `--color-danger` | `#a8212d` | Erreur ou action destructive |

Chaque couleur sémantique possède une surface et une bordure dédiées. Les variantes ne doivent pas être recréées localement dans les composants.

## 3. Typographie

Pile système : `Segoe UI`, puis `Inter` si déjà installée localement, `Arial`, `sans-serif`. Aucune police n’est téléchargée.

Niveaux disponibles :

- titre de page : token `--font-size-page-title`, graisse forte, interligne resserré ;
- titre de section : `--font-size-section-title` ;
- titre de carte : `--font-size-card-title` ;
- texte courant : `--font-size-body`, interligne `--line-height-body` ;
- libellé et aide : `--font-size-small` ;
- métadonnée : `--font-size-metadata`.

Le texte courant reste à 1 rem. Les tailles inférieures sont réservées aux aides ou métadonnées, jamais au contenu principal.

## 4. Espacements

Échelle unique :

| Token | Valeur |
|---|---:|
| `--space-1` | 4 px |
| `--space-2` | 8 px |
| `--space-3` | 12 px |
| `--space-4` | 16 px |
| `--space-6` | 24 px |
| `--space-8` | 32 px |
| `--space-12` | 48 px |

Les composants combinent ces valeurs sans créer d’échelle parallèle.

## 5. Rayons, bordures et ombres

- Rayons : petit 6 px, moyen 10 px, grand 14 px, pill uniquement pour badges et indicateurs.
- Bordure standard : 1 px, avec variantes neutre et renforcée.
- Ombre : une seule ombre légère `--shadow-small` pour les surfaces élevées.
- Focus : anneau ambre de 3 px avec décalage visible sur fond clair et sombre.
- Contrôles : hauteurs 36 px, 42 px et 48 px ; les actions principales utilisent au moins 42 px.

## 6. Composants disponibles

| Composant | Responsabilité | Variantes principales |
|---|---|---|
| `UiButton` | Action générique et blocage de double activation | Primary, Secondary, Tertiary, Danger ; Small, Medium, Large |
| `UiBadge` | Statut court avec symbole et texte | Neutral, Information, Success, Warning, Danger |
| `UiCard` | Surface sémantique avec header/footer facultatifs | Comfortable, Compact |
| `UiAlert` | Message contextuel annoncé par ARIA | Information, Success, Warning, Danger |
| `UiTextField` | Saisie liée avec label, aide et erreur | text, search, password, number |
| `UiSelect` | Choix générique lié et accessible | normal, required, disabled, error |
| `UiSpinner` | Progression locale annoncée | Small, Medium |
| `UiEmptyState` | Absence de contenu avec action facultative | Illustration CSS locale |

Les APIs restent volontairement courtes. Toute nouvelle variante devra répondre à un cas réel documenté.

## 7. États obligatoires

- Default : état de base immédiatement compréhensible.
- Hover : retour discret sans déplacement du contenu.
- Focus : anneau visible indépendant de la couleur du composant.
- Active : renforcement de la surface ou de la bordure.
- Disabled : contrôle indisponible, lisible et non cliquable.
- Loading : texte accessible, indicateur visuel et bouton désactivé.
- Error : texte proche du contrôle, préfixe explicite et `aria-invalid=true`.
- Success : texte et symbole, pas seulement une couleur verte.

`UiButton` utilise un verrou interne en plus des paramètres `Disabled` et `IsLoading` afin de prévenir une double activation pendant l’exécution du callback.

## 8. Règles clavier et accessibilité

- Utiliser des éléments natifs `button`, `input`, `select`, `nav`, `main`, `label` et une hiérarchie de titres ordonnée.
- Tous les contrôles interactifs sont atteignables avec Tab dans l’ordre du document.
- Les liens de navigation possèdent un état actif visible.
- Un lien d’évitement permet d’atteindre directement le contenu principal.
- Les changements de route déplacent le focus vers le titre principal.
- Les labels utilisent `for` avec un identifiant explicite et unique.
- Les aides et erreurs sont reliées par `aria-describedby`.
- Les alertes utilisent `status` ou `alert` selon leur urgence.
- Les zones uniquement iconiques disposent d’un nom accessible ou sont masquées aux technologies d’assistance.
- `prefers-reduced-motion: reduce` réduit animations et transitions au minimum.

## 9. Règles responsive

- Largeur de contenu maximale centralisée dans `--content-max-width`.
- Gouttières fluides sans largeur fixe causant un débordement horizontal.
- Grilles fondées sur `auto-fit`, `minmax` et `min(100%, …)`.
- À largeur confortable, les cartes utilisent deux ou trois colonnes selon l’espace disponible.
- Sous environ 900 px, les informations d’environnement passent à deux colonnes.
- Sous environ 700 px, les en-têtes, grilles principales et actions passent à une colonne.
- Les textes longs utilisent le retour à la ligne et les éléments flex/grid ont `min-width: 0`.
- Cibles de revue : 1366 × 768 au minimum et 1920 × 1080 en confort.

## 10. Ressources locales

- HTML, Razor et cinq fichiers CSS locaux uniquement.
- Police système uniquement.
- Illustration de l’état vide produite en CSS.
- Aucun CDN, URL HTTP/HTTPS, JavaScript tiers, image Internet ou bibliothèque d’icônes.
- Le seul script chargé reste `_framework/blazor.webview.js`, fourni localement par Blazor Hybrid.

## 11. Navigation de développement

La barre interne expose uniquement :

- `Diagnostic technique` sur `/` ;
- `UI Lab` sur `/ui-lab`.

Elle n’est pas le shell métier final et ne contient aucun module fonctionnel. Le routeur Blazor fonctionne dans WebView2 sans serveur HTTP.

## 12. Éléments différés

- Identité graphique commerciale et logo définitifs.
- Mode sombre.
- Internationalisation, langue principale et éventuel RTL.
- Tables de données, dialogues, notifications globales et composants spécialisés.
- Shell métier, navigation par permissions et raccourcis finaux.
- Tests automatisés de rendu par navigateur ou UI Automation dédiés.
- Validation terrain avec le poste et la mise à l’échelle Windows du pilote.

## 13. Critères de validation humaine

- Rendu professionnel, sobre et cohérent.
- Lecture confortable à 1366 × 768 et 1920 × 1080.
- Aucun contenu coupé ni débordement horizontal.
- Parcours Tab logique et focus toujours visible.
- Hover, active, disabled, loading, error et success distincts.
- Double clic sur la simulation loading sans seconde exécution.
- Labels, aides et erreurs compréhensibles.
- Réduction des animations respectée lorsque Windows la demande.
- Diagnostic Sprint 1 et UI Lab accessibles depuis la barre technique.
- Aucune ressource distante ni donnée réelle observée.

Le passage de ces critères doit être confirmé par l’utilisateur à partir de l’application réelle et de captures d’écran.
