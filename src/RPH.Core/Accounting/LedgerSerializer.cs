using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace RPH.Core.Accounting
{
    /// <summary>
    /// Plain-text, tab-separated save format, written next to the game's save.
    /// <code>
    /// RPH-LEDGER  1
    /// DAY  day  imported  startBalance  startHour  endBalance  reconciliationError
    /// L    category  department  source  shift  amount  count
    /// A    kind  procedureId  department  count
    /// </code>
    /// Empty optional numbers are written as "-". Unknown enum names are kept as Unknown / skipped.
    /// </summary>
    public static class LedgerSerializer
    {
        private const string Header = "RPH-LEDGER";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Write(Ledger ledger, TextWriter writer)
        {
            writer.Write(Header);
            writer.Write('\t');
            writer.Write(Ledger.FormatVersion.ToString(Inv));
            writer.Write('\n');
            foreach (DayBook book in ledger.Days)
            {
                WriteFields(writer, "DAY",
                    book.Day.ToString(Inv),
                    book.Imported ? "1" : "0",
                    Optional(book.StartBalance),
                    book.StartHour.ToString("R", Inv),
                    Optional(book.EndBalance),
                    Optional(book.ReconciliationError));
                foreach (LedgerLine line in book.Lines)
                {
                    WriteFields(writer, "L",
                        line.Category.ToString(),
                        Clean(line.Department),
                        Clean(line.Source),
                        line.Shift.ToString(),
                        line.Amount.ToString(Inv),
                        line.Count.ToString(Inv));
                }
                foreach (ActivityLine activity in book.Activities)
                {
                    WriteFields(writer, "A",
                        activity.Kind.ToString(),
                        Clean(activity.ProcedureId),
                        Clean(activity.Department),
                        activity.Count.ToString(Inv));
                }
            }
        }

        public static string WriteToString(Ledger ledger)
        {
            var builder = new StringBuilder();
            using (var writer = new StringWriter(builder, Inv))
            {
                Write(ledger, writer);
            }
            return builder.ToString();
        }

        /// <exception cref="FormatException">The text is not a ledger, or a newer format.</exception>
        public static Ledger Read(TextReader reader)
        {
            string first = reader.ReadLine();
            if (first == null)
            {
                throw new FormatException("Empty ledger file.");
            }
            string[] header = first.Split('\t');
            int version;
            if (header.Length < 2 || header[0] != Header || !int.TryParse(header[1], NumberStyles.Integer, Inv, out version))
            {
                throw new FormatException("Not an RPH ledger file.");
            }
            if (version > Ledger.FormatVersion)
            {
                throw new FormatException("Ledger format " + version + " is newer than this mod (" + Ledger.FormatVersion + ").");
            }

            var ledger = new Ledger();
            DayBook book = null;
            string text;
            while ((text = reader.ReadLine()) != null)
            {
                if (text.Length == 0)
                {
                    continue;
                }
                string[] f = text.Split('\t');
                switch (f[0])
                {
                    case "DAY":
                        book = ledger.GetOrCreateDay(int.Parse(f[1], Inv));
                        book.Imported = f[2] == "1";
                        book.StartBalance = ParseOptional(f[3]);
                        book.StartHour = float.Parse(f[4], Inv);
                        book.EndBalance = ParseOptional(f[5]);
                        book.ReconciliationError = ParseOptional(f[6]);
                        break;
                    case "L":
                        if (book == null)
                        {
                            throw new FormatException("Line before any DAY.");
                        }
                        book.Add(ParseEnum(f[1], LedgerCategory.Unknown), f[2], f[3], ParseEnum(f[4], WorkShift.Unknown),
                            long.Parse(f[5], Inv), int.Parse(f[6], Inv));
                        break;
                    case "A":
                        if (book == null)
                        {
                            throw new FormatException("Activity before any DAY.");
                        }
                        ActivityKind kind;
                        if (TryParseEnum(f[1], out kind))
                        {
                            book.AddActivity(kind, f[2], f[3], int.Parse(f[4], Inv));
                        }
                        break;
                }
            }
            return ledger;
        }

        public static Ledger ReadFromString(string text)
        {
            using (var reader = new StringReader(text))
            {
                return Read(reader);
            }
        }

        private static void WriteFields(TextWriter writer, string tag, params string[] fields)
        {
            writer.Write(tag);
            foreach (string field in fields)
            {
                writer.Write('\t');
                writer.Write(field);
            }
            writer.Write('\n');
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            return value.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        }

        private static string Optional(long? value)
        {
            return value.HasValue ? value.Value.ToString(Inv) : "-";
        }

        private static long? ParseOptional(string value)
        {
            if (value == "-")
            {
                return null;
            }
            return long.Parse(value, Inv);
        }

        private static T ParseEnum<T>(string value, T fallback)
        {
            T result;
            return TryParseEnum(value, out result) ? result : fallback;
        }

        private static bool TryParseEnum<T>(string value, out T result)
        {
            // Enum.TryParse does not exist in .NET 3.5.
            if (Enum.IsDefined(typeof(T), value))
            {
                result = (T)Enum.Parse(typeof(T), value);
                return true;
            }
            result = default(T);
            return false;
        }
    }
}
