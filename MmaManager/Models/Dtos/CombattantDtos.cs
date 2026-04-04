namespace MmaManager.Models.Dtos;

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
    decimal SalaireMensuel
);
