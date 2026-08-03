# POS V0.1 — Parcours nominal synthétique

Version : V0.1  
Date : 2026-08-03  
Statut : implémenté — revue manuelle requise

## Objectif

Le POS V0.1 remplace la page provisoire Ventes sur `/module/sales`. Il valide un premier parcours exécutable WPF/Blazor Hybrid : recherche locale, panier, paiement nominal et succès. Il s’agit d’un prototype de présentation, pas du modèle métier définitif.

La mention `Mode démonstration — aucune opération ne sera enregistrée` reste visible. L’état vit uniquement dans la session de l’application : aucun fichier, serveur, réseau, périphérique, base ou couche Infrastructure n’est sollicité.

## Architecture locale

```text
DemoPosPage
├── champ recherche / scan HID simulé
├── DemoCatalogPanel
├── DemoCartPanel
├── DemoPaymentPanel
└── DemoPosSuccess
        ↓
DemoPosSession (scoped, mémoire uniquement)
        ↓
DemoCatalog (liste immuable et fictive)
```

`DemoPosSession` gère catalogue, lignes, quantités, étape, paiement simulé, verrou de validation, résultat et réinitialisation. Les types portent le préfixe `Demo` pour empêcher toute confusion avec les futures entités Domain/Application.

## Catalogue synthétique unique

| Code | Article fictif | Présentation | Prix de vente de démonstration | Disponibilité synthétique |
|---|---|---|---:|---:|
| DEMO-000001 | Produit Démo A | Boîte 20 unités | 12,50 MAD | 8 |
| DEMO-000002 | Produit Démo B | Flacon 100 ml | 18,90 MAD | 6 |
| DEMO-000003 | Produit Démo C | Tube 50 g | 24,00 MAD | 10 |
| DEMO-000004 | Produit Démo D | Sachet 12 unités | 34,75 MAD | 5 |
| DEMO-000005 | Produit Démo E | Boîte 30 unités | 49,90 MAD | 4 |
| DEMO-000006 | Article Démo Hygiène A | Flacon 250 ml | 27,50 MAD | 7 |
| DEMO-000007 | Article Démo Parapharmacie A | Tube 75 ml | 39,90 MAD | 6 |
| DEMO-000008 | Produit Démo F | Boîte 10 unités | 15,25 MAD | 9 |
| DEMO-000009 | Article Démo Soin A | Flacon 50 ml | 55,00 MAD | 3 |
| DEMO-000010 | Article Démo Accessoire A | Unité | 8,75 MAD | 12 |

Les codes ne sont pas des EAN. Tous les noms, catégories, prix et plafonds sont déterministes et fictifs.

## Recherche et scan simulé

Le champ principal porte le libellé `Rechercher ou scanner un code` et reçoit le focus initial dans la mesure permise par le cycle de rendu WebView. La recherche filtre localement code, nom, présentation et catégorie sans tenir compte de la casse.

Un code exact suivi d’Entrée reproduit le comportement attendu d’un futur scanner USB HID. Un code inconnu n’ajoute rien et affiche : `Aucun article de démonstration trouvé. Aucun produit n’a été créé.`

## Panier et calculs

- Premier ajout : ligne à quantité 1.
- Ajout répété : fusion dans la ligne existante.
- Quantité : entier strictement positif, minimum 1.
- Plafond : disponibilité synthétique de l’article ; ce plafond ne constitue pas une validation de stock réel.
- Diminution depuis 1 : aucun passage implicite à 0 ; la suppression reste une action distincte.
- Vidage : confirmation intégrée dans la page, sans modale JavaScript.
- Sous-total : `quantité × prix unitaire`.
- Total : somme des sous-totaux.
- Type de tous les montants : `decimal`.
- Affichage : deux décimales et devise `MAD`.

Aucune remise, taxe, marge, PPV, PPH ou règle d’arrondi métier n’est interprétée.

## Paiements nominaux

### Espèces

Le montant reçu accepte un séparateur décimal virgule ou point. Un montant négatif/invalide est refusé. La confirmation reste désactivée si le montant reçu est inférieur au total. La monnaie est calculée exactement par `montant reçu − total`.

### Carte

La page indique `TPE externe — aucune donnée de carte n’est saisie dans le logiciel`. L’opérateur doit cocher `Paiement confirmé sur le TPE de démonstration` avant la validation. Aucun numéro, titulaire, date d’expiration, CVV ou reçu bancaire n’est saisi.

### Anti-double-validation

Le conteneur de session verrouille la complétion dès la première tentative acceptée. Le bouton passe aussi en loading et devient indisponible. La transition est locale et courte ; elle ne simule pas un délai réseau.

## Résultat et réinitialisation

Le succès génère en mémoire une référence `DEMO-SALE-0001`, puis incrémente ce compteur uniquement pendant la session. Total, mode de paiement, monnaie éventuelle, quantité et lignes sont affichés avec les rappels :

- `Aucune donnée enregistrée`
- `Aucun stock modifié`
- `Aucun ticket imprimé`

`Nouvelle vente` réinitialise panier, recherche, paiement, résultat, confirmation et messages. `Retour à l’accueil` termine aussi la session courante avant la navigation afin qu’une réouverture de Ventes commence proprement.

## Accessibilité et responsive

- Champs et groupes possèdent labels, descriptions et erreurs associées.
- Les contrôles fréquents sont des éléments HTML natifs ou des composants UI locaux fondés sur des boutons natifs.
- Les messages d’ajout, erreur, quantité et monnaie utilisent des régions live adaptées.
- Le focus vise successivement recherche, étape paiement et succès ; aucun piège clavier n’est introduit.
- Le focus visible, les états disabled/loading et reduced motion héritent des fondations UI.
- La grille utilise environ 62/38 à largeur suffisante ; sous 1024 px, le panier reste placé avant le catalogue et le paiement avant son résumé.
- Les contenus utilisent `minmax(0, 1fr)`, retour à la ligne et points de rupture 1024/768/576 px pour éviter un débordement horizontal.

## Limites V0.1

Ce sprint ne couvre pas persistance, stock réel, lots, FEFO, prix réglementaire, taxe, remise, client, crédit, paiement mixte, suspension/reprise, retour/annulation, caisse, impression, matériel, authentification ou audit. Les preuves automatisées ne remplacent pas une revue humaine aux résolutions 1100 × 700, 1366 × 768 et 1920 × 1080.
