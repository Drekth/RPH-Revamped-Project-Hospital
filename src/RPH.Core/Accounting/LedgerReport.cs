using System;
using System.Collections.Generic;

namespace RPH.Core.Accounting
{
    /// <summary>A group of lines summed under one key (a category, a department...).</summary>
    public sealed class LedgerTotal
    {
        public string Key;
        /// <summary>Money in (income lines).</summary>
        public long In;
        /// <summary>Money out (expense lines), negative or zero... or positive when refunds win.</summary>
        public long Out;
        public int Count;

        public long Net
        {
            get { return In + Out; }
        }
    }

    /// <summary>Read-only view of the ledger over a range of days.</summary>
    public sealed class LedgerReport
    {
        private readonly List<LedgerLine> m_lines = new List<LedgerLine>();

        public LedgerReport(Ledger ledger, int fromDay, int toDay)
        {
            FromDay = Math.Min(fromDay, toDay);
            ToDay = Math.Max(fromDay, toDay);

            var lineIndex = new Dictionary<string, LedgerLine>();
            foreach (DayBook book in ledger.Days)
            {
                if (book.Day < FromDay || book.Day > ToDay)
                {
                    continue;
                }
                DaysWithData++;
                if (book.Imported)
                {
                    HasImportedDays = true;
                }
                if (book.IsPartial)
                {
                    HasPartialDays = true;
                }
                if (book.ReconciliationError.HasValue)
                {
                    ReconciliationError += book.ReconciliationError.Value;
                }
                foreach (LedgerLine line in book.Lines)
                {
                    string key = LedgerLine.Key(line.Category, line.Department, line.Source, line.Shift);
                    LedgerLine merged;
                    if (!lineIndex.TryGetValue(key, out merged))
                    {
                        merged = new LedgerLine { Category = line.Category, Department = line.Department, Source = line.Source, Shift = line.Shift };
                        lineIndex.Add(key, merged);
                        m_lines.Add(merged);
                    }
                    merged.Amount += line.Amount;
                    merged.Count += line.Count;

                    switch (line.Group)
                    {
                        case CategoryGroup.Income:
                            MoneyIn += line.Amount;
                            break;
                        case CategoryGroup.Expense:
                            MoneyOut += line.Amount;
                            break;
                        default:
                            Financing += line.Amount;
                            break;
                    }
                }
            }
        }

        public int FromDay { get; private set; }
        public int ToDay { get; private set; }
        public int DaysWithData { get; private set; }
        public bool HasImportedDays { get; private set; }
        public bool HasPartialDays { get; private set; }
        public long ReconciliationError { get; private set; }

        /// <summary>Income, positive.</summary>
        public long MoneyIn { get; private set; }

        /// <summary>Expenses net of refunds, normally negative.</summary>
        public long MoneyOut { get; private set; }

        /// <summary>Loans taken minus loans repaid.</summary>
        public long Financing { get; private set; }

        public long Profit
        {
            get { return MoneyIn + MoneyOut; }
        }

        public IList<LedgerLine> Lines
        {
            get { return m_lines; }
        }

        /// <summary>Sums the lines that pass <paramref name="filter"/> under <paramref name="keySelector"/>. Financing is left out.</summary>
        public List<LedgerTotal> GroupBy(Func<LedgerLine, string> keySelector, Func<LedgerLine, bool> filter)
        {
            var index = new Dictionary<string, LedgerTotal>();
            var result = new List<LedgerTotal>();
            foreach (LedgerLine line in m_lines)
            {
                if (line.Group == CategoryGroup.Financing || (filter != null && !filter(line)))
                {
                    continue;
                }
                string key = keySelector(line) ?? string.Empty;
                LedgerTotal total;
                if (!index.TryGetValue(key, out total))
                {
                    total = new LedgerTotal { Key = key };
                    index.Add(key, total);
                    result.Add(total);
                }
                if (line.Group == CategoryGroup.Income)
                {
                    total.In += line.Amount;
                }
                else
                {
                    total.Out += line.Amount;
                }
                total.Count += line.Count;
            }
            return result;
        }
    }
}
