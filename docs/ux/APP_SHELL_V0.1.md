# App Shell V0.1

Date : 2026-08-03  
Statut : **provisoire — validation humaine du shell requise**

## 1. Statut V0.1

Le shell V0.1 est une fondation exécutable WPF + Blazor Hybrid. Il organise la navigation et présente des états entièrement synthétiques. Il ne constitue ni une interface commerciale définitivement approuvée, ni une fonctionnalité métier, ni une certification de sécurité.

## 2. Structure du shell

Le layout `AppShellLayout` assemble quatre zones stables :

1. sidebar à gauche ;
2. top bar contextuelle ;
3. contenu routé dans un élément `main` ;
4. status bar en bas.

Un lien d’évitement précède ces zones. Le shell consomme les tokens et composants de `UI Foundations V0.1` et ajoute uniquement `shell.css` et trois tokens de dimensions.

## 3. Navigation

`AppNavigationCatalog` est l’unique catalogue. Chaque entrée possède un identifiant stable, un libellé français, une route locale, une description courte, un groupe typé, une icône locale, un ordre, un état initial, un indicateur développement et un type produit/technique.

Les états d’affichage sont :

- `Available` : destination disponible ;
- `Soon` : destination descriptive disponible avec badge `Prévu` ;
- `Locked` : destination rendue comme bouton désactivé, donc non navigable.

## 4. Groupes

- Activité : Accueil, Ventes.
- Gestion : Catalogue, Stock, Achats et réceptions, Caisse, Clients et crédit.
- Pilotage : Rapports.
- Configuration : Administration.
- Développement : Diagnostic technique, UI Lab, uniquement dans le catalogue Debug.

## 5. Routes

| Route | Écran | État V0.1 |
|---|---|---|
| `/` | Accueil | disponible, route initiale |
| `/home` | Accueil | alias disponible |
| `/module/sales` | Ventes | page provisoire |
| `/module/catalog` | Catalogue | page provisoire |
| `/module/inventory` | Stock | page provisoire |
| `/module/purchasing` | Achats et réceptions | page provisoire |
| `/module/cash` | Caisse | page provisoire |
| `/module/customers` | Clients et crédit | page provisoire |
| `/module/reporting` | Rapports | page provisoire |
| `/module/administration` | Administration | page provisoire |
| `/diagnostic` | Diagnostic Sprint 1 | outil technique |
| `/ui-lab` | UI Lab Sprint 2 | outil technique |
| route inconnue | NotFound | état vide explicite et retour accueil |

Les routes techniques restent compilées en Release pour le diagnostic local, mais leurs entrées ne sont pas ajoutées au catalogue Release.

## 6. Comportement sidebar

La sidebar affiche un monogramme local, le nom du produit, les groupes et l’état actif. Le bouton de réduction publie `aria-expanded` et `aria-controls`. En mode compact, seuls les pictogrammes restent visibles ; les liens et boutons conservent un nom accessible et un tooltip natif.

Les entrées `Prévu` ouvrent le placeholder. Les entrées verrouillées sont des boutons `disabled`, affichent un cadenas et ne déclenchent aucune navigation. La colonne utilise son propre défilement vertical lorsque la hauteur est limitée.

## 7. Top bar

La top bar affiche le titre et la description de la route courante, `Mode local`, la session de démonstration et le bloc de compte limité :

- `Compte local de démonstration` ;
- `Authentification non implémentée`.

Aucun bouton d’alerte, verrouillage, compte ou authentification fictivement fonctionnel n’est présenté.

## 8. Status bar

La status bar résume : exécution locale, données de démonstration, base de données non connectée, configuration Debug/Release et état `Shell : prêt pour revue`. Les états différés ne sont pas traités comme des erreurs critiques.

## 9. Page d’accueil

L’accueil porte le titre `Accueil` et le sous-titre `Vue d’ensemble de démonstration`. Il contient :

- quatre métriques de présentation ;
- quatre accès directs vers les placeholders ;
- trois activités récentes génériques ;
- quatre états système locaux.

Aucun graphique, tendance, pourcentage, score ou calcul de performance n’est affiché.

## 10. Données synthétiques

Le message `Données entièrement synthétiques` est visible avant les métriques. Les valeurs centralisées sont `24 opérations`, `7 alertes`, `4 lots` et `3 actions`. Les références sont strictement génériques : `DEMO-VENTE-0001`, `DEMO-RECEPTION-0001` et `DEMO-INVENTAIRE-0001`.

Ces valeurs forment un modèle de présentation statique. Elles ne simulent aucun calcul métier et ne proviennent d’aucune source persistée.

## 11. Aperçu technique des accès

En Debug, trois profils locaux non persistants sont proposés :

- `Démo — accès complet` ;
- `Démo — opérations` ;
- `Démo — lecture`.

Le message `Profils de prévisualisation — ne représentent pas les rôles métier finaux` reste adjacent au sélecteur. L’aperçu modifie seulement la disponibilité visuelle des entrées. Il ne protège aucune route et ne remplace jamais les futures vérifications de permission dans Application.

## 12. Outils de développement

Diagnostic technique conserve le smoke screen Sprint 1 sur `/diagnostic`. UI Lab conserve les fondations visuelles Sprint 2 sur `/ui-lab`. Les deux entrées sont regroupées sous `Développement` en Debug et absentes du catalogue de navigation Release.

## 13. Responsive

- Cible utilisable technique : 1100 × 700.
- Cible minimale de revue : 1366 × 768.
- Cible confortable : 1920 × 1080.
- Grilles basées sur `auto-fit`, `minmax` et `min-width: 0`.
- Retour à la ligne de la top bar et des panneaux lorsque l’espace diminue.
- Défilement vertical naturel du contenu et de la sidebar.
- Aucune largeur de contenu susceptible d’imposer un débordement horizontal.

## 14. Accessibilité

- Navigation, zones `header`, `main`, `footer`, listes, titres et définitions sémantiques.
- Lien d’évitement vers le contenu principal.
- Focus global visible et déplacement du focus vers le `h1` après navigation.
- État actif renforcé par couleur, bordure et `aria-current`.
- Icônes décoratives `aria-hidden` ; contrôles iconiques nommés.
- Entrées verrouillées non activables au clavier ou à la souris.
- Contrôle de sidebar avec état développé/réduit annoncé.
- `prefers-reduced-motion` désactive la transition du shell.

## 15. Éléments différés

- Authentification, sessions réelles, rôles et permissions.
- Tout cas d’utilisation métier ou calcul de dashboard.
- Persistance, SQL Server, EF Core, migrations et données réelles.
- Connexion et diagnostic réels des périphériques.
- Sauvegarde, restauration, audit, import et export.
- Identité commerciale définitive, raccourcis finaux, langue principale et RTL.
- Tests automatisés de rendu par navigateur.

## 16. Critères de validation humaine

- Le shell est professionnel, sobre et cohérent avec UI Foundations V0.1.
- Accueil, sidebar, top bar et status bar sont lisibles aux deux résolutions cibles.
- Le lien actif est évident et le mode compact reste compréhensible.
- Toutes les pages provisoires s’ouvrent sans vide ni exception.
- Le parcours Tab est logique et le focus toujours visible.
- Les profils rendent les entrées attendues visibles ou verrouillées et l’avertissement est compris.
- Diagnostic et UI Lab sont accessibles en Debug.
- Aucun débordement horizontal, contenu coupé ou donnée réelle n’est observé.

Le shell ne sera pas présenté comme définitivement approuvé avant confirmation de ces points par l’utilisateur.
