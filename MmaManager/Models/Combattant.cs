using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MmaManager.Models;

[Table("Combattant")]
public class Combattant
{
    [Key]
    public int CombattantID { get; set; }

    [MaxLength(60)]
    public string Prenom { get; set; } = string.Empty;

    [Column("NomFamille")]
    [MaxLength(60)]
    public string NomFamille { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? Surnom { get; set; }

    public int PaysOrigineID { get; set; }
    public int? PaysResidenceID { get; set; }
    public DateTime DateNaissance { get; set; }

    [MaxLength(1)]
    public string Genre { get; set; } = "H";

    public int CategorieID { get; set; }
    public int? StylePrincipalID { get; set; }

    public byte StatFrappeDebout { get; set; }
    public byte StatPuissance { get; set; }
    public byte StatPrecision { get; set; }
    public byte StatWrestling { get; set; }
    public byte StatTakedown { get; set; }
    public byte StatAntiTakedown { get; set; }
    public byte StatJiuJitsu { get; set; }
    public byte StatSubmission { get; set; }
    public byte StatEvasionSub { get; set; }
    public byte StatCardio { get; set; }
    public byte StatForce { get; set; }
    public byte StatVitesse { get; set; }
    public byte StatAgilite { get; set; }
    public byte StatMentoniere { get; set; }
    public byte StatRecuperation { get; set; }
    public byte StatMental { get; set; }
    public byte StatExperience { get; set; }
    public byte StatAdaptation { get; set; }
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public byte Overall { get; set; }

    public int Salaire { get; set; }
    public int Valeur { get; set; }

    // Bilan sportif
    public short Victoires    { get; set; }
    public short Defaites     { get; set; }
    public short Nuls         { get; set; }
    public short VictoiresKO  { get; set; }
    public short VictoiresSub { get; set; }
    public short VictoiresDec { get; set; }
    public short DefaitesKO   { get; set; }
    public short DefaitesSub  { get; set; }

    public Pays? PaysOrigine { get; set; }
}
