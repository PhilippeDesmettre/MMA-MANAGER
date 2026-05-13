using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

[Table("Agent")]
public class Agent
{
    [Key]
    public int AgentID { get; set; }

    /// <summary>Null = agent NPC (pas lié à une partie)</summary>
    public int? PartieID { get; set; }

    /// <summary>True = c'est le joueur lui-même qui agit comme agent</summary>
    public bool EstJoueur { get; set; } = false;

    [MaxLength(60)] public string Prenom { get; set; } = string.Empty;
    [MaxLength(60)] public string Nom    { get; set; } = string.Empty;

    // ── Compétences d'agent ──────────────────────────────────────
    /// <summary>Capacité à créer des contacts dans l'industrie</summary>
    public int CompContact     { get; set; } = 30;

    /// <summary>Qualité des négociations de contrats</summary>
    public int CompNegociation { get; set; } = 30;

    /// <summary>Étendue du réseau (adversaires, organisations, scouts)</summary>
    public int CompReseau      { get; set; } = 30;

    /// <summary>Réputation personnelle dans l'industrie MMA</summary>
    public int CompReputation  { get; set; } = 30;

    /// <summary>Capacité à influencer matchmaking et classements</summary>
    public int CompInfluence   { get; set; } = 30;

    /// <summary>Promotion et branding des combattants</summary>
    public int CompMarketing   { get; set; } = 30;

    /// <summary>Compréhension des contrats, clauses, légalité</summary>
    public int CompJuridique   { get; set; } = 30;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SalaireMensuel { get; set; } = 0;

    [NotMapped]
    public int MaxCombattants => 2 + CompReseau / 20;
}
