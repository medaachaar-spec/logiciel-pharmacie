# Sprint 11 — Validation Inventory

Date : 2026-08-04  
Statut automatisé : `PASS`  
Gate restant : revue manuelle Stock/POS/Diagnostic et validation SQL Server Express.

## Environnement

- SQL Server 17.0.1125.2 Developer, compatibilité 170 ;
- base contrôlée `Pharmacy_Development` ;
- trois migrations appliquées, aucune en attente ;
- migration Inventory : `20260804181233_InventoryPersistenceV01` ;
- six tables `inventory`.

## Données Demo

Le seed Inventory a été exécuté deux fois avec `sqlcmd -b -f 65001`. Le clear conservateur a ensuite ramené les quatre ensembles Demo (emplacements/lots/balances/mouvements) à zéro, puis le seed a été rejoué deux fois. Le second passage n’a rien dupliqué : un emplacement, cinq lots, douze balances positives et douze mouvements `Opening`. Un lot expiré reste visible et exclu de la disponibilité POS. Le produit manuel préexistant `demo-manual-001` a été conservé.

## Preuves automatisées

- build Debug : 0 avertissement, 0 erreur ;
- tests Domain/Application sur quantités, négatif, suivi lot/expiration, date, directions et validation avant store ;
- scénario SQL réel sur `Pharmacy_Sprint11_Test_<hex>` : schéma, idempotence, mouvements/balance atomiques, disponibilité POS, FEFO, expiration, concurrence, inventaire finalisé, double soumission, contrainte SQL et cleanup ;
- rollback complet/réapplication couvert par `SqlServerPersistenceIntegrationTests` ;
- seed exécuté deux fois et `verify_sprint11_inventory.sql` réussi ;
- aucun fallback synthétique enregistré dans le Desktop.

## Contrôles manuels demandés

1. tables Inventory dans SSMS ;
2. balances et mouvements Demo ;
3. liste Stock et recherche ;
4. détail produit/lots et expirés ;
5. ajustement hausse/baisse et conflit ;
6. création, comptage et finalisation d’un inventaire ;
7. POS avec disponibilité SQL, sans décrément après succès simulé ;
8. Diagnostic et comportement sans SQL ;
9. clavier, focus et responsive.

## Écart conservé

Le script Sprint 10 compte les références `DEMO-*` selon la collation SQL insensible à la casse. Une référence manuelle inactive `demo-manual-001`, extérieure au seed canonique, porte ce total à onze et fait échouer son assertion historique « exactement dix ». Elle n’a pas été supprimée : le clear Sprint 10 est trop large pour garantir sa conservation. Le seed/clear Sprint 11 utilisent une liste canonique exacte et ne touchent pas cette donnée manuelle.
