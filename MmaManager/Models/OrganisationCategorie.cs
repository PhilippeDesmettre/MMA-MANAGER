using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

[Table("OrganisationCategorie")]
public class OrganisationCategorie
{
    [Key]
    public int OrganisationCategorieID { get; set; }

    public int OrganisationID { get; set; }
    public int CategorieID    { get; set; }

    [MaxLength(1)]
    public string Genre { get; set; } = "H";

    /// <summary>Année d'introduction de cette catégorie dans l'organisation</summary>
    public int AnneeIntroduction { get; set; }

    /// <summary>True = open weight, tout fighter peut y combattre quel que soit son poids</summary>
    public bool EstOpenWeight { get; set; } = false;

    public CombatOrganisation? Organisation { get; set; }
}
