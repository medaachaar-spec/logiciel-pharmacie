# Checklist pilote matériel V0.1

Environnement : poste pilote isolé, `Pharmacy_Development`, données synthétiques, sauvegarde récente, utilisateur RBAC dédié. Relever modèle, firmware/driver, port et résultat sans secret.

## Scanner HID

- [ ] Code connu + Entrée : une seule ligne.
- [ ] Double scan rapide : pas de doublon involontaire.
- [ ] Code inconnu : message, aucune création produit.
- [ ] Saisie manuelle et focus clavier visibles.
- [ ] Déconnexion/reconnexion et reprise après veille.

## Imprimante 80 mm

- [ ] Vente SQL commitée avant demande d’impression.
- [ ] Ticket lisible, montants/lignes exacts, largeur et découpe correctes.
- [ ] Papier absent, capot ouvert et USB débranché : vente inchangée.
- [ ] Réessai et continuation sans ticket.
- [ ] Réimpression explicitement demandée et auditée, sans double vente.

## TPE externe

- [ ] Montant présenté identique au total.
- [ ] Accepté, refusé et annulé confirmés manuellement.
- [ ] Aucune donnée bancaire dans UI, SQL, logs ou audit.

## Onduleur et poste

- [ ] Autonomie mesurée sous charge réelle.
- [ ] Alerte batterie visible par l’opérateur.
- [ ] Arrêt Windows/SQL propre documenté.
- [ ] Redémarrage, intégrité SQL et reprise Desktop vérifiés.

## Diagnostic et ergonomie

- [ ] SQL, sauvegarde locale/externe, runtime et périphériques correctement distingués.
- [ ] Aucun simulateur affiché comme matériel certifié.
- [ ] 1100×700, 1366×768, 1920×1080 sans débordement horizontal.
- [ ] Navigation clavier et focus visible.
- [ ] Compte sans permission : action refusée côté UI et backend.

Verdict par équipement : `PASS HARDWARE`, `FAIL HARDWARE` ou `NOT TESTED`. Tant que toutes les lignes physiques ne sont pas validées, conserver `HARDWARE PILOT GATE PENDING`.
