# Imprimante ticket 80 mm V0.1

## Invariant de vente

`IReceiptPrinter` est appelé uniquement avec un `SaleId` et une référence persistés. Le commit SQL Vente/Paiement/Stock précède toujours l’impression. Un échec, une absence de papier ou une déconnexion ne rollbacke jamais et ne recrée jamais la vente.

La V0.1 fournit `SimulatedReceiptPrinter`. Elle valide le contrat, les tentatives, la reprise et l’audit mais ne prétend pas imprimer sur un périphérique réel. Le POS affiche explicitement « simulateur » et permet : première impression, échec simulé, nouvelle tentative, continuation sans ticket et réimpression tracée.

## Données du ticket

Contenu minimal : référence de vente, date UTC/locale présentée, lignes (désignation, quantité, prix unitaire, total), total MAD et mode de paiement. Ne jamais inclure mot de passe, chaîne SQL, donnée bancaire, information clinique ou trace technique complète.

## Pilote matériel

Valider modèle/driver Windows, largeur réelle 80 mm, accents/arabe si requis, découpe, débit, papier absent, capot ouvert, USB débranché, reprise, réimpression et absence de double vente. Définir ensuite un adaptateur maintenu derrière `IReceiptPrinter`; ne pas coupler le POS à un SDK propriétaire.
