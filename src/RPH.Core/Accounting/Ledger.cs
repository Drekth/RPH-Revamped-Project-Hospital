using System.Collections.Generic;

namespace RPH.Core.Accounting
{
    /// <summary>Money moved for one (category, department, source, shift) on one day.</summary>
    public sealed class LedgerLine
    {
        public LedgerCategory Category;
        /// <summary>Game department ID (e.g. DPT_EMERGENCY), or empty for hospital-wide money.</summary>
        public string Department;
        /// <summary>What caused it: diagnosis ID, employee type... Empty when unknown.</summary>
        public string Source;
        public WorkShift Shift;
        public long Amount;
        public int Count;

        public CategoryGroup Group
        {
            get { return CategoryInfo.GetGroup(Category); }
        }

        internal static string Key(LedgerCategory category, string department, string source, WorkShift shift)
        {
            return (int)category + "|" + department + "|" + source + "|" + (int)shift;
        }
    }

    /// <summary>How many times a procedure was done in a department on one day.</summary>
    public sealed class ActivityLine
    {
        public ActivityKind Kind;
        public string ProcedureId;
        public string Department;
        public int Count;

        internal static string Key(ActivityKind kind, string procedureId, string department)
        {
            return (int)kind + "|" + procedureId + "|" + department;
        }
    }

    /// <summary>Everything recorded for one accounting day (the game's statistics day).</summary>
    public sealed class DayBook
    {
        public readonly int Day;

        /// <summary>True when the day was rebuilt from the game's daily totals (before the mod was installed).</summary>
        public bool Imported;

        /// <summary>Balance when tracking started this day; null for imported days.</summary>
        public long? StartBalance;

        /// <summary>In-game hour when tracking started (0 for a full day).</summary>
        public float StartHour;

        public long? EndBalance;

        /// <summary>End balance minus what the lines explain. 0 means every dollar is accounted for.</summary>
        public long? ReconciliationError;

        internal readonly Dictionary<string, LedgerLine> LineIndex = new Dictionary<string, LedgerLine>();
        internal readonly Dictionary<string, ActivityLine> ActivityIndex = new Dictionary<string, ActivityLine>();

        public DayBook(int day)
        {
            Day = day;
        }

        public IEnumerable<LedgerLine> Lines
        {
            get { return LineIndex.Values; }
        }

        public IEnumerable<ActivityLine> Activities
        {
            get { return ActivityIndex.Values; }
        }

        public bool IsPartial
        {
            get { return !Imported && StartHour > 0f; }
        }

        public void Add(LedgerCategory category, string department, string source, WorkShift shift, long amount, int count)
        {
            department = department ?? string.Empty;
            source = source ?? string.Empty;
            string key = LedgerLine.Key(category, department, source, shift);
            LedgerLine line;
            if (!LineIndex.TryGetValue(key, out line))
            {
                line = new LedgerLine { Category = category, Department = department, Source = source, Shift = shift };
                LineIndex.Add(key, line);
            }
            line.Amount += amount;
            line.Count += count;
        }

        public void AddActivity(ActivityKind kind, string procedureId, string department, int count)
        {
            procedureId = procedureId ?? string.Empty;
            department = department ?? string.Empty;
            string key = ActivityLine.Key(kind, procedureId, department);
            ActivityLine line;
            if (!ActivityIndex.TryGetValue(key, out line))
            {
                line = new ActivityLine { Kind = kind, ProcedureId = procedureId, Department = department };
                ActivityIndex.Add(key, line);
            }
            line.Count += count;
        }

        /// <summary>Sum of every line, financing included: how much the balance moved.</summary>
        public long BalanceChange()
        {
            long total = 0;
            foreach (LedgerLine line in LineIndex.Values)
            {
                total += line.Amount;
            }
            return total;
        }

        /// <summary>Records the closing balance and checks that the lines explain it.</summary>
        public void Close(long endBalance)
        {
            EndBalance = endBalance;
            if (StartBalance.HasValue)
            {
                ReconciliationError = endBalance - (StartBalance.Value + BalanceChange());
            }
        }
    }

    /// <summary>The hospital's money history, one book per accounting day.</summary>
    public sealed class Ledger
    {
        public const int FormatVersion = 1;

        private readonly SortedDictionary<int, DayBook> m_days = new SortedDictionary<int, DayBook>();

        public IEnumerable<DayBook> Days
        {
            get { return m_days.Values; }
        }

        public int DayCount
        {
            get { return m_days.Count; }
        }

        public bool HasDay(int day)
        {
            return m_days.ContainsKey(day);
        }

        public DayBook GetDay(int day)
        {
            DayBook book;
            return m_days.TryGetValue(day, out book) ? book : null;
        }

        public DayBook GetOrCreateDay(int day)
        {
            DayBook book;
            if (!m_days.TryGetValue(day, out book))
            {
                book = new DayBook(day);
                m_days.Add(day, book);
            }
            return book;
        }

        /// <summary>Drops days after <paramref name="day"/>: used when a game is loaded at an earlier day than the ledger.</summary>
        public void TruncateAfter(int day)
        {
            List<int> remove = new List<int>();
            foreach (int key in m_days.Keys)
            {
                if (key > day)
                {
                    remove.Add(key);
                }
            }
            foreach (int key in remove)
            {
                m_days.Remove(key);
            }
        }
    }
}
