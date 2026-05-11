using Microsoft.EntityFrameworkCore;
using MmaManager.Data;
using MmaManager.Models;
using MmaManager.Models.Dtos;

namespace MmaManager.Services;

public class RankingService(MmaContext db)
{
    public async Task MettreAJourApresCombat(
        int partieId, int combattantId, int adversaireId, int organisationId,
        bool estVictoire, bool estNul, string methode, int prestigeOrga,
        int? rankCatId = null)
    {
        int pointsBase    = estNul ? 30 : (estVictoire ? 100 : 10);
        int bonusMethode  = estVictoire ? methode switch
        {
            "KO" or "TKO" => 30,
            "Soumission"  => 25,
            _             => 0
        } : 0;

        double prestigeMult   = Math.Max(0.5, prestigeOrga / 3.0);
        int    pointsOrga     = (int)Math.Round((pointsBase + bonusMethode) * prestigeMult);
        int    pointsMondiaux = (int)Math.Round(pointsOrga * (prestigeOrga / 2.0));

        var combattant = await db.Combattants.FindAsync(combattantId);
        if (combattant is null) return;

        int combCatId = rankCatId ?? await DeterminerCategoriePourOrga(partieId, organisationId, combattant.Genre, combattant.CategorieID);

        await UpsertEntry(partieId, combattantId, organisationId, combattant.Genre,
            combCatId, pointsOrga, pointsMondiaux);

        var adversaire = await db.Combattants.FindAsync(adversaireId);
        int advCatId   = combCatId;
        if (adversaire is not null)
        {
            int pointsAdvBase     = estNul ? 30 : (estVictoire ? 10 : 100);
            int bonusAdv          = !estVictoire && !estNul ? bonusMethode : 0;
            int pointsOrgaAdv     = (int)Math.Round((pointsAdvBase + bonusAdv) * prestigeMult);
            int pointsMondiauxAdv = (int)Math.Round(pointsOrgaAdv * (prestigeOrga / 2.0));

            advCatId = rankCatId ?? await DeterminerCategoriePourOrga(partieId, organisationId, adversaire.Genre, adversaire.CategorieID);
            await UpsertEntry(partieId, adversaireId, organisationId, adversaire.Genre,
                advCatId, pointsOrgaAdv, pointsMondiauxAdv);
        }

        await db.SaveChangesAsync();

        await RecalculerRangs(partieId, organisationId, combattant.Genre, combCatId);
        if (adversaire is not null && advCatId != combCatId)
            await RecalculerRangs(partieId, organisationId, adversaire.Genre, advCatId);
    }

    private async Task<int> DeterminerCategoriePourOrga(int partieId, int organisationId, string genre, int naturalCatId)
    {
        var annee = await db.Parties
            .Where(p => p.PartieID == partieId)
            .Select(p => p.AnneeActuelle)
            .FirstOrDefaultAsync();

        var orgCats = await db.OrganisationCategories
            .Where(oc => oc.OrganisationID == organisationId
                      && oc.Genre == genre
                      && oc.AnneeIntroduction <= annee)
            .ToListAsync();

        if (!orgCats.Any()) return naturalCatId;

        if (orgCats.Any(oc => !oc.EstOpenWeight && oc.CategorieID == naturalCatId))
            return naturalCatId;

        if (orgCats.Any(oc => oc.EstOpenWeight))
        {
            var owCat = await db.CategoriesPoidsH.FirstOrDefaultAsync(c => c.Nom == "Open Weight");
            if (owCat is not null) return owCat.CategorieID;
        }

        return naturalCatId;
    }

    private async Task UpsertEntry(int partieId, int combattantId, int orgId,
        string genre, int catId, int points, int pointsMondiaux)
    {
        var entry = await db.RankingEntries.FirstOrDefaultAsync(r =>
            r.PartieID == partieId && r.OrganisationID == orgId && r.CombattantID == combattantId);

        if (entry is null)
        {
            db.RankingEntries.Add(new RankingEntry
            {
                PartieID       = partieId,
                CombattantID   = combattantId,
                OrganisationID = orgId,
                Genre          = genre,
                CategorieID    = catId,
                Points         = points,
                PointsMondiaux = pointsMondiaux
            });
        }
        else
        {
            entry.Points         += points;
            entry.PointsMondiaux += pointsMondiaux;
        }
    }

    private async Task RecalculerRangs(int partieId, int orgId, string genre, int catId)
    {
        var entries = await db.RankingEntries
            .Where(r => r.PartieID == partieId && r.OrganisationID == orgId
                     && r.Genre == genre && r.CategorieID == catId)
            .OrderByDescending(r => r.Points)
            .ToListAsync();

        var champion = await db.ChampionCeintures.FirstOrDefaultAsync(c =>
            c.PartieID == partieId && c.OrganisationID == orgId
            && c.Genre == genre && c.CategorieID == catId && c.CombattantID != null);

        int rang = 1;
        foreach (var e in entries)
        {
            if (champion is not null && e.CombattantID == champion.CombattantID)
            {
                e.Rang = 0;
            }
            else
            {
                e.Rang = rang++;
            }
            if (rang > 16) break;
        }

        foreach (var e in entries.Skip(15).Where(e => e.Rang is > 0))
            e.Rang = 0;
    }

    public async Task<List<RankingEntryDto>> GetClassementOrga(
        int partieId, int orgId, string genre, int catId)
    {
        var champion = await db.ChampionCeintures
            .Where(c => c.PartieID == partieId && c.OrganisationID == orgId
                     && c.Genre == genre && c.CategorieID == catId && c.CombattantID != null)
            .Include(c => c.Combattant)
            .FirstOrDefaultAsync();

        var entries = await db.RankingEntries
            .Where(r => r.PartieID == partieId && r.OrganisationID == orgId
                     && r.Genre == genre && r.CategorieID == catId && r.Rang > 0)
            .OrderBy(r => r.Rang)
            .Take(15)
            .Include(r => r.Combattant)
            .ToListAsync();

        var result = new List<RankingEntryDto>();

        if (champion?.Combattant is { } champ)
            result.Add(new RankingEntryDto(0, champ.CombattantID,
                $"{champ.Prenom} {champ.NomFamille}", champ.Overall,
                true, champion.NbDefenses, champ.Victoires, champ.Defaites, champ.Nuls));

        foreach (var e in entries)
        {
            var c = e.Combattant!;
            result.Add(new RankingEntryDto(e.Rang, c.CombattantID,
                $"{c.Prenom} {c.NomFamille}", c.Overall,
                false, 0, c.Victoires, c.Defaites, c.Nuls));
        }

        return result;
    }

    public async Task<List<RankingEntryDto>> GetClassementMondial(
        int partieId, string genre, int catId)
    {
        var mondial = await db.RankingEntries
            .Where(r => r.PartieID == partieId && r.Genre == genre && r.CategorieID == catId)
            .GroupBy(r => r.CombattantID)
            .Select(g => new { CombattantID = g.Key, Total = g.Sum(r => r.PointsMondiaux) })
            .OrderByDescending(x => x.Total)
            .Take(15)
            .ToListAsync();

        var result = new List<RankingEntryDto>();
        int rang = 1;
        foreach (var m in mondial)
        {
            var c = await db.Combattants.FindAsync(m.CombattantID);
            if (c is null) continue;
            result.Add(new RankingEntryDto(rang++, c.CombattantID,
                $"{c.Prenom} {c.NomFamille}", c.Overall,
                false, 0, c.Victoires, c.Defaites, c.Nuls));
        }
        return result;
    }

    public async Task<List<RankingEntryDto>> GetClassementP4P(int partieId)
    {
        var p4p = await db.RankingEntries
            .Where(r => r.PartieID == partieId)
            .GroupBy(r => r.CombattantID)
            .Select(g => new { CombattantID = g.Key, Total = g.Sum(r => r.PointsMondiaux) })
            .OrderByDescending(x => x.Total)
            .Take(15)
            .ToListAsync();

        var result = new List<RankingEntryDto>();
        int rang = 1;
        foreach (var m in p4p)
        {
            var c = await db.Combattants.FindAsync(m.CombattantID);
            if (c is null) continue;
            result.Add(new RankingEntryDto(rang++, c.CombattantID,
                $"{c.Prenom} {c.NomFamille}", c.Overall,
                false, 0, c.Victoires, c.Defaites, c.Nuls));
        }
        return result;
    }
}
