# Sprint 9 — Validation SQL Server et Catalogue physique V0.1

Date : 2026-08-03

Statut : `IMPLEMENTED — SQL SERVER DEVELOPER VALIDATION PASS — EXPRESS ENVIRONMENT GATE PENDING`

## Environnement

- instance : SQL Server locale par défaut ;
- version : `17.0.1125.2` ;
- édition : Developer 64 bits ;
- moteur majeur : 17 ;
- base : `Pharmacy_Development` ;
- compatibilité : 170 ;
- cible pilote Express : non validée sur ce poste.

La connexion est fournie hors Git. La valeur n’est pas reproduite dans ce rapport. La confiance du certificat local nécessite encore une remédiation ; l’exception de confiance utilisée pour les tests est provisoire et locale.

## Migration et schéma

- migration : `20260803185734_InitialCatalogPersistenceV01` ;
- EF Core / outil : 10.0.10 ;
- schéma : `catalog` ;
- tables : `ProductCategories`, `Products`, `ProductLocalSettings`, `ProductBarcodes`, `ProductPriceHistories` ;
- lignes Catalogue dans la base de développement : 0 ;
- migrations en attente : 0.

La divergence de nom avec la cible indicative `ProductReferences` est volontaire : la Phase 3 confirmée impose `ProductLocalSettings` en 1–1 avec `Products`, et relie barcodes/historiques directement aux produits.

## Résultats automatisés

- Debug : build 0 avertissement/0 erreur ; 283/283 tests réussis ;
- Release : build 0 avertissement/0 erreur ; 283/283 tests réussis ;
- tests ajoutés : 12, dont un scénario SQL réel complet ;
- anciens tests conservés : 271/271 ;
- script idempotent exécuté deux fois sans duplication ;
- migration `Up`, `Down` vers `0` et réapplication : réussies ;
- comparaison du schéma avant/après réapplication : identique ;
- bases temporaires résiduelles : 0 ;
- migration en attente : aucune.

## Contraintes et concurrence validées

- PK/FK et types physiques ;
- quatre FK `NO ACTION` ;
- code catégorie, référence interne et barcode uniques ;
- un seul barcode principal actif par produit ;
- prix et seuils non négatifs ;
- seuil minimum inférieur ou égal au maximum ;
- date de mise à jour cohérente ;
- quatre colonnes `rowversion` ;
- changement de version à l’update ;
- ancienne version rejetée par `DbUpdateConcurrencyException` ;
- état persistant cohérent après conflit.

## Non-régression et sécurité

- Desktop lancé sans variable SQL ;
- fenêtre répondante, fermeture propre, runner code 0 et stderr vide ;
- POS toujours sur les adaptateurs mémoire ;
- aucune migration automatique ;
- aucun `ISaleFinalizer` ou adaptateur SQL Sales ;
- aucun secret, mot de passe, connexion réelle ou compte SQL versionné ;
- scan spécialisé : aucune clé privée, token, mot de passe ou identifiant SQL détecté.

## Commandes manuelles

Depuis la racine du dépôt, définir localement les variables sans transmettre leur valeur :

```powershell
dotnet tool restore
dotnet restore .\Pharmacy.slnx
dotnet build .\Pharmacy.slnx -c Debug --no-restore
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet build .\Pharmacy.slnx -c Release --no-restore
dotnet test .\Pharmacy.slnx -c Release --no-build
```

Vérification SQL en lecture seule :

```powershell
sqlcmd -S <instance-explicite> -d Pharmacy_Development -E -C -b `
  -i .\database\verification\verify_sprint09_schema.sql
```

`-C` n’est acceptable ici que pour la revue locale provisoire déjà documentée. Retirer cette option après installation d’un certificat approuvé.

## Captures SSMS demandées

1. instance et base `Pharmacy_Development` ;
2. cinq tables du schéma `catalog` ;
3. colonnes `bigint`, `datetime2`, `decimal` et `rowversion` ;
4. clés étrangères et index ;
5. résultat du script de vérification ;
6. `dbo.__EFMigrationsHistory`.

## Checklist humaine

- [ ] base de développement présente ;
- [ ] migration appliquée ;
- [ ] cinq tables Catalogue présentes ;
- [ ] relations visibles ;
- [ ] PK en `bigint` ;
- [ ] prix en `decimal(19,4)` ;
- [ ] dates en `datetime2(7)` ;
- [ ] quatre `rowversion` ;
- [ ] unicité et prix négatif confirmés ;
- [ ] suppressions restrictives confirmées ;
- [ ] aucune donnée Catalogue réelle ;
- [ ] aucun secret versionné ;
- [ ] aucune migration automatique ;
- [ ] POS toujours opérationnel en mémoire ;
- [ ] aucune exception ;
- [ ] version/édition SQL et gate Express consignés.

## Limites

Le gate Express, la confiance TLS durable et les captures SSMS restent manuels. La réussite sur Developer ne certifie pas le poste pilote.
