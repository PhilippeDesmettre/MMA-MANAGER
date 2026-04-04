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
public class EntrainementsController(MmaContext db) : ControllerBase
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
            .AsNoTracking()
            .ToListAsync();

        return Ok(planifies.Select(ep => new EntrainementPlanifieDto(
            ep.EntrainementPlanifieID,
            ep.CombattantID,
            ep.Combattant!.Prenom,
            ep.Combattant!.NomFamille,
            ep.TypeEntrainement
        )));
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

        // Vérifier que le combattant est dans l'écurie
        var dansEcurie = await db.CombattantsPartie
            .AnyAsync(cp => cp.PartieID == partie.PartieID && cp.CombattantID == req.CombattantID);
        if (!dansEcurie) return BadRequest("Ce combattant n'est pas dans ton écurie.");

        // Vérifier la capacité staff (1 staff = 2 entraînements)
        var nbPlanifies = await db.EntrainementsPlanifies
            .CountAsync(ep => ep.PartieID == partie.PartieID);
        const int capaciteStaff = 2; // 1 staff × 2 entraînements
        if (nbPlanifies >= capaciteStaff)
            return BadRequest($"Capacité d'entraînement atteinte ({capaciteStaff} entraînements par tour).");

        // Ajouter ou remplacer l'entraînement du combattant
        var existant = await db.EntrainementsPlanifies
            .FirstOrDefaultAsync(ep => ep.PartieID == partie.PartieID && ep.CombattantID == req.CombattantID);

        if (existant is not null)
        {
            existant.TypeEntrainement = req.TypeEntrainement;
        }
        else
        {
            db.EntrainementsPlanifies.Add(new EntrainementPlanifie
            {
                PartieID         = partie.PartieID,
                CombattantID     = req.CombattantID,
                TypeEntrainement = req.TypeEntrainement
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
        var partie = await db.Parties
            .Where(p => p.UserID == CurrentUserId && p.EstActive)
            .FirstOrDefaultAsync();

        if (partie is null) return NotFound("Aucune partie active.");

        var entraineur = await db.EntraineursJoueur
            .FirstOrDefaultAsync(e => e.PartieID == partie.PartieID);

        if (entraineur is null) return BadRequest("Entraîneur introuvable.");

        var planifies = await db.EntrainementsPlanifies
            .Where(ep => ep.PartieID == partie.PartieID)
            .Include(ep => ep.Combattant)
            .ToListAsync();

        var rng = new Random();
        var resultats = new List<CombattantResultatDto>();

        foreach (var ep in planifies)
        {
            var c = ep.Combattant!;
            var gains = AppliquerEntrainement(c, ep.TypeEntrainement, entraineur, rng);
            resultats.Add(new CombattantResultatDto(
                c.CombattantID,
                c.Prenom,
                c.NomFamille,
                ep.TypeEntrainement,
                gains
            ));
        }

        // Persister les changements de stats
        await db.SaveChangesAsync();

        // Supprimer les entraînements planifiés
        db.EntrainementsPlanifies.RemoveRange(planifies);

        // Avancer le tour
        var tourJoue = partie.TourActuel;
        partie.TourActuel++;
        partie.MoisActuel++;
        if (partie.MoisActuel > 12)
        {
            partie.MoisActuel = 1;
            partie.AnneeActuelle++;
        }

        await db.SaveChangesAsync();

        return Ok(new TourResultatDto(tourJoue, partie.MoisActuel, partie.AnneeActuelle, resultats));
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<Partie?> PartieActive() =>
        await db.Parties
            .Where(p => p.UserID == CurrentUserId && p.EstActive)
            .FirstOrDefaultAsync();

    private static int CalculGain(int trainerComp, Random rng)
    {
        var factor = rng.Next(1, 4); // 1, 2 ou 3
        return Math.Max(1, (int)Math.Round(factor * trainerComp / 50.0));
    }

    private static byte ClampByte(int val) => (byte)Math.Min(100, Math.Max(0, val));

    private static IReadOnlyList<GainStatDto> AppliquerEntrainement(
        Combattant c, string type, EntraineurJoueur e, Random rng)
    {
        var gains = new List<GainStatDto>();

        switch (type)
        {
            case "Striking":
                gains.Add(Apply(c, "Frappe debout", e.CompStriking, rng,
                    () => c.StatFrappeDebout, v => c.StatFrappeDebout = v));
                gains.Add(Apply(c, "Puissance",     e.CompStriking, rng,
                    () => c.StatPuissance,    v => c.StatPuissance    = v));
                gains.Add(Apply(c, "Précision",     e.CompStriking, rng,
                    () => c.StatPrecision,    v => c.StatPrecision    = v));
                break;

            case "Lutte":
                gains.Add(Apply(c, "Wrestling",     e.CompLutte, rng,
                    () => c.StatWrestling,    v => c.StatWrestling    = v));
                gains.Add(Apply(c, "Takedown",      e.CompLutte, rng,
                    () => c.StatTakedown,     v => c.StatTakedown     = v));
                gains.Add(Apply(c, "Anti-takedown", e.CompLutte, rng,
                    () => c.StatAntiTakedown, v => c.StatAntiTakedown = v));
                break;

            case "Grappling":
                gains.Add(Apply(c, "Jiu-Jitsu",  e.CompGrappling, rng,
                    () => c.StatJiuJitsu,   v => c.StatJiuJitsu   = v));
                gains.Add(Apply(c, "Submission", e.CompGrappling, rng,
                    () => c.StatSubmission, v => c.StatSubmission  = v));
                gains.Add(Apply(c, "Évasion sub", e.CompGrappling, rng,
                    () => c.StatEvasionSub, v => c.StatEvasionSub  = v));
                break;

            case "Conditionnement":
                gains.Add(Apply(c, "Force",   e.CompConditioning, rng,
                    () => c.StatForce,   v => c.StatForce   = v));
                gains.Add(Apply(c, "Vitesse", e.CompConditioning, rng,
                    () => c.StatVitesse, v => c.StatVitesse = v));
                gains.Add(Apply(c, "Agilité", e.CompConditioning, rng,
                    () => c.StatAgilite, v => c.StatAgilite = v));
                gains.Add(Apply(c, "Cardio",  e.CompConditioning, rng,
                    () => c.StatCardio,  v => c.StatCardio  = v));
                gains.Add(Apply(c, "Récupération", e.CompConditioning, rng,
                    () => c.StatRecuperation, v => c.StatRecuperation = v));
                break;

            case "Mental":
                gains.Add(Apply(c, "Mental",      e.CompMental, rng,
                    () => c.StatMental,      v => c.StatMental      = v));
                gains.Add(Apply(c, "Expérience",  e.CompMental, rng,
                    () => c.StatExperience,  v => c.StatExperience  = v));
                gains.Add(Apply(c, "Adaptation",  e.CompMental, rng,
                    () => c.StatAdaptation,  v => c.StatAdaptation  = v));
                break;
        }

        return gains.Where(g => g.Gain > 0).ToList();
    }

    private static GainStatDto Apply(
        Combattant c,
        string nom,
        int trainerComp,
        Random rng,
        Func<byte> getter,
        Action<byte> setter)
    {
        var gain = CalculGain(trainerComp, rng);
        var ancien = getter();
        var nouveau = ClampByte(ancien + gain);
        setter(nouveau);
        return new GainStatDto(nom, nouveau - ancien);
    }
}
