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

        // Si aucun coach explicitement choisi, utiliser l'entraîneur joueur par défaut
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

        // L'existant compte comme 1 slot déjà pris, donc pas de blocage
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
        var partie = await db.Parties
            .Where(p => p.UserID == CurrentUserId && p.EstActive)
            .FirstOrDefaultAsync();

        if (partie is null) return NotFound("Aucune partie active.");

        var entraineurDefaut = await db.EntraineursJoueur
            .FirstOrDefaultAsync(e => e.PartieID == partie.PartieID);

        if (entraineurDefaut is null) return BadRequest("Entraîneur introuvable.");

        var rng = new Random();

        // ── ÉTAPE 1 : Combats planifiés pour ce tour ──────────────
        var combatsDuTour = await db.CombatsPlanifies
            .Where(cp => cp.PartieID  == partie.PartieID
                      && cp.TourPrevu == partie.TourActuel
                      && cp.Statut    == "Planifie")
            .Include(cp => cp.Combattant)
            .Include(cp => cp.Adversaire)
            .Include(cp => cp.Organisation)
            .ToListAsync();

        var combatsResultats = new List<CombatSimuleDto>();

        foreach (var combat in combatsDuTour)
        {
            var notre   = combat.Combattant!;
            var adverse = combat.Adversaire!;
            var orgNom  = combat.Organisation!.Nom;

            var sim = SimulerCombat(notre, adverse, orgNom, combat.Gameplan, rng);

            // ── Mise à jour des bilans sportifs ───────────────────
            if (sim.EstNul)
            {
                notre.Nuls++;
                adverse.Nuls++;
            }
            else if (sim.EstVictoire)
            {
                notre.Victoires++;
                adverse.Defaites++;
                switch (sim.Methode)
                {
                    case "KO":   case "TKO":  notre.VictoiresKO++;  adverse.DefaitesKO++;  break;
                    case "Soumission":        notre.VictoiresSub++; adverse.DefaitesSub++; break;
                    default:                  notre.VictoiresDec++;                        break;
                }
            }
            else // défaite
            {
                notre.Defaites++;
                adverse.Victoires++;
                switch (sim.Methode)
                {
                    case "KO":   case "TKO":  notre.DefaitesKO++;  adverse.VictoiresKO++;  break;
                    case "Soumission":        notre.DefaitesSub++; adverse.VictoiresSub++; break;
                    default:                  adverse.VictoiresDec++;                      break;
                }
            }

            // ── Historique dans la table ResultatCombatPartie ─────
            db.ResultatsCombat.Add(new ResultatCombatPartie
            {
                PartieID        = partie.PartieID,
                CombattantID    = notre.CombattantID,
                AdversaireID    = adverse.CombattantID,
                OrganisationNom = orgNom,
                TourCombat      = partie.TourActuel,
                EstVictoire     = sim.EstNul ? null : sim.EstVictoire,
                EstNul          = sim.EstNul,
                MethodeVictoire = sim.Methode,
                RoundFin        = sim.Round,
                Details         = sim.Details
            });

            // ── Marquer le combat comme effectué ─────────────────
            combat.Statut = "Effectue";

            combatsResultats.Add(new CombatSimuleDto(
                notre.CombattantID,
                $"{notre.Prenom} {notre.NomFamille}",
                adverse.CombattantID,
                $"{adverse.Prenom} {adverse.NomFamille}",
                orgNom,
                sim.EstVictoire,
                sim.EstNul,
                sim.Methode,
                sim.Round,
                sim.Details
            ));
        }

        // ── ÉTAPE 2 : Entraînements ───────────────────────────────
        var planifies = await db.EntrainementsPlanifies
            .Where(ep => ep.PartieID == partie.PartieID)
            .Include(ep => ep.Combattant)
            .Include(ep => ep.EntraineurJoueur)
            .Include(ep => ep.StaffPartie!)
                .ThenInclude(sp => sp.StaffDisponible!)
            .ToListAsync();

        var resultats = new List<CombattantResultatDto>();

        foreach (var ep in planifies)
        {
            var c = ep.Combattant!;
            CoachStats stats;

            if (ep.StaffPartie?.StaffDisponible is { } sd)
            {
                stats = new CoachStats(sd.CompStriking, sd.CompLutte, sd.CompGrappling, sd.CompConditioning, sd.CompMental);
            }
            else
            {
                var coach = ep.EntraineurJoueur ?? entraineurDefaut;
                stats = new CoachStats(
                    coach?.CompStriking     ?? 30,
                    coach?.CompLutte        ?? 30,
                    coach?.CompGrappling    ?? 30,
                    coach?.CompConditioning ?? 30,
                    coach?.CompMental       ?? 30);
            }

            var gains = AppliquerEntrainement(c, ep.TypeEntrainement, stats, rng);
            resultats.Add(new CombattantResultatDto(
                c.CombattantID,
                c.Prenom,
                c.NomFamille,
                ep.TypeEntrainement,
                gains
            ));
        }

        db.EntrainementsPlanifies.RemoveRange(planifies);

        // ── ÉTAPE 3 : Finances ────────────────────────────────────
        var ecurieCombattants = await db.CombattantsPartie
            .Where(cp => cp.PartieID == partie.PartieID)
            .Include(cp => cp.Combattant)
            .ToListAsync();

        var staffEmbauches = await db.StaffParties
            .Where(sp => sp.PartieID == partie.PartieID)
            .Include(sp => sp.StaffDisponible)
            .ToListAsync();

        int salairesTotaux  = ecurieCombattants.Sum(cp => cp.Combattant!.Salaire);
        decimal loyer       = 1m;
        decimal staffSal    = staffEmbauches.Sum(sp => sp.StaffDisponible!.SalaireMensuel);
        decimal depenses    = salairesTotaux + loyer + staffSal;

        decimal soldeAvant  = partie.Argent;
        partie.Argent      -= depenses;

        // ── Game Over si solde négatif ────────────────────────────
        bool estGameOver = partie.Argent < 0;
        if (estGameOver) partie.EstActive = false;

        // ── ÉTAPE 4 : Avancer le tour ─────────────────────────────
        var tourJoue       = partie.TourActuel;
        var ancienneEpoque = partie.Epoque;

        partie.TourActuel++;
        partie.MoisActuel++;
        if (partie.MoisActuel > 12)
        {
            partie.MoisActuel = 1;
            partie.AnneeActuelle++;
        }

        // ── Détection transition d'ère ────────────────────────────
        bool transition = false;
        string? messageTransition = null;

        if (ancienneEpoque == "NoRules" && partie.AnneeActuelle >= 2000)
        {
            partie.Epoque     = "GoldenAge";
            transition        = true;
            messageTransition =
                "🏆 Le monde du MMA entre dans son Âge d'Or !\n" +
                "PRIDE Fighting Championships, l'UFC en pleine ascension, " +
                "Fedor, Wanderlei, Chuck Liddell... Une nouvelle ère commence.";
        }
        else if (ancienneEpoque == "GoldenAge" && partie.AnneeActuelle >= 2013)
        {
            partie.Epoque     = "Modern";
            transition        = true;
            messageTransition =
                "🧠 Bienvenue dans l'ère Moderne du MMA !\n" +
                "L'UFC domine le monde, de nouvelles organisations émergent. " +
                "Conor McGregor, Jon Jones, Khabib Nurmagomedov... Le MMA est devenu un sport planétaire.";
        }

        await db.SaveChangesAsync();

        return Ok(new TourResultatDto(
            tourJoue,
            partie.MoisActuel,
            partie.AnneeActuelle,
            partie.Epoque,
            transition,
            messageTransition,
            combatsResultats,
            resultats,
            soldeAvant,
            partie.Argent,
            depenses,
            estGameOver
        ));
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<Partie?> PartieActive() =>
        await db.Parties
            .Where(p => p.UserID == CurrentUserId && p.EstActive)
            .FirstOrDefaultAsync();

    // ── Simulation de combat ──────────────────────────────────────

    private record SimResultat(
        bool   EstVictoire,
        bool   EstNul,
        string Methode,
        byte   Round,
        string Details);

    /// <summary>
    /// Simule un combat MMA en 3 rounds.
    /// Algorithme :
    ///   1. Scores composites (striking, grappling, cardio, fight IQ)
    ///   2. Phase du combat : debout vs sol selon les takedowns
    ///   3. Score effectif + bruit aléatoire gaussien (~±20 pts)
    ///   4. Nul si écart très faible (10 % de chance)
    ///   5. Méthode : KO, TKO, Soumission ou Décision
    /// Plus l'écart de niveau est grand, plus le résultat est prévisible.
    /// </summary>
    private static SimResultat SimulerCombat(
        Combattant notre, Combattant adverse, string orgNom, string gameplan, Random rng)
    {
        // ── Scores composites ─────────────────────────────────────
        static double Striking(Combattant c) =>
            c.StatFrappeDebout * 0.40 + c.StatPuissance * 0.30 + c.StatPrecision * 0.30;

        static double Grappling(Combattant c) =>
            c.StatWrestling * 0.25 + c.StatJiuJitsu * 0.40 + c.StatSubmission * 0.35;

        static double FightIQ(Combattant c) =>
            c.StatMental * 0.50 + c.StatExperience * 0.35 + c.StatAdaptation * 0.15;

        // ── Phase du combat : debout vs sol ───────────────────────
        double tdN = notre.StatTakedown   / (double)(notre.StatTakedown   + adverse.StatAntiTakedown + 1);
        double tdA = adverse.StatTakedown / (double)(adverse.StatTakedown + notre.StatAntiTakedown   + 1);

        // Bonus si grappler dominant
        if (Grappling(notre)   > Striking(notre)   + 8) tdN = Math.Min(0.80, tdN + 0.15);
        if (Grappling(adverse) > Striking(adverse) + 8) tdA = Math.Min(0.80, tdA + 0.15);

        // ── Influence du gameplan sur la phase ────────────────────
        // Striking : rend les takedowns très difficiles sur notre combattant
        // Grappling : boost significatif de la chance d'aller au sol
        if      (gameplan == "Striking")  tdN = tdN * 0.25;
        else if (gameplan == "Grappling") tdN = Math.Min(0.90, tdN * 1.8);

        bool auSol = rng.NextDouble() < Math.Max(tdN, tdA) * 0.65;

        // ── Score effectif + bruit aléatoire ─────────────────────
        // (somme de deux U[0,1] ≈ gaussienne centrée)
        double Effectif(Combattant c) =>
            (auSol ? Grappling(c) : Striking(c)) * 0.50
            + c.StatCardio * 0.25
            + FightIQ(c)   * 0.25;

        double Bruit() => (rng.NextDouble() + rng.NextDouble() - 1.0) * 20.0;

        double sN = Math.Max(1, Effectif(notre)   + Bruit());
        double sA = Math.Max(1, Effectif(adverse) + Bruit());

        // ── Nul ? (rare : <10 %, uniquement si scores très proches) ─
        bool estNul = Math.Abs(sN - sA) < 3.5 && rng.NextDouble() < 0.10;

        if (estNul)
            return new SimResultat(false, true, "Nul", 3, "Match nul — décision partagée");

        bool notreVictoire = sN > sA;
        var  winner = notreVictoire ? notre   : adverse;
        var  loser  = notreVictoire ? adverse : notre;

        // ── Méthode de victoire ───────────────────────────────────
        double strikeW    = Striking(winner);
        double grapplingW = Grappling(winner);

        // Modificateurs de gameplan sur les probabilités de finish
        double koMult  = gameplan == "Striking"  ? 1.35 : gameplan == "Grappling" ? 0.70 : 1.0;
        double subMult = gameplan == "Grappling" ? 1.40 : gameplan == "Striking"  ? 0.60 : 1.0;

        // Debout → KO
        double koChance  = !auSol ? (strikeW    / 100.0) * Math.Max(0, 1 - loser.StatMentoniere  / 100.0) * 0.55 * koMult  : 0;
        // Sol   → TKO (ground & pound)
        double tkoChance = auSol  ? (strikeW    / 100.0) * Math.Max(0, 1 - loser.StatMentoniere  / 100.0) * 0.22 * koMult  : 0;
        // Sol   → Soumission
        double subChance = auSol  ? (grapplingW / 100.0) * Math.Max(0, 1 - loser.StatEvasionSub  / 100.0) * 0.45 * subMult : 0;

        double roll = rng.NextDouble();
        string methode;
        byte   round;
        string details;

        if (roll < koChance)
        {
            methode = "KO";
            round   = (byte)(rng.Next(3) + 1);
            details = $"KO au round {round}";
        }
        else if (roll < koChance + tkoChance)
        {
            methode = "TKO";
            round   = (byte)(rng.Next(3) + 1);
            details = $"TKO (ground and pound) au round {round}";
        }
        else if (roll < koChance + tkoChance + subChance)
        {
            string[] subs = ["Rear Naked Choke", "Armbar", "Triangle", "Guillotine", "Heel Hook"];
            methode = "Soumission";
            round   = (byte)(rng.Next(3) + 1);
            details = $"Soumission ({subs[rng.Next(subs.Length)]}) au round {round}";
        }
        else
        {
            bool unanime = rng.NextDouble() < 0.70;
            methode = unanime ? "Décision unanime" : "Décision partagée";
            round   = 3;
            details = $"{methode} après 3 rounds";
        }

        return new SimResultat(notreVictoire, false, methode, round, details);
    }

    // ── Entraînement ─────────────────────────────────────────────

    private record CoachStats(int CompStriking, int CompLutte, int CompGrappling, int CompConditioning, int CompMental);

    private static int CalculGain(int trainerComp, Random rng)
    {
        var factor = rng.Next(1, 4);
        return Math.Max(1, (int)Math.Round(factor * trainerComp / 50.0));
    }

    private static byte ClampByte(int val) => (byte)Math.Min(100, Math.Max(0, val));

    private static IReadOnlyList<GainStatDto> AppliquerEntrainement(
        Combattant c, string type, CoachStats stats, Random rng)
    {
        var gains = new List<GainStatDto>();

        switch (type)
        {
            case "Striking":
                gains.Add(Apply(c, "Frappe debout", stats.CompStriking, rng,
                    () => c.StatFrappeDebout, v => c.StatFrappeDebout = v));
                gains.Add(Apply(c, "Puissance",     stats.CompStriking, rng,
                    () => c.StatPuissance,    v => c.StatPuissance    = v));
                gains.Add(Apply(c, "Précision",     stats.CompStriking, rng,
                    () => c.StatPrecision,    v => c.StatPrecision    = v));
                break;

            case "Lutte":
                gains.Add(Apply(c, "Wrestling",     stats.CompLutte, rng,
                    () => c.StatWrestling,    v => c.StatWrestling    = v));
                gains.Add(Apply(c, "Takedown",      stats.CompLutte, rng,
                    () => c.StatTakedown,     v => c.StatTakedown     = v));
                gains.Add(Apply(c, "Anti-takedown", stats.CompLutte, rng,
                    () => c.StatAntiTakedown, v => c.StatAntiTakedown = v));
                break;

            case "Grappling":
                gains.Add(Apply(c, "Jiu-Jitsu",   stats.CompGrappling, rng,
                    () => c.StatJiuJitsu,   v => c.StatJiuJitsu   = v));
                gains.Add(Apply(c, "Submission",  stats.CompGrappling, rng,
                    () => c.StatSubmission, v => c.StatSubmission  = v));
                gains.Add(Apply(c, "Évasion sub", stats.CompGrappling, rng,
                    () => c.StatEvasionSub, v => c.StatEvasionSub  = v));
                break;

            case "Conditionnement":
                gains.Add(Apply(c, "Force",        stats.CompConditioning, rng,
                    () => c.StatForce,        v => c.StatForce        = v));
                gains.Add(Apply(c, "Vitesse",      stats.CompConditioning, rng,
                    () => c.StatVitesse,      v => c.StatVitesse      = v));
                gains.Add(Apply(c, "Agilité",      stats.CompConditioning, rng,
                    () => c.StatAgilite,      v => c.StatAgilite      = v));
                gains.Add(Apply(c, "Cardio",       stats.CompConditioning, rng,
                    () => c.StatCardio,       v => c.StatCardio       = v));
                gains.Add(Apply(c, "Récupération", stats.CompConditioning, rng,
                    () => c.StatRecuperation, v => c.StatRecuperation = v));
                break;

            case "Mental":
                gains.Add(Apply(c, "Mental",     stats.CompMental, rng,
                    () => c.StatMental,     v => c.StatMental     = v));
                gains.Add(Apply(c, "Expérience", stats.CompMental, rng,
                    () => c.StatExperience, v => c.StatExperience = v));
                gains.Add(Apply(c, "Adaptation", stats.CompMental, rng,
                    () => c.StatAdaptation, v => c.StatAdaptation = v));
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
        var gain    = CalculGain(trainerComp, rng);
        var ancien  = getter();
        var nouveau = ClampByte(ancien + gain);
        setter(nouveau);
        return new GainStatDto(nom, nouveau - ancien);
    }
}
