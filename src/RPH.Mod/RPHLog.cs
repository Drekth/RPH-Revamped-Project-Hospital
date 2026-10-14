using System;
using System.Globalization;
using System.IO;
using System.Text;
using Lopital;
using UnityEngine;

namespace RPH.Mod
{
    /// <summary>
    /// The only logger of the mod. Every line starts with [RPH] in Player.log.
    /// Balance data goes to a separate file (see <see cref="Balance"/>).
    /// </summary>
    internal static class RPHLog
    {
        private const string Prefix = "[RPH] ";

        private static StreamWriter s_balance;
        private static bool s_balanceFailed;

        public static void Info(string message)
        {
            Debug.Log(Prefix + message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(Prefix + message);
        }

        public static void Error(string message, Exception exception)
        {
            Debug.LogError(Prefix + message + (exception != null ? ": " + exception : string.Empty));
        }

        /// <summary>Logs an exception thrown inside a Harmony patch, once per patch, so a broken patch cannot flood the log.</summary>
        public static void PatchError(string patch, Exception exception)
        {
            if (s_reported.Contains(patch))
            {
                return;
            }
            s_reported.Add(patch);
            Error("Patch " + patch + " failed (further errors from it are hidden)", exception);
        }

        private static readonly System.Collections.Generic.HashSet<string> s_reported = new System.Collections.Generic.HashSet<string>();

        /// <summary>
        /// One line of the balance log, kept to review the balancing after playing:
        /// <c>&lt;persistentDataPath&gt;/RPH/Logs/balance-yyyyMMdd-HHmmss.log</c>, one file per game launch.
        /// Tab-separated columns: real time, game day (D12), game hour (07:30), section (<see cref="BalanceSection"/>),
        /// data as space-separated key=value pairs.
        /// </summary>
        public static void Balance(string section, string data)
        {
            if (s_balanceFailed)
            {
                return;
            }
            try
            {
                if (s_balance == null)
                {
                    s_balance = OpenBalanceLog();
                }
                DayTime time = DayTime.Instance;
                string when = time != null ? "D" + time.GetDay() + "\t" + Hour(time.GetDayTimeHours()) : "-\t-";
                s_balance.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\t" + when + "\t" + section + "\t" + data);
            }
            catch (Exception e)
            {
                s_balanceFailed = true;
                Error("Balance log disabled", e);
            }
        }

        /// <summary>A number for the balance log: "1.03", never "1,03" whatever the player's language.</summary>
        public static string Num(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static StreamWriter OpenBalanceLog()
        {
            string folder = Path.Combine(Path.Combine(Application.persistentDataPath, "RPH"), "Logs");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "balance-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".log");
            var writer = new StreamWriter(path, true, new UTF8Encoding(false)) { AutoFlush = true };
            writer.WriteLine("# RPH balance log v" + RPHModEntry.Version + ". Columns: time, day, hour, section, key=value data. Sections: " + BalanceSection.All);
            Info("Balance log: " + path);
            return writer;
        }

        private static string Hour(float hours)
        {
            int minutes = (int)(hours * 60f);
            return (minutes / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (minutes % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Sections of the balance log: filter a file with e.g. <c>grep -P "\tLOANS\t"</c>.</summary>
    internal static class BalanceSection
    {
        /// <summary>A game was started or loaded, or saved: version, save, day, balance, other mods.</summary>
        public const string Session = "SESSION";
        /// <summary>At session start, one line per RPH tweakable in effect (switches and amounts).</summary>
        public const string Config = "CONFIG";
        /// <summary>A day closed: balance, money in and out, patients, prestige.</summary>
        public const string Day = "DAY";
        /// <summary>A day closed: one line per ledger category.</summary>
        public const string Money = "MONEY";
        /// <summary>Midnight: renown and the percentages it gives.</summary>
        public const string Renown = "RENOWN";
        /// <summary>An insurer counted its patients for the day.</summary>
        public const string Patients = "PATIENTS";
        /// <summary>Midnight: diagnoses drawn and refused by rarity since the last midnight.</summary>
        public const string Diagnosis = "DIAGNOSIS";
        /// <summary>Midnight: inpatient days billed, one line per hospitalization type.</summary>
        public const string Inpatient = "INPATIENT";
        /// <summary>A transport fee was paid.</summary>
        public const string Ambulance = "AMBULANCE";
        /// <summary>Midnight: loan interest, repayment, overdraft.</summary>
        public const string Loans = "LOANS";
        /// <summary>Midnight: exams billed since the last midnight.</summary>
        public const string Exams = "EXAMS";
        /// <summary>A machine broke down or was repaired.</summary>
        public const string Maintenance = "MAINTENANCE";

        public const string All = "SESSION CONFIG DAY MONEY RENOWN PATIENTS DIAGNOSIS INPATIENT AMBULANCE LOANS EXAMS MAINTENANCE";
    }
}
