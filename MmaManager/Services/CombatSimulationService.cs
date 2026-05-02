using MmaManager.Models;
using MmaManager.Models.Dtos;

namespace MmaManager.Services;

public record BlessureCombat(
    byte    Gravite,          // 1=légère, 2=modérée, 3=grave
    string? Zone,             // "Tête", "Main", "Genou"…
    short   SemainesIndispo); // tours d'indisponibilité

public record SimResultat(
    bool   EstVictoire,
    bool   EstNul,
    string Methode,
    byte   Round,
    string Details,
    List<RoundDetailDto> Rounds,
    BlessureCombat? BlessureNotre,
    BlessureCombat? BlessureAdverse);

public class CombatSimulationService
{
    private enum Phase { Debout, Clinch, Sol }

    // ── Scores composites ────────────────────────────────────────

    private static double StrikingDistance(Combattant c) =>
        c.StatFrappeDebout  * 0.20 + c.StatPuissance   * 0.15 + c.StatPrecision    * 0.20 +
        c.StatKick          * 0.15 + c.StatFootwork     * 0.15 + c.StatVitesseMains * 0.15;

    private static double ScoreClinch(Combattant c) =>
        c.StatClinic    * 0.30 + c.StatForce    * 0.20 + c.StatWrestling * 0.20 +
        c.StatPuissance * 0.15 + c.StatAgilite  * 0.15;

    private static double ScoreGrappling(Combattant c) =>
        c.StatJiuJitsu  * 0.30 + c.StatSubmission  * 0.25 + c.StatControleSol * 0.25 +
        c.StatWrestling * 0.10 + c.StatForce        * 0.10;

    private static double DefenseDebout(Combattant c) =>
        c.StatEsquive * 0.30 + c.StatBlocage  * 0.25 + c.StatFootwork * 0.25 +
        c.StatVitesse * 0.20;

    private static double DefenseSol(Combattant c) =>
        c.StatEvasionSub * 0.35 + c.StatControleSol * 0.25 +
        c.StatAgilite    * 0.20 + c.StatForce        * 0.20;

    private static double FightIQ(Combattant c) =>
        c.StatMental     * 0.30 + c.StatExperience * 0.30 +
        c.StatAdaptation * 0.25 + c.StatCoaching   * 0.15;

    private static double ScoreCombos(Combattant c) =>
        c.StatCombosDebout * 0.40 + c.StatVitesseMains * 0.30 + c.StatPrecision * 0.30;

    // ── Fatigue ──────────────────────────────────────────────────

    private static double CalculerFatigue(int round, int totalRounds, byte cardio)
    {
        if (round == 1) return 1.0;
        double cardioFactor      = cardio / 100.0;
        double roundProgression  = (double)(round - 1) / (totalRounds - 1);
        double fatigueLoss       = roundProgression * (0.40 - cardioFactor * 0.30);
        return Math.Max(0.55, 1.0 - fatigueLoss);
    }

    // ── Point d'entrée ───────────────────────────────────────────

    public SimResultat SimulerCombat(
        Combattant notre, Combattant adverse, string orgNom,
        string gameplan, Random rng, bool isTitleFight = false)
    {
        double Bruit() => (rng.NextDouble() + rng.NextDouble() - 1.0) * 12.0;

        double allongeN = notre.AllongeCm   ?? 180;
        double allongeA = adverse.AllongeCm ?? 180;
        double reachAdvantage = Math.Clamp((allongeN - allongeA) / 20.0, -1.0, 1.0);

        double tailleN = notre.TailleCm   ?? 178;
        double tailleA = adverse.TailleCm ?? 178;
        double heightAdvantage = Math.Clamp((tailleN - tailleA) / 25.0, -1.0, 1.0);

        int totalRounds = isTitleFight ? 5 : 3;

        double dommagesN = 0, dommagesA = 0;
        bool coupureN = false, coupureA = false;
        int coupureRoundN = 0, coupureRoundA = 0;

        var rounds = new List<RoundDetailDto>();
        bool combatTermine = false, notreVictoire = false, estNul = false;
        string methodeFinale = "";
        byte roundFin = (byte)totalRounds;
        string detailsFinal = "";
        int scoreCarteN = 0, scoreCarteA = 0;

        double koMult  = gameplan == "Striking"  ? 1.40 : gameplan == "Grappling" ? 0.65 : 1.0;
        double subMult = gameplan == "Grappling" ? 1.45 : gameplan == "Striking"  ? 0.55 : 1.0;

        // Probabilités de takedown (calculées une fois)
        double tdN = notre.StatTakedown   / (double)(notre.StatTakedown   + adverse.StatAntiTakedown + 1);
        double tdA = adverse.StatTakedown / (double)(adverse.StatTakedown + notre.StatAntiTakedown   + 1);

        if (ScoreGrappling(notre)   > StrikingDistance(notre)   + 8) tdN = Math.Min(0.80, tdN + 0.15);
        if (ScoreGrappling(adverse) > StrikingDistance(adverse) + 8) tdA = Math.Min(0.80, tdA + 0.15);

        if      (gameplan == "Striking")  { tdN *= 0.25; tdA *= 1.1; }
        else if (gameplan == "Grappling") { tdN  = Math.Min(0.90, tdN * 1.8); tdA *= 0.7; }

        tdN = Math.Clamp(tdN - heightAdvantage * 0.05, 0, 0.90);
        tdA = Math.Clamp(tdA + heightAdvantage * 0.05, 0, 0.90);

        string[] koTypes  = ["KO", "TKO (coups au visage)", "TKO (coups)"];
        string[] subTypes = [
            "Rear Naked Choke", "Armbar", "Triangle Choke", "Guillotine",
            "Heel Hook", "Kimura", "D'Arce Choke", "Anaconda Choke"
        ];

        Phase phaseActuelle = Phase.Debout;

        for (int rd = 1; rd <= totalRounds && !combatTermine; rd++)
        {
            double fatigueMult  = CalculerFatigue(rd, totalRounds, notre.StatCardio);
            double fatigueMultA = CalculerFatigue(rd, totalRounds, adverse.StatCardio);
            double effN = fatigueMult  * Math.Max(0.30, 1.0 - dommagesN / 200.0);
            double effA = fatigueMultA * Math.Max(0.30, 1.0 - dommagesA / 200.0);

            int nbSequences = 2 + (rng.NextDouble() < 0.35 ? 1 : 0);

            double scoreRoundN = 0, scoreRoundA = 0;
            var actionsRound = new List<string>();
            bool roundTermine = false, finish = false;
            string? methodeFinish = null;

            phaseActuelle = Phase.Debout;

            for (int seq = 0; seq < nbSequences && !roundTermine; seq++)
            {
                Phase phase = DeterminerPhase(notre, adverse, gameplan, rng,
                    reachAdvantage, heightAdvantage, seq, phaseActuelle, tdN, tdA);
                phaseActuelle = phase;

                double seqScoreN, seqScoreA;

                switch (phase)
                {
                    case Phase.Debout:
                        seqScoreN = StrikingDistance(notre)  * 0.35 * effN
                                  + DefenseDebout(notre)      * 0.15
                                  + ScoreCombos(notre)        * 0.15
                                  + FightIQ(notre)            * 0.15
                                  + reachAdvantage * 6.0 + Bruit();
                        seqScoreA = StrikingDistance(adverse) * 0.35 * effA
                                  + DefenseDebout(adverse)    * 0.15
                                  + ScoreCombos(adverse)      * 0.15
                                  + FightIQ(adverse)          * 0.15
                                  - reachAdvantage * 6.0 + Bruit();
                        actionsRound.Add(NarratifDebout(notre, adverse, seqScoreN, seqScoreA, rng));
                        break;

                    case Phase.Clinch:
                        // Le fighter plus petit a un avantage (centre de gravité, underhooks)
                        seqScoreN = ScoreClinch(notre)   * 0.40 * effN
                                  + FightIQ(notre)       * 0.20
                                  + notre.StatForce      * 0.15 * effN
                                  - heightAdvantage * 4.0 + Bruit();
                        seqScoreA = ScoreClinch(adverse) * 0.40 * effA
                                  + FightIQ(adverse)     * 0.20
                                  + adverse.StatForce    * 0.15 * effA
                                  + heightAdvantage * 4.0 + Bruit();
                        actionsRound.Add(NarratifClinch(notre, adverse, seqScoreN, seqScoreA, rng));
                        break;

                    default: // Sol
                        seqScoreN = ScoreGrappling(notre)  * 0.40 * effN
                                  + DefenseSol(notre)       * 0.15
                                  + FightIQ(notre)          * 0.20
                                  + notre.StatForce         * 0.10 * effN
                                  + Bruit();
                        seqScoreA = ScoreGrappling(adverse) * 0.40 * effA
                                  + DefenseSol(adverse)      * 0.15
                                  + FightIQ(adverse)         * 0.20
                                  + adverse.StatForce        * 0.10 * effA
                                  + Bruit();
                        actionsRound.Add(NarratifSol(notre, adverse, seqScoreN, seqScoreA, rng));
                        break;
                }

                scoreRoundN += seqScoreN;
                scoreRoundA += seqScoreA;

                // Dégâts cumulatifs : le perdant de la séquence absorbe
                bool notreSeqDom = seqScoreN >= seqScoreA;
                double diffSeq   = Math.Abs(seqScoreN - seqScoreA);
                if (notreSeqDom) dommagesA = Math.Min(100, dommagesA + diffSeq * 0.12);
                else             dommagesN = Math.Min(100, dommagesN + diffSeq * 0.12);

                // Attaquant / défenseur pour le check finish
                var    att              = notreSeqDom ? notre   : adverse;
                var    def              = notreSeqDom ? adverse : notre;
                bool   notreAtt         = notreSeqDom;
                double dommagesDef      = notreSeqDom ? dommagesA : dommagesN;
                double dommageMultDef   = 1.0 + dommagesDef / 50.0;
                string nomAtt = notreAtt ? "Notre combattant" : "L'adversaire";
                string nomDef = notreAtt ? "L'adversaire"    : "Notre combattant";

                double koChance = 0, tkoChance = 0, subChance = 0;

                if (phase == Phase.Debout)
                {
                    double reachKoBonus = 1.0 + reachAdvantage * (notreAtt ? 0.15 : -0.15);
                    double koBase = StrikingDistance(att) / 100.0
                                  * Math.Max(0.15, 1.0 - def.StatMentoniere / 100.0);
                    koChance = koBase * 0.12 * koMult * reachKoBonus * dommageMultDef;
                }
                else if (phase == Phase.Clinch)
                {
                    double clinchKoBonus = notreAtt
                        ? 1.0 - heightAdvantage * 0.15
                        : 1.0 + heightAdvantage * 0.15;
                    double koBase = ScoreClinch(att) / 100.0
                                  * Math.Max(0.15, 1.0 - def.StatMentoniere / 100.0);
                    koChance = koBase * 0.08 * koMult * clinchKoBonus * dommageMultDef;
                }
                else // Sol
                {
                    double tkoBase = StrikingDistance(att) / 100.0
                                   * Math.Max(0.15, 1.0 - def.StatMentoniere * 0.7 / 100.0);
                    tkoChance = tkoBase * 0.08 * koMult * dommageMultDef;

                    double subBase = ScoreGrappling(att) / 100.0
                                   * Math.Max(0.10, 1.0 - def.StatEvasionSub / 100.0);
                    subChance = subBase * 0.10 * subMult * (1.0 + dommagesDef / 80.0);
                }

                double roll = rng.NextDouble();

                if (roll < koChance)
                {
                    string koType = phase == Phase.Clinch && rng.NextDouble() < 0.5
                        ? "TKO (coudes au clinch)"
                        : koTypes[rng.Next(koTypes.Length)];
                    finish = true; methodeFinish = koType;
                    methodeFinale = koType.StartsWith("TKO") ? "TKO" : "KO";
                    roundFin = (byte)rd; notreVictoire = notreAtt;
                    detailsFinal = $"{koType} au round {rd}";
                    actionsRound.Add($"{nomAtt} décroche un {koType} dévastateur !");
                    roundTermine = true; combatTermine = true;
                }
                else if (roll < koChance + tkoChance)
                {
                    finish = true; methodeFinish = "TKO (ground and pound)";
                    methodeFinale = "TKO"; roundFin = (byte)rd; notreVictoire = notreAtt;
                    detailsFinal = $"TKO (ground and pound) au round {rd}";
                    actionsRound.Add($"{nomAtt} termine par TKO au sol, l'arbitre stoppe le combat !");
                    roundTermine = true; combatTermine = true;
                }
                else if (roll < koChance + tkoChance + subChance)
                {
                    string subType = subTypes[rng.Next(subTypes.Length)];
                    finish = true; methodeFinish = $"Soumission ({subType})";
                    methodeFinale = "Soumission"; roundFin = (byte)rd; notreVictoire = notreAtt;
                    detailsFinal = $"Soumission ({subType}) au round {rd}";
                    actionsRound.Add($"{nomAtt} force la soumission par {subType} !");
                    roundTermine = true; combatTermine = true;
                }
                else if (phase is Phase.Debout or Phase.Clinch)
                {
                    // Chance de coupure (coudes en clinch plus dangereux)
                    double coupureChance = (phase == Phase.Clinch ? 0.04 : 0.02)
                                         * att.StatPrecision / 100.0;
                    bool dejaCoupure = notreAtt ? coupureA : coupureN;
                    if (!dejaCoupure && rng.NextDouble() < coupureChance)
                    {
                        if (notreAtt) { coupureA = true; coupureRoundA = rd; }
                        else          { coupureN = true; coupureRoundN = rd; }
                        actionsRound.Add($"{nomDef} est coupé ! L'arbitre surveille la blessure.");
                    }
                }
            } // fin séquences

            // Arrêt médecin entre les rounds
            if (!combatTermine)
            {
                if (coupureN && rd - coupureRoundN >= 2 && dommagesN > 50)
                {
                    double p = 0.15 + (dommagesN - 50) / 200.0;
                    if (rng.NextDouble() < p)
                    {
                        finish = true; methodeFinish = "TKO (arrêt médecin)";
                        methodeFinale = "TKO"; roundFin = (byte)rd; notreVictoire = false;
                        detailsFinal = $"TKO (arrêt médecin) au round {rd}";
                        actionsRound.Add("L'arbitre stoppe le combat après examen médical de la coupure !");
                        combatTermine = true;
                    }
                }
                if (!combatTermine && coupureA && rd - coupureRoundA >= 2 && dommagesA > 50)
                {
                    double p = 0.15 + (dommagesA - 50) / 200.0;
                    if (rng.NextDouble() < p)
                    {
                        finish = true; methodeFinish = "TKO (arrêt médecin)";
                        methodeFinale = "TKO"; roundFin = (byte)rd; notreVictoire = true;
                        detailsFinal = $"TKO (arrêt médecin) au round {rd}";
                        actionsRound.Add("L'adversaire est retiré du combat suite à une coupure sévère !");
                        combatTermine = true;
                    }
                }
            }

            // Récupération entre les rounds
            dommagesN = Math.Max(0, dommagesN - notre.StatRecuperation   * 0.08);
            dommagesA = Math.Max(0, dommagesA - adverse.StatRecuperation * 0.08);

            // Scoring des juges (10-point must)
            string gagnantRound;
            int ptN = 9, ptA = 9;
            if (Math.Abs(scoreRoundN - scoreRoundA) < 6.0)
            {
                gagnantRound = "Egal"; ptN = 10; ptA = 10;
            }
            else if (scoreRoundN > scoreRoundA)
            {
                gagnantRound = "Combattant";
                ptN = 10; ptA = scoreRoundN - scoreRoundA > 22 ? 8 : 9;
            }
            else
            {
                gagnantRound = "Adversaire";
                ptA = 10; ptN = scoreRoundA - scoreRoundN > 22 ? 8 : 9;
            }

            scoreCarteN += ptN;
            scoreCarteA += ptA;

            rounds.Add(new RoundDetailDto(
                rd, gagnantRound, ptN, ptA,
                string.Join(" | ", actionsRound),
                finish, methodeFinish));
        } // fin rounds

        if (!combatTermine)
        {
            roundFin = (byte)totalRounds;
            if (Math.Abs(scoreCarteN - scoreCarteA) <= 1 && rng.NextDouble() < 0.10)
            {
                estNul        = true;
                methodeFinale = "Décision partagée (nul)";
                detailsFinal  = $"Match nul — décision partagée ({scoreCarteN}-{scoreCarteA})";
            }
            else
            {
                notreVictoire = scoreCarteN > scoreCarteA;
                if (scoreCarteN == scoreCarteA) notreVictoire = rng.NextDouble() < 0.50;
                bool unanime  = Math.Abs(scoreCarteN - scoreCarteA) >= (totalRounds >= 5 ? 3 : 2);
                methodeFinale = unanime ? "Décision unanime" : "Décision partagée";
                detailsFinal  = $"{methodeFinale} ({scoreCarteN}-{scoreCarteA})";
            }
        }

        bool notrePerdant   = !notreVictoire && !estNul;
        bool adversePerdant =  notreVictoire && !estNul;
        var blessureNotre   = GenererBlessure(notre,   dommagesN, methodeFinale, roundFin, notrePerdant,   rng);
        var blessureAdverse = GenererBlessure(adverse, dommagesA, methodeFinale, roundFin, adversePerdant, rng);

        return new SimResultat(notreVictoire, estNul, methodeFinale, roundFin, detailsFinal, rounds,
                               blessureNotre, blessureAdverse);
    }

    // ── Blessures post-combat ────────────────────────────────────

    private static BlessureCombat? GenererBlessure(
        Combattant combattant, double dommages, string methode,
        byte roundFin, bool estPerdant, Random rng)
    {
        double probBlessure;

        if (estPerdant && (methode == "KO" || methode == "TKO"))
            probBlessure = 0.80;
        else if (estPerdant && methode == "Soumission")
            probBlessure = 0.40;
        else
        {
            probBlessure = dommages / 200.0;
            if (!estPerdant) probBlessure *= 0.4;
        }

        probBlessure *= Math.Max(0.5, 1.0 - combattant.StatRecuperation / 200.0);

        if (rng.NextDouble() >= probBlessure) return null;

        string zone;
        byte   gravite;
        short  semainesIndispo;

        if (estPerdant && (methode == "KO" || methode == "TKO"))
        {
            string[] zones = ["Tête", "Tête", "Tête", "Arcade", "Nez", "Mâchoire"];
            zone            = zones[rng.Next(zones.Length)];
            gravite         = (byte)(2 + rng.Next(2));
            semainesIndispo = gravite == 3
                ? (short)(4 + rng.Next(5))
                : (short)(2 + rng.Next(3));
        }
        else if (estPerdant && methode == "Soumission")
        {
            string[] zones = ["Épaule", "Coude", "Genou", "Cheville", "Poignet", "Cou"];
            zone            = zones[rng.Next(zones.Length)];
            gravite         = (byte)(1 + rng.Next(3));
            semainesIndispo = gravite switch
            {
                3 => (short)(5 + rng.Next(4)),
                2 => (short)(3 + rng.Next(3)),
                _ => (short)(1 + rng.Next(2))
            };
        }
        else
        {
            string[] zones = ["Main", "Pied", "Côtes", "Arcade", "Genou", "Tibia"];
            zone            = zones[rng.Next(zones.Length)];
            gravite         = (byte)(1 + rng.Next(2));
            semainesIndispo = gravite == 2
                ? (short)(2 + rng.Next(2))
                : (short)(1 + rng.Next(2));
        }

        return new BlessureCombat(gravite, zone, semainesIndispo);
    }

    // ── Transition de phase ──────────────────────────────────────

    private Phase DeterminerPhase(
        Combattant notre, Combattant adverse, string gameplan, Random rng,
        double reachAdvantage, double heightAdvantage,
        int seq, Phase phaseActuelle, double tdN, double tdA)
    {
        if (seq == 0) return Phase.Debout;

        double roll = rng.NextDouble();

        switch (phaseActuelle)
        {
            case Phase.Debout:
            {
                double probClinch = Math.Clamp(
                    0.15
                    + (ScoreClinch(adverse) > ScoreClinch(notre) ? 0.10 : 0.0)
                    - reachAdvantage * 0.10
                    + (gameplan == "Grappling" ? 0.05 : 0.0)
                    - (gameplan == "Striking"  ? 0.08 : 0.0),
                    0.0, 0.50);

                double probSol = Math.Clamp(
                    Math.Max(tdN, tdA) * 0.45
                    * (gameplan == "Striking"  ? 0.30 : 1.0)
                    * (gameplan == "Grappling" ? 1.50 : 1.0),
                    0.0, 0.45);

                if (roll < probSol)              return Phase.Sol;
                if (roll < probSol + probClinch) return Phase.Clinch;
                return Phase.Debout;
            }

            case Phase.Clinch:
            {
                double probSol = Math.Clamp(
                    Math.Max(tdN, tdA) * 0.80
                    * (gameplan == "Striking"  ? 0.20 : 1.0)
                    * (gameplan == "Grappling" ? 1.30 : 1.0),
                    0.0, 0.60);

                double probDebout = Math.Clamp(
                    0.20 + notre.StatFootwork / 200.0
                    + (gameplan == "Striking" ? 0.15 : 0.0),
                    0.0, 0.55);

                if (roll < probSol)              return Phase.Sol;
                if (roll < probSol + probDebout) return Phase.Debout;
                return Phase.Clinch;
            }

            default: // Sol
            {
                double probDebout = Math.Clamp(
                    0.15
                    + (notre.StatAgilite + notre.StatEvasionSub) * 0.001
                    - adverse.StatControleSol * 0.0015
                    + (gameplan == "Striking"  ? 0.15 : 0.0)
                    - (gameplan == "Grappling" ? 0.10 : 0.0),
                    0.05, 0.55);

                if (roll < probDebout) return Phase.Debout;
                return Phase.Sol;
            }
        }
    }

    // ── Narratifs ────────────────────────────────────────────────

    private static string NarratifDebout(
        Combattant notre, Combattant adverse, double scoreN, double scoreA, Random rng)
    {
        double diff = scoreN - scoreA;
        if (diff > 8)
        {
            string[] p = [
                "Notre combattant touche avec des combinaisons précises à distance, l'adversaire recule.",
                "Notre combattant place un jab-cross percutant puis enchaîne avec un crochet dévastateur.",
                "Notre combattant domine les échanges debout, repoussant l'adversaire dans les cordes.",
                "Notre combattant use d'un jeu de jambes impeccable pour placer des coups propres.",
                "Notre combattant connecte un high kick dévastateur qui ébranle sérieusement l'adversaire."
            ];
            return p[rng.Next(p.Length)];
        }
        if (diff < -8)
        {
            string[] p = [
                "L'adversaire impose son jeu de jambes et touche avec des combinaisons précises.",
                "L'adversaire connecte un uppercut qui fait vaciller notre combattant.",
                "L'adversaire domine les échanges debout avec des coups plus précis et plus puissants.",
                "L'adversaire place un low kick répété qui affecte la mobilité de notre combattant.",
                "L'adversaire utilise une bonne distance et touche sans se faire contrer."
            ];
            return p[rng.Next(p.Length)];
        }
        string[] eq = [
            "Échanges équilibrés debout, les deux combattants se rendent coup pour coup.",
            "Round serré debout, bons échanges des deux côtés, aucun ne prend l'avantage.",
            "Les deux combattants se cherchent à distance, quelques bons coups de part et d'autre.",
            "Combat technique debout, les deux fighters s'observent et testent les défenses."
        ];
        return eq[rng.Next(eq.Length)];
    }

    private static string NarratifClinch(
        Combattant notre, Combattant adverse, double scoreN, double scoreA, Random rng)
    {
        double diff = scoreN - scoreA;
        if (diff > 8)
        {
            string[] p = [
                "Notre combattant domine dans le clinch avec des coudes tranchants.",
                "Notre combattant place de puissants genoux au corps dans le clinch.",
                "Notre combattant neutralise l'adversaire dans le clinch et place des uppercuts dévastateurs.",
                "Notre combattant prend les underhooks et impose sa force dans le corps-à-corps.",
                "Notre combattant s'impose dans le clinch avec des frappes sales répétées."
            ];
            return p[rng.Next(p.Length)];
        }
        if (diff < -8)
        {
            string[] p = [
                "L'adversaire place des coudes dévastateurs dans le clinch.",
                "L'adversaire domine le corps-à-corps avec des genoux au foie percutants.",
                "L'adversaire prend les underhooks et neutralise notre combattant dans le clinch.",
                "L'adversaire s'impose dans le clinch avec des frappes sales très efficaces.",
                "L'adversaire use de sa force pour dominer le clinch et placer des genoux."
            ];
            return p[rng.Next(p.Length)];
        }
        string[] eq = [
            "Combat sale dans le clinch, échanges de genoux au corps, aucun avantage net.",
            "Accrochage intense, les deux combattants se battent pour la position avant la séparation.",
            "Clinch disputé, les deux fighters placent quelques frappes sans se distinguer.",
            "Corps-à-corps équilibré, l'arbitre finit par séparer les deux combattants."
        ];
        return eq[rng.Next(eq.Length)];
    }

    private static string NarratifSol(
        Combattant notre, Combattant adverse, double scoreN, double scoreA, Random rng)
    {
        double diff = scoreN - scoreA;
        if (diff > 8)
        {
            string[] p = [
                "Notre combattant contrôle au sol avec une excellente position et des tentatives de soumission.",
                "Notre combattant domine avec du ground and pound efficace depuis la position de dessus.",
                "Notre combattant tient l'adversaire au sol et place des coups au corps répétés.",
                "Notre combattant passe à la position dos et cherche l'étranglement arrière.",
                "Notre combattant domine au sol, passe la garde et écrase l'adversaire."
            ];
            return p[rng.Next(p.Length)];
        }
        if (diff < -8)
        {
            string[] p = [
                "L'adversaire contrôle au sol et place des coups depuis la position de dessus.",
                "L'adversaire domine la position et cherche une soumission avec insistance.",
                "L'adversaire impose son jeu au sol, notre combattant défend avec difficulté.",
                "L'adversaire prend le dos de notre combattant et cherche l'étranglement arrière.",
                "L'adversaire tient notre combattant au sol avec un contrôle total, aucune issue."
            ];
            return p[rng.Next(p.Length)];
        }
        string[] eq = [
            "Combat au sol disputé, les deux combattants se battent pour la position.",
            "Grappling équilibré au sol, aucun ne prend l'avantage décisif.",
            "Notre combattant défend bien les soumissions, le combat reste serré au sol.",
            "Lutte au sol serrée, les deux fighters s'annulent mutuellement."
        ];
        return eq[rng.Next(eq.Length)];
    }
}
