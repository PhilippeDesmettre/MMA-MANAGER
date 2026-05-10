namespace MmaManager.Models.Dtos;

public record OrganisationDto(
    int    OrganisationID,
    string Nom,
    int    AnneeCreation,
    int    Prestige,
    string? Description,
    bool   EstFictive,
    int    BourseVictoireMin,
    int    BourseVictoireMax,
    int    BourseDefaiteMin,
    int    BourseDefaiteMax
);

public record AdversaireDto(
    int    CombattantID,
    string Prenom,
    string NomFamille,
    string CategoriePoids,
    string StylePrincipal,
    short? TailleCm,
    short? AllongeCm,
    decimal? PoidsReelKg,
    int    NoteGlobale,
    int    CompStriking,
    int    CompLutte,
    int    CompGrappling,
    int    CompConditioning,
    int    CompStamina,
    int    CompMental,
    int    Victoires,
    int    Defaites,
    int    Nuls
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

/// <summary>Contrat proposé par l'organisation au moment de planifier un combat.</summary>
public record ContratPropositionDto(
    int    OrganisationID,
    string OrganisationNom,
    int    NombreCombats,
    bool   EstExclusif,
    string Description
);

/// <summary>Contrat actif d'un combattant (dans la fiche combattant).</summary>
public record ContratActifDto(
    int    ContratID,
    int    OrganisationID,
    string OrganisationNom,
    int    Prestige,
    int    NombreCombats,
    int    CombatsEffectues,
    int    CombatsRestants,
    bool   EstExclusif,
    string Statut
);

/// <summary>Détails d'un round individuel dans un combat simulé.</summary>
public record RoundDetailDto(
    int    NumeroRound,
    string GagnantRound,       // "Combattant" | "Adversaire" | "Egal"
    int    ScoreCombattant,    // 10, 9, 8
    int    ScoreAdversaire,
    string ActionsPrincipales, // description des actions clés du round
    bool   EstFinish,          // true si le combat se termine dans ce round
    string? MethodeFinish      // KO, TKO, Soumission si finish
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
    string Details,
    decimal BourseGagnee,
    IReadOnlyList<RoundDetailDto> Rounds,
    byte?   BlessureGravite,
    string? BlessureZone,
    short?  SemainesIndispo,
    bool    NouvelleRivalite,
    byte?   RivaliteIntensite,
    string? RivaliteRaison
);
