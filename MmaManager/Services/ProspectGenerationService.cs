using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MmaManager.Data;
using MmaManager.Models;

public class ProspectGenerationService(MmaContext db)
{
    private record NomsPays(
        List<string> Prenoms_h,
        List<string> Prenoms_f,
        List<string> Noms);

    private Dictionary<string, NomsPays>? _nomsParPays;

    private Dictionary<string, NomsPays> ChargerNoms()
    {
        if (_nomsParPays is not null) return _nomsParPays;

        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "noms_par_pays.json");
        if (!File.Exists(jsonPath))
        {
            return _nomsParPays = new Dictionary<string, NomsPays>
            {
                ["_default"] = new(
                    ["Alex", "Daniel", "Max", "Marco", "Victor", "Leo", "Adam", "Erik", "Andre", "Oscar"],
                    ["Maria", "Sofia", "Elena", "Anna", "Sara", "Lina", "Nina", "Eva", "Mia", "Leila"],
                    ["Silva", "Kim", "Ali", "Smith", "Garcia", "Rossi", "Santos", "Petrov", "Tanaka", "Hansen"])
            };
        }

        var json = File.ReadAllText(jsonPath);
        _nomsParPays = JsonSerializer.Deserialize<Dictionary<string, NomsPays>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new Dictionary<string, NomsPays>();
        return _nomsParPays;
    }

    private (string prenom, string nom) GenererNom(string codePays, string genre, Random rng)
    {
        var noms = ChargerNoms();
        if (!noms.TryGetValue(codePays, out var pool))
            pool = noms.GetValueOrDefault("_default") ?? noms.Values.First();

        var prenoms = genre == "H" ? pool.Prenoms_h : pool.Prenoms_f;
        return (prenoms[rng.Next(prenoms.Count)], pool.Noms[rng.Next(pool.Noms.Count)]);
    }

    public async Task GenererProspects(int paysResidenceId, int anneeDepart)
    {
        var rng = new Random();

        var paysLocal = await db.Pays.FindAsync(paysResidenceId);
        var codePaysLocal = paysLocal?.Code ?? "_default";

        var paysInternationaux = await db.Pays
            .Where(p => p.PaysID != paysResidenceId)
            .ToListAsync();

        var prospects = new List<Combattant>();

        for (int i = 0; i < 40; i++)
            prospects.Add(CreerProspect(codePaysLocal, paysResidenceId, paysResidenceId, anneeDepart, rng));

        if (paysInternationaux.Count > 0)
        {
            for (int i = 0; i < 20; i++)
            {
                var p = paysInternationaux[rng.Next(paysInternationaux.Count)];
                prospects.Add(CreerProspect(p.Code, p.PaysID, p.PaysID, anneeDepart, rng));
            }
        }

        db.Combattants.AddRange(prospects);
        await db.SaveChangesAsync();
    }

    private Combattant CreerProspect(string codePays, int paysOrigineId, int paysResidenceId, int anneeDepart, Random rng)
    {
        var genre = rng.NextDouble() < 0.75 ? "H" : "F";
        var (prenom, nom) = GenererNom(codePays, genre, rng);

        int catMax = genre == "H" ? 10 : 6;
        int catId  = 1 + rng.Next(catMax);

        byte BaseStat() => (byte)(20 + rng.Next(31));

        int age = 18 + rng.Next(6);
        var dob = new DateTime(anneeDepart - age, 1 + rng.Next(12), 1 + rng.Next(28));

        int valeur = 2000 + rng.Next(10001);

        var (taille, allonge, poidsReel) = GetPhysique(genre, catId, rng);

        return new Combattant
        {
            Prenom          = prenom,
            NomFamille      = nom,
            Genre           = genre,
            CategorieID     = catId,
            PaysOrigineID   = paysOrigineId,
            PaysResidenceID = paysResidenceId,
            DateNaissance   = dob,
            PoidsCombatKg   = GetPoidsCombat(genre, catId),
            TailleCm        = taille,
            AllongeCm       = allonge,
            PoidsReelKg     = poidsReel,
            StylePrincipalID = 1 + rng.Next(7),

            StatFrappeDebout  = BaseStat(),
            StatPuissance     = BaseStat(),
            StatVitesseMains  = BaseStat(),
            StatPrecision     = BaseStat(),
            StatCombosDebout  = BaseStat(),
            StatKick          = BaseStat(),
            StatClinic        = BaseStat(),
            StatEsquive       = BaseStat(),
            StatBlocage       = BaseStat(),
            StatFootwork      = BaseStat(),
            StatWrestling     = BaseStat(),
            StatTakedown      = BaseStat(),
            StatAntiTakedown  = BaseStat(),
            StatControleSol   = BaseStat(),
            StatJiuJitsu      = BaseStat(),
            StatSubmission    = BaseStat(),
            StatEvasionSub    = BaseStat(),
            StatCardio        = BaseStat(),
            StatForce         = BaseStat(),
            StatVitesse       = BaseStat(),
            StatAgilite       = BaseStat(),
            StatMentoniere    = BaseStat(),
            StatRecuperation  = BaseStat(),
            StatMental        = BaseStat(),
            StatExperience    = (byte)(20 + rng.Next(15)),
            StatCoaching      = BaseStat(),
            StatAdaptation    = BaseStat(),

            Potentiel   = (byte)(40 + rng.Next(41)),
            Progression = (byte)(50 + rng.Next(31)),
            Moral       = 50,
            Fatigue     = 0,
            Motivation  = (byte)(50 + rng.Next(21)),

            BlessureGravite = 0,
            SemainesIndispo = 0,

            Statut      = "Libre",
            Salaire     = valeur / 80,
            PrimeSigne  = 0,
            Valeur      = valeur,
            SousContrat = false,

            Victoires    = 0, Defaites    = 0, Nuls        = 0,
            VictoiresKO  = 0, VictoiresSub = 0, VictoiresDec = 0,
            DefaitesKO   = 0, DefaitesSub  = 0,

            DateCreation = DateTime.UtcNow,
        };
    }

    private static decimal GetPoidsCombat(string genre, int catId)
    {
        if (genre == "H") return catId switch
        {
            1 => 52.2m, 2 => 56.7m, 3 => 61.2m, 4 => 65.8m, 5 => 70.3m,
            6 => 77.1m, 7 => 83.9m, 8 => 93.0m, _ => 120.2m
        };
        return catId switch
        {
            1 => 52.2m, 2 => 56.7m, 3 => 61.2m, 4 => 65.8m, 5 => 70.3m, _ => 77.1m
        };
    }

    private static (short taille, short allonge, decimal poidsReel) GetPhysique(string genre, int catId, Random rng)
    {
        short tailleMin, tailleRange;
        decimal poidsMin, poidsRange;

        if (genre == "H")
        {
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
        }
        else
        {
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
        }

        short taille = (short)(tailleMin + rng.Next(tailleRange));
        short allonge = (short)(taille + rng.Next(13) - 4);
        decimal poidsReel = poidsMin + Math.Round((decimal)rng.NextDouble() * poidsRange, 1);

        return (taille, allonge, poidsReel);
    }

    public async Task GenererOrganisationsLocales(int paysResidenceId, int anneeDepart, int partieId)
    {
        var pays = await db.Pays.FindAsync(paysResidenceId);
        var n = pays?.Nom ?? "Local";

        db.CombatOrganisations.AddRange(
            new CombatOrganisation
            {
                Nom           = $"{n} Underground Fight Club",
                PaysOrigineID = paysResidenceId,
                AnneeCreation = anneeDepart - 5,
                Prestige      = 1,
                Description   = "Combats clandestins organisés dans des entrepôts et garages. Peu de règles, beaucoup d'ambiance.",
                EstFictive    = true,
                PartieID      = partieId
            },
            new CombatOrganisation
            {
                Nom           = $"{n} Brawl Circuit",
                PaysOrigineID = paysResidenceId,
                AnneeCreation = anneeDepart - 3,
                Prestige      = 1,
                Description   = "Petite organisation locale qui fait tourner des cartes régulières dans les salles de quartier.",
                EstFictive    = true,
                PartieID      = partieId
            },
            new CombatOrganisation
            {
                Nom           = $"{n} Combat League",
                PaysOrigineID = paysResidenceId,
                AnneeCreation = anneeDepart - 1,
                Prestige      = 2,
                Description   = "Organisation régionale en pleine croissance, attire les meilleurs combattants du pays.",
                EstFictive    = true,
                PartieID      = partieId
            }
        );
        await db.SaveChangesAsync();
    }
}
