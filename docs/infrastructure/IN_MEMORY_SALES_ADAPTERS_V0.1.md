# Adaptateurs Sales en mémoire V0.1

Date : 2026-08-03

Statut : `PARTIALLY SUPERSEDED — DRAFT/PREPARATION STATE ONLY`

## 1. Objectif

Fournir des implémentations déterministes et testables aux contrats Sales Application, uniquement pour préparer la prochaine intégration frontend. Ces classes ne constituent pas une base de données ni une persistance.

## 2. Cycle de vie

Chaque adaptateur conserve son état dans ses champs d’instance. Aucune collection globale statique mutable n’existe. Les brouillons et préparations disparaissent à la fermeture du processus. Aucune donnée n’est écrite sur disque, dans un fichier, une base, le registre ou le réseau.

## 3. InMemorySaleDraftStore

Le store utilise un dictionnaire privé protégé par un verrou ciblé. Il conserve des snapshots internes composés de l’identifiant, de la version et de lignes immuables.

- création : version 1 ;
- chargement : reconstruction d’un nouvel agrégat `SaleDraft` ;
- sauvegarde : comparaison atomique de la version attendue ;
- succès : remplacement du snapshot et version +1 ;
- conflit : aucune modification ;
- aucune instance mutable interne n’est exposée.

Deux chargements concurrents produisent deux agrégats détachés. Si les deux tentent de sauvegarder la même version, une seule opération réussit.

## 4. InMemoryProductCatalogReader

Le catalogue contient exactement dix références synthétiques déterministes `DEMO-000001` à `DEMO-000010`, avec identifiants positifs, références uniques, libellés neutres et prix `decimal` en MAD. La recherche est insensible à la casse et couvre référence, nom et présentation. Les résultats sont ordonnés par `ProductId`.

Aucun produit, médicament, fabricant ou code réel n’est utilisé.

Le catalogue Desktop sera supprimé après connexion complète au Sprint suivant.

Cette duplication temporaire évite de modifier le frontend avant le sprint autorisé.

## 5. InMemoryStockAvailabilityReader

Les dix disponibilités reproduisent les quantités synthétiques du POS actuel. L’adaptateur répond uniquement à une lecture de quantité demandée. Il n’effectue aucun mouvement, réservation, FEFO, seuil ou classement. Ses données ne changent jamais pendant le processus.

## 6. InMemorySaleDraftIdGenerator

Le générateur crée un `Guid` avec `Guid.NewGuid()` puis le valide comme `SaleDraftId`. Il ne génère aucun numéro de vente ou document métier.

## 7. InMemorySaleFinalizationPreparationStore

Le store associe un `OperationId` à un snapshot de préparation immuable sous verrou. Une empreinte équivalente retourne l’instance déjà enregistrée. Une empreinte différente sous le même identifiant retourne `sales.operation.conflict`.

Aucune référence au brouillon mutable n’est stockée et aucune vente n’est finalisée.

## 8. Thread-safety

Les deux stores mutables utilisent des verrous privés d’instance pour rendre vérification et mise à jour atomiques. Catalogue et disponibilités sont construits une fois puis seulement lus. Aucun retry aveugle ou asynchronisme simulé avec `Task.Run` n’existe.

## 9. Interdictions respectées

Aucun EF Core, `DbContext`, SQL, migration, chaîne de connexion, API HTTP, serveur, `localhost`, JSON de données, repository générique, cache global, lot, mouvement de stock, paiement, audit ou impression n’est présent.

## 10. Remplacement futur

Le Desktop compose désormais le Catalogue, la disponibilité, la finalisation et la lecture des ventes avec les adaptateurs SQL. Seuls le store/générateur de brouillons et le store de préparations restent en mémoire ; les anciens lecteurs synthétiques sont conservés pour les tests et ne doivent jamais servir de fallback. Leur contenu ne doit jamais être utilisé comme migration, seed de production ou source de données réelle.
