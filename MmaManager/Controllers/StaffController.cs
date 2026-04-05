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
[Route("api/staff")]
[Authorize]
public class StaffController(MmaContext db) : ControllerBase
{
    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/staff — tous les staff disponibles avec statut embauché
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StaffDisponibleDto>>> GetAll()
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        var disponibles = await db.StaffDisponibles
            .OrderBy(s => s.Role)
            .ThenByDescending(s => s.Overall)
            .AsNoTracking()
            .ToListAsync();

        // Staff déjà embauché dans cette partie
        var embauches = await db.StaffParties
            .Where(sp => sp.PartieID == partie.PartieID)
            .AsNoTracking()
            .ToListAsync();

        var embaucheMap = embauches.ToDictionary(sp => sp.StaffDisponibleID, sp => sp.StaffPartieID);

        return Ok(disponibles.Select(s =>
        {
            bool embauche = embaucheMap.TryGetValue(s.StaffDisponibleID, out var spid);
            return new StaffDisponibleDto(
                s.StaffDisponibleID,
                s.Prenom,
                s.Nom,
                s.Role,
                s.Icone,
                s.Description,
                s.CompStriking,
                s.CompLutte,
                s.CompGrappling,
                s.CompConditioning,
                s.CompMental,
                s.Overall,
                s.SalaireMensuel,
                s.Nationalite,
                embauche,
                embauche ? spid : null
            );
        }));
    }

    // GET /api/staff/embauches — staff embauché dans la partie courante
    [HttpGet("embauches")]
    public async Task<ActionResult<IEnumerable<StaffPartieDto>>> GetEmbauches()
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        var embauches = await db.StaffParties
            .Where(sp => sp.PartieID == partie.PartieID)
            .Include(sp => sp.StaffDisponible)
            .AsNoTracking()
            .ToListAsync();

        return Ok(embauches.Select(sp => new StaffPartieDto(
            sp.StaffPartieID,
            sp.StaffDisponibleID,
            sp.StaffDisponible!.Prenom,
            sp.StaffDisponible!.Nom,
            sp.StaffDisponible!.Role,
            sp.StaffDisponible!.Icone,
            sp.StaffDisponible!.CompStriking,
            sp.StaffDisponible!.CompLutte,
            sp.StaffDisponible!.CompGrappling,
            sp.StaffDisponible!.CompConditioning,
            sp.StaffDisponible!.CompMental,
            sp.StaffDisponible!.Overall,
            sp.StaffDisponible!.SalaireMensuel
        )));
    }

    // POST /api/staff/{staffDisponibleID}/embaucher
    [HttpPost("{staffDisponibleID:int}/embaucher")]
    public async Task<IActionResult> Embaucher(int staffDisponibleID)
    {
        var partie = await db.Parties
            .Where(p => p.UserID == CurrentUserId && p.EstActive)
            .FirstOrDefaultAsync();
        if (partie is null) return NotFound();

        var staff = await db.StaffDisponibles.FindAsync(staffDisponibleID);
        if (staff is null) return BadRequest("Staff introuvable.");

        // Vérifier qu'il n'est pas déjà embauché
        var dejaEmbauche = await db.StaffParties
            .AnyAsync(sp => sp.PartieID == partie.PartieID && sp.StaffDisponibleID == staffDisponibleID);
        if (dejaEmbauche) return BadRequest("Ce membre du staff est déjà dans ton écurie.");

        // Vérifier les fonds
        if (partie.Argent < staff.SalaireMensuel)
            return BadRequest($"Fonds insuffisants. Salaire mensuel : {staff.SalaireMensuel:N0} €.");

        db.StaffParties.Add(new StaffPartie
        {
            PartieID          = partie.PartieID,
            StaffDisponibleID = staffDisponibleID,
            DateEmbauche      = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        return Ok();
    }

    // DELETE /api/staff/embauches/{staffPartieID}
    [HttpDelete("embauches/{staffPartieID:int}")]
    public async Task<IActionResult> Licencier(int staffPartieID)
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        var sp = await db.StaffParties
            .FirstOrDefaultAsync(sp => sp.StaffPartieID == staffPartieID && sp.PartieID == partie.PartieID);

        if (sp is null) return NotFound();

        // Nullifier les entraînements planifiés qui référencent ce staff (cascade manuelle)
        var entrainementsLies = await db.EntrainementsPlanifies
            .Where(ep => ep.StaffPartieID == staffPartieID)
            .ToListAsync();
        foreach (var ep in entrainementsLies)
            ep.StaffPartieID = null;

        db.StaffParties.Remove(sp);
        await db.SaveChangesAsync();
        return Ok();
    }

    private async Task<Partie?> PartieActive() =>
        await db.Parties
            .Where(p => p.UserID == CurrentUserId && p.EstActive)
            .FirstOrDefaultAsync();
}
