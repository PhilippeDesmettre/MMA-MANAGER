using MmaManager.Models;
using MmaManager.Models.Dtos;

namespace MmaManager.Services;

public record CoachStats(int CompStriking, int CompLutte, int CompGrappling, int CompConditioning, int CompMental);

public class TrainingService
{
    public IReadOnlyList<GainStatDto> AppliquerEntrainement(
        Combattant c, string type, CoachStats stats, Random rng)
    {
        var gains = new List<GainStatDto>();

        switch (type)
        {
            case "Striking":
                gains.Add(Apply(c, "Frappe debout", stats.CompStriking, rng,
                    () => c.StatFrappeDebout, v => c.StatFrappeDebout = v));
                gains.Add(Apply(c, "Puissance",     stats.CompStriking, rng,
                    () => c.StatPuissance,    v => c.StatPuissance    = v));
                gains.Add(Apply(c, "Précision",     stats.CompStriking, rng,
                    () => c.StatPrecision,    v => c.StatPrecision    = v));
                break;

            case "Lutte":
                gains.Add(Apply(c, "Wrestling",     stats.CompLutte, rng,
                    () => c.StatWrestling,    v => c.StatWrestling    = v));
                gains.Add(Apply(c, "Takedown",      stats.CompLutte, rng,
                    () => c.StatTakedown,     v => c.StatTakedown     = v));
                gains.Add(Apply(c, "Anti-takedown", stats.CompLutte, rng,
                    () => c.StatAntiTakedown, v => c.StatAntiTakedown = v));
                break;

            case "Grappling":
                gains.Add(Apply(c, "Jiu-Jitsu",   stats.CompGrappling, rng,
                    () => c.StatJiuJitsu,   v => c.StatJiuJitsu   = v));
                gains.Add(Apply(c, "Submission",  stats.CompGrappling, rng,
                    () => c.StatSubmission, v => c.StatSubmission  = v));
                gains.Add(Apply(c, "Évasion sub", stats.CompGrappling, rng,
                    () => c.StatEvasionSub, v => c.StatEvasionSub  = v));
                break;

            case "Conditionnement":
                gains.Add(Apply(c, "Force",        stats.CompConditioning, rng,
                    () => c.StatForce,        v => c.StatForce        = v));
                gains.Add(Apply(c, "Vitesse",      stats.CompConditioning, rng,
                    () => c.StatVitesse,      v => c.StatVitesse      = v));
                gains.Add(Apply(c, "Agilité",      stats.CompConditioning, rng,
                    () => c.StatAgilite,      v => c.StatAgilite      = v));
                gains.Add(Apply(c, "Cardio",       stats.CompConditioning, rng,
                    () => c.StatCardio,       v => c.StatCardio       = v));
                gains.Add(Apply(c, "Récupération", stats.CompConditioning, rng,
                    () => c.StatRecuperation, v => c.StatRecuperation = v));
                break;

            case "Mental":
                gains.Add(Apply(c, "Mental",     stats.CompMental, rng,
                    () => c.StatMental,     v => c.StatMental     = v));
                gains.Add(Apply(c, "Expérience", stats.CompMental, rng,
                    () => c.StatExperience, v => c.StatExperience = v));
                gains.Add(Apply(c, "Adaptation", stats.CompMental, rng,
                    () => c.StatAdaptation, v => c.StatAdaptation = v));
                break;
        }

        return gains.Where(g => g.Gain > 0).ToList();
    }

    private static int CalculGain(int trainerComp, Random rng)
    {
        var factor = rng.Next(1, 4);
        return Math.Max(1, (int)Math.Round(factor * trainerComp / 50.0));
    }

    private static byte ClampByte(int val) => (byte)Math.Min(100, Math.Max(0, val));

    private static GainStatDto Apply(
        Combattant c,
        string nom,
        int trainerComp,
        Random rng,
        Func<byte> getter,
        Action<byte> setter)
    {
        var gain    = CalculGain(trainerComp, rng);
        var ancien  = getter();
        var nouveau = ClampByte(ancien + gain);
        setter(nouveau);
        return new GainStatDto(nom, nouveau - ancien);
    }
}
