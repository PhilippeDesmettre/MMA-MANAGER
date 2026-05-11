using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MmaManager.Data;
using MmaManager.Services;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "FrontendDev";

// ── Database ──────────────────────────────────────────────────
builder.Services.AddDbContext<MmaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MmaDatabase")));

// ── JWT Auth ──────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// ── CORS ──────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddOpenApi();

builder.Services.AddScoped<CombatSimulationService>();
builder.Services.AddScoped<TrainingService>();
builder.Services.AddScoped<RankingService>();
builder.Services.AddScoped<WorldSimulationService>();
builder.Services.AddScoped<TurnAdvancementService>();
builder.Services.AddScoped<ProspectGenerationService>();
builder.Services.AddScoped<RosterHistoriqueService>();

var app = builder.Build();

// ── Initialisation DB au démarrage ────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MmaContext>();

    // Table Users
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users' AND type = 'U')
        CREATE TABLE Users (
            UserID       INT IDENTITY(1,1) PRIMARY KEY,
            Email        NVARCHAR(256) NOT NULL UNIQUE,
            PasswordHash NVARCHAR(500) NOT NULL,
            CreatedAt    DATETIME      NOT NULL DEFAULT GETDATE()
        );
    """);

    // Table Background
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Background' AND type = 'U')
        CREATE TABLE Background (
            BackgroundID      INT IDENTITY(1,1) PRIMARY KEY,
            Nom               NVARCHAR(120) NOT NULL,
            Description       NVARCHAR(500) NOT NULL,
            Icone             NVARCHAR(10),
            BonusStriking     INT NOT NULL DEFAULT 0,
            BonusLutte        INT NOT NULL DEFAULT 0,
            BonusGrappling    INT NOT NULL DEFAULT 0,
            BonusConditioning INT NOT NULL DEFAULT 0,
            BonusStamina      INT NOT NULL DEFAULT 0,
            BonusMental       INT NOT NULL DEFAULT 0,
            BonusStrategie    INT NOT NULL DEFAULT 0,
            BonusNegociation  INT NOT NULL DEFAULT 0,
            BonusMotivation   INT NOT NULL DEFAULT 0
        );
    """);

    // Table Partie
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Partie' AND type = 'U')
        CREATE TABLE Partie (
            PartieID                INT IDENTITY(1,1) PRIMARY KEY,
            UserID                  INT NOT NULL REFERENCES Users(UserID),
            Epoque                  NVARCHAR(30) NOT NULL,
            Argent                  DECIMAL(18,2) NOT NULL DEFAULT 50000.00,
            DateCreation            DATETIME NOT NULL DEFAULT GETDATE(),
            DateDerniereConnexion   DATETIME NOT NULL DEFAULT GETDATE(),
            EstActive               BIT NOT NULL DEFAULT 1
        );
    """);

    // Table EntraineurJoueur
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EntraineurJoueur' AND type = 'U')
        CREATE TABLE EntraineurJoueur (
            EntraineurJoueurID  INT IDENTITY(1,1) PRIMARY KEY,
            PartieID            INT NOT NULL REFERENCES Partie(PartieID) ON DELETE CASCADE,
            Prenom              NVARCHAR(60) NOT NULL,
            Nom                 NVARCHAR(60) NOT NULL,
            PaysOrigineID       INT NOT NULL REFERENCES Pays(PaysID),
            PaysResidenceID     INT NOT NULL REFERENCES Pays(PaysID),
            BackgroundID        INT NOT NULL REFERENCES Background(BackgroundID),
            CompStriking        INT NOT NULL DEFAULT 30,
            CompLutte           INT NOT NULL DEFAULT 30,
            CompGrappling       INT NOT NULL DEFAULT 30,
            CompConditioning    INT NOT NULL DEFAULT 30,
            CompStamina         INT NOT NULL DEFAULT 30,
            CompMental          INT NOT NULL DEFAULT 30,
            CompStrategie       INT NOT NULL DEFAULT 30,
            CompNegociation     INT NOT NULL DEFAULT 30,
            CompMotivation      INT NOT NULL DEFAULT 30
        );
    """);

    // Colonnes de suivi du tour dans Partie (ajoutées si absentes — table déjà existante)
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Partie') AND name = 'TourActuel')
            ALTER TABLE Partie ADD TourActuel INT NOT NULL DEFAULT 1;
    """);
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Partie') AND name = 'MoisActuel')
            ALTER TABLE Partie ADD MoisActuel INT NOT NULL DEFAULT 1;
    """);
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Partie') AND name = 'AnneeActuelle')
            ALTER TABLE Partie ADD AnneeActuelle INT NOT NULL DEFAULT 1985;
    """);
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Partie') AND name = 'PrestigeEcurie')
            ALTER TABLE Partie ADD PrestigeEcurie INT NOT NULL DEFAULT 1;
    """);

    // Table Agent (joueur peut être son propre agent)
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Agent' AND type = 'U')
        CREATE TABLE Agent (
            AgentID        INT IDENTITY(1,1) PRIMARY KEY,
            PartieID       INT NULL REFERENCES Partie(PartieID) ON DELETE SET NULL,
            EstJoueur      BIT NOT NULL DEFAULT 0,
            Prenom         NVARCHAR(60) NOT NULL,
            Nom            NVARCHAR(60) NOT NULL,
            CompContact    INT NOT NULL DEFAULT 30,
            CompNegociation INT NOT NULL DEFAULT 30,
            CompReseau     INT NOT NULL DEFAULT 30,
            CompReputation INT NOT NULL DEFAULT 30,
            CompInfluence  INT NOT NULL DEFAULT 30,
            CompMarketing  INT NOT NULL DEFAULT 30,
            CompJuridique  INT NOT NULL DEFAULT 30
        );
    """);

    // Table CombatOrganisation
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CombatOrganisation' AND type = 'U')
        CREATE TABLE CombatOrganisation (
            OrganisationID  INT IDENTITY(1,1) PRIMARY KEY,
            Nom             NVARCHAR(100) NOT NULL,
            PaysOrigineID   INT NULL REFERENCES Pays(PaysID),
            AnneeCreation   INT NOT NULL,
            Prestige        INT NOT NULL DEFAULT 1,
            Description     NVARCHAR(500) NULL,
            EstFictive      BIT NOT NULL DEFAULT 0
        );
    """);

    // Colonne PartieID dans CombatOrganisation (orgs locales liées à une partie) — CASCADE
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CombatOrganisation') AND name = 'PartieID')
            ALTER TABLE CombatOrganisation ADD PartieID INT NULL REFERENCES Partie(PartieID) ON DELETE CASCADE;
    """);

    // Recréer la FK avec CASCADE si elle existe déjà avec SET NULL
    db.Database.ExecuteSqlRaw("""
        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CombatOrganisation') AND name = 'PartieID')
        BEGIN
            DECLARE @fkName NVARCHAR(200);
            SELECT @fkName = fk.name
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            WHERE fk.parent_object_id = OBJECT_ID('CombatOrganisation')
              AND COL_NAME(fk.parent_object_id, fkc.parent_column_id) = 'PartieID'
              AND fk.delete_referential_action = 2; -- 2 = SET NULL

            IF @fkName IS NOT NULL
            BEGIN
                EXEC('ALTER TABLE CombatOrganisation DROP CONSTRAINT ' + @fkName);
                ALTER TABLE CombatOrganisation ADD CONSTRAINT FK_CombatOrganisation_Partie
                    FOREIGN KEY (PartieID) REFERENCES Partie(PartieID) ON DELETE CASCADE;
            END
        END
    """);

    // Table SurEntrainement
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SurEntrainement' AND type = 'U')
        CREATE TABLE SurEntrainement (
            SurEntrainementID INT IDENTITY(1,1) PRIMARY KEY,
            CombattantID      INT NOT NULL REFERENCES Combattant(CombattantID),
            PartieID          INT NOT NULL REFERENCES Partie(PartieID) ON DELETE CASCADE,
            TourDebut         INT NOT NULL,
            TourFin           INT NOT NULL,
            Cause             NVARCHAR(200) NULL
        );
    """);

    // Table CombatPlanifie
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CombatPlanifie' AND type = 'U')
        CREATE TABLE CombatPlanifie (
            CombatPlanifieID INT IDENTITY(1,1) PRIMARY KEY,
            PartieID         INT NOT NULL REFERENCES Partie(PartieID) ON DELETE CASCADE,
            CombattantID     INT NOT NULL REFERENCES Combattant(CombattantID),
            AdversaireID     INT NOT NULL REFERENCES Combattant(CombattantID),
            AgentID          INT NULL REFERENCES Agent(AgentID),
            OrganisationID   INT NOT NULL REFERENCES CombatOrganisation(OrganisationID),
            TourPrevu        INT NOT NULL,
            Statut           NVARCHAR(20) NOT NULL DEFAULT 'Planifie'
        );
    """);

    // Colonne Gameplan dans CombatPlanifie (ajoutée si absente)
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CombatPlanifie') AND name = 'Gameplan')
            ALTER TABLE CombatPlanifie ADD Gameplan NVARCHAR(20) NOT NULL DEFAULT 'Balanced';
    """);

    // Étendre Gameplan à NVARCHAR(200) pour le format JSON multi-axes
    db.Database.ExecuteSqlRaw("""
        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CombatPlanifie') AND name = 'Gameplan' AND max_length < 400)
            ALTER TABLE CombatPlanifie ALTER COLUMN Gameplan NVARCHAR(200) NOT NULL;
    """);

    // Table EntrainementPlanifie
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EntrainementPlanifie' AND type = 'U')
        CREATE TABLE EntrainementPlanifie (
            EntrainementPlanifieID INT IDENTITY(1,1) PRIMARY KEY,
            PartieID               INT NOT NULL REFERENCES Partie(PartieID) ON DELETE CASCADE,
            CombattantID           INT NOT NULL REFERENCES Combattant(CombattantID),
            TypeEntrainement       NVARCHAR(20) NOT NULL,
            EntraineurJoueurID     INT NULL REFERENCES EntraineurJoueur(EntraineurJoueurID),
            UNIQUE (PartieID, CombattantID)
        );
    """);

    // Colonne EntraineurJoueurID dans EntrainementPlanifie (ajoutée si absente)
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EntrainementPlanifie') AND name = 'EntraineurJoueurID')
            ALTER TABLE EntrainementPlanifie ADD EntraineurJoueurID INT NULL REFERENCES EntraineurJoueur(EntraineurJoueurID);
    """);

    // Table HistoriqueEntrainement (pour future référence / détection sur-entraînement)
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HistoriqueEntrainement' AND type = 'U')
        CREATE TABLE HistoriqueEntrainement (
            HistoriqueEntrainementID INT IDENTITY(1,1) PRIMARY KEY,
            PartieID                 INT NOT NULL REFERENCES Partie(PartieID) ON DELETE CASCADE,
            CombattantID             INT NOT NULL REFERENCES Combattant(CombattantID),
            TourNumero               INT NOT NULL,
            TypeEntrainement         NVARCHAR(20) NOT NULL,
            GainTotal                INT NOT NULL DEFAULT 0,
            DateEnregistrement       DATETIME NOT NULL DEFAULT GETDATE()
        );
    """);

    // Table ResultatCombatPartie (historique des combats simulés)
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ResultatCombatPartie' AND type = 'U')
        CREATE TABLE ResultatCombatPartie (
            ResultatID       INT IDENTITY(1,1) PRIMARY KEY,
            PartieID         INT NOT NULL REFERENCES Partie(PartieID) ON DELETE CASCADE,
            CombattantID     INT NOT NULL REFERENCES Combattant(CombattantID),
            AdversaireID     INT NOT NULL REFERENCES Combattant(CombattantID),
            OrganisationNom  NVARCHAR(100) NOT NULL,
            TourCombat       INT NOT NULL,
            EstVictoire      BIT NULL,
            EstNul           BIT NOT NULL DEFAULT 0,
            MethodeVictoire  NVARCHAR(40) NULL,
            RoundFin         TINYINT NOT NULL DEFAULT 3,
            Details          NVARCHAR(200) NULL
        );
    """);

    // Table StaffDisponible
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StaffDisponible' AND type = 'U')
        CREATE TABLE StaffDisponible (
            StaffDisponibleID INT IDENTITY(1,1) PRIMARY KEY,
            Prenom            NVARCHAR(60)  NOT NULL,
            Nom               NVARCHAR(60)  NOT NULL,
            Role              NVARCHAR(30)  NOT NULL,
            Icone             NVARCHAR(10)  NULL,
            Description       NVARCHAR(300) NULL,
            CompStriking      INT NOT NULL DEFAULT 30,
            CompLutte         INT NOT NULL DEFAULT 30,
            CompGrappling     INT NOT NULL DEFAULT 30,
            CompConditioning  INT NOT NULL DEFAULT 30,
            CompMental        INT NOT NULL DEFAULT 30,
            Overall           INT NOT NULL DEFAULT 50,
            SalaireMensuel    DECIMAL(18,2) NOT NULL DEFAULT 2000.00,
            Nationalite       NVARCHAR(60)  NULL
        );
    """);

    // Table StaffPartie
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StaffPartie' AND type = 'U')
        CREATE TABLE StaffPartie (
            StaffPartieID     INT IDENTITY(1,1) PRIMARY KEY,
            PartieID          INT NOT NULL REFERENCES Partie(PartieID) ON DELETE CASCADE,
            StaffDisponibleID INT NOT NULL REFERENCES StaffDisponible(StaffDisponibleID),
            DateEmbauche      DATETIME NOT NULL DEFAULT GETDATE()
        );
    """);

    // Table Rivalite
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Rivalite' AND type = 'U')
        CREATE TABLE Rivalite (
            RivaliteID        INT IDENTITY(1,1) PRIMARY KEY,
            PartieID          INT NOT NULL REFERENCES Partie(PartieID) ON DELETE CASCADE,
            Combattant1ID     INT NOT NULL REFERENCES Combattant(CombattantID),
            Combattant2ID     INT NOT NULL REFERENCES Combattant(CombattantID),
            Intensite         TINYINT NOT NULL DEFAULT 1,
            NbConfrontations  TINYINT NOT NULL DEFAULT 1,
            TourCreation      INT NOT NULL,
            TourDernierCombat INT NOT NULL,
            Raison            NVARCHAR(200) NULL
        );
    """);

    // Table RankingEntry
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RankingEntry' AND type = 'U')
        CREATE TABLE RankingEntry (
            RankingEntryID INT IDENTITY(1,1) PRIMARY KEY,
            PartieID       INT NOT NULL REFERENCES Partie(PartieID) ON DELETE CASCADE,
            CombattantID   INT NOT NULL REFERENCES Combattant(CombattantID),
            OrganisationID INT NOT NULL REFERENCES CombatOrganisation(OrganisationID),
            Genre          CHAR(1) NOT NULL DEFAULT 'H',
            CategorieID    INT NOT NULL,
            Points         INT NOT NULL DEFAULT 0,
            Rang           INT NOT NULL DEFAULT 0,
            PointsMondiaux INT NOT NULL DEFAULT 0
        );
    """);

    // Table ChampionCeinture
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChampionCeinture' AND type = 'U')
        CREATE TABLE ChampionCeinture (
            ChampionCeintureID INT IDENTITY(1,1) PRIMARY KEY,
            PartieID           INT NOT NULL REFERENCES Partie(PartieID) ON DELETE CASCADE,
            OrganisationID     INT NOT NULL REFERENCES CombatOrganisation(OrganisationID),
            CategorieID        INT NOT NULL,
            Genre              CHAR(1) NOT NULL DEFAULT 'H',
            CombattantID       INT NULL REFERENCES Combattant(CombattantID),
            TourObtention      INT NOT NULL DEFAULT 0,
            NbDefenses         INT NOT NULL DEFAULT 0
        );
    """);

    // Table ContratOrganisation
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ContratOrganisation' AND type = 'U')
        CREATE TABLE ContratOrganisation (
            ContratID         INT IDENTITY(1,1) PRIMARY KEY,
            PartieID          INT NOT NULL REFERENCES Partie(PartieID) ON DELETE CASCADE,
            CombattantID      INT NOT NULL REFERENCES Combattant(CombattantID),
            OrganisationID    INT NOT NULL REFERENCES CombatOrganisation(OrganisationID),
            NombreCombats     INT NOT NULL DEFAULT 1,
            CombatsEffectues  INT NOT NULL DEFAULT 0,
            EstExclusif       BIT NOT NULL DEFAULT 0,
            TourDebut         INT NOT NULL DEFAULT 0,
            Statut            NVARCHAR(20) NOT NULL DEFAULT N'Actif'
        );
    """);

    // Colonne StaffPartieID dans EntrainementPlanifie (ajoutée si absente — NO ACTION pour éviter cycle cascade)
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EntrainementPlanifie') AND name = 'StaffPartieID')
            ALTER TABLE EntrainementPlanifie ADD StaffPartieID INT NULL REFERENCES StaffPartie(StaffPartieID) ON DELETE NO ACTION;
    """);

    // Colonnes physiques sur Combattant (ajoutées si absentes)
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Combattant') AND name = 'TailleCm')
            ALTER TABLE Combattant ADD TailleCm SMALLINT NULL;
    """);
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Combattant') AND name = 'AllongeCm')
            ALTER TABLE Combattant ADD AllongeCm SMALLINT NULL;
    """);
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Combattant') AND name = 'PoidsReelKg')
            ALTER TABLE Combattant ADD PoidsReelKg DECIMAL(5,1) NULL;
    """);

    // Seed des attributs physiques par catégorie — Hommes
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 160 + ABS(CHECKSUM(NEWID())) % 13, PoidsReelKg = 54.0 + CAST(ABS(CHECKSUM(NEWID())) % 50 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'H' AND CategorieID = 1 AND TailleCm IS NULL;");
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 162 + ABS(CHECKSUM(NEWID())) % 14, PoidsReelKg = 59.0 + CAST(ABS(CHECKSUM(NEWID())) % 50 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'H' AND CategorieID = 2 AND TailleCm IS NULL;");
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 165 + ABS(CHECKSUM(NEWID())) % 14, PoidsReelKg = 63.0 + CAST(ABS(CHECKSUM(NEWID())) % 60 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'H' AND CategorieID = 3 AND TailleCm IS NULL;");
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 168 + ABS(CHECKSUM(NEWID())) % 13, PoidsReelKg = 68.0 + CAST(ABS(CHECKSUM(NEWID())) % 60 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'H' AND CategorieID = 4 AND TailleCm IS NULL;");
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 170 + ABS(CHECKSUM(NEWID())) % 14, PoidsReelKg = 73.0 + CAST(ABS(CHECKSUM(NEWID())) % 60 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'H' AND CategorieID = 5 AND TailleCm IS NULL;");
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 175 + ABS(CHECKSUM(NEWID())) % 14, PoidsReelKg = 79.0 + CAST(ABS(CHECKSUM(NEWID())) % 60 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'H' AND CategorieID = 6 AND TailleCm IS NULL;");
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 178 + ABS(CHECKSUM(NEWID())) % 16, PoidsReelKg = 85.0 + CAST(ABS(CHECKSUM(NEWID())) % 90 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'H' AND CategorieID = 7 AND TailleCm IS NULL;");
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 183 + ABS(CHECKSUM(NEWID())) % 14, PoidsReelKg = 94.0 + CAST(ABS(CHECKSUM(NEWID())) % 90 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'H' AND CategorieID = 8 AND TailleCm IS NULL;");
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 185 + ABS(CHECKSUM(NEWID())) % 14, PoidsReelKg = 103.0 + CAST(ABS(CHECKSUM(NEWID())) % 180 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'H' AND CategorieID = 9 AND TailleCm IS NULL;");
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 185 + ABS(CHECKSUM(NEWID())) % 19, PoidsReelKg = 115.0 + CAST(ABS(CHECKSUM(NEWID())) % 310 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'H' AND CategorieID = 10 AND TailleCm IS NULL;");

    // Seed des attributs physiques par catégorie — Femmes
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 155 + ABS(CHECKSUM(NEWID())) % 14, PoidsReelKg = 52.0 + CAST(ABS(CHECKSUM(NEWID())) % 60 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'F' AND CategorieID = 1 AND TailleCm IS NULL;");
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 157 + ABS(CHECKSUM(NEWID())) % 14, PoidsReelKg = 57.0 + CAST(ABS(CHECKSUM(NEWID())) % 50 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'F' AND CategorieID = 2 AND TailleCm IS NULL;");
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 160 + ABS(CHECKSUM(NEWID())) % 14, PoidsReelKg = 61.0 + CAST(ABS(CHECKSUM(NEWID())) % 60 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'F' AND CategorieID = 3 AND TailleCm IS NULL;");
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 163 + ABS(CHECKSUM(NEWID())) % 16, PoidsReelKg = 66.0 + CAST(ABS(CHECKSUM(NEWID())) % 50 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'F' AND CategorieID = 4 AND TailleCm IS NULL;");
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 165 + ABS(CHECKSUM(NEWID())) % 14, PoidsReelKg = 70.0 + CAST(ABS(CHECKSUM(NEWID())) % 60 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'F' AND CategorieID = 5 AND TailleCm IS NULL;");
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET TailleCm = 168 + ABS(CHECKSUM(NEWID())) % 13, PoidsReelKg = 75.0 + CAST(ABS(CHECKSUM(NEWID())) % 60 AS DECIMAL(5,1)) / 10.0 WHERE Genre = 'F' AND CategorieID = 6 AND TailleCm IS NULL;");

    // AllongeCm basé sur TailleCm (TailleCm - 4 à TailleCm + 8, outliers possibles)
    db.Database.ExecuteSqlRaw("UPDATE Combattant SET AllongeCm = TailleCm + (-4 + ABS(CHECKSUM(NEWID())) % 13) WHERE AllongeCm IS NULL AND TailleCm IS NOT NULL;");

    // NOTE : les prospects sont générés à la création de chaque partie (PartieController.Create)
    //        via ProspectGenerationService, pas au démarrage de l'app.

    // Seed staff disponibles (si la table est vide)
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM StaffDisponible)
        INSERT INTO StaffDisponible (Prenom, Nom, Role, Icone, Description, CompStriking, CompLutte, CompGrappling, CompConditioning, CompMental, Overall, SalaireMensuel, Nationalite)
        VALUES
        -- ── Coachs Striking ──────────────────────────────────────────────────────────────────
        (N'Marco',  N'Pellegrini',    N'CoachStriking', N'🥊',
         N'Ex-champion de boxe thaï. Techniques impeccables, sens du timing hors pair.',
         82, 25, 20, 35, 50,  62, 3200.00, N'Italienne'),

        (N'Ryu',    N'Tanaka',        N'CoachStriking', N'🥊',
         N'Maître karatéka reconverti. Frappe à distance et contre-attaque sont ses spécialités.',
         75, 20, 18, 30, 55,  56, 2500.00, N'Japonaise'),

        (N'Davina', N'Legrand',       N'CoachStriking', N'🥊',
         N'Ancienne championne de savate. Travaille les combinaisons et la précision.',
         68, 15, 22, 28, 60,  50, 2000.00, N'Française'),

        -- ── Coachs Lutte ─────────────────────────────────────────────────────────────────────
        (N'Dmitri', N'Volkov',        N'CoachLutte',    N'🤼',
         N'Catcheur soviétique formé à l''école de sambo. Contrôle total et puissance de takedown.',
         22, 85, 40, 45, 35,  62, 3400.00, N'Russe'),

        (N'Curtis', N'Williams',      N'CoachLutte',    N'🤼',
         N'Lutteur collégien américain. Niveau d''élite en wrestling défensif et offensif.',
         20, 78, 30, 40, 40,  58, 2800.00, N'Américaine'),

        (N'Erika',  N'Svensson',      N'CoachLutte',    N'🤼',
         N'Championne nationale suédoise de lutte. Explosivité et technique de déplacement.',
         18, 70, 25, 38, 45,  50, 2200.00, N'Suédoise'),

        -- ── Coachs Grappling ─────────────────────────────────────────────────────────────────
        (N'Rafael', N'Carvalho',      N'CoachGrappling', N'⛩️',
         N'Ceinture noire de BJJ 4e degré. Encyclopédie vivante des soumissions.',
         15, 35, 88, 30, 50,  66, 3800.00, N'Brésilienne'),

        (N'Kenji',  N'Mori',          N'CoachGrappling', N'⛩️',
         N'Judoka reconverti au grappling. Projections et étranglements en position dominante.',
         18, 40, 80, 28, 48,  60, 3000.00, N'Japonaise'),

        (N'Amara',  N'Diallo',        N'CoachGrappling', N'⛩️',
         N'Formée en Afrique de l''Ouest puis spécialisée en submission. Efficace et méthodique.',
         15, 30, 72, 25, 52,  52, 2300.00, N'Sénégalaise'),

        -- ── Préparateurs Physiques ───────────────────────────────────────────────────────────
        (N'Sebastian', N'Hoffmann',   N'PreparateurPhysique', N'🏋️',
         N'Préparateur olympique. Spécialiste force-vitesse, a formé plusieurs médaillés.',
         20, 20, 20, 88, 45,  62, 3600.00, N'Allemande'),

        (N'Clara',  N'Torres',        N'PreparateurPhysique', N'🏋️',
         N'Ancienne triathlète. Endurance et récupération sont sa marque de fabrique.',
         15, 15, 15, 80, 40,  56, 2800.00, N'Espagnole'),

        (N'Marcus', N'Johnson',       N'PreparateurPhysique', N'🏋️',
         N'Préparateur NFL reconverti. Explosivité et gainage selon la méthode américaine.',
         18, 18, 18, 75, 38,  52, 2400.00, N'Américaine'),

        -- ── Coachs Mental ────────────────────────────────────────────────────────────────────
        (N'Dr. Yuki', N'Nakamura',    N'CoachMental',    N'🧠',
         N'Psychologue du sport. Gestion du stress, visualisation et concentration pré-combat.',
         15, 15, 15, 25, 85,  56, 3000.00, N'Japonaise'),

        (N'Sophia', N'Beaumont',      N'CoachMental',    N'🧠',
         N'Ancienne combattante reconvertie en coach mental. Parle aux combattants depuis l''intérieur.',
         30, 28, 25, 30, 78,  52, 2400.00, N'Canadienne'),

        (N'Pierre', N'Moreau',        N'CoachMental',    N'🧠',
         N'Coach de vie et préparateur mental. Spécialiste de la gestion des défaites et rebonds.',
         10, 10, 10, 20, 72,  46, 2000.00, N'Française'),

        -- ── Coachs MMA Complets (polyvalents) ────────────────────────────────────────────────
        (N'Tony',   N'Maguire',       N'CoachGrappling', N'⛩️',
         N'Vétéran MMA 15 ans de carrière. Polyvalent mais excellence particulière au sol.',
         45, 48, 65, 50, 55,  56, 2600.00, N'Irlandaise'),

        (N'Viktor', N'Petrenko',      N'CoachLutte',     N'🤼',
         N'Ex-lutteur ukrainien. Efficacité brute, sans fioritures, résultats garantis.',
         25, 72, 35, 50, 42,  54, 2500.00, N'Ukrainienne'),

        (N'Lisa',   N'Huang',         N'CoachStriking',  N'🥊',
         N'Championne de wushu reconvertie. Rapidité d''exécution et travail des angles.',
         71, 20, 25, 32, 58,  53, 2200.00, N'Américaine'),

        (N'Bruno',  N'Salave''a',     N'PreparateurPhysique', N'🏋️',
         N'Polynésien de Samoa. Force brute et endurance surhumaine, méthode traditionnelle.',
         22, 22, 20, 78, 35,  54, 2600.00, N'Américaine'),

        (N'Ingrid', N'Larsson',       N'CoachMental',    N'🧠',
         N'Psychologue sportive nordique. Approche froide et rationnelle, dépassement des limites.',
         12, 14, 12, 28, 80,  52, 2600.00, N'Suédoise')
    """);

    // Table CombattantPartie
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CombattantPartie' AND type = 'U')
        CREATE TABLE CombattantPartie (
            CombattantPartieID INT IDENTITY(1,1) PRIMARY KEY,
            CombattantID       INT NOT NULL REFERENCES Combattant(CombattantID),
            PartieID           INT NOT NULL REFERENCES Partie(PartieID) ON DELETE CASCADE,
            DateRecrutement    DATETIME NOT NULL DEFAULT GETDATE(),
            UNIQUE (CombattantID, PartieID)
        );
    """);

    // Seed des backgrounds (si la table est vide)
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM Background)
        INSERT INTO Background (Nom, Description, Icone,
            BonusStriking, BonusLutte, BonusGrappling, BonusConditioning,
            BonusStamina, BonusMental, BonusStrategie, BonusNegociation, BonusMotivation)
        VALUES
        ('Ancien Judoka',
         'Maîtrise des projections et du contrôle au sol. Ton background judo te donne une base de grappling solide et un sens tactique développé.',
         N'🥋', 0, 15, 20, 5, 5, 10, 10, 0, 0),

        ('Ancien Samboïste',
         'Alliance redoutable de la lutte et des soumissions. L''école soviétique t''a forgé une endurance et une puissance hors normes.',
         N'🤼', 0, 20, 10, 10, 10, 5, 5, 0, 0),

        ('Ancien Boxeur Professionnel',
         'Des années de ring t''ont appris la distance, le timing et la frappe. Tu sais lire un combat debout mieux que quiconque.',
         N'🥊', 25, 0, 0, 5, 5, 10, 0, 10, 5),

        ('Ancien Kickboxeur',
         'Poings et pieds combinés à grande vitesse. Tu formes des frappeurs polyvalents avec une endurance supérieure.',
         N'🦵', 20, 0, 0, 15, 15, 0, 0, 0, 0),

        ('Ceinture Noire BJJ',
         'Expert des soumissions et du jeu au sol. Ton œil technique te permet de décrypter chaque position.',
         N'⛩️', 0, 0, 25, 0, 5, 10, 15, 0, 5),

        ('Ancien Lutteur',
         'La lutte est la base du MMA. Ta maîtrise du contrôle et des takedowns fait de toi un coach fondamental.',
         N'💪', 0, 25, 5, 15, 10, 0, 0, 0, 0),

        ('Champion Muay Thai',
         'L''art des 8 membres dans toute sa splendeur. Coups de pied, genoux, coudes — tu maîtrises chaque arme.',
         N'🐉', 20, 0, 0, 5, 10, 15, 0, 0, 5),

        ('Préparateur Physique',
         'Le corps est la première arme. Tu transformes tes combattants en machines athlétiques capables de tenir 5 rounds à pleine intensité.',
         N'🏋️', 0, 0, 0, 25, 20, 5, 0, 0, 5),

        ('Ancien Combattant MMA',
         'Tu as tout vécu sur le terrain. Polyvalent et pragmatique, tu transmet une expérience complète à tes combattants.',
         N'⚡', 8, 8, 8, 5, 8, 8, 5, 0, 5),

        ('Agent Sportif',
         'Tu connais le business comme ta poche. Négociations, contacts, réputation — ton réseau est ton arme secrète.',
         N'🤝', 0, 0, 0, 0, 0, 10, 15, 25, 10);
    """);

    // Pays manquants importants pour le MMA (idempotent)
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM Pays WHERE Code = N'ITA')
            INSERT INTO Pays (Code, Nom, Continent) VALUES (N'ITA', N'Italie', N'Europe');
        IF NOT EXISTS (SELECT 1 FROM Pays WHERE Code = N'ESP')
            INSERT INTO Pays (Code, Nom, Continent) VALUES (N'ESP', N'Espagne', N'Europe');
        IF NOT EXISTS (SELECT 1 FROM Pays WHERE Code = N'DEU')
            INSERT INTO Pays (Code, Nom, Continent) VALUES (N'DEU', N'Allemagne', N'Europe');
        IF NOT EXISTS (SELECT 1 FROM Pays WHERE Code = N'BEL')
            INSERT INTO Pays (Code, Nom, Continent) VALUES (N'BEL', N'Belgique', N'Europe');
        IF NOT EXISTS (SELECT 1 FROM Pays WHERE Code = N'ARG')
            INSERT INTO Pays (Code, Nom, Continent) VALUES (N'ARG', N'Argentine', N'Amérique du Sud');
        IF NOT EXISTS (SELECT 1 FROM Pays WHERE Code = N'ALG')
            INSERT INTO Pays (Code, Nom, Continent) VALUES (N'ALG', N'Algérie', N'Afrique');
        IF NOT EXISTS (SELECT 1 FROM Pays WHERE Code = N'TUN')
            INSERT INTO Pays (Code, Nom, Continent) VALUES (N'TUN', N'Tunisie', N'Afrique');
        IF NOT EXISTS (SELECT 1 FROM Pays WHERE Code = N'UZB')
            INSERT INTO Pays (Code, Nom, Continent) VALUES (N'UZB', N'Ouzbékistan', N'Asie');
    """);

    // Seed des organisations de combat (si la table est vide)
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM CombatOrganisation)
        BEGIN
            DECLARE @USA INT = (SELECT TOP 1 PaysID FROM Pays WHERE Code = 'USA')
            DECLARE @JPN INT = (SELECT TOP 1 PaysID FROM Pays WHERE Code = 'JPN')
            DECLARE @BRA INT = (SELECT TOP 1 PaysID FROM Pays WHERE Code = 'BRA')
            DECLARE @FRA INT = (SELECT TOP 1 PaysID FROM Pays WHERE Code = 'FRA')

            -- ═══ ÈRE NO RULES (1985 – 1999) ═══
            -- Organisations réelles
            INSERT INTO CombatOrganisation (Nom, PaysOrigineID, AnneeCreation, Prestige, Description, EstFictive) VALUES
            (N'Shooto', @JPN, 1985, 3,
             N'Une des premières organisations MMA structurées au monde, fondée au Japon. Règles techniques, culture martiale, combat complet.', 0),
            (N'Vale Tudo Brazil', @BRA, 1985, 2,
             N'Circuit sauvage brésilien sans règles. Pas d''organisation centralisée mais un réseau d''événements qui ont forgé les premières légendes du MMA.', 0),
            (N'Ultimate Fighting Championship', @USA, 1993, 5,
             N'LA référence mondiale. Fondée en 1993, l''UFC a lancé la révolution du MMA moderne. Ses premiers événements sans divisions ni rounds ont choqué le monde.', 0),
            (N'Pancrase', @JPN, 1993, 3,
             N'Organisation japonaise au style hybride entre catch et MMA. Berceau de nombreuses légendes, dont Ken Shamrock et Bas Rutten.', 0),
            (N'Extreme Fighting', @USA, 1995, 2,
             N'Organisation américaine concurrente de l''UFC dans les années 90. Organisait des événements brutaux sur la côte Est.', 0),
            (N'World Vale Tudo Championship', @BRA, 1996, 2,
             N'Circuit brésilien organisé avec des combats sans règles ou presque. Vivier de talent pour les futures organisations mondiales.', 0),
            (N'International Vale Tudo Championship', @BRA, 1997, 2,
             N'Pendant brésilien de l''IVC, un des circuits les plus respectés du Vale Tudo à la fin des années 90.', 0),
            -- Organisations fictives (low prestige, accessibles dès 1985)
            (N'Iron Cage Circuit', @USA, 1986, 1,
             N'Petit circuit américain né dans les salles de garage et les foires régionales. Aucune règle sérieuse, mais une scène qui forge les guerriers. Idéal pour débuter.', 1),
            (N'Vale Tudo Challenge', @BRA, 1990, 1,
             N'Tournoi brésilien local organisé par d''anciens pratiquants de capoeira et lutte. Peu médiatisé mais respecté dans les cercles underground.', 1),

            -- ═══ ÈRE GOLDEN AGE (2000 – 2012) ═══
            -- Organisations réelles
            (N'PRIDE Fighting Championships', NULL, 1997, 5,
             N'La légende japonaise. PRIDE était le cœur du MMA mondial au début des années 2000, avec des combats épiques dans les plus grands stades du Japon.', 0),
            (N'World Extreme Cagefighting', @USA, 2001, 3,
             N'Organisation américaine spécialisée dans les catégories légères. Rival sérieux de l''UFC avant son rachat en 2010.', 0),
            (N'K-1 MMA / Hero''s', @JPN, 2005, 3,
             N'Branche MMA du géant K-1, très populaire en Asie. Combinait les étoiles du kickboxing avec des combattants MMA de haut niveau.', 0),
            (N'Strikeforce', @USA, 2006, 4,
             N'Le principal rival de l''UFC dans les années 2006-2011. Promoteur de nombreux champions et scène de combats historiques.', 0),
            (N'EliteXC', @USA, 2006, 2,
             N'Organisation américaine qui misait sur les grands marchés et la télévision en prime time. Éphémère mais ambitieuse.', 0),
            (N'Dream', @JPN, 2008, 3,
             N'Successeur spirituel de PRIDE au Japon. Plateforme pour les meilleurs combattants asiatiques et quelques stars mondiales.', 0),
            (N'Affliction', @USA, 2008, 2,
             N'Organisation née d''une marque de vêtements, qui a organisé quelques gala de rêve avant de fermer ses portes rapidement.', 0),
            -- Organisation fictive (accessible dès 2000)
            (N'Pacific Combat Series', @JPN, 2001, 1,
             N'Circuit régional asiatique qui fait tourner les combattants entre le Japon, la Corée et l''Australie. Peu de médias, mais une bonne école.', 1),
            (N'Euro Fight Alliance', @FRA, 2003, 1,
             N'Fédération européenne indépendante qui organise des événements en France, Belgique et Pays-Bas. Antichambre des grandes promotions pour les combattants européens.', 1),

            -- ═══ ÈRE MODERN (2013 – aujourd'hui) ═══
            -- Organisations réelles
            (N'Bellator MMA', @USA, 2008, 4,
             N'Numéro 2 mondial pendant des années. Format tournoiphase finale, puis passage aux événements classiques. Fusion avec PFL annoncée en 2023.', 0),
            (N'ONE Championship', NULL, 2011, 4,
             N'La puissance asiatique du MMA. Plus grande organisation d''arts martiaux au monde en termes d''audience, avec des règles uniques et des valeurs d''honneur.', 0),
            (N'Professional Fighters League', @USA, 2017, 3,
             N'Modèle innovant en format ligue : saison régulière, playoffs et finale avec prize money garanti. Alternative sérieuse à l''UFC.', 0),
            -- Organisation fictive (accessible dès 2013)
            (N'Global Combat Network', @USA, 2015, 1,
             N'Réseau de promotions affiliées qui organise des événements dans des marchés secondaires : villes moyennes, pays émergents. Bonne porte d''entrée pour les nouveaux talents.', 1)
        END
    """);

    // Corriger les dates de naissance invalides (combattants trop jeunes pour l'ère 1985)
    // Un combattant doit avoir au moins 18 ans dès 1985 → né avant 1968
    db.Database.ExecuteSqlRaw("""
        UPDATE Combattant
        SET DateNaissance = DATEADD(year,
            -(18 + (ABS(CHECKSUM(CombattantID)) % 20)),
            '1985-06-01')
        WHERE YEAR(DateNaissance) > 1967
    """);
}

// ── Pipeline ──────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
