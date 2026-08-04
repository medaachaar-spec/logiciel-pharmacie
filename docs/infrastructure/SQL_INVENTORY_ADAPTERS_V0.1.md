# SQL Inventory Adapters V0.1

Date : 2026-08-04  
Statut : `IMPLEMENTED — REAL SQL SERVER TEST PASS`

## Composition

`AddSqlInventoryAdapters` enregistre une horloge système, `SqlInventoryReader`, `SqlInventoryStore` et `SqlInventoryStockAvailabilityReader`. Ils réutilisent l’`IDbContextFactory<PharmacyDbContext>` configurée par le Catalogue. Le Desktop n’accède jamais au contexte.

## Lecture

Les lectures utilisent des contextes courts et `AsNoTracking`, des projections bornées, la date métier injectée et des tris déterministes. Le résumé distingue total, vendable et expiré. FEFO trie d’abord les dates de péremption les plus proches, puis la création/clé comme départage stable. Un lot expiré reste lisible mais est exclu de la disponibilité POS.

## Écriture

Le store utilise des contextes courts. Mouvement + balance, démarrage de session et finalisation passent par la stratégie d’exécution EF et une transaction explicite. Les baisses valident le nouvel état avant `SaveChangesAsync`. Les contraintes SQL renforcent les invariants applicatifs.

## Idempotence et concurrence

Les opérations persistées combinent un `OperationId` unique et une empreinte structurelle. Une répétition équivalente retourne le résultat existant ; une réutilisation différente retourne `inventory.operation.conflict`. Les agrégats mutables utilisent `rowversion`; une version obsolète retourne `inventory.concurrency_conflict` sans écrasement.

## Panne SQL

Les numéros SQL de connectivité sont traduits en `inventory.database.unavailable`. En absence de connexion vers `Pharmacy_Development`, la factory indisponible garde le shell démarrable. Aucun fallback vers le lecteur synthétique retiré du runtime n’existe et aucune valeur de connexion n’est affichée.

## Limites

Aucune migration automatique, aucun repository générique, aucun contexte long-lived, aucun seed dans les migrations, aucune donnée réelle et aucune écriture Sales ne sont introduits.
