using MmaManager.Models;
using MmaManager.Models.Dtos;

namespace MmaManager.Services;

public record SimResultat(
    bool   EstVictoire,
    bool   EstNul,
    string Methode,
    byte   Round,
    string Details,
    List<RoundDetailDto> Rounds);

public class CombatSimulationService
{
    public SimResultat SimulerCombat(
        Combattant notre, Combattant adverse, string orgNom, string gameplan, Random rng)
    {
        static double Striking(Combattant c) =>
            c.StatFrappeDebout * 0.40 + c.StatPuissance * 0.35 + c.StatPrecision * 0.25;

        static double Grappling(Combattant c) =>
            c.StatWrestling * 0.25 + c.StatJiuJitsu * 0.40 + c.StatSubmission * 0.35;

        static double Defense(Combattant c) =>
            c.StatVitesse * 0.30 + c.StatAgilite * 0.30 + c.StatAntiTakedown * 0.20 + c.StatRecuperation * 0.20;

        static double FightIQ(Combattant c) =>
            c.StatMental * 0.40 + c.StatExperience * 0.35 + c.StatAdaptation * 0.25;

        double Bruit() => (rng.NextDouble() + rng.NextDouble() - 1.0) * 15.0;

        string[] koTypes  = ["KO", "TKO (coups)", "TKO (arrêt médecin)"];
        string[] subTypes = ["Rear Naked Choke", "Armbar", "Triangle Choke", "Guillotine", "Heel Hook",
                             "Kimura", "D'Arce Choke", "Anaconda Choke"];
        string[] gnpDescs = ["ground and pound", "coups au sol"];

        var rounds = new List<RoundDetailDto>();
        bool combatTermine = false;
        bool notreVictoire = false;
        bool estNul        = false;
        string methodeFinale = "";
        byte roundFin       = 3;
        string detailsFinal = "";

        int scoreCarteN = 0;
        int scoreCarteA = 0;

        for (int rd = 1; rd <= 3 && !combatTermine; rd++)
        {
            double fatigueMult = rd switch
            {
                1 => 1.0,
                2 => 0.90 - (1.0 - notre.StatCardio / 100.0) * 0.10,
                3 => 0.80 - (1.0 - notre.StatCardio / 100.0) * 0.20,
                _ => 0.75
            };
            double fatigueMultA = rd switch
            {
                1 => 1.0,
                2 => 0.90 - (1.0 - adverse.StatCardio / 100.0) * 0.10,
                3 => 0.80 - (1.0 - adverse.StatCardio / 100.0) * 0.20,
                _ => 0.75
            };

            double tdN = notre.StatTakedown   / (double)(notre.StatTakedown   + adverse.StatAntiTakedown + 1);
            double tdA = adverse.StatTakedown / (double)(adverse.StatTakedown + notre.StatAntiTakedown   + 1);

            if (Grappling(notre)   > Striking(notre)   + 8)  tdN = Math.Min(0.80, tdN + 0.15);
            if (Grappling(adverse) > Striking(adverse) + 8)  tdA = Math.Min(0.80, tdA + 0.15);

            if      (gameplan == "Striking")  { tdN *= 0.25; tdA *= 1.1; }
            else if (gameplan == "Grappling") { tdN = Math.Min(0.90, tdN * 1.8); tdA *= 0.7; }

            bool auSol = rng.NextDouble() < Math.Max(tdN, tdA) * 0.60;

            double scoreN, scoreA;
            if (auSol)
            {
                scoreN = Grappling(notre)   * 0.45 * fatigueMult  + FightIQ(notre)   * 0.25 + notre.StatForce   * 0.15 + Bruit();
                scoreA = Grappling(adverse) * 0.45 * fatigueMultA + FightIQ(adverse) * 0.25 + adverse.StatForce * 0.15 + Bruit();
            }
            else
            {
                scoreN = Striking(notre)   * 0.40 * fatigueMult  + Defense(notre)   * 0.20 + FightIQ(notre)   * 0.20 + notre.StatVitesse   * 0.10 + Bruit();
                scoreA = Striking(adverse) * 0.40 * fatigueMultA + Defense(adverse) * 0.20 + FightIQ(adverse) * 0.20 + adverse.StatVitesse * 0.10 + Bruit();
            }

            string gagnantRound;
            int ptN = 9, ptA = 9;
            if (Math.Abs(scoreN - scoreA) < 3.0)
            {
                gagnantRound = "Egal";
                ptN = 10; ptA = 10;
            }
            else if (scoreN > scoreA)
            {
                gagnantRound = "Combattant";
                ptN = 10; ptA = scoreN - scoreA > 12 ? 8 : 9;
            }
            else
            {
                gagnantRound = "Adversaire";
                ptA = 10; ptN = scoreA - scoreN > 12 ? 8 : 9;
            }

            scoreCarteN += ptN;
            scoreCarteA += ptA;

            double koMult  = gameplan == "Striking"  ? 1.40 : gameplan == "Grappling" ? 0.65 : 1.0;
            double subMult = gameplan == "Grappling" ? 1.45 : gameplan == "Striking"  ? 0.55 : 1.0;

            var attaquant = scoreN > scoreA ? notre   : adverse;
            var defenseur = scoreN > scoreA ? adverse : notre;
            bool notreAttaque = scoreN > scoreA;

            double mentonDefenseur = defenseur.StatMentoniere / 100.0;
            double strikingAtt    = Striking(attaquant) / 100.0;

            double koChance = 0;
            if (!auSol)
            {
                koChance = strikingAtt * Math.Max(0.15, 1.0 - mentonDefenseur) * 0.55 * koMult;
                if (Math.Abs(scoreN - scoreA) > 10) koChance *= 1.6;
            }

            double tkoChance = 0;
            if (auSol)
            {
                tkoChance = strikingAtt * Math.Max(0.15, 1.0 - mentonDefenseur * 0.7) * 0.35 * koMult;
            }

            double grapplingAtt     = Grappling(attaquant) / 100.0;
            double evasionDefenseur = defenseur.StatEvasionSub / 100.0;

            double subChance = 0;
            if (auSol)
            {
                subChance = grapplingAtt * Math.Max(0.10, 1.0 - evasionDefenseur) * 0.48 * subMult;
            }

            double fatigueBonus = rd == 3 ? 1.5 : rd == 2 ? 1.2 : 1.0;
            koChance  *= fatigueBonus;
            tkoChance *= fatigueBonus;
            subChance *= fatigueBonus;

            double roll = rng.NextDouble();
            string actions;
            bool finish = false;
            string? methodeFinish = null;

            if (roll < koChance)
            {
                finish = true;
                methodeFinish = koTypes[rng.Next(koTypes.Length)];
                combatTermine = true;
                notreVictoire = notreAttaque;
                methodeFinale = methodeFinish.StartsWith("TKO") ? "TKO" : "KO";
                roundFin = (byte)rd;
                actions = $"{(notreAttaque ? "Notre combattant" : "L'adversaire")} décroche un {methodeFinish} dévastateur !";
                detailsFinal = $"{methodeFinish} au round {rd}";
            }
            else if (roll < koChance + tkoChance)
            {
                finish = true;
                methodeFinish = $"TKO ({gnpDescs[rng.Next(gnpDescs.Length)]})";
                combatTermine = true;
                notreVictoire = notreAttaque;
                methodeFinale = "TKO";
                roundFin = (byte)rd;
                actions = $"{(notreAttaque ? "Notre combattant" : "L'adversaire")} finit par {methodeFinish} !";
                detailsFinal = $"{methodeFinish} au round {rd}";
            }
            else if (roll < koChance + tkoChance + subChance)
            {
                var subType = subTypes[rng.Next(subTypes.Length)];
                finish = true;
                methodeFinish = $"Soumission ({subType})";
                combatTermine = true;
                notreVictoire = notreAttaque;
                methodeFinale = "Soumission";
                roundFin = (byte)rd;
                actions = $"{(notreAttaque ? "Notre combattant" : "L'adversaire")} obtient une {subType} !";
                detailsFinal = $"Soumission ({subType}) au round {rd}";
            }
            else
            {
                if (auSol)
                {
                    if (gagnantRound == "Egal")
                        actions = "Round serré au sol, contrôle partagé, peu de dégâts significatifs.";
                    else
                    {
                        var dominant = gagnantRound == "Combattant" ? "Notre combattant" : "L'adversaire";
                        actions = ptN == 8 || ptA == 8
                            ? $"{dominant} domine largement au sol avec un contrôle total et des tentatives de soumission."
                            : $"{dominant} contrôle le round au sol avec une bonne position et quelques frappes.";
                    }
                }
                else
                {
                    if (gagnantRound == "Egal")
                        actions = "Round très serré debout, échanges équilibrés, les deux combattants se neutralisent.";
                    else
                    {
                        var dominant = gagnantRound == "Combattant" ? "Notre combattant" : "L'adversaire";
                        actions = ptN == 8 || ptA == 8
                            ? $"{dominant} domine le round avec des combinaisons précises et des dégâts visibles."
                            : $"{dominant} remporte le round aux points grâce à un meilleur volume de frappes.";
                    }
                }
            }

            rounds.Add(new RoundDetailDto(rd, gagnantRound, ptN, ptA, actions, finish, methodeFinish));
        }

        if (!combatTermine)
        {
            roundFin = 3;
            if (Math.Abs(scoreCarteN - scoreCarteA) <= 1 && rng.NextDouble() < 0.10)
            {
                estNul = true;
                methodeFinale = "Décision partagée (nul)";
                detailsFinal = $"Match nul — décision partagée ({scoreCarteN}-{scoreCarteA})";
            }
            else
            {
                notreVictoire = scoreCarteN > scoreCarteA;
                if (scoreCarteN == scoreCarteA)
                    notreVictoire = rng.NextDouble() < 0.50;

                bool unanime = Math.Abs(scoreCarteN - scoreCarteA) >= 2;
                methodeFinale = unanime ? "Décision unanime" : "Décision partagée";
                detailsFinal = $"{methodeFinale} ({scoreCarteN}-{scoreCarteA})";
            }
        }

        return new SimResultat(notreVictoire, estNul, methodeFinale, roundFin, detailsFinal, rounds);
    }
}
