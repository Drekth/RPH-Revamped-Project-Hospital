using System.Collections.Generic;

namespace RPH.Core.Accounting
{
    /// <summary>
    /// Turns balance changes seen by the game hooks into ledger lines.
    /// A scope adds context (department, source) that the low-level hook does not know:
    /// for example Department.Pay opens a scope, Hospital.Pay inside it reports the amount,
    /// and the scope books it under the department when it closes.
    /// </summary>
    public sealed class LedgerRecorder
    {
        private sealed class Scope
        {
            public string Department;
            public string Source;
            public LedgerCategory Category;
            public long Amount;
            public int Changes;
        }

        private readonly Stack<Scope> m_scopes = new Stack<Scope>();

        public LedgerRecorder(Ledger ledger)
        {
            Ledger = ledger;
        }

        public Ledger Ledger { get; private set; }

        /// <summary>The accounting day lines are booked to. Follows the game's statistics day, not the clock.</summary>
        public int CurrentDay { get; private set; }

        /// <summary>Opens (or resumes) the book for <paramref name="day"/>.</summary>
        public void StartDay(int day, long balance, float hour)
        {
            m_scopes.Clear();
            CurrentDay = day;
            DayBook book = Ledger.GetOrCreateDay(day);
            if (!book.StartBalance.HasValue && !book.Imported)
            {
                book.StartBalance = balance;
                book.StartHour = hour;
            }
        }

        /// <summary>Closes the current day with the balance at the moment the game switches statistics.</summary>
        public void CloseDay(long balance)
        {
            DayBook book = Ledger.GetDay(CurrentDay);
            if (book != null && !book.Imported)
            {
                book.Close(balance);
            }
        }

        public void BeginScope(string department, string source)
        {
            m_scopes.Push(new Scope { Department = department, Source = source });
        }

        /// <summary>
        /// Closes the innermost scope and books what happened inside it.
        /// <paramref name="categoryOverride"/> replaces the category reported by the hook (e.g. clinic vs ward wages).
        /// </summary>
        public void EndScope(WorkShift shift, LedgerCategory? categoryOverride)
        {
            if (m_scopes.Count == 0)
            {
                return;
            }
            Scope scope = m_scopes.Pop();
            if (scope.Changes == 0)
            {
                return;
            }
            LedgerCategory category = categoryOverride.HasValue ? categoryOverride.Value : scope.Category;
            Book(category, scope.Department, scope.Source, shift, scope.Amount, 1);
        }

        /// <summary>Called by the balance hooks with the signed change they observed.</summary>
        public void OnBalanceChanged(long delta, LedgerCategory category, WorkShift shift)
        {
            if (delta == 0)
            {
                return;
            }
            if (m_scopes.Count > 0)
            {
                Scope scope = m_scopes.Peek();
                scope.Amount += delta;
                scope.Category = category;
                scope.Changes++;
                return;
            }
            Book(category, string.Empty, string.Empty, shift, delta, 1);
        }

        public void Book(LedgerCategory category, string department, string source, WorkShift shift, long amount, int count)
        {
            Ledger.GetOrCreateDay(CurrentDay).Add(category, department, source, shift, amount, count);
        }

        public void RecordActivity(ActivityKind kind, string procedureId, string department)
        {
            Ledger.GetOrCreateDay(CurrentDay).AddActivity(kind, procedureId, department, 1);
        }
    }
}
