# Dashboard opérationnel V0.1

## Objectif

La page Accueil fournit une composition unique qui s'adapte aux permissions effectives de la session. Elle ne distingue pas les rôles par une vue codée en dur : chaque projection sensible est autorisée côté backend, puis chaque bloc est rendu uniquement si la permission correspondante est effective.

## Composition

### Expérience globale

L'accès à `reports.view` affiche les ventes du jour, le chiffre d'affaires du jour, la tendance quotidienne sur 7 ou 30 jours et les produits les plus vendus. Les agrégations sont produites en SQL et suivent la convention Reporting.

`customers.manage_credit` ajoute le crédit client ouvert. `inventory.view` ajoute le nombre de produits en stock faible et les alertes de péremption selon l'horizon configuré. `security.view_employee_activity` ajoute la comparaison des ventes par employé, Managers vendeurs inclus.

### Expérience personnelle

L'accès à `sales.view_history`, sans accès aux ventes globales, affiche uniquement les ventes du jour et les ventes récentes de l'utilisateur connecté. `sales.create` contrôle la présence du bouton principal **Nouvelle vente**. La session courante est présentée comme une information de session et jamais comme une mesure RH.

## Projections et sécurité

Le service Dashboard expose trois lectures granulaires : ventes globales, crédit ouvert et alertes d'inventaire. Chaque méthode impose sa permission côté Infrastructure avant d'accéder à SQL. Les ventes personnelles et la comparaison entre employés réutilisent la couche EMP-02 et ses règles d'isolation.

Une permission absente empêche à la fois l'appel et le rendu. Un override utilisateur recompose donc la page sans dépendre du rôle nominal. Une panne d'une projection conserve les autres sections valides, efface les anciennes valeurs et présente un message court au niveau de la section concernée.

## Données et performance

- Ventes et chiffre d'affaires : agrégation directe de `sales.Sales`, sans jointure multiplicative avec les lignes, paiements ou allocations.
- Tendance : groupement SQL quotidien, complété par des zéros côté projection pour les jours sans vente.
- Top produits : agrégation de `reporting.vw_ProductSales`, limitée côté SQL.
- Crédit : somme issue de `reporting.vw_CustomerCredit`.
- Stock faible : `reporting.vw_StockSummary` et réglage d'alerte existant.
- Péremption : `reporting.vw_ExpiryAlerts` et horizon `ReportSettings`.
- Employés : synthèses EMP-02, sans requête par employé.

Toutes les lectures sont sans mutation et aucun calcul ne charge l'historique complet en mémoire.

## Graphiques et accessibilité

La tendance est un SVG responsive avec titre, description, labels et tableau textuel accessible. Les produits et employés utilisent des barres horizontales CSS adaptées aux libellés longs, accompagnées de valeurs textuelles. Les alertes de péremption indiquent explicitement leur état ; la couleur n'est jamais l'unique vecteur d'information.

Les styles utilisent les tokens BRAND-01, notamment `--color-accent`, ainsi que les couleurs sémantiques existantes. Aucune couleur client fixe et aucune dépendance graphique supplémentaire ne sont introduites.

## États et responsive

Un squelette unifié couvre le chargement initial. Les états vides utilisent un vocabulaire métier simple. Le bouton **Actualiser** déclenche un rechargement explicite et est protégé contre les doubles chargements.

La grille se replie aux seuils existants pour 1100×700, 1366×768 et 1920×1080. Les cartes se recomposent, les graphiques occupent la largeur disponible, les noms longs sont tronqués visuellement avec leur valeur accessible complète et aucun conteneur n'impose de largeur globale fixe.
