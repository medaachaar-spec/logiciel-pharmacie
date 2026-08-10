# Paramètres d’alertes V0.1

## Stock bas

L’activation est globale dans `reporting.ReportSettings.LowStockAlertsEnabled`. Le seuil reste propre à chaque produit dans `catalog.ProductLocalSettings.MinimumStock`. Un produit sans seuil n’est jamais classé « sous seuil ». La rupture (`quantité vendable ≤ 0`) reste un constat descriptif indépendant.

## Péremption

`ExpiryAlertsEnabled` active le calcul et `ExpiryHorizonDays` définit la borne inclusive. Aucun horizon par défaut n’est inventé : la valeur seedée est `NULL` et l’activation est refusée tant qu’un entier positif ou nul n’a pas été explicitement saisi.

## Gouvernance et sécurité

- consultation : `reports.view` ;
- modification : `reports.manage_settings` et réauthentification récente ;
- concurrence : `rowversion`, avec rechargement obligatoire après conflit ;
- audit : action `alerts.settings.update`, module `reporting`, sans ancienne/nouvelle valeur sensible.

Les réglages ne modifient aucune vente, balance, lot, commande, écriture client ou caisse. Ils changent seulement la classification descriptive présentée.
