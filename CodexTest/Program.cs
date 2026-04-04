using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CodexTest.Data;

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
            Argent                  DECIMAL(18,2) NOT NULL DEFAULT 1000.00,
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
