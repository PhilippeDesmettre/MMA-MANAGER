using MmaManager.Models;
using MmaManager.Models.Dtos;

namespace MmaManager.Services;

public record CoachStats(int CompStriking, int CompLutte, int CompGrappling, int CompConditioning, int CompMental);

public class TrainingService
{
    public IReadOnlyList<GainStatDto> AppliquerEntrainement(
        Combattant c, string type, CoachStats stats, Random rng, int age)
    {
        var gains = new List<GainStatDto>();

        switch (type)
        {
            case "Striking":
                gains.Add(Apply(c, "Frappe debout", stats.CompStriking, rng, age, false,
                    () => c.StatFrappeDebout, v => c.StatFrappeDebout = v));
                gains.Add(Apply(c, "Puissance",     stats.CompStriking, rng, age, false,
                    () => c.StatPuissance,    v => c.StatPuissance    = v));
                gains.Add(Apply(c, "Précision",     stats.CompStriking, rng, age, false,
                    () => c.StatPrecision,    v => c.StatPrecision    = v));
                break;

            case "Lutte":
                gains.Add(Apply(c, "Wrestling",     stats.CompLutte, rng, age, false,
                    () => c.StatWrestling,    v => c.StatWrestling    = v));
                gains.Add(Apply(c, "Takedown",      stats.CompLutte, rng, age, false,
                    () => c.StatTakedown,     v => c.StatTakedown     = v));
                gains.Add(Apply(c, "Anti-takedown", stats.CompLutte, rng, age, false,
                    () => c.StatAntiTakedown, v => c.StatAntiTakedown = v));
                break;

            case "Grappling":
                gains.Add(Apply(c, "Jiu-Jitsu",   stats.CompGrappling, rng, age, false,
                    () => c.StatJiuJitsu,   v => c.StatJiuJitsu   = v));
                gains.Add(Apply(c, "Submission",  stats.CompGrappling, rng, age, false,
                    () => c.StatSubmission, v => c.StatSubmission  = v));
                gains.Add(Apply(c, "Évasion sub", stats.CompGrappling, rng, age, false,
                    () => c.StatEvasionSub, v => c.StatEvasionSub  = v));
                break;

            case "Conditionnement":
                gains.Add(Apply(c, "Force",        stats.CompConditioning, rng, age, true,
                    () => c.StatForce,        v => c.StatForce        = v));
                gains.Add(Apply(c, "Vitesse",      stats.CompConditioning, rng, age, true,
                    () => c.StatVitesse,      v => c.StatVitesse      = v));
                gains.Add(Apply(c, "Agilité",      stats.CompConditioning, rng, age, true,
                    () => c.StatAgilite,      v => c.StatAgilite      = v));
                gains.Add(Apply(c, "Cardio",       stats.CompConditioning, rng, age, true,
                    () => c.StatCardio,       v => c.StatCardio       = v));
                gains.Add(Apply(c, "Récupération", stats.CompConditioning, rng, age, true,
                    () => c.StatRecuperation, v => c.StatRecuperation = v));
                break;

            case "Mental":
                gains.Add(Apply(c, "Mental",     stats.CompMental, rng, age, false,
                    () => c.StatMental,     v => c.StatMental     = v));
                gains.Add(Apply(c, "Expérience", stats.CompMental, rng, age, false,
                    () => c.StatExperience, v => c.StatExperience = v));
                gains.Add(Apply(c, "Adaptation", stats.CompMental, rng, age, false,
                    () => c.StatAdaptation, v => c.StatAdaptation = v));
                break;
        }

        return gains.Where(g => g.Gain > 0).ToList();
    }

    private static int CalculGain(
        int trainerComp, Random rng,
        int age, byte potentiel, byte currentStat, byte progression, bool isPhysical)
    {
        if (currentStat >= potentiel) return 0;

        var factor   = rng.Next(1, 4);
        int gainBase = Math.Max(1, (int)Math.Round(factor * trainerComp / 50.0));

        double ageMult;
        if (age < 22)       ageMult = 1.3;
        else if (age < 28)  ageMult = 1.0;
        else if (age <= 33) ageMult = 0.6;
        else if (age <= 38) ageMult = isPhysical ? 0.1 : 0.5;
        else                ageMult = isPhysical ? 0.0 : 0.3;

        double progMult = 0.7 + (progression / 100.0) * 0.6;

        int gain = (int)Math.Round(gainBase * ageMult * progMult);

        if (currentStat + gain > potentiel)
            gain = Math.Max(0, potentiel - currentStat);

        return gain;
    }

    private static byte ClampByte(int val) => (byte)Math.Min(100, Math.Max(0, val));

    private static GainStatDto Apply(
        Combattant c,
        string nom,
        int trainerComp,
        Random rng,
        int age,
        bool isPhysical,
        Func<byte> getter,
        Action<byte> setter)
    {
        var gain    = CalculGain(trainerComp, rng, age, c.Potentiel, getter(), c.Progression, isPhysical);
        var ancien  = getter();
        var nouveau = ClampByte(ancien + gain);
        setter(nouveau);
        return new GainStatDto(nom, nouveau - ancien);
    }
}
