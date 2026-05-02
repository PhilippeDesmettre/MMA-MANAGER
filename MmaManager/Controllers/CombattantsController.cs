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

        return Ok(combattants.Select(c => ToDetailDto(c, references, partie.AnneeActuelle, partie.MoisActuel)));
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

        return Ok(combattants.Select(c => ToDetailDto(c, references, partie.AnneeActuelle, partie.MoisActuel)));
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

        return Ok(ToDetailDto(combattant, references, partie?.AnneeActuelle ?? DateTime.Today.Year, partie?.MoisActuel ?? DateTime.Today.Month));
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

    private static CombattantDetailDto ToDetailDto(Combattant c, CombattantReferenceData references, int anneeJeu, int moisJeu)
    {
        var pays = GetPays(c, references);

        return new CombattantDetailDto(
            c.CombattantID,
            c.Prenom,
            c.NomFamille,
            pays?.Nom ?? "-",
            pays?.Code ?? "-",
            CalculerAge(c.DateNaissance, anneeJeu, moisJeu),
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
            c.SemainesIndispo
        );
    }
}
