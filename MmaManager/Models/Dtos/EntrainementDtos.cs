namespace MmaManager.Models.Dtos;

// ── Staff disponible (pool global) ───────────────────────────────
public record StaffDisponibleDto(
    int     StaffDisponibleID,
    string  Prenom,
    string  NomFamille,
    string  Role,
    string? Icone,
    string? Description,
    int     CompStriking,
    int     CompLutte,
    int     CompGrappling,
    int     CompConditioning,
    int     CompMental,
    int     Overall,
    decimal SalaireMensuel,
    string? Nationalite,
    bool    EstEmbauche,
    int?    StaffPartieID    // null si pas embauché
);

// ── Staff embauché (dans la partie) ─────────────────────────────
public record StaffPartieDto(
    int     StaffPartieID,
    int     StaffDisponibleID,
    string  Prenom,
    string  NomFamille,
    string  Role,
    string? Icone,
    int     CompStriking,
    int     CompLutte,
    int     CompGrappling,
    int     CompConditioning,
    int     CompMental,
    int     Overall,
    decimal SalaireMensuel
);

// ── Coach unifié pour la sélection entraînement ──────────────────
public record CoachDto(
    int    ID,
    string Type,     // "Joueur" | "Staff"
    string Prenom,
    string Nom,
    string Icone,
    int    CompStriking,
    int    CompLutte,
    int    CompGrappling,
    int    CompConditioning,
    int    CompMental
);

public record EntrainementPlanifieDto(
    int    EntrainementPlanifieID,
    int    CombattantID,
    string Prenom,
    string NomFamille,
    string TypeEntrainement,
    int?   EntraineurJoueurID,
    int?   StaffPartieID,
    string CoachPrenom,
    string CoachNom
);

public record PlanifierEntrainementRequest(
    int    CombattantID,
    string TypeEntrainement,
    int?   EntraineurJoueurID = null,  // coach joueur
    int?   StaffPartieID      = null   // ou staff embauché
);

public record GainStatDto(string Stat, int Gain);

public record CombattantResultatDto(
    int    CombattantID,
    string Prenom,
    string NomFamille,
    string TypeEntrainement,
    IReadOnlyList<GainStatDto> Gains
);

public record ContratTermineDto(
    string CombattantNom,
    string OrganisationNom,
    int    CombatsEffectues
);

public record TourResultatDto(
    int    TourJoue,
    int    NouveauMois,
    int    NouvelleAnnee,
    string NouvelleEpoque,
    bool   TransitionEpoque,
    string? MessageTransition,
    IReadOnlyList<CombatSimuleDto>       CombatsResultats,
    IReadOnlyList<CombattantResultatDto> Resultats,
    decimal SoldeAvant,
    decimal SoldeApres,
    decimal DepensesTotales,
    bool   EstGameOver,
    int    PrestigeEcurie,
    bool   PrestigeAugmente,
    IReadOnlyList<ContratTermineDto> ContratsTermines
);
