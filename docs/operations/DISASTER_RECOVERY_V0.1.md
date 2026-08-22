# Plan de reprise d’activité V0.1

## Cibles et responsabilités

- RPO : 15 minutes au maximum lorsque le mode `FULL` et les sauvegardes de journal sont opérationnels.
- RTO : 2 heures pour restaurer, vérifier et rouvrir le service local.
- Le responsable d’exploitation décide du point de restauration et autorise toute opération destructive.
- L’opérateur réalise les sauvegardes et conserve les preuves SHA-256/restore-test.
- Un utilisateur métier valide Catalogue, Stock, Ventes, Clients et Rapports après reprise.

Ces objectifs sont des cibles de procédure, pas une garantie sans calendrier Windows actif, support externe disponible et exercices réguliers.

## Prévention

1. Vérifier quotidiennement la dernière sauvegarde complète, la dernière différentielle et le dernier journal.
2. Vérifier l’espace local et externe sans journaliser de secret.
3. Exiger `RESTORE VERIFYONLY`, SHA-256 et copie externe sur volume chiffré.
4. Exécuter un restore-test isolé au moins mensuellement et après changement majeur de migration.
5. Conserver l’application, les scripts de migration et les documents de reprise séparément des données.
6. L’onduleur reste un contrôle manuel en V0.1 ; tester autonomie et arrêt propre pendant le pilote matériel.

## Déclenchement

Déclarer un incident si SQL Server est indisponible, si la base est suspecte/corrompue, si le disque est perdu ou si une erreur humaine a altéré des données. Ne pas multiplier les tentatives d’écriture. Relever l’heure UTC, arrêter les clients Desktop et préserver les fichiers/journaux existants.

## Séquence de reprise

1. Identifier le dernier point cohérent et les sauvegardes nécessaires.
2. Copier les sauvegardes retenues vers un espace de travail protégé et comparer les SHA-256.
3. Faire une sauvegarde de sécurité de l’état actuel si SQL le permet.
4. Tester la chaîne dans une base temporaire, jamais dans `Pharmacy_Development`.
5. Autoriser formellement la restauration réelle hors application.
6. Restaurer complète, différentielle puis journaux dans l’ordre, avec `NORECOVERY` jusqu’au dernier élément et `RECOVERY` à la fin.
7. Vérifier migrations, contraintes, tables critiques, cohérence fonctionnelle et absence de migration en attente.
8. Rouvrir d’abord en lecture contrôlée, puis les écritures après validation métier.
9. Archiver la chronologie, les hashes, les décisions et le résultat.

## Scénarios dégradés

- Support externe absent : conserver la sauvegarde locale, signaler RPO non protégé hors machine et restaurer le support ; ne pas déclarer la continuité conforme.
- Sauvegarde invalide : ne pas restaurer ; revenir à la génération précédente vérifiée et recalculer la perte potentielle.
- SQL indisponible : le POS ne doit pas promettre une vente persistée ; diagnostiquer service, stockage et droits avant reprise.
- Imprimante indisponible : aucun impact lorsque l’impression est désactivée ; lorsqu’elle est activée, la vente déjà commitée reste valide et peut être réimprimée sans recréer vente/paiement.
- Panne électrique : appliquer l’arrêt contrôlé de l’onduleur et vérifier SQL au redémarrage.

## Exercice et clôture

Un exercice est PASS uniquement si la base temporaire est restaurée, les quatre domaines critiques sont présents, les migrations sont lisibles, la base temporaire est supprimée et aucun fichier/secret n’est abandonné. Mesurer le temps réel et corriger la procédure si le RTO de 2 h n’est pas tenable.
