using System.ComponentModel.DataAnnotations;

namespace MmaManager.Models.Dtos;

public record CreatePartieRequest(
    [Required, MaxLength(60)] string Prenom,
    [Required, MaxLength(60)] string Nom,
    [Required] int PaysOrigineID,
    [Required] int PaysResidenceID,
    [Required] int BackgroundID,
    [Required] string Epoque   // "NoRules" | "GoldenAge" | "Modern"
);

public record StatTrainerDto(string Nom, string Cle, int Valeur);

public record EntraineurDto(
    string Prenom,
    string Nom,
    string PaysOrigineNom,
    string PaysResidenceNom,
    string? PaysResidenceCode,
    string BackgroundNom,
    string BackgroundDescription,
    string BackgroundIcone,
    IEnumerable<StatTrainerDto> Stats
);

public record PartieDto(
    int      PartieID,
    string   Epoque,
    decimal  Argent,
    DateTime DateCreation,
    int      TourActuel,
    int      MoisActuel,
    int      AnneeActuelle,
    int      PrestigeEcurie,
    EntraineurDto Entraineur
);
