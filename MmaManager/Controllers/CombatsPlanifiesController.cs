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
[Route("api/combats-planifies")]
[Authorize]
public class CombatsPlanifiesController(MmaContext db) : ControllerBase
{
    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/combats-planifies
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CombatPlanifieDto>>> GetAll()
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        var combats = await db.CombatsPlanifies
            .Where(cp => cp.PartieID == partie.PartieID && cp.Statut == "Planifie")
            .Include(cp => cp.Combattant)
            .Include(cp => cp.Adversaire)
            .Include(cp => cp.Organisation)
            .AsNoTracking()
            .ToListAsync();

        return Ok(combats.Select(cp => new CombatPlanifieDto(
            cp.CombatPlanifieID,
            cp.CombattantID,
            cp.Combattant!.Prenom,
            cp.Combattant!.NomFamille,
            cp.AdversaireID,
            cp.Adversaire!.Prenom,
            cp.Adversaire!.NomFamille,
            cp.Organisation!.Nom,
            cp.TourPrevu,
            cp.Statut,
            cp.Gameplan
        )));
    }

    // GET /api/combats-planifies/organisations
    [HttpGet("organisations")]
    public async Task<ActionResult<IEnumerable<OrganisationDto>>> GetOrganisations()
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        var entraineur = await db.EntraineursJoueur
            .FirstOrDefaultAsync(e => e.PartieID == partie.PartieID);
        var paysResidenceId = entraineur?.PaysResidenceID;

        var orgs = await db.CombatOrganisations
            .Where(o => o.AnneeCreation <= partie.AnneeActuelle
                     && o.Prestige <= partie.PrestigeEcurie + 1
                     && (o.PartieID == null || o.PartieID == partie.PartieID)
                     && (o.Prestige >= 3
                         || o.PaysOrigineID == null
                         || o.PaysOrigineID == paysResidenceId))
            .OrderByDescending(o => o.Prestige)
            .ThenBy(o => o.Nom)
            .AsNoTracking()
            .ToListAsync();

        return Ok(orgs.Select(o =>
        {
            // Bourses basées sur le prestige de l'organisation
            // Prestige 1: 500-1500 (victoire), 200-500 (défaite)
            // Prestige 5: 15000-50000 (victoire), 5000-15000 (défaite)
            int bourseVMin = o.Prestige * o.Prestige * 500;
            int bourseVMax = o.Prestige * o.Prestige * 2000;
            int bourseDMin = (int)(bourseVMin * 0.3);
            int bourseDMax = (int)(bourseVMax * 0.3);
            return new OrganisationDto(
                o.OrganisationID, o.Nom, o.AnneeCreation, o.Prestige,
                o.Description, o.EstFictive,
                bourseVMin, bourseVMax, bourseDMin, bourseDMax
            );
        }));
    }

    // GET /api/combats-planifies/organisations/{orgId}/categories
    [HttpGet("organisations/{orgId:int}/categories")]
    public async Task<ActionResult> GetCategoriesOrga(int orgId)
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

    // GET /api/combats-planifies/adversaires/{combattantID}?organisationID=X&openWeight=false
    [HttpGet("adversaires/{combattantID:int}")]
    public async Task<ActionResult<IEnumerable<AdversaireDto>>> GetAdversaires(
        int combattantID,
        [FromQuery] int?  organisationID = null,
        [FromQuery] bool  openWeight     = false)
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        // Récupère la catégorie du combattant pour filtrer les adversaires compatibles
        var combattant = await db.Combattants.FindAsync(combattantID);
        if (combattant is null) return NotFound();

        // Exclure les combattants de l'écurie du joueur comme adversaires
        var ecurie = await db.CombattantsPartie
            .Where(cp => cp.PartieID == partie.PartieID)
            .Select(cp => cp.CombattantID)
            .ToListAsync();

        // Référence catégories
        var categoriesH = await db.CategoriesPoidsH.ToDictionaryAsync(c => c.CategorieID, c => c.Nom);
        var categoriesF = await db.CategoriesPoidsF.ToDictionaryAsync(c => c.CategorieID, c => c.Nom);
        var styles      = await db.StylesCombat.ToDictionaryAsync(s => s.StyleID, s => s.Nom);

        // Déterminer le prestige de l'organisation pour filtrer le niveau des adversaires
        int prestigeOrg = 3; // défaut moyen
        if (organisationID.HasValue)
        {
            var org = await db.CombatOrganisations.FindAsync(organisationID.Value);
            if (org is not null) prestigeOrg = org.Prestige;
        }

        // Fourchette d'overall basée sur le prestige de l'organisation
        int overallMin = Math.Max(0, prestigeOrg * 12 + 8);
        int overallMax = Math.Min(99, prestigeOrg * 12 + 43);

        var adversaires = await db.Combattants
            .Where(c => !ecurie.Contains(c.CombattantID)
                     && c.Genre       == combattant.Genre
                     && c.Overall     >= overallMin
                     && c.Overall     <= overallMax
                     && (openWeight || c.CategorieID == combattant.CategorieID))
            .OrderByDescending(c => c.Overall)
            .Take(15)
            .AsNoTracking()
            .ToListAsync();

        static int Moyenne(params int[] v) => v.Length == 0 ? 0 : (int)Math.Round(v.Average());

        return Ok(adversaires.Select(c =>
        {
            var cats = c.Genre == "F" ? categoriesF : categoriesH;
            var cat  = cats.TryGetValue(c.CategorieID, out var n) ? n : "Inconnue";
            var sty  = c.StylePrincipalID.HasValue && styles.TryGetValue(c.StylePrincipalID.Value, out var s) ? s : "Polyvalent";
            return new AdversaireDto(
                c.CombattantID, c.Prenom, c.NomFamille, cat, sty,
                c.TailleCm, c.AllongeCm, c.PoidsReelKg,
                c.Overall,
                Moyenne(c.StatFrappeDebout, c.StatPuissance, c.StatPrecision),
                Moyenne(c.StatWrestling, c.StatTakedown, c.StatAntiTakedown),
                Moyenne(c.StatJiuJitsu, c.StatSubmission, c.StatEvasionSub),
                Moyenne(c.StatForce, c.StatVitesse, c.StatAgilite),
                Moyenne(c.StatCardio, c.StatRecuperation, c.StatMentoniere),
                Moyenne(c.StatMental, c.StatExperience, c.StatAdaptation),
                c.Victoires, c.Defaites, c.Nuls
            );
        }));
    }

    // POST /api/combats-planifies
    [HttpPost]
    public async Task<IActionResult> Planifier(PlanifierCombatRequest req)
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        // Vérifier que le combattant est dans l'écurie
        var dansEcurie = await db.CombattantsPartie
            .AnyAsync(cp => cp.PartieID == partie.PartieID && cp.CombattantID == req.CombattantID);
        if (!dansEcurie) return BadRequest("Ce combattant n'est pas dans ton écurie.");

        var combattant = await db.Combattants.FindAsync(req.CombattantID);
        if (combattant is null) return BadRequest("Combattant introuvable.");
        if (combattant.SemainesIndispo > 0)
            return BadRequest($"{combattant.Prenom} {combattant.NomFamille} est blessé ({combattant.BlessureZone}) et indisponible pendant encore {combattant.SemainesIndispo} tour(s).");

        // Vérifier que l'adversaire existe
        var adversaire = await db.Combattants.FindAsync(req.AdversaireID);
        if (adversaire is null) return BadRequest("Adversaire introuvable.");

        // Vérifier que l'organisation est disponible et accessible
        var org = await db.CombatOrganisations.FindAsync(req.OrganisationID);
        if (org is null) return BadRequest("Organisation non disponible.");

        if (org.PartieID is not null && org.PartieID != partie.PartieID)
            return BadRequest("Cette organisation n'est pas accessible.");

        if (org.AnneeCreation > partie.AnneeActuelle)
            return BadRequest("Cette organisation n'existe pas encore.");

        if (org.Prestige > partie.PrestigeEcurie + 1)
            return BadRequest("Votre écurie n'a pas assez de prestige pour cette organisation.");

        if (org.Prestige <= 2)
        {
            var entraineur = await db.EntraineursJoueur
                .FirstOrDefaultAsync(e => e.PartieID == partie.PartieID);

            if (entraineur is not null && org.PaysOrigineID is not null
                && org.PaysOrigineID != entraineur.PaysResidenceID)
                return BadRequest("Votre écurie n'a pas encore la visibilité internationale pour cette organisation.");
        }

        // Un combattant ne peut avoir qu'un combat planifié à la fois
        var combatExistant = await db.CombatsPlanifies
            .AnyAsync(cp => cp.PartieID    == partie.PartieID
                         && cp.CombattantID == req.CombattantID
                         && cp.Statut       == "Planifie");
        if (combatExistant) return BadRequest("Ce combattant a déjà un combat planifié.");

        // Vérifier si un contrat exclusif empêche de combattre dans cette organisation
        var contratExclusif = await db.ContratsOrganisation
            .FirstOrDefaultAsync(co => co.PartieID     == partie.PartieID
                                    && co.CombattantID == req.CombattantID
                                    && co.EstExclusif
                                    && co.Statut       == "Actif");

        if (contratExclusif is not null && contratExclusif.OrganisationID != req.OrganisationID)
            return BadRequest($"Ce combattant est sous contrat exclusif. Il doit encore effectuer {contratExclusif.NombreCombats - contratExclusif.CombatsEffectues} combat(s) avec son organisation actuelle.");

        // Créer un contrat d'organisation si aucun contrat actif avec cette org
        var contratActif = await db.ContratsOrganisation
            .FirstOrDefaultAsync(co => co.PartieID       == partie.PartieID
                                    && co.CombattantID   == req.CombattantID
                                    && co.OrganisationID == req.OrganisationID
                                    && co.Statut         == "Actif");

        if (contratActif is null)
        {
            // Déterminer le type de contrat selon l'ère
            int nbCombats = 1;
            bool exclusif = false;

            if (partie.Epoque != "NoRules")
            {
                // GoldenAge et Modern : contrats multi-combats possibles, surtout pour les orgs de prestige élevé
                if (org.Prestige >= 4)
                {
                    nbCombats = 3;
                    exclusif = true;
                }
                else if (org.Prestige >= 3)
                {
                    nbCombats = 2;
                    exclusif = partie.Epoque == "Modern"; // exclusivité seulement en Modern pour prestige 3
                }
            }

            db.ContratsOrganisation.Add(new ContratOrganisation
            {
                PartieID       = partie.PartieID,
                CombattantID   = req.CombattantID,
                OrganisationID = req.OrganisationID,
                NombreCombats  = nbCombats,
                EstExclusif    = exclusif,
                TourDebut      = partie.TourActuel,
            });
        }

        // Récupérer l'agent joueur
        var agent = await db.Agents
            .FirstOrDefaultAsync(a => a.PartieID == partie.PartieID && a.EstJoueur);

        db.CombatsPlanifies.Add(new CombatPlanifie
        {
            PartieID      = partie.PartieID,
            CombattantID  = req.CombattantID,
            AdversaireID  = req.AdversaireID,
            AgentID       = agent?.AgentID,
            OrganisationID = req.OrganisationID,
            TourPrevu     = req.TourPrevu,
            Statut        = "Planifie"
        });

        await db.SaveChangesAsync();
        return Ok();
    }

    // DELETE /api/combats-planifies/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Annuler(int id)
    {
        var partie = await PartieActive();
        if (partie is null) return NotFound();

        var combat = await db.CombatsPlanifies
            .FirstOrDefaultAsync(cp => cp.CombatPlanifieID == id && cp.PartieID == partie.PartieID);

        if (combat is null) return NotFound();

        combat.Statut = "Annule";
        await db.SaveChangesAsync();
        return Ok();
    }

    private async Task<Partie?> PartieActive() =>
        await db.Parties
            .Where(p => p.UserID == CurrentUserId && p.EstActive)
            .FirstOrDefaultAsync();
}
