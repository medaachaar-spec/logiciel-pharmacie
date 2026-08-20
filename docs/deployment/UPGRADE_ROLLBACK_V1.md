# Upgrade et rollback pilote V1

## Invariants

- application fermée ;
- identité Windows autorisée sur SQL ;
- aucune migration depuis `Pharmacy.Desktop` ;
- sauvegarde pré-upgrade vérifiée avant toute migration d’une base existante ;
- downgrade EF automatique interdit ; rollback par restauration seulement.

## Pré-upgrade

1. Lire version actuelle et cible.
2. Vérifier espace disque, SQL, WebView2 et configuration avec `Pharmacy.Deployment preflight`.
3. Vérifier une sauvegarde récente et une copie externe chiffrée.
4. Suspendre la saisie et fermer `Pharmacy.Desktop.exe`.
5. Lancer le Setup de la nouvelle RC. `install-or-upgrade` énumère les migrations pending. S’il y en a, il crée `Pharmacy_PreUpgrade_*.bak`, exécute `RESTORE VERIFYONLY`, puis écrit le `.sha256` avant migration.
6. Après migration : `health`, connexion, données critiques, vente synthétique et backup.

Les migrations ne doivent être que celles présentes dans l’assembly livré. Un écart inattendu impose NO-GO.

## Rollback par restauration

Utiliser uniquement le backup pré-upgrade correspondant au poste et à la base. Cette opération remplace la base et détruit les écritures postérieures au backup : décision Owner/support obligatoire.

```powershell
& "$env:ProgramFiles\Logiciel Pharmacie\deployment\Pharmacy.Deployment.exe" rollback `
  --config "$env:ProgramData\LogicielPharmacie\config\pilot.json" `
  --backup "C:\chemin\Pharmacy_PreUpgrade_YYYYMMDD_HHMMSS.bak" `
  --confirm-database Pharmacy
```

1. Vérifier le SHA-256 avant exécution.
2. Conserver une copie du backup et le log d’échec.
3. Restaurer les binaires de la version compatible via son Setup.
4. Exécuter `health` puis un contrôle fonctionnel en lecture.
5. Documenter heure, opérateur, motif, backup et résultat.

Ne jamais restaurer vers une autre base par simple renommage non contrôlé. Ne jamais modifier `__EFMigrationsHistory` manuellement.
