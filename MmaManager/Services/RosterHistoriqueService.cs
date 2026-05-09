using Microsoft.EntityFrameworkCore;
using MmaManager.Data;
using MmaManager.Models;

namespace MmaManager.Services;

public class RosterHistoriqueService(MmaContext db)
{
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

    public async Task ChargerRoster(int partieId, int anneeDepart)
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "roster_historique.json");
        if (!File.Exists(jsonPath)) return;

        var json = await File.ReadAllTextAsync(jsonPath);
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var roster = System.Text.Json.JsonSerializer.Deserialize<RosterFile>(json, options);
        if (roster?.Fighters is null) return;

        string ereKey = anneeDepart switch
        {
            < 2000 => "pionniers",
            < 2013 => "goldenAge",
            _      => "modernMMA"
        };

        var organisations = await db.CombatOrganisations.ToListAsync();
        var pays = await db.Pays.ToDictionaryAsync(p => p.Code, p => p.PaysID);

        var rng = new Random();

        foreach (var f in roster.Fighters)
        {
            if (!f.Eres.TryGetValue(ereKey, out var snapshot) || snapshot is null)
                continue;

            if (snapshot.DateDebut > anneeDepart)
                continue;

            if (!pays.TryGetValue(f.Pays, out int paysId))
                continue;

            // Éviter les doublons
            var existe = await db.Combattants
                .AnyAsync(c => c.Prenom == f.Prenom && c.NomFamille == f.Nom);
            if (existe) continue;

            var (taille, allonge, poidsReel) = GenererPhysique(f.Genre, f.CategorieH, rng);

            var combattant = new Combattant
            {
                Prenom          = f.Prenom,
                NomFamille      = f.Nom,
                Genre           = f.Genre,
                CategorieID     = f.CategorieH,
                PaysOrigineID   = paysId,
                PaysResidenceID = paysId,
                DateNaissance   = DateTime.Parse(f.DateNaissance),
                StylePrincipalID = f.Style,
                PoidsCombatKg   = GetPoidsCombat(f.Genre, f.CategorieH),
                TailleCm        = taille,
                AllongeCm       = allonge,
                PoidsReelKg     = poidsReel,
                // Stats
                StatFrappeDebout  = (byte)snapshot.Stats.GetValueOrDefault("frappeDebout",  50),
                StatPuissance     = (byte)snapshot.Stats.GetValueOrDefault("puissance",     50),
                StatVitesseMains  = (byte)snapshot.Stats.GetValueOrDefault("vitesseMains",  50),
                StatPrecision     = (byte)snapshot.Stats.GetValueOrDefault("precision",     50),
                StatCombosDebout  = (byte)snapshot.Stats.GetValueOrDefault("combosDebout",  50),
                StatKick          = (byte)snapshot.Stats.GetValueOrDefault("kick",          50),
                StatClinic        = (byte)snapshot.Stats.GetValueOrDefault("clinic",        50),
                StatEsquive       = (byte)snapshot.Stats.GetValueOrDefault("esquive",       50),
                StatBlocage       = (byte)snapshot.Stats.GetValueOrDefault("blocage",       50),
                StatFootwork      = (byte)snapshot.Stats.GetValueOrDefault("footwork",      50),
                StatWrestling     = (byte)snapshot.Stats.GetValueOrDefault("wrestling",     50),
                StatTakedown      = (byte)snapshot.Stats.GetValueOrDefault("takedown",      50),
                StatAntiTakedown  = (byte)snapshot.Stats.GetValueOrDefault("antiTakedown",  50),
                StatControleSol   = (byte)snapshot.Stats.GetValueOrDefault("controleSol",   50),
                StatJiuJitsu      = (byte)snapshot.Stats.GetValueOrDefault("jiuJitsu",      50),
                StatSubmission    = (byte)snapshot.Stats.GetValueOrDefault("submission",    50),
                StatEvasionSub    = (byte)snapshot.Stats.GetValueOrDefault("evasionSub",    50),
                StatCardio        = (byte)snapshot.Stats.GetValueOrDefault("cardio",        50),
                StatForce         = (byte)snapshot.Stats.GetValueOrDefault("force",         50),
                StatVitesse       = (byte)snapshot.Stats.GetValueOrDefault("vitesse",       50),
                StatAgilite       = (byte)snapshot.Stats.GetValueOrDefault("agilite",       50),
                StatMentoniere    = (byte)snapshot.Stats.GetValueOrDefault("mentoniere",    50),
                StatRecuperation  = (byte)snapshot.Stats.GetValueOrDefault("recuperation",  50),
                StatMental        = (byte)snapshot.Stats.GetValueOrDefault("mental",        50),
                StatExperience    = (byte)snapshot.Stats.GetValueOrDefault("experience",    50),
                StatCoaching      = (byte)snapshot.Stats.GetValueOrDefault("coaching",      50),
                StatAdaptation    = (byte)snapshot.Stats.GetValueOrDefault("adaptation",    50),
                // Développement
                Potentiel   = (byte)Math.Min(100, snapshot.Potentiel),
                Progression = (byte)Math.Min(100, snapshot.Progression),
                // État
                Moral           = 50,
                Fatigue         = 0,
                Motivation      = 60,
                BlessureGravite = 0,
                SemainesIndispo = 0,
                Statut          = "Libre",
                Salaire         = 0,
                PrimeSigne      = 0,
                Valeur          = CalculerValeur(snapshot),
                SousContrat     = false,
                // Record
                Victoires    = (short)snapshot.Record.V,
                Defaites     = (short)snapshot.Record.D,
                Nuls         = (short)snapshot.Record.N,
                VictoiresKO  = (short)snapshot.Record.Ko,
                VictoiresSub = (short)snapshot.Record.Sub,
                VictoiresDec = (short)snapshot.Record.Dec,
                DefaitesKO   = (short)snapshot.Record.Dko,
                DefaitesSub  = (short)snapshot.Record.Dsub,
                DateCreation = DateTime.UtcNow
            };

            db.Combattants.Add(combattant);
            await db.SaveChangesAsync();

            if (snapshot.Classement is not null)
            {
                var org = organisations.FirstOrDefault(o =>
                    o.Nom.Equals(snapshot.Classement.Organisation, StringComparison.OrdinalIgnoreCase));

                if (org is not null)
                {
                    int rang          = snapshot.Classement.Rang;
                    bool estChampion  = snapshot.Classement.Champion;
                    int points        = estChampion ? 5000 : Math.Max(0, (16 - rang)) * 300;
                    int ptsMondial    = estChampion ? 8000 : Math.Max(0, (16 - rang)) * 500;

                    db.RankingEntries.Add(new RankingEntry
                    {
                        PartieID       = partieId,
                        CombattantID   = combattant.CombattantID,
                        OrganisationID = org.OrganisationID,
                        Genre          = f.Genre,
                        CategorieID    = f.CategorieH,
                        Points         = points,
                        Rang           = estChampion ? 0 : rang,
                        PointsMondiaux = ptsMondial
                    });

                    if (estChampion)
                    {
                        var ceintureExistante = await db.ChampionCeintures
                            .FirstOrDefaultAsync(c => c.PartieID == partieId
                                && c.OrganisationID == org.OrganisationID
                                && c.CategorieID == f.CategorieH
                                && c.Genre == f.Genre);

                        if (ceintureExistante is null)
                        {
                            db.ChampionCeintures.Add(new ChampionCeinture
                            {
                                PartieID       = partieId,
                                OrganisationID = org.OrganisationID,
                                CategorieID    = f.CategorieH,
                                Genre          = f.Genre,
                                CombattantID   = combattant.CombattantID,
                                TourObtention  = 0,
                                NbDefenses     = 0
                            });
                        }
                    }
                }
            }
        }

        await db.SaveChangesAsync();
    }

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
