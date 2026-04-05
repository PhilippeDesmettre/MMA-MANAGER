namespace MmaManager.Models.Dtos;

public record FinancesCombattantDto(
    int     CombattantID,
    string  Prenom,
    string  NomFamille,
    decimal Salaire
);

public record FinancesDto(
    decimal Solde,
    decimal Loyer,
    decimal SalairesTotaux,
    decimal StaffTotal,
    decimal DepensesTotales,
    decimal SoldeApres,
    IReadOnlyList<FinancesCombattantDto> Combattants
);

public record CombattantListDto(
    int     CombattantID,
    string  Prenom,
    string  Nom,
    string  Nationalite,
    string  CodePays,
    int     Age,
    string  CategoriePoids,
    string  StylePrincipal,
    int     NoteGlobale,
    decimal PrixAchat,
    decimal SalaireMensuel
);

public record CombattantDetailDto(
    int     CombattantID,
    string  Prenom,
    string  Nom,
    string  Nationalite,
    string  CodePays,
    int     Age,
    string  CategoriePoids,
    string  StylePrincipal,
    string? Biographie,
    int     CompStriking,
    int     CompLutte,
    int     CompGrappling,
    int     CompConditioning,
    int     CompStamina,
    int     CompMental,
    int     NoteGlobale,
    decimal PrixAchat,
    decimal SalaireMensuel,
    // Bilan sportif
    int     Victoires,
    int     Defaites,
    int     Nuls
);
