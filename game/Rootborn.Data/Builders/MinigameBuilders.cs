using Rootborn.Core.Minigames;

namespace Rootborn.Data.Builders
{
    /// <summary>미니게임과 도전 과제, 성장 보너스. symbolSprites는 그림이라 읽지 않는다.</summary>
    internal static class MinigameBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("MinigameDefinition", (n, c) => new Minigame(
                n.Id, n.Str("displayNameKey"), c.All<IChallenge>(n, "challenges").ToArray(),
                c.All<IMinigameBonusRule>(n, "bonuses").ToArray(), n.Int("successScore")));

            b.Add("SequenceChallengeDefinition", (n, c) => new SequenceChallenge(
                n.Str("instructionKey"), n.IntArray("answers"), n.Strings("symbols"), n.Float("revealSeconds"),
                n.Float("timeLimitSeconds"), n.Int("mistakeAllowance")));
            b.Add("ChoiceChallengeDefinition", (n, c) => new ChoiceChallenge(
                n.Str("instructionKey"), n.IntArray("answers"), n.Strings("prompts"), n.Strings("symbols"),
                n.Float("timeLimitSeconds"), n.Int("mistakeAllowance")));
            b.Add("TimingChallengeDefinition", (n, c) => new TimingChallenge(
                n.Str("instructionKey"), n.FloatArray("beatTimes"), n.Float("windowSeconds"),
                n.Float("timeLimitSeconds"), n.Int("mistakeAllowance")));
            b.Add("PlacementChallengeDefinition", (n, c) => new PlacementChallenge(
                n.Str("instructionKey"), BuilderHelpers.Grid(n, "boardSize"), BuilderHelpers.Grids(n, "pieceSizes"),
                BuilderHelpers.Grids(n, "blockedCells"), BuilderHelpers.Grids(n, "requiredCells"),
                n.Float("timeLimitSeconds"), n.Int("mistakeAllowance")));
            b.Add("TargetDodgeChallengeDefinition", (n, c) => new TargetDodgeChallenge(
                n.Str("instructionKey"), BuilderHelpers.Planes(n, "targets"), n.IntArray("obstacleLanes"),
                n.Float("obstacleIntervalSeconds"), n.Float("targetRadius"), n.Float("targetWobble"), n.Int("laneCount"),
                n.Float("timeLimitSeconds"), n.Int("mistakeAllowance")));

            b.Add("GrowthMinigameBonusRule", (n, c) => new GrowthMinigameBonusRule(
                n.RefId("trait"), n.RefId("skill"), n.Enum<MinigameBonusTarget>("target"),
                n.Float("amountPerPoint"), n.Float("maximumBonus")));
        }
    }
}
