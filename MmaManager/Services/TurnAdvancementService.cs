using Microsoft.EntityFrameworkCore;
using MmaManager.Data;
using MmaManager.Models;
using MmaManager.Models.Dtos;

namespace MmaManager.Services;

public class TurnAdvancementService(
    MmaContext db,
    CombatSimulationService combatService,
    TrainingService trainingService)
{
    public async Task<TourResultatDto?> AvancerTour(int userId)
    {
        var partie = await db.Parties
            .Where(p => p.UserID == userId && p.EstActive)
            .FirstOrDefaultAsync();

        if (partie is null) return null;

        var entraineurDefaut = await db.EntraineursJoueur
            .FirstOrDefaultAsync(e => e.PartieID == partie.PartieID);

        if (entraineurDefaut is null) return null;

        var rng = new Random();

        // ── ÉTAPE 1 : Combats planifiés pour le tour suivant ──────
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

            var sim = combatService.SimulerCombat(notre, adverse, orgNom, combat.Gameplan, rng);

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
            else
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

            combat.Statut = "Effectue";

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

            var prestige   = combat.Organisation!.Prestige;
            int bourseVMin = prestige * prestige * 500;
            int bourseVMax = prestige * prestige * 2000;
            int bourseDMin = (int)(bourseVMin * 0.3);
            int bourseDMax = (int)(bourseVMax * 0.3);
            decimal bourse;
            if (sim.EstNul)
                bourse = rng.Next(bourseDMax, bourseVMin + 1);
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

            var gains = trainingService.AppliquerEntrainement(c, ep.TypeEntrainement, stats, rng);
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

        int salairesTotaux = ecurieCombattants.Sum(cp => cp.Combattant!.Salaire);
        decimal loyer      = 1m;
        decimal staffSal   = staffEmbauches.Sum(sp => sp.StaffDisponible!.SalaireMensuel);
        decimal depenses   = salairesTotaux + loyer + staffSal;

        decimal soldeAvant = partie.Argent;
        partie.Argent     -= depenses;

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

        // ── ÉTAPE 5 : Prestige de l'écurie ───────────────────────
        bool prestigeAugmente = false;
        if (!estGameOver && partie.PrestigeEcurie < 5)
        {
            int totalVictoires = ecurieCombattants.Sum(cp => cp.Combattant!.Victoires);
            int seuil = partie.PrestigeEcurie switch
            {
                1 => 3,
                2 => 8,
                3 => 20,
                4 => 40,
                _ => int.MaxValue
            };
            if (totalVictoires >= seuil)
            {
                partie.PrestigeEcurie++;
                prestigeAugmente = true;
            }
        }

        await db.SaveChangesAsync();

        return new TourResultatDto(
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
            estGameOver,
            partie.PrestigeEcurie,
            prestigeAugmente
        );
    }
}
