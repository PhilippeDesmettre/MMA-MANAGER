using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

/// <summary>
/// Pool global de staff disponibles à recruter.
/// Indépendant des parties — peut être embauché par n'importe quel joueur.
/// </summary>
[Table("StaffDisponible")]
public class StaffDisponible
{
    [Key]
    public int StaffDisponibleID { get; set; }

    [MaxLength(60)] public string Prenom { get; set; } = string.Empty;
    [MaxLength(60)] public string Nom    { get; set; } = string.Empty;

    /// <summary>
    /// CoachStriking | CoachLutte | CoachGrappling | CoachConditioning | CoachMental | PreparateurPhysique
    /// </summary>
    [MaxLength(30)]
    public string Role { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? Icone { get; set; }

    /// <summary>Courte biographie ou accroche</summary>
    [MaxLength(300)]
    public string? Description { get; set; }

    // ── Compétences (0–99) ──────────────────────────────────────
    public int CompStriking      { get; set; } = 30;
    public int CompLutte         { get; set; } = 30;
    public int CompGrappling     { get; set; } = 30;
    public int CompConditioning  { get; set; } = 30;
    public int CompMental        { get; set; } = 30;

    /// <summary>Note globale calculée</summary>
    public int Overall { get; set; } = 50;

    /// <summary>Salaire mensuel en euros</summary>
    public decimal SalaireMensuel { get; set; } = 2000m;

    /// <summary>Nationalité (affichage)</summary>
    [MaxLength(60)]
    public string? Nationalite { get; set; }
}
