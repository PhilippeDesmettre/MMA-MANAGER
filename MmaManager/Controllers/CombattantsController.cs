using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmaManager.Data;
using MmaManager.Models;
using MmaManager.Models.Dtos;

namespace MmaManager.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CombattantsController(MmaContext db) : ControllerBase
{
    private sealed record CombattantReferenceData(
        IReadOnlyDictionary<int, Pays> PaysById,
        IReadOnlyDictionary<int, string> StylesById,
        IReadOnlyDictionary<int, string> CategoriesHById,
        IReadOnlyDictionary<int, string> CategoriesFById);

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("disponibles")]
    public async Task<ActionResult<IEnumerable<CombattantDetailDto>>> GetDisponibles()
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound("Aucune partie active.");

        var recrutes = await db.CombattantsPartie
            .Where(cp => cp.PartieID == partie.PartieID)
            .Select(cp => cp.CombattantID)
            .ToListAsync();

        var references = await LoadReferenceData();

        int prestige = partie.PrestigeEcurie;

        int overallMax = prestige switch
        {
            1 => 55,
            2 => 65,
            3 => 75,
            4 => 85,
            _ => 99
        };

        IQueryable<Combattant> query = db.Combattants
            .AsNoTracking()
            .Where(c => !recrutes.Contains(c.CombattantID) && c.Overall <= overallMax);

        if (prestige <= 2)
        {
            var entraineur = await db.EntraineursJoueur
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.PartieID == partie.PartieID);
            if (entraineur is not null)
                query = query.Where(c => c.PaysOrigineID == entraineur.PaysResidenceID);
        }

        var combattants = await query
            .OrderBy(c => c.NomFamille)
            .ThenBy(c => c.Prenom)
            .ToListAsync();

        return Ok(combattants.Select(c => ToDetailDto(c, references, partie.AnneeActuelle, partie.MoisActuel, [])));
    }

    [HttpGet("ecurie")]
    public async Task<ActionResult<IEnumerable<CombattantDetailDto>>> GetEcurie()
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound("Aucune partie active.");

        var references = await LoadReferenceData();

        var combattants = await db.CombattantsPartie
            .Where(cp => cp.PartieID == partie.PartieID)
            .Include(cp => cp.Combattant)
            .AsNoTracking()
            .OrderBy(cp => cp.Combattant!.NomFamille)
            .ThenBy(cp => cp.Combattant!.Prenom)
            .Select(cp => cp.Combattant!)
            .ToListAsync();

        var rivalites = await db.Rivalites
            .Where(r => r.PartieID == partie.PartieID)
            .Include(r => r.Combattant1)
            .Include(r => r.Combattant2)
            .AsNoTracking()
            .ToListAsync();

        var ecurieCombattantIds = combattants.Select(c => c.CombattantID).ToList();

        var rankingEntries = await db.RankingEntries
            .Where(r => r.PartieID == partie.PartieID && ecurieCombattantIds.Contains(r.CombattantID) && r.Rang > 0)
            .Include(r => r.Organisation)
            .AsNoTracking()
            .ToListAsync();

        var ceintures = await db.ChampionCeintures
            .Where(c => c.PartieID == partie.PartieID && c.CombattantID != null
                     && ecurieCombattantIds.Contains(c.CombattantID!.Value))
            .Include(c => c.Organisation)
            .AsNoTracking()
            .ToListAsync();

        var mondialPoints = await db.RankingEntries
            .Where(r => r.PartieID == partie.PartieID)
            .GroupBy(r => new { r.Genre, r.CategorieID, r.CombattantID })
            .Select(g => new { g.Key.Genre, g.Key.CategorieID, g.Key.CombattantID, Total = g.Sum(r => r.PointsMondiaux) })
            .ToListAsync();

        var mondialGrouped = mondialPoints
            .GroupBy(x => (x.Genre, x.CategorieID))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Total).Select(x => x.CombattantID).ToList());

        var contratsActifs = await db.ContratsOrganisation
            .Where(co => co.PartieID == partie.PartieID && co.Statut == "Actif"
                      && ecurieCombattantIds.Contains(co.CombattantID))
            .Include(co => co.Organisation)
            .AsNoTracking()
            .ToListAsync();

        var cpAgents = await db.CombattantsPartie
            .Where(cp => cp.PartieID == partie.PartieID && cp.AgentID != null)
            .Include(cp => cp.Agent)
            .AsNoTracking()
            .ToListAsync();

        var agentFighterCounts = cpAgents
            .GroupBy(cp => cp.AgentID!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var agentLookup = cpAgents
            .ToDictionary(cp => cp.CombattantID, cp => cp.Agent!);

        var rankingLookup = new Dictionary<int, (string? meilleurRang, int? rangMondial)>();
        foreach (var c in combattants)
        {
            var ceinture = ceintures.FirstOrDefault(cc => cc.CombattantID == c.CombattantID);
            string? meilleurRang = null;
            if (ceinture is not null)
                meilleurRang = $"🏆 Champion {ceinture.Organisation!.Nom}";
            else
            {
                var best = rankingEntries.Where(r => r.CombattantID == c.CombattantID).MinBy(r => r.Rang);
                if (best is not null)
                    meilleurRang = $"#{best.Rang} {best.Organisation!.Nom}";
            }

            int? rangMondial = null;
            var key = (c.Genre, c.CategorieID);
            if (mondialGrouped.TryGetValue(key, out var list))
            {
                var pos = list.IndexOf(c.CombattantID);
                if (pos >= 0 && pos < 15) rangMondial = pos + 1;
            }

            rankingLookup[c.CombattantID] = (meilleurRang, rangMondial);
        }

        return Ok(combattants.Select(c =>
        {
            var (meilleurRang, rangMondial) = rankingLookup.GetValueOrDefault(c.CombattantID);
            var contratsDto = contratsActifs
                .Where(co => co.CombattantID == c.CombattantID)
                .Select(co => new ContratActifDto(
                    co.ContratID,
                    co.OrganisationID,
                    co.Organisation!.Nom,
                    co.Organisation.Prestige,
                    co.NombreCombats,
                    co.CombatsEffectues,
                    co.NombreCombats - co.CombatsEffectues,
                    co.EstExclusif,
                    co.Statut))
                .ToList();
            AgentDto? agentDto = null;
            if (agentLookup.TryGetValue(c.CombattantID, out var agent))
            {
                int nb = agentFighterCounts.GetValueOrDefault(agent.AgentID);
                agentDto = new AgentDto(agent.AgentID, agent.Prenom, agent.Nom, agent.EstJoueur,
                    agent.CompContact, agent.CompNegociation, agent.CompReseau,
                    agent.CompReputation, agent.CompInfluence, agent.CompMarketing,
                    agent.CompJuridique, agent.SalaireMensuel, agent.MaxCombattants, nb);
            }
            return ToDetailDto(c, references, partie.AnneeActuelle, partie.MoisActuel,
                rivalites, meilleurRang, rangMondial, contratsDto, agentDto);
        }));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CombattantDetailDto>> GetById(int id)
    {
        var partie = await PartieActive();
        var references = await LoadReferenceData();

        var combattant = await db.Combattants
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CombattantID == id);

        if (combattant is null) return NotFound();

        List<Rivalite>       rivalites    = [];
        List<ContratActifDto> contratsDto = [];
        string? meilleurRangOrga = null;
        int?    rangMondial      = null;

        if (partie is not null)
        {
            rivalites = await db.Rivalites
                .Where(r => r.PartieID == partie.PartieID
                         && (r.Combattant1ID == id || r.Combattant2ID == id))
                .Include(r => r.Combattant1)
                .Include(r => r.Combattant2)
                .AsNoTracking()
                .ToListAsync();

            var contratsRaw = await db.ContratsOrganisation
                .Where(co => co.PartieID == partie.PartieID
                          && co.CombattantID == id
                          && co.Statut == "Actif")
                .Include(co => co.Organisation)
                .AsNoTracking()
                .ToListAsync();
            contratsDto = contratsRaw.Select(co => new ContratActifDto(
                co.ContratID,
                co.OrganisationID,
                co.Organisation!.Nom,
                co.Organisation.Prestige,
                co.NombreCombats,
                co.CombatsEffectues,
                co.NombreCombats - co.CombatsEffectues,
                co.EstExclusif,
                co.Statut)).ToList();

            var ceinture = await db.ChampionCeintures
                .Where(c => c.PartieID == partie.PartieID && c.CombattantID == id)
                .Include(c => c.Organisation)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (ceinture is not null)
                meilleurRangOrga = $"🏆 Champion {ceinture.Organisation!.Nom}";
            else
            {
                var best = await db.RankingEntries
                    .Where(r => r.PartieID == partie.PartieID && r.CombattantID == id && r.Rang > 0)
                    .Include(r => r.Organisation)
                    .AsNoTracking()
                    .OrderBy(r => r.Rang)
                    .FirstOrDefaultAsync();
                if (best is not null)
                    meilleurRangOrga = $"#{best.Rang} {best.Organisation!.Nom}";
            }

            var totalPoints = await db.RankingEntries
                .Where(r => r.PartieID == partie.PartieID && r.CombattantID == id)
                .SumAsync(r => r.PointsMondiaux);

            if (totalPoints > 0 && combattant is not null)
            {
                var nbMieux = await db.RankingEntries
                    .Where(r => r.PartieID == partie.PartieID
                             && r.Genre == combattant.Genre
                             && r.CategorieID == combattant.CategorieID)
                    .GroupBy(r => r.CombattantID)
                    .Where(g => g.Sum(r => r.PointsMondiaux) > totalPoints)
                    .CountAsync();
                var rang = nbMieux + 1;
                if (rang <= 15) rangMondial = rang;
            }
        }

        AgentDto? agentDto = null;
        if (partie is not null)
        {
            var cpAgent = await db.CombattantsPartie
                .Where(cp => cp.PartieID == partie.PartieID && cp.CombattantID == id && cp.AgentID != null)
                .Include(cp => cp.Agent)
                .AsNoTracking()
                .FirstOrDefaultAsync();
            if (cpAgent?.Agent is not null)
            {
                int nb = await db.CombattantsPartie
                    .CountAsync(x => x.PartieID == partie.PartieID && x.AgentID == cpAgent.AgentID);
                var a = cpAgent.Agent;
                agentDto = new AgentDto(a.AgentID, a.Prenom, a.Nom, a.EstJoueur,
                    a.CompContact, a.CompNegociation, a.CompReseau,
                    a.CompReputation, a.CompInfluence, a.CompMarketing,
                    a.CompJuridique, a.SalaireMensuel, a.MaxCombattants, nb);
            }
        }

        return Ok(ToDetailDto(combattant!, references,
            partie?.AnneeActuelle ?? DateTime.Today.Year,
            partie?.MoisActuel ?? DateTime.Today.Month,
            rivalites, meilleurRangOrga, rangMondial, contratsDto, agentDto));
    }

    [HttpGet("agents-disponibles")]
    public async Task<ActionResult> GetAgentsDisponibles()
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        var agents = await db.Agents
            .Where(a => a.PartieID == null || a.PartieID == partie.PartieID)
            .ToListAsync();

        var result = new List<AgentDto>();
        foreach (var a in agents)
        {
            int nb = await db.CombattantsPartie
                .CountAsync(cp => cp.PartieID == partie.PartieID && cp.AgentID == a.AgentID);
            result.Add(new AgentDto(a.AgentID, a.Prenom, a.Nom, a.EstJoueur,
                a.CompContact, a.CompNegociation, a.CompReseau,
                a.CompReputation, a.CompInfluence, a.CompMarketing,
                a.CompJuridique, a.SalaireMensuel, a.MaxCombattants, nb));
        }

        return Ok(result.OrderByDescending(a => a.EstJoueur).ThenByDescending(a => a.CompNegociation));
    }

    [HttpPost("{id:int}/assigner-agent")]
    public async Task<ActionResult> AssignerAgent(int id, [FromBody] AssignerAgentRequest req)
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        var cp = await db.CombattantsPartie
            .FirstOrDefaultAsync(cp => cp.PartieID == partie.PartieID && cp.CombattantID == id);
        if (cp is null) return NotFound("Ce combattant n'est pas dans votre écurie.");

        if (req.AgentID is null)
        {
            cp.AgentID = null;
            await db.SaveChangesAsync();
            return Ok(new { message = "Agent retiré. Vous gérez ce combattant directement." });
        }

        var agent = await db.Agents.FindAsync(req.AgentID);
        if (agent is null) return NotFound("Agent introuvable.");

        int nbActuels = await db.CombattantsPartie
            .CountAsync(x => x.PartieID == partie.PartieID && x.AgentID == req.AgentID);
        if (nbActuels >= agent.MaxCombattants)
            return BadRequest($"{agent.Prenom} {agent.Nom} gère déjà {nbActuels} combattants (max {agent.MaxCombattants}).");

        cp.AgentID = req.AgentID;
        await db.SaveChangesAsync();
        return Ok(new { message = $"{agent.Prenom} {agent.Nom} est maintenant l'agent de ce combattant." });
    }

    [HttpPost("{id:int}/recruter")]
    public async Task<IActionResult> Recruter(int id)
    {
        var partie = await db.Parties
            .Where(p => p.UserID == CurrentUserId && p.EstActive)
            .FirstOrDefaultAsync();
        if (partie is null) return NotFound("Aucune partie active.");

        var combattant = await db.Combattants.FindAsync(id);
        if (combattant is null) return NotFound("Combattant introuvable.");

        var dejaRecrute = await db.CombattantsPartie
            .AnyAsync(cp => cp.PartieID == partie.PartieID && cp.CombattantID == id);

        if (dejaRecrute) return BadRequest("Ce combattant est déjà dans ton écurie.");

        // Vérifier les fonds suffisants
        var cout = (decimal)combattant.Valeur;
        if (partie.Argent < cout)
            return BadRequest($"Fonds insuffisants. Coût : {cout:N0} €, Solde : {partie.Argent:N0} €");

        partie.Argent -= cout;

        db.CombattantsPartie.Add(new CombattantPartie
        {
            CombattantID    = id,
            PartieID        = partie.PartieID,
            DateRecrutement = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return Ok(new { nouveauSolde = partie.Argent });
    }

    private async Task<Partie?> PartieActive() =>
        await db.Parties
            .Where(p => p.UserID == CurrentUserId && p.EstActive)
            .FirstOrDefaultAsync();

    private async Task<CombattantReferenceData> LoadReferenceData()
    {
        var pays = await db.Pays
            .AsNoTracking()
            .ToDictionaryAsync(p => p.PaysID);

        var styles = await db.StylesCombat
            .AsNoTracking()
            .ToDictionaryAsync(s => s.StyleID, s => s.Nom);

        var categoriesH = await db.CategoriesPoidsH
            .AsNoTracking()
            .ToDictionaryAsync(c => c.CategorieID, c => c.Nom);

        var categoriesF = await db.CategoriesPoidsF
            .AsNoTracking()
            .ToDictionaryAsync(c => c.CategorieID, c => c.Nom);

        return new CombattantReferenceData(
            pays,
            styles,
            categoriesH,
            categoriesF);
    }

    private static int CalculerAge(DateTime dateNaissance, int anneeJeu, int moisJeu)
    {
        var age = anneeJeu - dateNaissance.Year;
        if (dateNaissance.Month > moisJeu) age--;
        return Math.Max(18, age); // un combattant a au minimum 18 ans
    }

    private static int Moyenne(params int[] valeurs) =>
        valeurs.Length == 0 ? 0 : (int)Math.Round(valeurs.Average());

    private static Pays? GetPays(Combattant c, CombattantReferenceData references) =>
        references.PaysById.TryGetValue(c.PaysOrigineID, out var pays) ? pays : null;

    private static string GetCategoriePoids(Combattant c, CombattantReferenceData references)
    {
        var categories = c.Genre.Equals("F", StringComparison.OrdinalIgnoreCase)
            ? references.CategoriesFById
            : references.CategoriesHById;

        return categories.TryGetValue(c.CategorieID, out var nom) ? nom : "Inconnue";
    }

    private static string GetStylePrincipal(Combattant c, CombattantReferenceData references) =>
        c.StylePrincipalID is int styleId &&
        references.StylesById.TryGetValue(styleId, out var styleNom)
            ? styleNom
            : "Polyvalent";

    private static string? GetBiographie(Combattant c) =>
        string.IsNullOrWhiteSpace(c.Surnom) ? null : $"Surnom : {c.Surnom}";

    private static int GetCompStriking(Combattant c) =>
        Moyenne(c.StatFrappeDebout, c.StatPuissance, c.StatPrecision);

    private static int GetCompLutte(Combattant c) =>
        Moyenne(c.StatWrestling, c.StatTakedown, c.StatAntiTakedown);

    private static int GetCompGrappling(Combattant c) =>
        Moyenne(c.StatJiuJitsu, c.StatSubmission, c.StatEvasionSub);

    private static int GetCompConditioning(Combattant c) =>
        Moyenne(c.StatForce, c.StatVitesse, c.StatAgilite);

    private static int GetCompStamina(Combattant c) =>
        Moyenne(c.StatCardio, c.StatRecuperation, c.StatMentoniere);

    private static int GetCompMental(Combattant c) =>
        Moyenne(c.StatMental, c.StatExperience, c.StatAdaptation);

    private static int NoteGlobale(Combattant c) => c.Overall;

    private static CombattantDetailDto ToDetailDto(
        Combattant c,
        CombattantReferenceData references,
        int anneeJeu, int moisJeu,
        IReadOnlyList<Rivalite> rivalites,
        string? meilleurRangOrga = null,
        int? rangMondial = null,
        IReadOnlyList<ContratActifDto>? contrats = null,
        AgentDto? agentAttitre = null)
    {
        var pays = GetPays(c, references);
        int age  = CalculerAge(c.DateNaissance, anneeJeu, moisJeu);

        string phase = age switch
        {
            < 28  => "Développement",
            <= 33 => "Pic",
            <= 38 => "Déclin",
            _     => "Vétéran"
        };

        byte potentielAffiche = (byte)(c.Potentiel / 5 * 5);

        var rivalitesDto = rivalites
            .Where(r => r.Combattant1ID == c.CombattantID || r.Combattant2ID == c.CombattantID)
            .Select(r =>
            {
                bool isC1 = r.Combattant1ID == c.CombattantID;
                var autre = isC1 ? r.Combattant2! : r.Combattant1!;
                return new RivaliteDto(
                    r.RivaliteID,
                    autre.CombattantID,
                    $"{autre.Prenom} {autre.NomFamille}",
                    r.Intensite,
                    r.NbConfrontations,
                    r.Raison);
            })
            .ToList();

        return new CombattantDetailDto(
            c.CombattantID,
            c.Prenom,
            c.NomFamille,
            pays?.Nom ?? "-",
            pays?.Code ?? "-",
            age,
            c.Genre,
            c.TailleCm,
            c.AllongeCm,
            c.PoidsReelKg,
            GetCategoriePoids(c, references),
            GetStylePrincipal(c, references),
            GetBiographie(c),
            GetCompStriking(c),
            GetCompLutte(c),
            GetCompGrappling(c),
            GetCompConditioning(c),
            GetCompStamina(c),
            GetCompMental(c),
            NoteGlobale(c),
            c.Valeur,
            c.Salaire,
            c.Victoires,
            c.Defaites,
            c.Nuls,
            c.VictoiresKO,
            c.VictoiresSub,
            c.VictoiresDec,
            c.DefaitesKO,
            c.DefaitesSub,
            c.Defaites - c.DefaitesKO - c.DefaitesSub,
            c.StatFrappeDebout,
            c.StatPuissance,
            c.StatPrecision,
            c.StatWrestling,
            c.StatTakedown,
            c.StatAntiTakedown,
            c.StatJiuJitsu,
            c.StatSubmission,
            c.StatEvasionSub,
            c.StatForce,
            c.StatVitesse,
            c.StatAgilite,
            c.StatCardio,
            c.StatRecuperation,
            c.StatMentoniere,
            c.StatMental,
            c.StatExperience,
            c.StatAdaptation,
            c.BlessureGravite,
            c.BlessureZone,
            c.SemainesIndispo,
            phase,
            potentielAffiche,
            rivalitesDto,
            rangMondial,
            meilleurRangOrga,
            contrats ?? [],
            agentAttitre
        );
    }
}
