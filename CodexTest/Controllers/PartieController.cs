using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodexTest.Data;
using CodexTest.Models;
using CodexTest.Models.Dtos;

namespace CodexTest.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PartieController(MmaContext db) : ControllerBase
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
        var partie = new Partie
        {
            UserID = CurrentUserId,
            Epoque = req.Epoque
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
        await db.SaveChangesAsync();

        // Recharger avec navigations
        partie.Entraineur = entraineur;
        entraineur.Background = background;

        return CreatedAtAction(nameof(GetCurrent), await ToDto(partie));
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
            new EntraineurDto(
                e.Prenom,
                e.Nom,
                paysOrigine?.Nom   ?? "—",
                paysResidence?.Nom ?? "—",
                bg.Nom,
                bg.Description,
                bg.Icone,
                stats
            )
        );
    }
}
