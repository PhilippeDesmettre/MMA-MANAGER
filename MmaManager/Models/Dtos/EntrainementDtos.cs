namespace MmaManager.Models.Dtos;

public record EntrainementPlanifieDto(
    int    EntrainementPlanifieID,
    int    CombattantID,
    string Prenom,
    string NomFamille,
    string TypeEntrainement
);

public record PlanifierEntrainementRequest(
    int    CombattantID,
    string TypeEntrainement
);

public record GainStatDto(string Stat, int Gain);

public record CombattantResultatDto(
    int    CombattantID,
    string Prenom,
    string NomFamille,
    string TypeEntrainement,
    IReadOnlyList<GainStatDto> Gains
);

public record TourResultatDto(
    int    TourJoue,
    int    NouveauMois,
    int    NouvelleAnnee,
    IReadOnlyList<CombattantResultatDto> Resultats
);
