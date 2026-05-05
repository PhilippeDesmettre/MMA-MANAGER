namespace MmaManager.Models.Dtos;

public record RankingEntryDto(
    int    Rang,
    int    CombattantID,
    string NomComplet,
    int    Overall,
    bool   EstChampion,
    int    NbDefenses,
    int    Victoires,
    int    Defaites,
    int    Nuls
);

public record ClassementOrgaDto(
    int    OrganisationID,
    string OrganisationNom,
    int    Prestige,
    string Genre,
    int    CategorieID,
    string CategorieNom,
    IReadOnlyList<RankingEntryDto> Classement
);
