using System.Collections.Generic;
using System.Linq;
using Lopital;
using RPH.Core.Accounting;

namespace RPH.Mod.Accounting
{
    /// <summary>Balance log lines written by the ledger: sessions, settings in effect and the summary of each day.</summary>
    internal static class LedgerBalanceLog
    {
        private const string TweakablePrefix = "RPH_";

        /// <summary>A game was started (<paramref name="save"/> is null) or loaded: context, then every RPH tweakable in effect.</summary>
        public static void Session(string save)
        {
            try
            {
                WriteSession(save);
            }
            catch (System.Exception e)
            {
                RPHLog.PatchError("BalanceLog.Session", e);
            }
        }

        public static void Saved(string save)
        {
            RPHLog.Balance(BalanceSection.Session, "event=save save=" + save);
        }

        /// <summary>The day just closed: totals, the game's patient and prestige figures, then one line per ledger category.</summary>
        public static void Day(DayBook book)
        {
            try
            {
                WriteDay(book);
            }
            catch (System.Exception e)
            {
                RPHLog.PatchError("BalanceLog.Day", e);
            }
        }

        private static void WriteSession(string save)
        {
            Budget budget = Hospital.Instance.m_state.m_budget;
            RPHLog.Balance(BalanceSection.Session,
                "event=" + (save != null ? "load" : "new")
                + " save=" + (save ?? "-")
                + " version=" + RPHModEntry.Version
                + " balance=" + budget.m_currentBalance
                + " loan=" + budget.m_loan
                + " pook=" + (RPHCompatibility.PooKEconomyLoaded ? 1 : 0)
                + " ems=" + (RPHCompatibility.HospitalEmsLoaded ? 1 : 0));
            foreach (GameDBTweakableInt value in Entries<GameDBTweakableInt>())
            {
                RPHLog.Balance(BalanceSection.Config, value.DatabaseID + "=" + value.Value);
            }
            foreach (GameDBTweakableFloat value in Entries<GameDBTweakableFloat>())
            {
                RPHLog.Balance(BalanceSection.Config, value.DatabaseID + "=" + RPHLog.Num(value.Value));
            }
        }

        private static void WriteDay(DayBook book)
        {
            List<LedgerLine> lines = book.Lines.ToList();
            DailyStatistics stats = Hospital.Instance.m_state.m_dailyStatistics;
            RPHLog.Balance(BalanceSection.Day,
                "start=" + (book.StartBalance.HasValue ? book.StartBalance.Value.ToString() : "-")
                + " end=" + (book.EndBalance.HasValue ? book.EndBalance.Value.ToString() : "-")
                + " in=" + lines.Where(l => l.Group == CategoryGroup.Income).Sum(l => l.Amount)
                + " out=" + lines.Where(l => l.Group == CategoryGroup.Expense).Sum(l => l.Amount)
                + " unexplained=" + (book.ReconciliationError.HasValue ? book.ReconciliationError.Value.ToString() : "-")
                + " loan=" + Hospital.Instance.m_state.m_budget.m_loan
                + " prestige=" + RPHLog.Num(stats.m_reputation * 5f)
                + " treated=" + stats.m_treatedPatients
                + " untreated=" + stats.m_untreatedPatients
                + " dead=" + stats.m_deadPatients
                + " wrong_diagnosis=" + stats.m_incorrectlyDiagnosedPatients
                + " inpatients=" + Hospital.Instance.CountHospitalizedPatients());
            foreach (var category in lines.GroupBy(l => l.Category).OrderBy(g => g.Key))
            {
                RPHLog.Balance(BalanceSection.Money,
                    "category=" + category.Key + " amount=" + category.Sum(l => l.Amount) + " count=" + category.Sum(l => l.Count));
            }
        }

        private static IEnumerable<T> Entries<T>() where T : DatabaseEntry
        {
            T[] entries = Database.Instance.GetEntries<T>();
            return entries == null
                ? Enumerable.Empty<T>()
                : entries.Where(e => e.DatabaseID.ToString().StartsWith(TweakablePrefix, System.StringComparison.Ordinal))
                    .OrderBy(e => e.DatabaseID.ToString(), System.StringComparer.Ordinal);
        }
    }
}
