using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

[Table("Rivalite")]
public class Rivalite
{
    [Key]
    public int RivaliteID { get; set; }

    public int PartieID { get; set; }

    public int Combattant1ID { get; set; }

    public int Combattant2ID { get; set; }

    public byte Intensite { get; set; } = 1;

    public byte NbConfrontations { get; set; } = 1;

    public int TourCreation { get; set; }

    public int TourDernierCombat { get; set; }

    [MaxLength(200)]
    public string? Raison { get; set; }

    public Partie? Partie { get; set; }
    public Combattant? Combattant1 { get; set; }
    public Combattant? Combattant2 { get; set; }
}
