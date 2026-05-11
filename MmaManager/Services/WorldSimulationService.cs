using Microsoft.EntityFrameworkCore;
using MmaManager.Data;
using MmaManager.Models;
using MmaManager.Models.Dtos;

namespace MmaManager.Services;

public class WorldSimulationService(MmaContext db, RankingService rankingService)
{
    private record SimResultatSimplifie(int Gagnant, bool EstNul, string Methode, int Round);

    public async Task<List<NouvelleMondeDto>> SimulerTour(int partieId, int annee, int mois, Random rng)
    {
        var nouvelles = new List<NouvelleMondeDto>();

        var ecurieIds = (await db.CombattantsPartie
            .Where(cp => cp.PartieID == partieId)
            .Select(cp => cp.CombattantID)
            .ToListAsync()).ToHashSet();

        var organisations = await db.CombatOrganisations
            .Where(o => o.AnneeCreation <= annee
                     && (o.PartieID == null || o.PartieID == partieId))
            .ToListAsync();
        if (organisations.Count == 0) return nouvelles;

        int nbCombats = Math.Min(8, 3 + organisations.Count / 3);

        // Catégorie Open Weight
        var openWeightCat = await db.CategoriesPoidsH.FirstOrDefaultAsync(c => c.Nom == "Open Weight");
        int openWeightCatId = openWeightCat?.CategorieID ?? -1;

        // Catégories par organisation actives cette année
        var orgCategories = await db.OrganisationCategories
            .Where(oc => oc.AnneeIntroduction <= annee)
            .ToListAsync();
        var orgCatLookup = orgCategories
            .GroupBy(oc => oc.OrganisationID)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Charger tous les fighters non-écurie pour progression/déclin/guérison
        var tousNonEcurie = await db.Combattants
            .Where(c => !ecurieIds.Contains(c.CombattantID))
            .ToListAsync();

        foreach (var f in tousNonEcurie)
        {
            if (f.SemainesIndispo > 0)
            {
                f.SemainesIndispo--;
                if (f.SemainesIndispo == 0) { f.BlessureGravite = 0; f.BlessureZone = null; }
            }

            if (rng.NextDouble() < 0.30)
                ProgresserStatAleatoire(f, rng);

            int age = CalculerAge(f.DateNaissance, annee, mois);
            if (age > 33)
                DeclinStatWorld(f, age > 38 ? 0.30 : 0.15, age > 38 ? 2 : 1, rng);
        }

        // Candidats au combat (non blessés, Overall > 30)
        var candidats = tousNonEcurie
            .Where(c => c.Overall > 30 && c.SemainesIndispo == 0)
            .ToList();

        if (candidats.Count < 2)
        {
            await db.SaveChangesAsync();
            return nouvelles;
        }

        var dejaUtilises = new HashSet<int>();
        var orgsShufflee  = organisations.ToList();

        for (int i = 0; i < nbCombats; i++)
        {
            // Mélanger les orgs pour varier
            for (int s = orgsShufflee.Count - 1; s > 0; s--)
            {
                int t = rng.Next(s + 1);
                (orgsShufflee[s], orgsShufflee[t]) = (orgsShufflee[t], orgsShufflee[s]);
            }

            CombatOrganisation? orgChoisie = null;
            Combattant? f1 = null, f2 = null;
            string genre      = "H";
            int    categorieID = 0;

            foreach (var orgTry in orgsShufflee)
            {
                var orgCats = orgCatLookup.GetValueOrDefault(orgTry.OrganisationID, []);
                if (!orgCats.Any()) continue;

                // Essai Open Weight (cross-catégorie, même genre)
                foreach (var owCat in orgCats.Where(oc => oc.EstOpenWeight))
                {
                    var avail = candidats
                        .Where(c => c.Genre == owCat.Genre && !dejaUtilises.Contains(c.CombattantID))
                        .ToList();
                    if (avail.Count < 2) continue;

                    genre      = owCat.Genre;
                    categorieID = openWeightCatId;
                    var top = avail.OrderByDescending(c => c.Overall).Take(10).ToList();
                    for (int j = top.Count - 1; j > 0; j--)
                    {
                        int k = rng.Next(j + 1);
                        (top[j], top[k]) = (top[k], top[j]);
                    }
                    f1 = top[0]; f2 = top[1]; orgChoisie = orgTry;
                    break;
                }
                if (orgChoisie != null) break;

                // Essai catégories spécifiques
                var specificCats = orgCats
                    .Where(oc => !oc.EstOpenWeight)
                    .OrderBy(_ => rng.Next())
                    .ToList();
                foreach (var cat in specificCats)
                {
                    var avail = candidats
                        .Where(c => c.Genre == cat.Genre && c.CategorieID == cat.CategorieID
                                 && !dejaUtilises.Contains(c.CombattantID))
                        .ToList();
                    if (avail.Count < 2) continue;

                    genre      = cat.Genre;
                    categorieID = cat.CategorieID;
                    var top = avail.OrderByDescending(c => c.Overall).Take(10).ToList();
                    for (int j = top.Count - 1; j > 0; j--)
                    {
                        int k = rng.Next(j + 1);
                        (top[j], top[k]) = (top[k], top[j]);
                    }
                    f1 = top[0]; f2 = top[1]; orgChoisie = orgTry;
                    break;
                }
                if (orgChoisie != null) break;
            }

            if (orgChoisie == null || f1 == null || f2 == null) break;

            dejaUtilises.Add(f1.CombattantID);
            dejaUtilises.Add(f2.CombattantID);

            var org = orgChoisie;
            var sim = SimulerCombatSimplifie(f1, f2, rng);

            Combattant? gagnant = sim.EstNul ? null : (sim.Gagnant == 1 ? f1 : f2);
            Combattant? perdant  = sim.EstNul ? null : (sim.Gagnant == 1 ? f2 : f1);

            if (sim.EstNul)
            {
                f1.Nuls++;
                f2.Nuls++;
            }
            else
            {
                gagnant!.Victoires++;
                perdant!.Defaites++;
                switch (sim.Methode)
                {
                    case "KO": case "TKO": gagnant.VictoiresKO++; perdant.DefaitesKO++;  break;
                    case "Soumission":     gagnant.VictoiresSub++; perdant.DefaitesSub++; break;
                    default:               gagnant.VictoiresDec++;                        break;
                }

                double blessChance = sim.Methode is "KO" or "TKO" ? 0.50 : 0.15;
                if (rng.NextDouble() < blessChance)
                {
                    perdant.BlessureGravite = 1;
                    perdant.BlessureZone    = "Combat";
                    perdant.SemainesIndispo = (short)(1 + rng.Next(3));
                }
            }

            if (sim.EstNul)
                await rankingService.MettreAJourApresCombat(
                    partieId, f1.CombattantID, f2.CombattantID,
                    org.OrganisationID, false, true, sim.Methode, org.Prestige, categorieID);
            else
                await rankingService.MettreAJourApresCombat(
                    partieId, gagnant!.CombattantID, perdant!.CombattantID,
                    org.OrganisationID, true, false, sim.Methode, org.Prestige, categorieID);

            // ── Logique de ceinture ───────────────────────────────
            bool changementChampion = false;
            if (!sim.EstNul)
            {
                var ceinture = await db.ChampionCeintures
                    .FirstOrDefaultAsync(c => c.PartieID       == partieId
                                           && c.OrganisationID == org.OrganisationID
                                           && c.CategorieID    == categorieID
                                           && c.Genre          == genre);

                if (ceinture is not null)
                {
                    if (ceinture.CombattantID == perdant!.CombattantID)
                    {
                        ceinture.CombattantID = gagnant!.CombattantID;
                        ceinture.NbDefenses   = 0;
                        changementChampion    = true;
                    }
                    else if (ceinture.CombattantID == gagnant!.CombattantID)
                        ceinture.NbDefenses++;
                }
                else
                {
                    // Créer la ceinture si les deux sont dans le top 5 par points
                    var top5 = await db.RankingEntries
                        .Where(r => r.PartieID       == partieId
                                 && r.OrganisationID == org.OrganisationID
                                 && r.CategorieID    == categorieID
                                 && r.Genre          == genre)
                        .OrderByDescending(r => r.Points)
                        .Select(r => r.CombattantID)
                        .Take(5)
                        .ToListAsync();

                    if (top5.Contains(gagnant!.CombattantID) && top5.Contains(perdant!.CombattantID))
                    {
                        db.ChampionCeintures.Add(new ChampionCeinture
                        {
                            PartieID       = partieId,
                            OrganisationID = org.OrganisationID,
                            CategorieID    = categorieID,
                            Genre          = genre,
                            CombattantID   = gagnant.CombattantID,
                            TourObtention  = 0,
                            NbDefenses     = 0
                        });
                        changementChampion = true;
                    }
                }
            }

            // ── Nouvelles ─────────────────────────────────────────
            string nomG  = gagnant is null ? "" : $"{gagnant.Prenom} {gagnant.NomFamille}";
            string nomP  = perdant  is null ? "" : $"{perdant.Prenom} {perdant.NomFamille}";
            string nomF1 = $"{f1.Prenom} {f1.NomFamille}";
            string nomF2 = $"{f2.Prenom} {f2.NomFamille}";

            if (changementChampion)
                nouvelles.Add(new NouvelleMondeDto(
                    $"🏆 Nouveau champion ! {nomG} s'empare de la ceinture {org.Nom}",
                    $"Défaite de {nomP} par {sim.Methode} au round {sim.Round}",
                    "majeure"));
            else if (sim.EstNul)
                nouvelles.Add(new NouvelleMondeDto(
                    $"🤝 Match nul entre {nomF1} et {nomF2}",
                    $"{org.Nom} · {sim.Methode} · Round {sim.Round}",
                    "normale"));
            else if (sim.Methode is "KO" or "TKO")
                nouvelles.Add(new NouvelleMondeDto(
                    $"💥 {nomG} signe un {sim.Methode} brutal",
                    $"Défaite de {nomP} à {org.Nom} · Round {sim.Round}",
                    "importante"));
            else
                nouvelles.Add(new NouvelleMondeDto(
                    $"⚔️ {nomG} bat {nomP}",
                    $"{org.Nom} · {sim.Methode} · Round {sim.Round}",
                    "normale"));
        }

        await db.SaveChangesAsync();
        return nouvelles;
    }

    private static SimResultatSimplifie SimulerCombatSimplifie(Combattant f1, Combattant f2, Random rng)
    {
        double s1 = f1.Overall + rng.Next(-15, 16) + f1.StatExperience * 0.1 + f1.StatMental * 0.05;
        double s2 = f2.Overall + rng.Next(-15, 16) + f2.StatExperience * 0.1 + f2.StatMental * 0.05;
        double ecart = Math.Abs(s1 - s2);

        if (ecart < 3 && rng.NextDouble() < 0.15)
            return new SimResultatSimplifie(0, true, "Décision", 3);

        int gIdx   = s1 >= s2 ? 1 : 2;
        var gagnant = gIdx == 1 ? f1 : f2;

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

    private static void ProgresserStatAleatoire(Combattant f, Random rng)
    {
        switch (rng.Next(10))
        {
            case 0: if (f.StatFrappeDebout < f.Potentiel) f.StatFrappeDebout  = (byte)Math.Min(99, f.StatFrappeDebout  + 1); break;
            case 1: if (f.StatPuissance    < f.Potentiel) f.StatPuissance     = (byte)Math.Min(99, f.StatPuissance     + 1); break;
            case 2: if (f.StatPrecision    < f.Potentiel) f.StatPrecision     = (byte)Math.Min(99, f.StatPrecision     + 1); break;
            case 3: if (f.StatWrestling    < f.Potentiel) f.StatWrestling     = (byte)Math.Min(99, f.StatWrestling     + 1); break;
            case 4: if (f.StatTakedown     < f.Potentiel) f.StatTakedown      = (byte)Math.Min(99, f.StatTakedown      + 1); break;
            case 5: if (f.StatJiuJitsu     < f.Potentiel) f.StatJiuJitsu      = (byte)Math.Min(99, f.StatJiuJitsu      + 1); break;
            case 6: if (f.StatCardio       < f.Potentiel) f.StatCardio        = (byte)Math.Min(99, f.StatCardio        + 1); break;
            case 7: if (f.StatVitesse      < f.Potentiel) f.StatVitesse       = (byte)Math.Min(99, f.StatVitesse       + 1); break;
            case 8: if (f.StatMental       < f.Potentiel) f.StatMental        = (byte)Math.Min(99, f.StatMental        + 1); break;
            case 9: if (f.StatAdaptation   < f.Potentiel) f.StatAdaptation    = (byte)Math.Min(99, f.StatAdaptation    + 1); break;
        }
    }

    private static void DeclinStatWorld(Combattant f, double chance, int maxLoss, Random rng)
    {
        void D(Func<byte> get, Action<byte> set)
        {
            if (rng.NextDouble() < chance)
                set((byte)Math.Max(10, get() - (1 + rng.Next(maxLoss))));
        }
        D(() => f.StatVitesse,      v => f.StatVitesse      = v);
        D(() => f.StatAgilite,      v => f.StatAgilite      = v);
        D(() => f.StatCardio,       v => f.StatCardio       = v);
        D(() => f.StatRecuperation, v => f.StatRecuperation = v);
        D(() => f.StatMentoniere,   v => f.StatMentoniere   = v);
        D(() => f.StatForce,        v => f.StatForce        = v);
        D(() => f.StatVitesseMains, v => f.StatVitesseMains = v);
        D(() => f.StatFootwork,     v => f.StatFootwork     = v);
    }

    private static int CalculerAge(DateTime dateNaissance, int annee, int mois)
    {
        int age = annee - dateNaissance.Year;
        if (mois < dateNaissance.Month) age--;
        return Math.Max(0, age);
    }
}
