using System.Collections.Generic;
using Lopital;
using RPH.Core.Economy;

namespace RPH.Mod.Economy
{
    /// <summary>
    /// The hospital's renown: its average prestige over the last closed days, in stars (0-5).
    /// The game's prestige is a daily score; averaging it means one bad day does not change everything the next.
    /// </summary>
    internal static class Renown
    {
        private const string DaysId = "RPH_ECONOMY_RENOWN_DAYS";

        public static float Stars()
        {
            List<DailyStatistics> history = Hospital.Instance.m_state.m_statisticsHistory;
            int days = System.Math.Min(System.Math.Max(Tweakables.Int(DaysId), 1), history.Count);
            if (days == 0)
            {
                // First day: no closed day yet, use today's running score.
                return Hospital.Instance.m_state.m_dailyStatistics.m_reputation * 5f;
            }
            float sum = 0f;
            for (int i = history.Count - days; i < history.Count; i++)
            {
                sum += history[i].m_reputation;
            }
            return sum / days * 5f;
        }

        /// <summary>
        /// A value by renown, read from four tweakables: <c>prefix_LOW_STARS</c>, <c>prefix_LOW_PERCENT</c>,
        /// <c>prefix_HIGH_STARS</c>, <c>prefix_HIGH_PERCENT</c>. Linear in between, flat outside.
        /// </summary>
        public static float Ramp(string prefix)
        {
            return EconomyMath.Ramp(
                Stars(),
                Tweakables.Float(prefix + "_LOW_STARS"),
                Tweakables.Float(prefix + "_LOW_PERCENT"),
                Tweakables.Float(prefix + "_HIGH_STARS"),
                Tweakables.Float(prefix + "_HIGH_PERCENT"));
        }

        public static bool HasRamp(string prefix)
        {
            return Database.Instance.GetEntry<GameDBTweakableFloat>(prefix + "_LOW_STARS") != null;
        }
    }

    /// <summary>Reads RPH tweakables; a missing entry reads as 0.</summary>
    internal static class Tweakables
    {
        public static int Int(string id)
        {
            GameDBTweakableInt value = Database.Instance.GetEntry<GameDBTweakableInt>(id);
            return value != null ? value.Value : 0;
        }

        public static float Float(string id)
        {
            GameDBTweakableFloat value = Database.Instance.GetEntry<GameDBTweakableFloat>(id);
            return value != null ? value.Value : 0f;
        }
    }
}
