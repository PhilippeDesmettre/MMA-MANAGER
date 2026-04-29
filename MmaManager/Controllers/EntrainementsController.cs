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
public class EntrainementsController(MmaContext db, TurnAdvancementService turnService) : ControllerBase
{
    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/entrainements/planifies
    [HttpGet("planifies")]
    public async Task<ActionResult<IEnumerable<EntrainementPlanifieDto>>> GetPlanifies()
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound("Aucune partie active.");

        var planifies = await db.EntrainementsPlanifies
            .Where(ep => ep.PartieID == partie.PartieID)
            .Include(ep => ep.Combattant)
            .Include(ep => ep.EntraineurJoueur)
            .Include(ep => ep.StaffPartie!)
                .ThenInclude(sp => sp.StaffDisponible!)
            .AsNoTracking()
            .ToListAsync();

        var entraineurDefaut = await db.EntraineursJoueur
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.PartieID == partie.PartieID);

        return Ok(planifies.Select(ep =>
        {
            string coachPrenom, coachNom;
            if (ep.StaffPartie?.StaffDisponible is { } sd)
            {
                coachPrenom = sd.Prenom;
                coachNom    = sd.Nom;
            }
            else
            {
                var coach = ep.EntraineurJoueur ?? entraineurDefaut;
                coachPrenom = coach?.Prenom ?? "—";
                coachNom    = coach?.Nom    ?? "—";
            }

            return new EntrainementPlanifieDto(
                ep.EntrainementPlanifieID,
                ep.CombattantID,
                ep.Combattant!.Prenom,
                ep.Combattant!.NomFamille,
                ep.TypeEntrainement,
                ep.EntraineurJoueurID ?? entraineurDefaut?.EntraineurJoueurID,
                ep.StaffPartieID,
                coachPrenom,
                coachNom
            );
        }));
    }

    // GET /api/entrainements/staff — liste unifiée (entraîneur joueur + staff embauché)
    [HttpGet("staff")]
    public async Task<ActionResult<IEnumerable<CoachDto>>> GetStaff()
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound("Aucune partie active.");

        var coaches = new List<CoachDto>();

        // ── Entraîneur joueur ──────────────────────────────────────
        var entraineur = await db.EntraineursJoueur
            .Include(e => e.Background)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.PartieID == partie.PartieID);

        if (entraineur is not null)
        {
            coaches.Add(new CoachDto(
                entraineur.EntraineurJoueurID,
                "Joueur",
                entraineur.Prenom,
                entraineur.Nom,
                entraineur.Background?.Icone ?? "🏋️",
                entraineur.CompStriking,
                entraineur.CompLutte,
                entraineur.CompGrappling,
                entraineur.CompConditioning,
                entraineur.CompMental
            ));
        }

        // ── Staff embauché ─────────────────────────────────────────
        var staffEmbauches = await db.StaffParties
            .Where(sp => sp.PartieID == partie.PartieID)
            .Include(sp => sp.StaffDisponible)
            .AsNoTracking()
            .ToListAsync();

        foreach (var sp in staffEmbauches)
        {
            var s = sp.StaffDisponible!;
            coaches.Add(new CoachDto(
                sp.StaffPartieID,
                "Staff",
                s.Prenom,
                s.Nom,
                s.Icone ?? "👥",
                s.CompStriking,
                s.CompLutte,
                s.CompGrappling,
                s.CompConditioning,
                s.CompMental
            ));
        }

        return Ok(coaches);
    }

    // POST /api/entrainements/planifier
    [HttpPost("planifier")]
    public async Task<IActionResult> Planifier(PlanifierEntrainementRequest req)
    {
        var typesValides = new[] { "Striking", "Lutte", "Grappling", "Conditionnement", "Mental" };
        if (!typesValides.Contains(req.TypeEntrainement))
            return BadRequest("Type d'entraînement invalide.");

        var partie = await PartieActive();
        if (partie is null) return NotFound("Aucune partie active.");

        var dansEcurie = await db.CombattantsPartie
            .AnyAsync(cp => cp.PartieID == partie.PartieID && cp.CombattantID == req.CombattantID);
        if (!dansEcurie) return BadRequest("Ce combattant n'est pas dans ton écurie.");

        // ── Résoudre le coach ────────────────────────────────────
        int? entraineurId  = req.EntraineurJoueurID;
        int? staffPartieId = req.StaffPartieID;

        if (entraineurId is null && staffPartieId is null)
        {
            entraineurId = await db.EntraineursJoueur
                .Where(e => e.PartieID == partie.PartieID)
                .Select(e => (int?)e.EntraineurJoueurID)
                .FirstOrDefaultAsync();
        }

        // ── Capacité dynamique : (1 entraîneur joueur + nb staff embauché) × 2 ─
        var nbStaffEmbauche = await db.StaffParties
            .CountAsync(sp => sp.PartieID == partie.PartieID);
        int capacite = (1 + nbStaffEmbauche) * 2;

        var nbPlanifies = await db.EntrainementsPlanifies
            .CountAsync(ep => ep.PartieID == partie.PartieID);

        var existant = await db.EntrainementsPlanifies
            .FirstOrDefaultAsync(ep => ep.PartieID == partie.PartieID && ep.CombattantID == req.CombattantID);

        if (existant is null && nbPlanifies >= capacite)
            return BadRequest($"Capacité d'entraînement atteinte ({capacite} entraînements par tour).");

        if (existant is not null)
        {
            existant.TypeEntrainement   = req.TypeEntrainement;
            existant.EntraineurJoueurID = entraineurId;
            existant.StaffPartieID      = staffPartieId;
        }
        else
        {
            db.EntrainementsPlanifies.Add(new EntrainementPlanifie
            {
                PartieID            = partie.PartieID,
                CombattantID        = req.CombattantID,
                TypeEntrainement    = req.TypeEntrainement,
                EntraineurJoueurID  = entraineurId,
                StaffPartieID       = staffPartieId
            });
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    // DELETE /api/entrainements/{id}/annuler
    [HttpDelete("{id:int}/annuler")]
    public async Task<IActionResult> Annuler(int id)
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound("Aucune partie active.");

        var ep = await db.EntrainementsPlanifies
            .FirstOrDefaultAsync(e => e.EntrainementPlanifieID == id && e.PartieID == partie.PartieID);

        if (ep is null) return NotFound();

        db.EntrainementsPlanifies.Remove(ep);
        await db.SaveChangesAsync();
        return Ok();
    }

    // POST /api/entrainements/avancer-tour
    [HttpPost("avancer-tour")]
    public async Task<ActionResult<TourResultatDto>> AvancerTour()
    {
        var result = await turnService.AvancerTour(CurrentUserId);
        if (result is null) return NotFound("Aucune partie active.");
        return Ok(result);
    }

    private async Task<Partie?> PartieActive() =>
        await db.Parties
            .Where(p => p.UserID == CurrentUserId && p.EstActive)
            .FirstOrDefaultAsync();
}
