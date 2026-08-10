# TPE externe manuel V0.1

Le terminal bancaire reste externe à l’application. L’opérateur saisit le montant sur le TPE, attend son résultat puis confirme manuellement `Accepted`, `Declined` ou `Cancelled`. `IManualPaymentTerminal` ne transporte aucun PAN, cryptogramme, piste, PIN, token acquéreur ou reçu bancaire.

Une vente carte ne peut être finalisée que si le résultat externe est confirmé. Un refus ou une annulation laisse le panier contrôlable et ne doit pas créer un paiement réussi. L’application journalise uniquement l’issue métier et la référence de vente, jamais les données du moyen de paiement.

Le pilote doit vérifier : montant identique, acceptation, refus, annulation, timeout opérateur, correction avant commit et formation de caisse. Toute intégration future à un fournisseur TPE nécessite une revue sécurité/PCI distincte ; elle est hors Sprint 17.
