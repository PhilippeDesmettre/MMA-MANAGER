using Microsoft.EntityFrameworkCore;
using MmaManager.Data;
using MmaManager.Models;
using MmaManager.Models.Dtos;

namespace MmaManager.Services;

public class TurnAdvancementService(
    MmaContext db,
    CombatSimulationService combatService,
    TrainingService trainingService,
    RankingService rankingService,
    WorldSimulationService worldService,
    RosterHistoriqueService rosterService)
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

        var rivaliteIdsImpliques = combatsDuTour
            .SelectMany(c => new[] { c.CombattantID, c.AdversaireID })
            .Distinct()
            .ToList();

        var rivalitesPartie = await db.Rivalites
            .Where(r => r.PartieID == partie.PartieID
                     && (rivaliteIdsImpliques.Contains(r.Combattant1ID)
                      || rivaliteIdsImpliques.Contains(r.Combattant2ID)))
            .Include(r => r.Combattant1)
            .Include(r => r.Combattant2)
            .ToListAsync();

        var combatsResultats  = new List<CombatSimuleDto>();
        var contratsTermines  = new List<ContratTermineDto>();

        foreach (var combat in combatsDuTour)
        {
            var notre   = combat.Combattant!;
            var adverse = combat.Adversaire!;
            var orgNom  = combat.Organisation!.Nom;

            int idMin = Math.Min(notre.CombattantID, adverse.CombattantID);
            int idMax = Math.Max(notre.CombattantID, adverse.CombattantID);
            var rivalite = rivalitesPartie
                .FirstOrDefault(r => r.Combattant1ID == idMin && r.Combattant2ID == idMax);
            byte intensiteRiv = rivalite?.Intensite ?? 0;

            var sim = combatService.SimulerCombat(notre, adverse, orgNom, combat.Gameplan, rng,
                isTitleFight: false, rivaliteIntensite: intensiteRiv);

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

            // Appliquer les blessures de combat
            if (sim.BlessureNotre is { } blessN)
            {
                notre.BlessureGravite  = blessN.Gravite;
                notre.BlessureZone     = blessN.Zone;
                notre.SemainesIndispo  = blessN.SemainesIndispo;
            }
            if (sim.BlessureAdverse is { } blessA)
            {
                adverse.BlessureGravite = blessA.Gravite;
                adverse.BlessureZone    = blessA.Zone;
                adverse.SemainesIndispo = blessA.SemainesIndispo;
            }

            // Rivalités
            bool    nouvRiv     = false;
            string? nouvelleRaison = null;

            if (rivalite is not null)
            {
                rivalite.NbConfrontations++;
                rivalite.TourDernierCombat = tourCible;
                if (rivalite.Intensite < 5)
                {
                    rivalite.Intensite++;
                    if (rivalite.Intensite >= 3)
                        rivalite.Raison = $"Trilogie épique ({rivalite.NbConfrontations} confrontations)";
                }
                intensiteRiv = rivalite.Intensite;
            }
            else
            {
                bool creer = false;

                if (sim.Methode is "KO" or "TKO")
                {
                    creer = rng.NextDouble() < 0.60;
                    nouvelleRaison = sim.EstVictoire
                        ? $"{notre.Prenom} {notre.NomFamille} a mis KO {adverse.Prenom} {adverse.NomFamille}"
                        : $"{adverse.Prenom} {adverse.NomFamille} a mis KO {notre.Prenom} {notre.NomFamille}";
                }
                else if (sim.Methode == "Soumission")
                {
                    creer = rng.NextDouble() < 0.40;
                    nouvelleRaison = "Soumission controversée";
                }
                else if (sim.Methode.Contains("Décision"))
                {
                    creer = rng.NextDouble() < 0.30;
                    nouvelleRaison = "Décision serrée, les deux combattants veulent un rematch";
                }

                if (creer)
                {
                    db.Rivalites.Add(new Rivalite
                    {
                        PartieID          = partie.PartieID,
                        Combattant1ID     = idMin,
                        Combattant2ID     = idMax,
                        Intensite         = 1,
                        NbConfrontations  = 1,
                        TourCreation      = tourCible,
                        TourDernierCombat = tourCible,
                        Raison            = nouvelleRaison
                    });
                    nouvRiv      = true;
                    intensiteRiv = 1;
                }
            }

            byte?   rivIntensite = (nouvRiv || rivalite is not null) ? intensiteRiv : null;
            string? rivRaison    = rivIntensite.HasValue
                ? (nouvRiv ? nouvelleRaison : rivalite!.Raison ?? $"Rivalité intensité {rivalite!.Intensite}")
                : null;

            // ── Classement ───────────────────────────────────────────
            await rankingService.MettreAJourApresCombat(
                partie.PartieID, notre.CombattantID, adverse.CombattantID,
                combat.OrganisationID, sim.EstVictoire, sim.EstNul,
                sim.Methode, combat.Organisation!.Prestige);

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
                {
                    contrat.Statut = "Termine";
                    contratsTermines.Add(new ContratTermineDto(
                        $"{notre.Prenom} {notre.NomFamille}",
                        orgNom,
                        contrat.CombatsEffectues
                    ));
                }
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
                sim.Rounds,
                sim.BlessureNotre?.Gravite,
                sim.BlessureNotre?.Zone,
                sim.BlessureNotre?.SemainesIndispo,
                nouvRiv,
                rivIntensite,
                rivRaison
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

            if (c.SemainesIndispo > 0)
            {
                resultats.Add(new CombattantResultatDto(
                    c.CombattantID, c.Prenom, c.NomFamille,
                    "Blessé", Array.Empty<GainStatDto>()));
                continue;
            }

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

            int age = CalculerAge(c.DateNaissance, partie.AnneeActuelle, partie.MoisActuel);
            var gains = trainingService.AppliquerEntrainement(c, ep.TypeEntrainement, stats, rng, age);

            // Risque de blessure à l'entraînement (3%)
            if (rng.NextDouble() < 0.03)
            {
                string[] zones = ["Genou", "Épaule", "Dos", "Cheville", "Poignet", "Tibia"];
                c.BlessureGravite  = 1;
                c.BlessureZone     = zones[rng.Next(zones.Length)];
                c.SemainesIndispo  = (short)(1 + rng.Next(2));
            }

            resultats.Add(new CombattantResultatDto(
                c.CombattantID,
                c.Prenom,
                c.NomFamille,
                ep.TypeEntrainement,
                gains
            ));
        }

        db.EntrainementsPlanifies.RemoveRange(planifies);

        // ── ÉTAPE 2b : Guérison des blessures ─────────────────────
        var ecurieCombattants = await db.CombattantsPartie
            .Where(cp => cp.PartieID == partie.PartieID)
            .Include(cp => cp.Combattant)
            .ToListAsync();

        foreach (var cp in ecurieCombattants)
        {
            var cb = cp.Combattant!;
            if (cb.SemainesIndispo > 0)
            {
                cb.SemainesIndispo--;
                if (cb.SemainesIndispo == 0)
                {
                    cb.BlessureGravite = 0;
                    cb.BlessureZone    = null;
                }
            }
        }

        // ── ÉTAPE 2c : Vieillissement / déclin ───────────────────
        foreach (var cp in ecurieCombattants)
        {
            var cb  = cp.Combattant!;
            int age = CalculerAge(cb.DateNaissance, partie.AnneeActuelle, partie.MoisActuel);

            if (age > 33)
            {
                double declinChance = age > 38 ? 0.30 : 0.15;
                int    declinMax    = age > 38 ? 2    : 1;

                DeclinStat(() => cb.StatVitesse,      v => cb.StatVitesse      = v, declinChance, declinMax, rng);
                DeclinStat(() => cb.StatAgilite,      v => cb.StatAgilite      = v, declinChance, declinMax, rng);
                DeclinStat(() => cb.StatCardio,       v => cb.StatCardio       = v, declinChance, declinMax, rng);
                DeclinStat(() => cb.StatRecuperation, v => cb.StatRecuperation = v, declinChance, declinMax, rng);
                DeclinStat(() => cb.StatMentoniere,   v => cb.StatMentoniere   = v, declinChance, declinMax, rng);
                DeclinStat(() => cb.StatForce,        v => cb.StatForce        = v, declinChance, declinMax, rng);
                DeclinStat(() => cb.StatVitesseMains, v => cb.StatVitesseMains = v, declinChance, declinMax, rng);
                DeclinStat(() => cb.StatFootwork,     v => cb.StatFootwork     = v, declinChance, declinMax, rng);

                if (cb.StatExperience < 99)
                    cb.StatExperience = (byte)Math.Min(99, cb.StatExperience + 1);
            }
        }

        // ── ÉTAPE 2d : Apparitions de nouveaux fighters historiques ──
        var nouvellesApparitions = await rosterService.VerifierApparitions(
            partie.PartieID, partie.AnneeActuelle, partie.MoisActuel);

        // ── ÉTAPE 2e : Simulation du monde ───────────────────────
        var nouvellesMonde = await worldService.SimulerTour(
            partie.PartieID, partie.AnneeActuelle, partie.MoisActuel, rng);

        nouvellesMonde.InsertRange(0, nouvellesApparitions);

        // ── ÉTAPE 3 : Finances ────────────────────────────────────
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
            prestigeAugmente,
            contratsTermines,
            nouvellesMonde
        );
    }

    private static int CalculerAge(DateTime dateNaissance, int annee, int mois)
    {
        int age = annee - dateNaissance.Year;
        if (mois < dateNaissance.Month) age--;
        return Math.Max(0, age);
    }

    private static void DeclinStat(
        Func<byte> getter, Action<byte> setter,
        double chance, int maxLoss, Random rng)
    {
        if (rng.NextDouble() < chance)
        {
            int loss    = 1 + rng.Next(maxLoss);
            int current = getter();
            setter((byte)Math.Max(10, current - loss));
        }
    }
}
