using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmaManager.Data;
using MmaManager.Models;
using MmaManager.Models.Dtos;
using MmaManager.Services;

namespace MmaManager.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PartieController(MmaContext db, ProspectGenerationService prospectService) : ControllerBase
{
    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/partie/current
    [HttpGet("current")]
    public async Task<ActionResult<PartieDto>> GetCurrent()
    {
        var partie = await db.Parties
            .Include(p => p.Entraineur)
                .ThenInclude(e => e!.Background)
            .Where(p => p.UserID == CurrentUserId && p.EstActive)
            .FirstOrDefaultAsync();

        if (partie is null) return NotFound();

        return Ok(await ToDto(partie));
    }

    // POST /api/partie
    [HttpPost]
    public async Task<ActionResult<PartieDto>> Create(CreatePartieRequest req)
    {
        var validEpoques = new[] { "NoRules", "GoldenAge", "Modern" };
        if (!validEpoques.Contains(req.Epoque))
            return BadRequest("Époque invalide.");

        var background = await db.Backgrounds.FindAsync(req.BackgroundID);
        if (background is null) return BadRequest("Background introuvable.");

        // Désactiver la partie existante
        var existing = await db.Parties
            .Where(p => p.UserID == CurrentUserId && p.EstActive)
            .FirstOrDefaultAsync();

        if (existing is not null)
            existing.EstActive = false;

        // Créer la nouvelle partie
        var anneeDepart = req.Epoque switch
        {
            "GoldenAge" => 2000,
            "Modern"    => 2013,
            _           => 1985
        };

        var partie = new Partie
        {
            UserID        = CurrentUserId,
            Epoque        = req.Epoque,
            Argent        = 50_000m,    // budget de départ (beta)
            TourActuel    = 1,
            MoisActuel    = 1,
            AnneeActuelle = anneeDepart
        };
        db.Parties.Add(partie);
        await db.SaveChangesAsync(); // Nécessaire pour obtenir le PartieID

        // Créer l'entraîneur avec les stats calculées
        static int Stat(int bonus) => Math.Min(99, 30 + bonus);

        var entraineur = new EntraineurJoueur
        {
            PartieID        = partie.PartieID,
            Prenom          = req.Prenom,
            Nom             = req.Nom,
            PaysOrigineID   = req.PaysOrigineID,
            PaysResidenceID = req.PaysResidenceID,
            BackgroundID    = req.BackgroundID,
            CompStriking     = Stat(background.BonusStriking),
            CompLutte        = Stat(background.BonusLutte),
            CompGrappling    = Stat(background.BonusGrappling),
            CompConditioning = Stat(background.BonusConditioning),
            CompStamina      = Stat(background.BonusStamina),
            CompMental       = Stat(background.BonusMental),
            CompStrategie    = Stat(background.BonusStrategie),
            CompNegociation  = Stat(background.BonusNegociation),
            CompMotivation   = Stat(background.BonusMotivation),
        };
        db.EntraineursJoueur.Add(entraineur);

        // Créer l'agent joueur (le joueur est son propre agent par défaut)
        // BonusNegociation du background s'applique à CompNegociation
        var agent = new MmaManager.Models.Agent
        {
            PartieID       = partie.PartieID,
            EstJoueur      = true,
            Prenom         = req.Prenom,
            Nom            = req.Nom,
            CompNegociation = Math.Min(99, 30 + background.BonusNegociation),
            CompContact    = 30,
            CompReseau     = 30,
            CompReputation = 30,
            CompInfluence  = 30,
            CompMarketing  = 30,
            CompJuridique  = 30,
        };
        db.Agents.Add(agent);

        await db.SaveChangesAsync();

        await prospectService.GenererProspects(req.PaysResidenceID, anneeDepart);
        await prospectService.GenererOrganisationsLocales(req.PaysResidenceID, anneeDepart);

        // Recharger avec navigations
        partie.Entraineur = entraineur;
        entraineur.Background = background;

        return CreatedAtAction(nameof(GetCurrent), await ToDto(partie));
    }

    // GET /api/partie/all — toutes les parties de l'utilisateur
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        var parties = await db.Parties
            .Include(p => p.Entraineur)
                .ThenInclude(e => e!.Background)
            .Where(p => p.UserID == CurrentUserId)
            .OrderByDescending(p => p.DateDerniereConnexion)
            .AsNoTracking()
            .ToListAsync();

        return Ok(parties.Select(p => new
        {
            p.PartieID,
            p.Epoque,
            p.Argent,
            p.TourActuel,
            p.MoisActuel,
            p.AnneeActuelle,
            p.EstActive,
            p.DateCreation,
            p.DateDerniereConnexion,
            EntraineurPrenom = p.Entraineur?.Prenom ?? "—",
            EntraineurNom    = p.Entraineur?.Nom ?? "—",
            BackgroundIcone  = p.Entraineur?.Background?.Icone ?? "",
            BackgroundNom    = p.Entraineur?.Background?.Nom ?? "",
        }));
    }

    // POST /api/partie/{id}/charger — réactiver une ancienne partie
    [HttpPost("{id:int}/charger")]
    public async Task<IActionResult> Charger(int id)
    {
        var partie = await db.Parties
            .FirstOrDefaultAsync(p => p.PartieID == id && p.UserID == CurrentUserId);

        if (partie is null) return NotFound("Partie introuvable.");

        // Désactiver toute partie active
        var active = await db.Parties
            .Where(p => p.UserID == CurrentUserId && p.EstActive)
            .ToListAsync();

        foreach (var a in active) a.EstActive = false;

        // Réactiver la partie choisie
        partie.EstActive = true;
        partie.DateDerniereConnexion = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok();
    }

    // GET /api/partie/finances
    [HttpGet("finances")]
    public async Task<ActionResult<FinancesDto>> GetFinances()
    {
        var partie = await db.Parties
            .Where(p => p.UserID == CurrentUserId && p.EstActive)
            .FirstOrDefaultAsync();

        if (partie is null) return NotFound();

        var ecurie = await db.CombattantsPartie
            .Where(cp => cp.PartieID == partie.PartieID)
            .Include(cp => cp.Combattant)
            .AsNoTracking()
            .ToListAsync();

        var combattantsDto = ecurie.Select(cp => new FinancesCombattantDto(
            cp.CombattantID,
            cp.Combattant!.Prenom,
            cp.Combattant!.NomFamille,
            cp.Combattant!.Salaire
        )).ToList();

        var staffEmbauches = await db.StaffParties
            .Where(sp => sp.PartieID == partie.PartieID)
            .Include(sp => sp.StaffDisponible)
            .AsNoTracking()
            .ToListAsync();

        decimal salairesTotaux = combattantsDto.Sum(c => c.Salaire);
        decimal loyer          = 1m;
        decimal staffTotal     = staffEmbauches.Sum(sp => sp.StaffDisponible!.SalaireMensuel);
        decimal depenses       = salairesTotaux + loyer + staffTotal;

        return Ok(new FinancesDto(
            partie.Argent,
            loyer,
            salairesTotaux,
            staffTotal,
            depenses,
            partie.Argent - depenses,
            combattantsDto
        ));
    }

    // ── Helper ────────────────────────────────────────────────
    private async Task<PartieDto> ToDto(Partie p)
    {
        var e = p.Entraineur!;
        var bg = e.Background!;

        var paysOrigine   = await db.Pays.FindAsync(e.PaysOrigineID);
        var paysResidence = await db.Pays.FindAsync(e.PaysResidenceID);

        var stats = new List<StatTrainerDto>
        {
            new("Frappe debout", "striking",     e.CompStriking),
            new("Wrestling",     "lutte",        e.CompLutte),
            new("Grappling",     "grappling",    e.CompGrappling),
            new("Conditionnement","conditioning",e.CompConditioning),
            new("Endurance",     "stamina",      e.CompStamina),
            new("Mental",        "mental",       e.CompMental),
            new("Stratégie",     "strategie",    e.CompStrategie),
            new("Négociation",   "negociation",  e.CompNegociation),
            new("Motivation",    "motivation",   e.CompMotivation),
        };

        return new PartieDto(
            p.PartieID,
            p.Epoque,
            p.Argent,
            p.DateCreation,
            p.TourActuel,
            p.MoisActuel,
            p.AnneeActuelle,
            p.PrestigeEcurie,
            new EntraineurDto(
                e.Prenom,
                e.Nom,
                paysOrigine?.Nom   ?? "—",
                paysResidence?.Nom ?? "—",
                paysResidence?.Code,
                bg.Nom,
                bg.Description,
                bg.Icone,
                stats
            )
        );
    }
}
