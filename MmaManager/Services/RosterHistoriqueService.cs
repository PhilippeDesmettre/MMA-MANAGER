using Microsoft.EntityFrameworkCore;
using MmaManager.Data;
using MmaManager.Models;
using MmaManager.Models.Dtos;

namespace MmaManager.Services;

public class RosterHistoriqueService(MmaContext db, RankingService rankingService)
{
    private record SimResultatSimplifie(int Gagnant, bool EstNul, string Methode, int Round);
    private record RosterFile(List<RosterFighter> Fighters);

    private record RosterFighter(
        string Id, string Prenom, string Nom, string Pays, string Genre,
        int CategorieH, int Style, string DateNaissance,
        Dictionary<string, RosterEre?> Eres);

    private record RosterEre(
        int DateDebut,
        Dictionary<string, int> Stats,
        int Potentiel, int Progression,
        RosterRecord Record,
        RosterClassement? Classement);

    private record RosterRecord(int V, int D, int N, int Ko, int Sub, int Dec, int Dko, int Dsub);
    private record RosterClassement(string Organisation, int Rang, bool Champion);

    private static readonly Dictionary<string, string> OrgAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "UFC",         "Ultimate Fighting Championship" },
        { "PRIDE",       "PRIDE Fighting Championships"  },
        { "WEC",         "World Extreme Cagefighting"    },
        { "Strikeforce", "Strikeforce"                   },
        { "Bellator",    "Bellator MMA"                  },
        { "ONE",         "ONE Championship"              },
        { "PFL",         "Professional Fighters League"  },
        { "K-1",         "K-1 MMA / Hero's"             }
    };

    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── Chargement initial à la création de partie ─────────────────────────
    public async Task ChargerRosterInitial(int partieId, int anneeDepart)
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "roster_historique.json");
        if (!File.Exists(jsonPath)) return;

        var json   = await File.ReadAllTextAsync(jsonPath);
        var roster = System.Text.Json.JsonSerializer.Deserialize<RosterFile>(json, JsonOpts);
        if (roster?.Fighters is null) return;

        string ereKey = anneeDepart switch
        {
            < 2000 => "pionniers",
            < 2013 => "goldenAge",
            _      => "modernMMA"
        };

        var organisations = await db.CombatOrganisations.ToListAsync();
        var pays          = await db.Pays.ToDictionaryAsync(p => p.Code, p => p.PaysID);
        var rng           = new Random();

        foreach (var f in roster.Fighters)
        {
            if (!f.Eres.TryGetValue(ereKey, out var snapshot) || snapshot is null) continue;
            if (snapshot.DateDebut > anneeDepart) continue;
            if (!pays.TryGetValue(f.Pays, out int paysId)) continue;

            var existe = await db.Combattants.AnyAsync(c => c.Prenom == f.Prenom && c.NomFamille == f.Nom);
            if (existe) continue;

            var (taille, allonge, poidsReel) = GenererPhysique(f.Genre, f.CategorieH, rng);

            var combattant = BuildCombattant(f, snapshot, paysId, taille, allonge, poidsReel);
            db.Combattants.Add(combattant);
            await db.SaveChangesAsync();

            CombatOrganisation? orgCible    = null;
            bool                estChampion = false;

            if (snapshot.Classement is not null)
            {
                string orgName = snapshot.Classement.Organisation;
                if (OrgAliases.TryGetValue(orgName, out var resolved)) orgName = resolved;
                orgCible    = organisations.FirstOrDefault(o => o.Nom.Equals(orgName, StringComparison.OrdinalIgnoreCase));
                estChampion = snapshot.Classement.Champion;
            }

            int nbRetro = Math.Min(8, Math.Max(2, snapshot.Record.V + snapshot.Record.D + snapshot.Record.N));
            await SimulerCombatsRetroactifs(partieId, combattant, orgCible, nbRetro, estChampion, rng);
        }
    }

    // ── Vérification des apparitions chaque début d'année ─────────────────
    public async Task<List<NouvelleMondeDto>> VerifierApparitions(int partieId, int anneeActuelle, int moisActuel)
    {
        var nouvelles = new List<NouvelleMondeDto>();

        if (moisActuel != 1) return nouvelles;

        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "roster_historique.json");
        if (!File.Exists(jsonPath)) return nouvelles;

        var json   = await File.ReadAllTextAsync(jsonPath);
        var roster = System.Text.Json.JsonSerializer.Deserialize<RosterFile>(json, JsonOpts);
        if (roster?.Fighters is null) return nouvelles;

        string ereKey = anneeActuelle switch
        {
            < 2000 => "pionniers",
            < 2013 => "goldenAge",
            _      => "modernMMA"
        };

        var organisations = await db.CombatOrganisations.ToListAsync();
        var pays          = await db.Pays.ToDictionaryAsync(p => p.Code, p => p.PaysID);
        var rng           = new Random();

        foreach (var f in roster.Fighters)
        {
            if (!f.Eres.TryGetValue(ereKey, out var snapshot) || snapshot is null) continue;
            if (snapshot.DateDebut != anneeActuelle) continue;
            if (!pays.TryGetValue(f.Pays, out int paysId)) continue;

            var existe = await db.Combattants.AnyAsync(c => c.Prenom == f.Prenom && c.NomFamille == f.Nom);
            if (existe) continue;

            var (taille, allonge, poidsReel) = GenererPhysique(f.Genre, f.CategorieH, rng);

            var combattant = BuildCombattant(f, snapshot, paysId, taille, allonge, poidsReel);
            db.Combattants.Add(combattant);
            await db.SaveChangesAsync();

            CombatOrganisation? orgCible    = null;
            bool                estChampion = false;

            if (snapshot.Classement is not null)
            {
                string orgName = snapshot.Classement.Organisation;
                if (OrgAliases.TryGetValue(orgName, out var resolved)) orgName = resolved;
                orgCible    = organisations.FirstOrDefault(o => o.Nom.Equals(orgName, StringComparison.OrdinalIgnoreCase));
                estChampion = snapshot.Classement.Champion;
            }

            int nbRetro = Math.Min(4, Math.Max(2, snapshot.Record.V + snapshot.Record.D));
            await SimulerCombatsRetroactifs(partieId, combattant, orgCible, nbRetro, estChampion, rng);

            string record = $"{combattant.Victoires}-{combattant.Defaites}-{combattant.Nuls}";
            string orgNom = orgCible?.Nom ?? "le circuit local";
            nouvelles.Add(new NouvelleMondeDto(
                $"🆕 Nouveau talent ! {combattant.Prenom} {combattant.NomFamille} fait ses débuts",
                $"Débuts à {orgNom} · Bilan : {record}",
                "importante"));
        }

        return nouvelles;
    }

    // ── Simulation rétroactive ─────────────────────────────────────────────
    private async Task SimulerCombatsRetroactifs(
        int partieId, Combattant fighter, CombatOrganisation? orgCible,
        int nbCombats, bool estFuturChampion, Random rng)
    {
        if (orgCible is null)
        {
            byte ov         = fighter.Overall;
            int  presMin    = ov >= 86 ? 4 : ov >= 71 ? 3 : ov >= 56 ? 2 : 1;
            int  presMax    = ov >= 86 ? 5 : ov >= 71 ? 4 : ov >= 56 ? 3 : 2;

            var orgs = await db.CombatOrganisations
                .Where(o => o.Prestige >= presMin && o.Prestige <= presMax
                         && (o.PartieID == null || o.PartieID == partieId))
                .ToListAsync();

            if (orgs.Count == 0) return;
            orgCible = orgs[rng.Next(orgs.Count)];
        }

        var ecurieIds = (await db.CombattantsPartie
            .Where(cp => cp.PartieID == partieId)
            .Select(cp => cp.CombattantID)
            .ToListAsync()).ToHashSet();

        byte fOv       = fighter.Overall;
        int  ovMin     = Math.Max(20, fOv - (estFuturChampion ? 20 : 15));
        int  ovMax     = fOv + (estFuturChampion ? 5 : 15);

        var adversairePool = await db.Combattants
            .Where(c => c.CombattantID != fighter.CombattantID
                     && !ecurieIds.Contains(c.CombattantID)
                     && c.Genre == fighter.Genre
                     && c.SemainesIndispo == 0
                     && c.Overall >= ovMin
                     && c.Overall <= ovMax)
            .ToListAsync();

        if (adversairePool.Count == 0) return;

        for (int i = 0; i < nbCombats; i++)
        {
            var adversaire = adversairePool[rng.Next(adversairePool.Count)];
            var sim        = SimulerCombatSimplifie(fighter, adversaire, rng);

            if (sim.EstNul)
            {
                fighter.Nuls++;
                adversaire.Nuls++;
            }
            else
            {
                var gagnant = sim.Gagnant == 1 ? fighter : adversaire;
                var perdant  = sim.Gagnant == 1 ? adversaire : fighter;
                gagnant.Victoires++;
                perdant.Defaites++;
                switch (sim.Methode)
                {
                    case "KO": case "TKO": gagnant.VictoiresKO++; perdant.DefaitesKO++;  break;
                    case "Soumission":     gagnant.VictoiresSub++; perdant.DefaitesSub++; break;
                    default:               gagnant.VictoiresDec++;                        break;
                }
            }

            bool fighterGagne = !sim.EstNul && sim.Gagnant == 1;
            await rankingService.MettreAJourApresCombat(
                partieId, fighter.CombattantID, adversaire.CombattantID,
                orgCible.OrganisationID, fighterGagne, sim.EstNul,
                sim.Methode, orgCible.Prestige);
        }

        await db.SaveChangesAsync();
    }

    private static SimResultatSimplifie SimulerCombatSimplifie(Combattant f1, Combattant f2, Random rng)
    {
        double s1    = f1.Overall + rng.Next(-15, 16) + f1.StatExperience * 0.1 + f1.StatMental * 0.05;
        double s2    = f2.Overall + rng.Next(-15, 16) + f2.StatExperience * 0.1 + f2.StatMental * 0.05;
        double ecart = Math.Abs(s1 - s2);

        if (ecart < 3 && rng.NextDouble() < 0.15)
            return new SimResultatSimplifie(0, true, "Décision", 3);

        int  gIdx   = s1 >= s2 ? 1 : 2;
        var  gagnant = gIdx == 1 ? f1 : f2;
        string methode;
        int    round;

        if (ecart > 20 && gagnant.StatPuissance > 70)
        {
            methode = rng.NextDouble() < 0.6 ? "KO" : "TKO";
            round   = 1 + rng.Next(2);
        }
        else if (ecart > 12 && gagnant.StatSubmission > 65)
        {
            methode = "Soumission";
            round   = 1 + rng.Next(3);
        }
        else if (ecart > 10)
        {
            methode = "Décision unanime";
            round   = 3;
        }
        else
        {
            methode = rng.NextDouble() < 0.3 ? "Décision partagée" : "Décision unanime";
            round   = 3;
        }

        return new SimResultatSimplifie(gIdx, false, methode, round);
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private static Combattant BuildCombattant(
        RosterFighter f, RosterEre snap, int paysId,
        short taille, short allonge, decimal poidsReel) =>
        new()
        {
            Prenom           = f.Prenom,
            NomFamille       = f.Nom,
            Genre            = f.Genre,
            CategorieID      = f.CategorieH,
            PaysOrigineID    = paysId,
            PaysResidenceID  = paysId,
            DateNaissance    = DateTime.Parse(f.DateNaissance),
            StylePrincipalID = f.Style,
            PoidsCombatKg    = GetPoidsCombat(f.Genre, f.CategorieH),
            TailleCm         = taille,
            AllongeCm        = allonge,
            PoidsReelKg      = poidsReel,
            StatFrappeDebout  = (byte)snap.Stats.GetValueOrDefault("frappeDebout",  50),
            StatPuissance     = (byte)snap.Stats.GetValueOrDefault("puissance",     50),
            StatVitesseMains  = (byte)snap.Stats.GetValueOrDefault("vitesseMains",  50),
            StatPrecision     = (byte)snap.Stats.GetValueOrDefault("precision",     50),
            StatCombosDebout  = (byte)snap.Stats.GetValueOrDefault("combosDebout",  50),
            StatKick          = (byte)snap.Stats.GetValueOrDefault("kick",          50),
            StatClinic        = (byte)snap.Stats.GetValueOrDefault("clinic",        50),
            StatEsquive       = (byte)snap.Stats.GetValueOrDefault("esquive",       50),
            StatBlocage       = (byte)snap.Stats.GetValueOrDefault("blocage",       50),
            StatFootwork      = (byte)snap.Stats.GetValueOrDefault("footwork",      50),
            StatWrestling     = (byte)snap.Stats.GetValueOrDefault("wrestling",     50),
            StatTakedown      = (byte)snap.Stats.GetValueOrDefault("takedown",      50),
            StatAntiTakedown  = (byte)snap.Stats.GetValueOrDefault("antiTakedown",  50),
            StatControleSol   = (byte)snap.Stats.GetValueOrDefault("controleSol",   50),
            StatJiuJitsu      = (byte)snap.Stats.GetValueOrDefault("jiuJitsu",      50),
            StatSubmission    = (byte)snap.Stats.GetValueOrDefault("submission",    50),
            StatEvasionSub    = (byte)snap.Stats.GetValueOrDefault("evasionSub",    50),
            StatCardio        = (byte)snap.Stats.GetValueOrDefault("cardio",        50),
            StatForce         = (byte)snap.Stats.GetValueOrDefault("force",         50),
            StatVitesse       = (byte)snap.Stats.GetValueOrDefault("vitesse",       50),
            StatAgilite       = (byte)snap.Stats.GetValueOrDefault("agilite",       50),
            StatMentoniere    = (byte)snap.Stats.GetValueOrDefault("mentoniere",    50),
            StatRecuperation  = (byte)snap.Stats.GetValueOrDefault("recuperation",  50),
            StatMental        = (byte)snap.Stats.GetValueOrDefault("mental",        50),
            StatExperience    = (byte)snap.Stats.GetValueOrDefault("experience",    50),
            StatCoaching      = (byte)snap.Stats.GetValueOrDefault("coaching",      50),
            StatAdaptation    = (byte)snap.Stats.GetValueOrDefault("adaptation",    50),
            Potentiel         = (byte)Math.Min(100, snap.Potentiel),
            Progression       = (byte)Math.Min(100, snap.Progression),
            Moral             = 50,
            Fatigue           = 0,
            Motivation        = 60,
            BlessureGravite   = 0,
            SemainesIndispo   = 0,
            Statut            = "Libre",
            Salaire           = 0,
            PrimeSigne        = 0,
            Valeur            = CalculerValeur(snap),
            SousContrat       = false,
            Victoires         = 0,
            Defaites          = 0,
            Nuls              = 0,
            VictoiresKO       = 0,
            VictoiresSub      = 0,
            VictoiresDec      = 0,
            DefaitesKO        = 0,
            DefaitesSub       = 0,
            DateCreation      = DateTime.UtcNow
        };

    private static int CalculerValeur(RosterEre snapshot)
    {
        var vals = snapshot.Stats.Values;
        int moyenne = vals.Count > 0 ? (int)vals.Average() : 50;
        return moyenne * 500 + snapshot.Record.V * 200;
    }

    private static decimal GetPoidsCombat(string genre, int catId) =>
        genre == "H" ? catId switch
        {
            1 => 52.2m, 2 => 56.7m, 3 => 61.2m, 4 => 65.8m,  5 => 70.3m,
            6 => 77.1m, 7 => 83.9m, 8 => 93.0m, 9 => 120.2m, 10 => 120.2m,
            _ => 77.1m
        } : catId switch
        {
            1 => 52.2m, 2 => 56.7m, 3 => 61.2m, 4 => 65.8m, 5 => 70.3m, 6 => 77.1m,
            _ => 61.2m
        };

    private static (short taille, short allonge, decimal poidsReel) GenererPhysique(
        string genre, int catId, Random rng)
    {
        short tailleMin, tailleRange;
        decimal poidsMin, poidsRange;

        if (genre == "H")
            (tailleMin, tailleRange, poidsMin, poidsRange) = catId switch
            {
                1  => ((short)160, (short)12, 54.0m, 4.0m),
                2  => ((short)162, (short)13, 59.0m, 4.0m),
                3  => ((short)165, (short)13, 63.0m, 5.0m),
                4  => ((short)168, (short)12, 68.0m, 5.0m),
                5  => ((short)170, (short)13, 73.0m, 5.0m),
                6  => ((short)175, (short)13, 79.0m, 5.0m),
                7  => ((short)178, (short)15, 85.0m, 8.0m),
                8  => ((short)183, (short)13, 94.0m, 8.0m),
                9  => ((short)185, (short)13, 103.0m, 17.0m),
                10 => ((short)185, (short)18, 115.0m, 30.0m),
                _  => ((short)175, (short)13, 79.0m, 5.0m)
            };
        else
            (tailleMin, tailleRange, poidsMin, poidsRange) = catId switch
            {
                1 => ((short)155, (short)13, 52.0m, 5.0m),
                2 => ((short)157, (short)13, 57.0m, 4.0m),
                3 => ((short)160, (short)13, 61.0m, 5.0m),
                4 => ((short)163, (short)15, 66.0m, 4.0m),
                5 => ((short)165, (short)13, 70.0m, 5.0m),
                6 => ((short)168, (short)12, 75.0m, 5.0m),
                _ => ((short)160, (short)13, 61.0m, 5.0m)
            };

        short   taille   = (short)(tailleMin + rng.Next(tailleRange));
        short   allonge  = (short)(taille + rng.Next(13) - 4);
        decimal poidsReel = Math.Round(poidsMin + (decimal)rng.NextDouble() * poidsRange, 1);

        return (taille, allonge, poidsReel);
    }
}
