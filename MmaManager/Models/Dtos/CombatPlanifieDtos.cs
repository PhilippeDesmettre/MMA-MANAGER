namespace MmaManager.Models.Dtos;

public record OrganisationDto(
    int    OrganisationID,
    string Nom,
    int    AnneeCreation,
    int    Prestige,
    string? Description,
    bool   EstFictive
);

public record AdversaireDto(
    int    CombattantID,
    string Prenom,
    string NomFamille,
    string CategoriePoids,
    string StylePrincipal,
    int    NoteGlobale
);

public record CombatPlanifieDto(
    int    CombatPlanifieID,
    int    CombattantID,
    string CombattantPrenom,
    string CombattantNom,
    int    AdversaireID,
    string AdversairePrenom,
    string AdversaireNom,
    string Organisation,
    int    TourPrevu,
    string Statut,
    string Gameplan
);

public record PlanifierCombatRequest(
    int    CombattantID,
    int    AdversaireID,
    int    OrganisationID,
    int    TourPrevu,
    string Gameplan = "Balanced"   // Striking | Grappling | Balanced
);

/// <summary>Résultat d'un combat simulé lors du passage de tour.</summary>
public record CombatSimuleDto(
    int    CombattantID,
    string CombattantNom,
    int    AdversaireID,
    string AdversaireNom,
    string Organisation,
    bool   EstVictoire,
    bool   EstNul,
    string MethodeVictoire,
    int    RoundFin,
    string Details
);
