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

        // ── ÉTAPE 1 : Combats planifiés pour le tour suivant ──────
        // On simule les combats prévus pour le tour vers lequel on avance
        var tourCible = partie.TourActuel + 1;
        var combatsDuTour = await db.CombatsPlanifies
            .Where(cp => cp.PartieID  == partie.PartieID
                      && cp.TourPrevu <= tourCible
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

            // ── Mettre à jour le contrat d'organisation ──────────
            var contrat = await db.ContratsOrganisation
                .FirstOrDefaultAsync(co => co.PartieID       == partie.PartieID
                                        && co.CombattantID   == notre.CombattantID
                                        && co.OrganisationID == combat.OrganisationID
                                        && co.Statut         == "Actif");
            if (contrat is not null)
            {
                contrat.CombatsEffectues++;
                if (contrat.CombatsEffectues >= contrat.NombreCombats)
                    contrat.Statut = "Termine";
            }

            // ── Calcul de la bourse selon le prestige de l'organisation ──
            var prestige = combat.Organisation!.Prestige;
            int bourseVMin = prestige * prestige * 500;
            int bourseVMax = prestige * prestige * 2000;
            int bourseDMin = (int)(bourseVMin * 0.3);
            int bourseDMax = (int)(bourseVMax * 0.3);
            decimal bourse;
            if (sim.EstNul)
                bourse = rng.Next(bourseDMax, bourseVMin + 1); // entre défaite max et victoire min
            else if (sim.EstVictoire)
                bourse = rng.Next(bourseVMin, bourseVMax + 1);
            else
                bourse = rng.Next(bourseDMin, bourseDMax + 1);

            partie.Argent += bourse;

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
                sim.Details,
                bourse,
                sim.Rounds
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

    // ── Simulation de combat round par round ─────────────────────

    private record SimResultat(
        bool   EstVictoire,
        bool   EstNul,
        string Methode,
        byte   Round,
        string Details,
        List<RoundDetailDto> Rounds);

    /// <summary>
    /// Simule un combat MMA en 3 rounds, round par round.
    /// Chaque round est simulé indépendamment avec :
    ///   - Phase (debout/sol) déterminée par les takedowns
    ///   - Score de domination influencé par le gameplan
    ///   - Possibilité de finish (KO/TKO/Soumission) à chaque round
    ///   - Fatigue progressive (cardio)
    ///   - Le "menton" (StatMentoniere) influence fortement les chances de KO
    /// </summary>
    private static SimResultat SimulerCombat(
        Combattant notre, Combattant adverse, string orgNom, string gameplan, Random rng)
    {
        // ── Scores composites (uniquement champs existants sur Combattant) ──
        static double Striking(Combattant c) =>
            c.StatFrappeDebout * 0.40 + c.StatPuissance * 0.35 + c.StatPrecision * 0.25;

        static double Grappling(Combattant c) =>
            c.StatWrestling * 0.25 + c.StatJiuJitsu * 0.40 + c.StatSubmission * 0.35;

        // Défense : vitesse + agilité pour esquiver, anti-takedown, récupération
        static double Defense(Combattant c) =>
            c.StatVitesse * 0.30 + c.StatAgilite * 0.30 + c.StatAntiTakedown * 0.20 + c.StatRecuperation * 0.20;

        static double FightIQ(Combattant c) =>
            c.StatMental * 0.40 + c.StatExperience * 0.35 + c.StatAdaptation * 0.25;

        double Bruit() => (rng.NextDouble() + rng.NextDouble() - 1.0) * 15.0;

        string[] koTypes   = ["KO", "TKO (coups)", "TKO (arrêt médecin)"];
        string[] subTypes  = ["Rear Naked Choke", "Armbar", "Triangle Choke", "Guillotine", "Heel Hook",
                              "Kimura", "D'Arce Choke", "Anaconda Choke"];
        string[] gnpDescs  = ["ground and pound", "coups au sol"];

        var rounds = new List<RoundDetailDto>();
        bool combatTermine = false;
        bool notreVictoire = false;
        bool estNul        = false;
        string methodeFinale = "";
        byte roundFin       = 3;
        string detailsFinal = "";

        int scoreCarteN = 0; // cumul points carte de score (notre)
        int scoreCarteA = 0;

        for (int rd = 1; rd <= 3 && !combatTermine; rd++)
        {
            // ── Fatigue : les stats baissent avec les rounds ─────
            double fatigueMult = rd switch
            {
                1 => 1.0,
                2 => 0.90 - (1.0 - notre.StatCardio / 100.0) * 0.10,
                3 => 0.80 - (1.0 - notre.StatCardio / 100.0) * 0.20,
                _ => 0.75
            };
            double fatigueMultA = rd switch
            {
                1 => 1.0,
                2 => 0.90 - (1.0 - adverse.StatCardio / 100.0) * 0.10,
                3 => 0.80 - (1.0 - adverse.StatCardio / 100.0) * 0.20,
                _ => 0.75
            };

            // ── Phase du round : debout vs sol ───────────────────
            double tdN = notre.StatTakedown   / (double)(notre.StatTakedown   + adverse.StatAntiTakedown + 1);
            double tdA = adverse.StatTakedown / (double)(adverse.StatTakedown + notre.StatAntiTakedown   + 1);

            if (Grappling(notre)   > Striking(notre)   + 8)  tdN = Math.Min(0.80, tdN + 0.15);
            if (Grappling(adverse) > Striking(adverse) + 8)  tdA = Math.Min(0.80, tdA + 0.15);

            // Influence du gameplan
            if      (gameplan == "Striking")  { tdN *= 0.25; tdA *= 1.1; } // notre veut rester debout
            else if (gameplan == "Grappling") { tdN = Math.Min(0.90, tdN * 1.8); tdA *= 0.7; }

            bool auSol = rng.NextDouble() < Math.Max(tdN, tdA) * 0.60;

            // ── Score de round ───────────────────────────────────
            double scoreN, scoreA;
            if (auSol)
            {
                scoreN = Grappling(notre)   * 0.45 * fatigueMult  + FightIQ(notre)   * 0.25 + notre.StatForce   * 0.15 + Bruit();
                scoreA = Grappling(adverse) * 0.45 * fatigueMultA + FightIQ(adverse) * 0.25 + adverse.StatForce * 0.15 + Bruit();
            }
            else
            {
                scoreN = Striking(notre)   * 0.40 * fatigueMult  + Defense(notre)   * 0.20 + FightIQ(notre)   * 0.20 + notre.StatVitesse   * 0.10 + Bruit();
                scoreA = Striking(adverse) * 0.40 * fatigueMultA + Defense(adverse) * 0.20 + FightIQ(adverse) * 0.20 + adverse.StatVitesse * 0.10 + Bruit();
            }

            string gagnantRound;
            int ptN = 9, ptA = 9;
            if (Math.Abs(scoreN - scoreA) < 3.0)
            {
                gagnantRound = "Egal";
                ptN = 10; ptA = 10;
            }
            else if (scoreN > scoreA)
            {
                gagnantRound = "Combattant";
                ptN = 10; ptA = scoreN - scoreA > 12 ? 8 : 9;
            }
            else
            {
                gagnantRound = "Adversaire";
                ptA = 10; ptN = scoreA - scoreN > 12 ? 8 : 9;
            }

            scoreCarteN += ptN;
            scoreCarteA += ptA;

            // ── Tentative de finish ──────────────────────────────
            // Calcul des chances de KO, TKO et soumission pour ce round
            double koMult  = gameplan == "Striking"  ? 1.40 : gameplan == "Grappling" ? 0.65 : 1.0;
            double subMult = gameplan == "Grappling" ? 1.45 : gameplan == "Striking"  ? 0.55 : 1.0;

            // Chance de KO/TKO : dépend du striking du gagnant ET du menton du perdant
            var attaquant = scoreN > scoreA ? notre   : adverse;
            var defenseur = scoreN > scoreA ? adverse : notre;
            bool notreAttaque = scoreN > scoreA;

            double mentonDefenseur = defenseur.StatMentoniere / 100.0;
            double strikingAtt    = Striking(attaquant) / 100.0;

            // Le menton est LE facteur clé pour les KO
            // Un faible menton (< 30) rend le KO très probable face à un bon frappeur
            double koChance = 0;
            if (!auSol)
            {
                // Debout : KO propre — le menton est LE facteur clé
                // Math.Max garanti un minimum de 15% d'effet même avec un menton de fer
                koChance = strikingAtt * Math.Max(0.15, 1.0 - mentonDefenseur) * 0.55 * koMult;
                // Bonus si le round est dominé largement
                if (Math.Abs(scoreN - scoreA) > 10) koChance *= 1.6;
            }

            double tkoChance = 0;
            if (auSol)
            {
                // Sol : TKO par ground & pound
                tkoChance = strikingAtt * Math.Max(0.15, 1.0 - mentonDefenseur * 0.7) * 0.35 * koMult;
            }

            double grapplingAtt    = Grappling(attaquant) / 100.0;
            double evasionDefenseur = defenseur.StatEvasionSub / 100.0;

            double subChance = 0;
            if (auSol)
            {
                subChance = grapplingAtt * Math.Max(0.10, 1.0 - evasionDefenseur) * 0.48 * subMult;
            }

            // Fatigue augmente les chances de finish dans les rounds tardifs
            double fatigueBonus = rd == 3 ? 1.5 : rd == 2 ? 1.2 : 1.0;
            koChance  *= fatigueBonus;
            tkoChance *= fatigueBonus;
            subChance *= fatigueBonus;

            double roll = rng.NextDouble();
            string actions;
            bool finish = false;
            string? methodeFinish = null;

            if (roll < koChance)
            {
                finish = true;
                methodeFinish = koTypes[rng.Next(koTypes.Length)];
                combatTermine = true;
                notreVictoire = notreAttaque;
                methodeFinale = methodeFinish.StartsWith("TKO") ? "TKO" : "KO";
                roundFin = (byte)rd;
                actions = $"{(notreAttaque ? "Notre combattant" : "L'adversaire")} décroche un {methodeFinish} dévastateur !";
                detailsFinal = $"{methodeFinish} au round {rd}";
            }
            else if (roll < koChance + tkoChance)
            {
                finish = true;
                methodeFinish = $"TKO ({gnpDescs[rng.Next(gnpDescs.Length)]})";
                combatTermine = true;
                notreVictoire = notreAttaque;
                methodeFinale = "TKO";
                roundFin = (byte)rd;
                actions = $"{(notreAttaque ? "Notre combattant" : "L'adversaire")} finit par {methodeFinish} !";
                detailsFinal = $"{methodeFinish} au round {rd}";
            }
            else if (roll < koChance + tkoChance + subChance)
            {
                var subType = subTypes[rng.Next(subTypes.Length)];
                finish = true;
                methodeFinish = $"Soumission ({subType})";
                combatTermine = true;
                notreVictoire = notreAttaque;
                methodeFinale = "Soumission";
                roundFin = (byte)rd;
                actions = $"{(notreAttaque ? "Notre combattant" : "L'adversaire")} obtient une {subType} !";
                detailsFinal = $"Soumission ({subType}) au round {rd}";
            }
            else
            {
                // Round sans finish — description des actions
                if (auSol)
                {
                    if (gagnantRound == "Egal")
                        actions = "Round serré au sol, contrôle partagé, peu de dégâts significatifs.";
                    else
                    {
                        var dominant = gagnantRound == "Combattant" ? "Notre combattant" : "L'adversaire";
                        actions = ptN == 8 || ptA == 8
                            ? $"{dominant} domine largement au sol avec un contrôle total et des tentatives de soumission."
                            : $"{dominant} contrôle le round au sol avec une bonne position et quelques frappes.";
                    }
                }
                else
                {
                    if (gagnantRound == "Egal")
                        actions = "Round très serré debout, échanges équilibrés, les deux combattants se neutralisent.";
                    else
                    {
                        var dominant = gagnantRound == "Combattant" ? "Notre combattant" : "L'adversaire";
                        actions = ptN == 8 || ptA == 8
                            ? $"{dominant} domine le round avec des combinaisons précises et des dégâts visibles."
                            : $"{dominant} remporte le round aux points grâce à un meilleur volume de frappes.";
                    }
                }
            }

            rounds.Add(new RoundDetailDto(rd, gagnantRound, ptN, ptA, actions, finish, methodeFinish));
        }

        // ── Si on est allé à la décision ──────────────────────────
        if (!combatTermine)
        {
            roundFin = 3;
            if (Math.Abs(scoreCarteN - scoreCarteA) <= 1 && rng.NextDouble() < 0.10)
            {
                estNul = true;
                methodeFinale = "Décision partagée (nul)";
                detailsFinal = $"Match nul — décision partagée ({scoreCarteN}-{scoreCarteA})";
            }
            else
            {
                notreVictoire = scoreCarteN > scoreCarteA;
                if (scoreCarteN == scoreCarteA)
                    notreVictoire = rng.NextDouble() < 0.50; // départage aléatoire si scores identiques

                bool unanime = Math.Abs(scoreCarteN - scoreCarteA) >= 2;
                methodeFinale = unanime ? "Décision unanime" : "Décision partagée";
                detailsFinal = $"{methodeFinale} ({scoreCarteN}-{scoreCarteA})";
            }
        }

        return new SimResultat(notreVictoire, estNul, methodeFinale, roundFin, detailsFinal, rounds);
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
