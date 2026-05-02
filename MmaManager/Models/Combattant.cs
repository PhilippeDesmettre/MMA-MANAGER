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

    public int  PaysOrigineID   { get; set; }
    public int? PaysResidenceID { get; set; }

    public DateTime DateNaissance { get; set; }

    [MaxLength(1)]
    public string Genre      { get; set; } = "H";
    public int    CategorieID { get; set; }

    [Column(TypeName = "decimal(5,1)")]
    public decimal PoidsCombatKg { get; set; }

    public short? TailleCm  { get; set; }
    public short? AllongeCm { get; set; }

    public int? StylePrincipalID { get; set; }
    public int? GymActuelID      { get; set; }

    // ── Stats debout ─────────────────────────────────────────
    public byte StatFrappeDebout  { get; set; }
    public byte StatPuissance     { get; set; }
    public byte StatVitesseMains  { get; set; }
    public byte StatPrecision     { get; set; }
    public byte StatCombosDebout  { get; set; }
    public byte StatKick          { get; set; }
    public byte StatClinic        { get; set; }
    public byte StatEsquive       { get; set; }
    public byte StatBlocage       { get; set; }
    public byte StatFootwork      { get; set; }

    // ── Stats lutte / sol ────────────────────────────────────
    public byte StatWrestling     { get; set; }
    public byte StatTakedown      { get; set; }
    public byte StatAntiTakedown  { get; set; }

    [Column("StatContrôleSol")]
    public byte StatControleSol   { get; set; }

    public byte StatJiuJitsu      { get; set; }
    public byte StatSubmission    { get; set; }
    public byte StatEvasionSub    { get; set; }

    // ── Stats athlétisme ─────────────────────────────────────
    public byte StatCardio        { get; set; }
    public byte StatForce         { get; set; }
    public byte StatVitesse       { get; set; }
    public byte StatAgilite       { get; set; }
    public byte StatMentoniere    { get; set; }
    public byte StatRecuperation  { get; set; }

    // ── Stats mentales ───────────────────────────────────────
    public byte StatMental        { get; set; }
    public byte StatExperience    { get; set; }
    public byte StatCoaching      { get; set; }
    public byte StatAdaptation    { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public byte Overall { get; set; }

    // ── Développement ────────────────────────────────────────
    public byte Potentiel  { get; set; }
    public byte Progression { get; set; }

    // ── État ─────────────────────────────────────────────────
    public byte    Moral           { get; set; } = 50;
    public byte    Fatigue         { get; set; }
    public byte    Motivation      { get; set; } = 50;
    public byte    BlessureGravite { get; set; }
    public string? BlessureZone    { get; set; }
    public short   SemainesIndispo { get; set; }

    [MaxLength(30)]
    public string Statut { get; set; } = "Libre";

    // ── Contrat ──────────────────────────────────────────────
    public int  Salaire      { get; set; }
    public int  PrimeSigne   { get; set; }
    public int  Valeur       { get; set; }
    public bool SousContrat  { get; set; }
    public int? GymContratID { get; set; }

    // ── Bilan sportif ─────────────────────────────────────────
    public short Victoires    { get; set; }
    public short Defaites     { get; set; }
    public short Nuls         { get; set; }
    public short VictoiresKO  { get; set; }
    public short VictoiresSub { get; set; }
    public short VictoiresDec { get; set; }
    public short DefaitesKO   { get; set; }
    public short DefaitesSub  { get; set; }

    public DateTime? DateDebut    { get; set; }
    public DateTime  DateCreation { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(5,1)")]
    public decimal? PoidsReelKg { get; set; }

    // Navigation
    public Pays? PaysOrigine { get; set; }
}
