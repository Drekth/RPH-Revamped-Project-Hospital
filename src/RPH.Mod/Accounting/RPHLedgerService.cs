using System;
using System.IO;
using System.Text;
using GLib;
using Lopital;
using RPH.Core.Accounting;
using UnityEngine;

namespace RPH.Mod.Accounting
{
    /// <summary>
    /// Owns the ledger of the game being played: follows game sessions (new game, load),
    /// imports the days played before the mod was installed, and saves the ledger next to the save.
    /// </summary>
    internal static class RPHLedgerService
    {
        private const string SidecarSuffix = ".rph.txt";

        private static LedgerRecorder s_recorder = new LedgerRecorder(new Ledger());
        private static object s_session;
        private static bool s_ready;
        private static string s_pendingSidecar;

        public static Ledger Ledger
        {
            get { return s_recorder.Ledger; }
        }

        /// <summary>
        /// The recorder of the current session. Sessions only switch in <see cref="Tick"/>: a save is loaded
        /// in one synchronous call, so money moved during the load lands in the old ledger, which is then dropped.
        /// </summary>
        public static LedgerRecorder Recorder
        {
            get { return s_recorder; }
        }

        public static bool IsReady
        {
            get { return s_ready; }
        }

        public static int CurrentDay
        {
            get { return s_recorder.CurrentDay; }
        }

        /// <summary>Raised when the ledger changes session (load, new game) so open windows can rebuild.</summary>
        public static event Action SessionChanged;

        public static bool GameRunning
        {
            get
            {
                return DayTime.Instance != null
                    && Hospital.Instance != null
                    && Hospital.Instance.m_state != null
                    && Hospital.Instance.m_departments != null
                    && Hospital.Instance.m_departments.Count > 0;
            }
        }

        public static long Balance
        {
            get { return GameRunning ? Hospital.Instance.m_state.m_budget.m_currentBalance : 0; }
        }

        public static WorkShift CurrentShift
        {
            get
            {
                if (DayTime.Instance == null)
                {
                    return WorkShift.Unknown;
                }
                return DayTime.Instance.GetShift() == Shift.NIGHT ? WorkShift.Night : WorkShift.Day;
            }
        }

        public static void Tick()
        {
            try
            {
                EnsureSession();
            }
            catch (Exception e)
            {
                RPHLog.PatchError("LedgerService.Tick", e);
            }
        }

        /// <summary>Called when the game reads a save: its ledger (if any) is next to it.</summary>
        public static void OnGameLoading(string saveXmlPath)
        {
            s_pendingSidecar = SidecarPathFor(saveXmlPath);
            s_ready = false;
        }

        /// <summary>Called after the game wrote a save.</summary>
        public static void OnGameSaved(string saveXmlPath)
        {
            if (!s_ready || string.IsNullOrEmpty(saveXmlPath))
            {
                return;
            }
            // Scenarios created in the editor live under StreamingAssets: they start a new game, no history.
            if (saveXmlPath.StartsWith(Application.streamingAssetsPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            string path = SidecarPathFor(saveXmlPath);
            try
            {
                File.WriteAllText(path, LedgerSerializer.WriteToString(s_recorder.Ledger), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                RPHLog.Error("Could not write the ledger to " + path, e);
            }
            LedgerBalanceLog.Saved(Path.GetFileNameWithoutExtension(saveXmlPath));
        }

        public static void OnDayEnding()
        {
            EnsureSession();
            if (!s_ready)
            {
                return;
            }
            s_recorder.CloseDay(Balance);
            DayBook book = s_recorder.Ledger.GetDay(s_recorder.CurrentDay);
            if (book != null && book.ReconciliationError.HasValue && book.ReconciliationError.Value != 0)
            {
                RPHLog.Warn("Day " + book.Day + ": " + book.ReconciliationError.Value + " $ of balance change not explained by the ledger.");
            }
            if (book != null)
            {
                LedgerBalanceLog.Day(book);
            }
        }

        public static void OnDayStarted()
        {
            if (!s_ready)
            {
                return;
            }
            s_recorder.StartDay(DayTime.Instance.GetDay(), Balance, 0f);
        }

        /// <summary>Department.Pay (the game's department stats count it), booked under our own category.</summary>
        public static void PayDepartment(Department department, int amount, PaymentCategory category, Entity patient, LedgerCategory ledgerCategory)
        {
            DepartmentPayPatch.CategoryOverride = ledgerCategory;
            try
            {
                department.Pay(amount, category, patient);
            }
            finally
            {
                DepartmentPayPatch.CategoryOverride = null;
            }
        }

        /// <summary>Hospital.Spend, booked under a department, a source and our own category.</summary>
        public static void Spend(int amount, ExpenseCategory category, string department, string source, LedgerCategory ledgerCategory)
        {
            s_recorder.BeginScope(department, source);
            try
            {
                Hospital.Instance.Spend(amount, category);
            }
            finally
            {
                s_recorder.EndScope(CurrentShift, ledgerCategory);
            }
        }

        private static string SidecarPathFor(string saveXmlPath)
        {
            string withoutExtension = saveXmlPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                ? saveXmlPath.Substring(0, saveXmlPath.Length - 4)
                : saveXmlPath;
            return withoutExtension + SidecarSuffix;
        }

        /// <summary>
        /// Starts a fresh ledger when the game state object changes (new game) or a save was loaded.
        /// Cheap when nothing changed: two reference comparisons.
        /// </summary>
        private static void EnsureSession()
        {
            if (!GameRunning)
            {
                return;
            }
            object session = Hospital.Instance.m_state;
            if (s_ready && ReferenceEquals(session, s_session) && s_pendingSidecar == null)
            {
                return;
            }
            s_session = session;
            string save = s_pendingSidecar != null ? Path.GetFileName(s_pendingSidecar).Replace(SidecarSuffix, string.Empty) : null;
            Ledger ledger = LoadSidecar(s_pendingSidecar) ?? new Ledger();
            s_pendingSidecar = null;

            int today = DayTime.Instance.GetDay();
            // A save older than the ledger (same name, loaded again): forget the days that did not happen.
            ledger.TruncateAfter(today);
            int imported = ImportVanillaDays(ledger, today);

            s_recorder = new LedgerRecorder(ledger);
            s_recorder.StartDay(today, Balance, DayTime.Instance.GetDayTimeHours());
            s_ready = true;
            RPHLog.Info("Ledger ready: day " + today + ", " + ledger.DayCount + " days, " + imported + " imported from the game's totals.");
            if (SessionChanged != null)
            {
                SessionChanged();
            }
            LedgerBalanceLog.Session(save);
        }

        private static Ledger LoadSidecar(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }
            try
            {
                using (var reader = new StreamReader(path, Encoding.UTF8))
                {
                    return LedgerSerializer.Read(reader);
                }
            }
            catch (Exception e)
            {
                RPHLog.Error("Could not read the ledger " + path + ", starting a new one", e);
                return null;
            }
        }

        /// <summary>Rebuilds the finished days the ledger does not have from the game's daily totals.</summary>
        private static int ImportVanillaDays(Ledger ledger, int today)
        {
            var history = Hospital.Instance.m_state.m_statisticsHistory;
            int count = 0;
            for (int day = 1; day < today; day++)
            {
                if (ledger.HasDay(day) || history == null || history.Count < day)
                {
                    continue;
                }
                DayBook book = ledger.GetOrCreateDay(day);
                book.Imported = true;

                DailyStatistics h = history[day - 1];
                Import(book, LedgerCategory.InsuranceObjectives, string.Empty, h.m_incomeObjectives);
                Import(book, LedgerCategory.EventIncome, string.Empty, h.m_incomeEvents);
                Import(book, LedgerCategory.Building, string.Empty, -h.m_expensesBuilding);
                Import(book, LedgerCategory.Ambulances, string.Empty, -h.m_expensesAmbulance);
                Import(book, LedgerCategory.LoanInterest, string.Empty, -h.m_expensesInterest);
                // The game books hiring agency fees as penalties: they cannot be told apart in old days.
                Import(book, LedgerCategory.Penalties, string.Empty, -h.m_expensesPenalties);
                Import(book, LedgerCategory.EventExpense, string.Empty, -h.m_expensesEvents);
                Import(book, LedgerCategory.Training, string.Empty, -h.m_expensesTraining);

                foreach (Department department in Hospital.Instance.m_departments)
                {
                    string id = RPHGameData.DepartmentId(department);
                    // Wages come back negative from GetComputedStatistics.
                    DailyStatistics d = department.GetComputedStatistics(day);
                    Import(book, LedgerCategory.InsuranceClinic, id, d.m_incomeClinic);
                    Import(book, LedgerCategory.InsuranceHospitalization, id, d.m_incomeHospitalization);
                    Import(book, LedgerCategory.Food, id, d.m_incomeFood);
                    Import(book, LedgerCategory.WagesClinic, id, d.m_wagesClinic);
                    Import(book, LedgerCategory.WagesHospitalization, id, d.m_wagesHospitalization);
                }
                count++;
            }
            return count;
        }

        private static void Import(DayBook book, LedgerCategory category, string department, long amount)
        {
            if (amount != 0)
            {
                book.Add(category, department, string.Empty, WorkShift.Unknown, amount, 0);
            }
        }
    }
}
