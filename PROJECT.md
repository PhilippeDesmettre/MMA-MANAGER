# MMA Manager — Structure du projet

## Architecture

- **Backend** : ASP.NET Core Web API (.NET 9), JWT auth, Entity Framework Core
- **Frontend** : Vue 3 + Vuetify 3 SPA (Vite)
- **Base de données** : SQL Server (MMAManager)

## Arborescence

```
MmaManager/
├── MmaManager/                 # Backend .NET
│   ├── Controllers/            # API REST
│   ├── Models/                 # Entités EF + DTOs
│   ├── Data/MmaContext.cs      # DbContext
│   └── Program.cs
├── frontend/                   # Frontend Vue 3
│   ├── src/
│   │   ├── views/              # Pages (HomeView, GameView, AuthView, CreateTrainerView)
│   │   ├── components/         # Composants (FightersTab, TrainingTab, CombatPlanifieTab, StaffTab, FinancesTab)
│   │   └── composables/        # useAuth
│   └── public/
└── scriptMmaManager.sql        # Script complet de la BDD
```

## Schéma de base de données

### Tables principales

| Table | Description |
|-------|-------------|
| `Users` | Comptes utilisateurs (email, password hash) |
| `Partie` | Parties de jeu (UserID, Epoque, Argent, TourActuel, MoisActuel, AnneeActuelle, EstActive) |
| `Combattant` | ~1800 combattants avec 30+ stats individuelles, Overall calculé (colonne persistée) |
| `CombattantPartie` | Écurie du joueur (CombattantID ↔ PartieID) |
| `EntraineurJoueur` | Entraîneur du joueur avec compétences et background |
| `Background` | Backgrounds d'entraîneur (bonus stats) |

### Catégories & styles

| Table | Description |
|-------|-------------|
| `CategoriePoidsH` | 10 catégories hommes (Poids paille → Super-lourd) |
| `CategoriePoidsF` | 6 catégories femmes (Poids paille F → Poids mi-moyen F) |
| `StyleCombat` | Styles (Kickboxeur, Boxeur, Lutteur, BJJ, Muay Thai, etc.) |

### Combat & organisations

| Table | Description |
|-------|-------------|
| `CombatOrganisation` | Organisations de combat (Nom, Prestige 1-5, AnneeCreation, EstFictive) |
| `CombatPlanifie` | Combats planifiés (CombattantID, AdversaireID, OrganisationID, TourPrevu, Gameplan, Statut) |
| `ResultatCombatPartie` | Historique des combats simulés |
| `Combat` | Table historique de combats (non utilisée dans le mode partie) |

### Entraînement

| Table | Description |
|-------|-------------|
| `EntrainementPlanifie` | Entraînements planifiés par tour (type, coach assigné) |
| `HistoriqueEntrainement` | Historique des gains d'entraînement |
| `StaffDisponible` | Staff recrutables (rôle, compétences, salaire) |
| `StaffPartie` | Staff embauchés par le joueur |

### Autres

| Table | Description |
|-------|-------------|
| `Pays` | 60+ pays avec bonus par discipline |
| `Gym` | Salles d'entraînement |
| `Agent` | Agents liés à une partie |
| `Promotion` | Promotions MMA (UFC, PRIDE, etc.) |
| `Evenement` | Événements de combat |
| `LieuCombat` | Arènes et salles |
| `TitreChampionnat` | Ceintures par promotion et catégorie |
| `Classement` | Rankings par promotion |
| `Rivalite` | Rivalités entre combattants |

### Colonnes clés de Combattant

**Stats de frappe** : StatFrappeDebout, StatPuissance, StatVitesseMains, StatPrecision, StatCombosDebout, StatKick, StatClinic, StatEsquive, StatBlocage, StatFootwork
**Stats de lutte** : StatWrestling, StatTakedown, StatAntiTakedown, StatContrôleSol
**Stats de grappling** : StatJiuJitsu, StatSubmission, StatEvasionSub
**Stats physiques** : StatCardio, StatForce, StatVitesse, StatAgilite, StatMentoniere, StatRecuperation
**Stats mentales** : StatMental, StatExperience, StatCoaching, StatAdaptation
**Overall** : Colonne calculée persistée (moyenne de 10 stats clés)

## Instructions

- **Branche develop** : version en cours (vNext)
- **Branche main** : version stable
- A chaque prompt, ajouter une ligne dans le README.md pour le changelog vNext
