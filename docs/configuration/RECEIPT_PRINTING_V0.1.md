# Impression des tickets V0.1

L’impression des tickets est une option globale de la pharmacie. Elle est stockée dans le singleton SQL `configuration.ApplicationSettings` et vaut `false` sur une base neuve.

## Comportement

- Désactivée : la finalisation persiste vente, paiement, caisse et stock, puis s’arrête sur « Vente enregistrée ». Aucun service d’impression, tentative, statut ou prérequis imprimante n’intervient.
- Activée : le workflow post-commit existant reste disponible. Une panne d’impression ne rollback jamais la vente et la réimpression réutilise le snapshot persistant.

Le réglage se modifie dans Administration, section « Matériel et paramètres ». La permission `devices.configure` est requise ; le rôle Seller ne la possède pas.

## Exploitation

Le diagnostic indique explicitement si l’impression est désactivée. L’imprimante 80 mm n’est un prérequis matériel que lorsque l’option est activée. Les sauvegardes SQL incluent ce réglage et le restore-test isolé le vérifie.
