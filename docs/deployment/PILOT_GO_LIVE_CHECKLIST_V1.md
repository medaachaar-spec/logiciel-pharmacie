# Checklist GO-LIVE pilote V1

Cette RC n’est pas une validation production finale.

## J-7 à J-1

- [ ] poste Windows 11 x64 dédié, mises à jour et heure correctes ;
- [ ] SQL Express `PHARMACY` réel validé ;
- [ ] hash/signature du Setup vérifié ;
- [ ] UAT signée, aucun S1/S2 ;
- [ ] hardware réel validé ou explicitement hors besoin initial ;
- [ ] backup automatique, copie externe chiffrée et restore-test PASS ;
- [ ] Owner/récupération remis séparément ;
- [ ] support, rollback, installateur précédent et contacts disponibles ;
- [ ] données initiales relues et importées via pipeline contrôlé.

## Jour J

- [ ] backup de référence ;
- [ ] `health` PASS, 0 migration pending ;
- [ ] connexion Owner puis utilisateur limité ;
- [ ] vente synthétique cash et retour ; ticket uniquement si l’impression est activée ;
- [ ] réconciliation stock/caisse ;
- [ ] aucune alerte sécurité/SQL ;
- [ ] décision GO datée par propriétaire métier et responsable technique.

## Surveillance J+1 / J+7

- [ ] backups et espace disque ;
- [ ] erreurs application/Windows/SQL ;
- [ ] ventes, caisse, stock et audit réconciliés ;
- [ ] incidents utilisateurs classés ;
- [ ] support bundle sans données sensibles si escalade.

NO-GO : SQL Express non validé, restore non prouvé, UAT critique incomplète, risque de perte, erreur migration, sécurité/RBAC non conforme ou hardware nécessaire indisponible.
