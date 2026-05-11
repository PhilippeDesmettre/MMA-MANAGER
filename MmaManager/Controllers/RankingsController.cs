using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmaManager.Data;
using MmaManager.Services;

namespace MmaManager.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RankingsController(MmaContext db, RankingService rankingService) : ControllerBase
{
    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<Models.Partie?> PartieActive() =>
        await db.Parties.FirstOrDefaultAsync(p => p.UserID == CurrentUserId && p.EstActive);

    [HttpGet("orga/{orgId:int}")]
    public async Task<ActionResult> GetClassementOrga(
        int orgId,
        [FromQuery] string genre = "H",
        [FromQuery] int categorieId = 5)
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        var org = await db.CombatOrganisations.FindAsync(orgId);
        if (org is null) return NotFound();

        var catNom = genre == "F"
            ? (await db.CategoriesPoidsF.FindAsync(categorieId))?.Nom ?? "?"
            : (await db.CategoriesPoidsH.FindAsync(categorieId))?.Nom ?? "?";

        var classement = await rankingService.GetClassementOrga(partie.PartieID, orgId, genre, categorieId);

        return Ok(new Models.Dtos.ClassementOrgaDto(
            orgId, org.Nom, org.Prestige, genre, categorieId, catNom, classement));
    }

    [HttpGet("mondial")]
    public async Task<ActionResult> GetClassementMondial(
        [FromQuery] string genre = "H",
        [FromQuery] int categorieId = 5)
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        var classement = await rankingService.GetClassementMondial(partie.PartieID, genre, categorieId);
        return Ok(classement);
    }

    [HttpGet("p4p")]
    public async Task<ActionResult> GetP4P()
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        var classement = await rankingService.GetClassementP4P(partie.PartieID);
        return Ok(classement);
    }

    [HttpGet("organisations")]
    public async Task<ActionResult> GetOrganisations()
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        var orgs = await db.CombatOrganisations
            .Where(o => o.AnneeCreation <= partie.AnneeActuelle
                     && (o.PartieID == null || o.PartieID == partie.PartieID))
            .OrderByDescending(o => o.Prestige)
            .Select(o => new { o.OrganisationID, o.Nom, o.Prestige })
            .ToListAsync();

        return Ok(orgs);
    }

    [HttpGet("organisations/{orgId:int}/categories")]
    public async Task<ActionResult> GetCategoriesOrg(int orgId)
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        var cats = await db.OrganisationCategories
            .Where(oc => oc.OrganisationID == orgId && oc.AnneeIntroduction <= partie.AnneeActuelle)
            .ToListAsync();

        var catsH = await db.CategoriesPoidsH.ToDictionaryAsync(c => c.CategorieID, c => c.Nom);
        var catsF = await db.CategoriesPoidsF.ToDictionaryAsync(c => c.CategorieID, c => c.Nom);

        return Ok(cats.Select(c => new
        {
            c.CategorieID,
            c.Genre,
            c.EstOpenWeight,
            Nom = c.EstOpenWeight ? "Open Weight"
                : (c.Genre == "H" ? catsH.GetValueOrDefault(c.CategorieID, "?")
                                  : catsF.GetValueOrDefault(c.CategorieID, "?"))
        }));
    }

    [HttpGet("categories")]
    public async Task<ActionResult> GetCategories([FromQuery] string genre = "H")
    {
        if (genre == "F")
        {
            var cats = await db.CategoriesPoidsF.OrderBy(c => c.CategorieID).ToListAsync();
            return Ok(cats.Select(c => new { c.CategorieID, c.Nom }));
        }
        else
        {
            var cats = await db.CategoriesPoidsH.OrderBy(c => c.CategorieID).ToListAsync();
            return Ok(cats.Select(c => new { c.CategorieID, c.Nom }));
        }
    }
}
