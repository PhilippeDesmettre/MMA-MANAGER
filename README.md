# MMA Manager

Jeu de gestion d'écurie MMA — Vue 3 + ASP.NET Core + SQL Server.

## Changelog

### vNext (develop)

- Fix filtre catégories de poids : catégories chargées dynamiquement depuis les données au lieu d'être hardcodées
- Highlight visuel du staff sélectionné dans l'onglet entraînement
- Affichage de l'âge des combattants
- Ajout type Agent dans le staff (négociation de contrats)
- Système de contrats par ère (exclusivité multi-combats pour Golden Age et Modern MMA)
- Fix bug timing combat (combats prévus au tour N arrivent maintenant au tour N)
- Simulation de combat round par round avec détails par round
- Ajout stat "menton" et amélioration simulation KO/soumission avec logique gameplan
- Dialog résultats combats : affichage du détail round par round (gagnant, score, actions, finish)
- CombatPlanifieTab : affichage des termes du contrat proposé (nombre de combats, exclusivité) selon l'ère
- Simulation : augmentation significative des taux de KO/TKO/Soumission pour des combats plus spectaculaires
- Fiche combattant complète : identité (âge, nationalité, genre, style), bilan sportif avec détail KO/Sub/Déc, stats composites et stats individuelles par discipline
- Dialog combats élargi (820px) et détails round sur plusieurs lignes (texte visible en entier)
- Dialog entraînement : section "Résultats d'entraînement" avec icône par type, compteur de stats améliorées
