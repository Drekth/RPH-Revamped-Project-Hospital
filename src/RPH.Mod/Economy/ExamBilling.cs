using GLib;
using Lopital;

namespace RPH.Mod.Economy
{
    /// <summary>
    /// One act = one price: the insurance pays the performing department for every priced exam.
    /// Prices and the on/off switch are tweakables in mod/Database/Economy/ExamPrices.xml.
    /// </summary>
    internal static class ExamBilling
    {
        private const string EnabledId = "RPH_ECONOMY_EXAM_BILLING_ENABLED";
        private const string PricePrefix = "RPH_ECONOMY_EXAM_PRICE_";

        // Balance log counters since the last midnight.
        private static int s_billedToday;
        private static int s_amountToday;

        /// <summary>Off when the XML switch is 0, or when PooK Hospital Economy prices the same exams.</summary>
        public static bool Enabled
        {
            get
            {
                GameDBTweakableInt enabled = Database.Instance.GetEntry<GameDBTweakableInt>(EnabledId);
                return enabled != null && enabled.Value != 0 && !RPHCompatibility.PooKEconomyLoaded;
            }
        }

        public static void Bill(GameDBExamination examination, Entity patient, Department department)
        {
            if (examination == null || patient == null || department == null || !Enabled)
            {
                return;
            }
            GameDBTweakableInt price = Database.Instance.GetEntry<GameDBTweakableInt>(PricePrefix + examination.DatabaseID);
            if (price == null || price.Value <= 0)
            {
                return;
            }
            HospitalizationComponent hospitalization = patient.GetComponent<HospitalizationComponent>();
            PaymentCategory category = hospitalization != null && hospitalization.IsHospitalized()
                ? PaymentCategory.INSURANCE_HOSPITALIZED
                : PaymentCategory.INSURANCE_CLINIC;
            department.Pay(price.Value, category, patient);
            s_billedToday++;
            s_amountToday += price.Value;
        }

        /// <summary>Midnight: exams billed since the last midnight.</summary>
        public static void LogDay()
        {
            if (Enabled)
            {
                RPHLog.Balance(BalanceSection.Exams, "billed=" + s_billedToday + " amount=" + s_amountToday);
            }
            s_billedToday = 0;
            s_amountToday = 0;
        }
    }
}
