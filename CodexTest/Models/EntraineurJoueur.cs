using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CodexTest.Models;

[Table("EntraineurJoueur")]
public class EntraineurJoueur
{
    [Key]
    public int EntraineurJoueurID { get; set; }

    public int PartieID        { get; set; }
    public int PaysOrigineID   { get; set; }
    public int PaysResidenceID { get; set; }
    public int BackgroundID    { get; set; }

    [MaxLength(60)] public string Prenom { get; set; } = string.Empty;
    [MaxLength(60)] public string Nom    { get; set; } = string.Empty;

    // Stats (base 30 + bonus background)
    public int CompStriking     { get; set; } = 30;
    public int CompLutte        { get; set; } = 30;
    public int CompGrappling    { get; set; } = 30;
    public int CompConditioning { get; set; } = 30;
    public int CompStamina      { get; set; } = 30;
    public int CompMental       { get; set; } = 30;
    public int CompStrategie    { get; set; } = 30;
    public int CompNegociation  { get; set; } = 30;
    public int CompMotivation   { get; set; } = 30;

    // Navigation
    public Background? Background { get; set; }
}
