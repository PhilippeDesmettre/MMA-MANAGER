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
    public async Task<ActionResult<IEnumerable<CombattantListDto>>> GetDisponibles()
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound("Aucune partie active.");

        var recrutes = await db.CombattantsPartie
            .Where(cp => cp.PartieID == partie.PartieID)
            .Select(cp => cp.CombattantID)
            .ToListAsync();

        var references = await LoadReferenceData();

        var combattants = await db.Combattants
            .AsNoTracking()
            .Where(c => !recrutes.Contains(c.CombattantID))
            .OrderBy(c => c.NomFamille)
            .ThenBy(c => c.Prenom)
            .ToListAsync();

        return Ok(combattants.Select(c => ToListDto(c, references)));
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

        return Ok(combattants.Select(c => ToDetailDto(c, references)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CombattantDetailDto>> GetById(int id)
    {
        var combattant = await db.Combattants
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CombattantID == id);

        if (combattant is null) return NotFound();

        var references = await LoadReferenceData();
        return Ok(ToDetailDto(combattant, references));
    }

    [HttpPost("{id:int}/recruter")]
    public async Task<IActionResult> Recruter(int id)
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound("Aucune partie active.");

        var combattant = await db.Combattants.FindAsync(id);
        if (combattant is null) return NotFound("Combattant introuvable.");

        var dejaRecrute = await db.CombattantsPartie
            .AnyAsync(cp => cp.PartieID == partie.PartieID && cp.CombattantID == id);

        if (dejaRecrute) return BadRequest("Ce combattant est deja dans ton ecurie.");

        db.CombattantsPartie.Add(new CombattantPartie
        {
            CombattantID = id,
            PartieID = partie.PartieID,
            DateRecrutement = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return Ok();
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

    private static int CalculerAge(DateTime dateNaissance)
    {
        var today = DateTime.Today;
        var age = today.Year - dateNaissance.Year;

        if (dateNaissance.Date > today.AddYears(-age))
            age--;

        return age;
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

    private static CombattantListDto ToListDto(Combattant c, CombattantReferenceData references)
    {
        var pays = GetPays(c, references);

        return new CombattantListDto(
            c.CombattantID,
            c.Prenom,
            c.NomFamille,
            pays?.Nom ?? "-",
            pays?.Code ?? "-",
            CalculerAge(c.DateNaissance),
            GetCategoriePoids(c, references),
            GetStylePrincipal(c, references),
            NoteGlobale(c),
            c.Valeur,
            c.Salaire
        );
    }

    private static CombattantDetailDto ToDetailDto(Combattant c, CombattantReferenceData references)
    {
        var pays = GetPays(c, references);

        return new CombattantDetailDto(
            c.CombattantID,
            c.Prenom,
            c.NomFamille,
            pays?.Nom ?? "-",
            pays?.Code ?? "-",
            CalculerAge(c.DateNaissance),
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
            c.Salaire
        );
    }
}
