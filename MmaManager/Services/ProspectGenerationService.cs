using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MmaManager.Data;
using MmaManager.Models;

public class ProspectGenerationService(MmaContext db)
{
    public async Task GenererProspects(int paysResidenceId, int anneeDepart)
    {
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @ph TABLE (i INT, v NVARCHAR(60))
            INSERT INTO @ph VALUES
            (0,N'Lucas'),(1,N'Théo'),(2,N'Maxime'),(3,N'Antoine'),(4,N'Baptiste'),
            (5,N'Hugo'),(6,N'Tom'),(7,N'Mathieu'),(8,N'Pierre'),(9,N'Julien'),
            (10,N'Nicolas'),(11,N'Clément'),(12,N'Alexandre'),(13,N'Thomas'),(14,N'Kevin'),
            (15,N'Romain'),(16,N'Alexis'),(17,N'Dylan'),(18,N'Jordan'),(19,N'Florian')

            DECLARE @pf TABLE (i INT, v NVARCHAR(60))
            INSERT INTO @pf VALUES
            (0,N'Léa'),(1,N'Emma'),(2,N'Chloé'),(3,N'Manon'),(4,N'Inès'),
            (5,N'Sarah'),(6,N'Jade'),(7,N'Camille'),(8,N'Laura'),(9,N'Noémie'),
            (10,N'Marine'),(11,N'Lucie'),(12,N'Clara'),(13,N'Pauline'),(14,N'Marie')

            DECLARE @ln TABLE (i INT, v NVARCHAR(60))
            INSERT INTO @ln VALUES
            (0,N'Dupont'),(1,N'Martin'),(2,N'Bernard'),(3,N'Dubois'),(4,N'Moreau'),
            (5,N'Laurent'),(6,N'Simon'),(7,N'Michel'),(8,N'Lefebvre'),(9,N'Leroy'),
            (10,N'Roux'),(11,N'David'),(12,N'Bertrand'),(13,N'Morel'),(14,N'Fournier'),
            (15,N'Girard'),(16,N'Bonnet'),(17,N'Dupuis'),(18,N'Lacroix'),(19,N'Boyer')

            -- Pool international : exclure le pays local
            DECLARE @pp TABLE (i INT, pid INT)
            INSERT INTO @pp
            SELECT CAST(ROW_NUMBER() OVER (ORDER BY PaysID) AS INT) - 1, PaysID
            FROM Pays
            WHERE Code IN (N'FRA',N'BEL',N'CAN',N'MAR',N'ESP',N'ITA',N'GBR',N'DEU',
                           N'USA',N'BRA',N'MEX',N'ARG',N'JPN',N'RUS',N'KOR',N'AUS',
                           N'SEN',N'CMR',N'ALG',N'TUN')
              AND PaysID <> @paysLocal

            DECLARE @nPays INT = (SELECT COUNT(*) FROM @pp)
            DECLARE @j INT = 0
            DECLARE @g NVARCHAR(1), @prenom2 NVARCHAR(60), @nom2 NVARCHAR(60)
            DECLARE @paysID2 INT, @catID2 INT, @s INT, @val2 INT, @catMax2 INT
            DECLARE @dob2 DATE, @poidsCombat DECIMAL(5,1)

            WHILE @j < 60
            BEGIN
                SET @g = CASE WHEN @j % 4 = 0 THEN N'F' ELSE N'H' END
                SET @catMax2 = CASE WHEN @g = N'H' THEN 10 ELSE 6 END
                SET @catID2 = (@j % @catMax2) + 1

                IF @g = N'H'
                    SELECT @prenom2 = v FROM @ph WHERE i = @j % 20
                ELSE
                    SELECT @prenom2 = v FROM @pf WHERE i = (@j / 4) % 15

                SELECT @nom2 = v FROM @ln WHERE i = ((@j * 7 + 3) % 20)

                -- 40 locaux (j < 40), 20 internationaux (j >= 40)
                IF @j < 40
                    SET @paysID2 = @paysLocal
                ELSE IF @nPays > 0
                    SELECT @paysID2 = pid FROM @pp WHERE i = ((@j - 40) % @nPays)
                ELSE
                    SET @paysID2 = @paysLocal

                SET @s = 20 + ABS(CHECKSUM(NEWID())) % 31
                SET @val2 = 2000 + ABS(CHECKSUM(NEWID())) % 10001
                SET @dob2 = DATEFROMPARTS(@anneeDepart - 18 - (@j % 6), (@j % 12) + 1, (@j % 28) + 1)

                SET @poidsCombat = CASE @catID2
                    WHEN 1 THEN CAST(52.2  AS DECIMAL(5,1))
                    WHEN 2 THEN CAST(56.7  AS DECIMAL(5,1))
                    WHEN 3 THEN CAST(61.2  AS DECIMAL(5,1))
                    WHEN 4 THEN CAST(65.8  AS DECIMAL(5,1))
                    WHEN 5 THEN CAST(70.3  AS DECIMAL(5,1))
                    WHEN 6 THEN CAST(77.1  AS DECIMAL(5,1))
                    WHEN 7 THEN CAST(83.9  AS DECIMAL(5,1))
                    WHEN 8 THEN CAST(93.0  AS DECIMAL(5,1))
                    ELSE        CAST(120.2 AS DECIMAL(5,1))
                END

                INSERT INTO Combattant (
                    Prenom, NomFamille, Genre, CategorieID,
                    PaysOrigineID, PaysResidenceID, DateNaissance, PoidsCombatKg,
                    StatFrappeDebout, StatPuissance, StatVitesseMains, StatPrecision,
                    StatCombosDebout, StatKick, StatClinic,
                    StatEsquive, StatBlocage, StatFootwork,
                    StatWrestling, StatTakedown, StatAntiTakedown,
                    [StatContrôleSol],
                    StatJiuJitsu, StatSubmission, StatEvasionSub,
                    StatCardio, StatForce, StatVitesse, StatAgilite,
                    StatMentoniere, StatRecuperation,
                    StatMental, StatExperience, StatCoaching, StatAdaptation,
                    Potentiel, Progression,
                    Moral, Fatigue, Motivation, BlessureGravite, SemainesIndispo,
                    Statut, Salaire, PrimeSigne, Valeur, SousContrat,
                    Victoires, Defaites, Nuls,
                    VictoiresKO, VictoiresSub, VictoiresDec, DefaitesKO, DefaitesSub,
                    DateCreation
                ) VALUES (
                    @prenom2, @nom2, @g, @catID2,
                    @paysID2, @paysID2, @dob2, @poidsCombat,
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(@s + ABS(CHECKSUM(NEWID())) % 11 - 5 AS TINYINT),
                    CAST(40 + ABS(CHECKSUM(NEWID())) % 41 AS TINYINT),
                    CAST(50 + ABS(CHECKSUM(NEWID())) % 31 AS TINYINT),
                    50, 0, CAST(50 + ABS(CHECKSUM(NEWID())) % 21 AS TINYINT), 0, 0,
                    N'Libre', @val2 / 80, 0, @val2, 0,
                    0, 0, 0, 0, 0, 0, 0, 0,
                    GETDATE()
                )

                SET @j = @j + 1
            END

            -- Attributs physiques pour les prospects générés
            UPDATE Combattant SET
                TailleCm = CASE
                    WHEN Genre = N'H' AND CategorieID IN (1,2) THEN 160 + ABS(CHECKSUM(NEWID())) % 10
                    WHEN Genre = N'H' AND CategorieID IN (3,4) THEN 166 + ABS(CHECKSUM(NEWID())) % 12
                    WHEN Genre = N'H' AND CategorieID IN (5,6) THEN 170 + ABS(CHECKSUM(NEWID())) % 14
                    WHEN Genre = N'H' AND CategorieID IN (7,8) THEN 176 + ABS(CHECKSUM(NEWID())) % 14
                    WHEN Genre = N'H' THEN 182 + ABS(CHECKSUM(NEWID())) % 14
                    WHEN Genre = N'F' AND CategorieID IN (1,2) THEN 155 + ABS(CHECKSUM(NEWID())) % 12
                    WHEN Genre = N'F' AND CategorieID IN (3,4) THEN 160 + ABS(CHECKSUM(NEWID())) % 12
                    ELSE 163 + ABS(CHECKSUM(NEWID())) % 12
                END,
                PoidsReelKg = CAST(55.0 + ABS(CHECKSUM(NEWID())) % 400 / 10.0 AS DECIMAL(5,1))
            WHERE TailleCm IS NULL AND Statut = N'Libre' AND Victoires = 0 AND Defaites = 0

            UPDATE Combattant SET AllongeCm = TailleCm + (-4 + ABS(CHECKSUM(NEWID())) % 13)
            WHERE AllongeCm IS NULL AND TailleCm IS NOT NULL
            """,
            new SqlParameter("@paysLocal",    paysResidenceId),
            new SqlParameter("@anneeDepart",  anneeDepart));
    }

    public async Task GenererOrganisationsLocales(int paysResidenceId, int anneeDepart)
    {
        var pays = await db.Pays.FindAsync(paysResidenceId);
        var n = pays?.Nom ?? "Local";

        db.CombatOrganisations.AddRange(
            new CombatOrganisation
            {
                Nom          = $"{n} Underground Fight Club",
                PaysOrigineID = paysResidenceId,
                AnneeCreation = anneeDepart - 5,
                Prestige     = 1,
                Description  = "Combats clandestins organisés dans des entrepôts et garages. Peu de règles, beaucoup d'ambiance.",
                EstFictive   = true
            },
            new CombatOrganisation
            {
                Nom          = $"{n} Brawl Circuit",
                PaysOrigineID = paysResidenceId,
                AnneeCreation = anneeDepart - 3,
                Prestige     = 1,
                Description  = "Petite organisation locale qui fait tourner des cartes régulières dans les salles de quartier.",
                EstFictive   = true
            },
            new CombatOrganisation
            {
                Nom          = $"{n} Combat League",
                PaysOrigineID = paysResidenceId,
                AnneeCreation = anneeDepart - 1,
                Prestige     = 2,
                Description  = "Organisation régionale en pleine croissance, attire les meilleurs combattants du pays.",
                EstFictive   = true
            }
        );
        await db.SaveChangesAsync();
    }
}
