using System.Collections.Generic;
using GLib;
using Lopital;
using RPH.Core.Accounting;
using RPH.Mod.Accounting;

namespace RPH.Mod.Economy
{
    /// <summary>
    /// Every night, each patient in a hospital bed counts one day: the insurance pays the department a day rate,
    /// and the care (drugs, consumables, treatments) costs the hospital a day cost. Both depend on the
    /// hospitalization type and are tweakables in mod/Database/Economy/InpatientDays.xml.
    /// The game's diagnosis payment at discharge is unchanged.
    /// </summary>
    internal static class InpatientDays
    {
        private const string EnabledId = "RPH_ECONOMY_INPATIENT_DAYS_ENABLED";
        private const string PayPrefix = "RPH_ECONOMY_INPATIENT_DAY_PAY_";
        private const string CostPrefix = "RPH_ECONOMY_INPATIENT_DAY_COST_";

        /// <summary>Off when the XML switch is 0, or when PooK Hospital Economy already bills inpatient hours.</summary>
        public static bool Enabled
        {
            get
            {
                GameDBTweakableInt enabled = Database.Instance.GetEntry<GameDBTweakableInt>(EnabledId);
                return enabled != null && enabled.Value != 0 && !RPHCompatibility.PooKEconomyLoaded;
            }
        }

        /// <summary>Called once at midnight, before the game closes the day's statistics.</summary>
        public static void BillDay()
        {
            if (!Enabled)
            {
                return;
            }
            var patientsByType = new Dictionary<string, int>();
            foreach (Department department in Hospital.Instance.m_departments)
            {
                foreach (EntityIDPointer<Entity> pointer in department.m_departmentPersistentData.m_patients)
                {
                    Entity patient = pointer.GetEntity();
                    GameDBTreatment hospitalization = BedHospitalization(patient);
                    if (hospitalization != null)
                    {
                        Bill(department, patient, hospitalization);
                        string id = hospitalization.DatabaseID.ToString();
                        int count;
                        patientsByType.TryGetValue(id, out count);
                        patientsByType[id] = count + 1;
                    }
                }
            }
            foreach (KeyValuePair<string, int> type in patientsByType)
            {
                RPHLog.Balance(BalanceSection.Inpatient, "type=" + type.Key + " patients=" + type.Value
                    + " pay=" + type.Value * Value(PayPrefix + type.Key) + " cost=" + type.Value * Value(CostPrefix + type.Key));
            }
        }

        /// <summary>The hospitalization of a living patient who has a bed; null otherwise (outpatient, waiting for a bed, dead).</summary>
        private static GameDBTreatment BedHospitalization(Entity patient)
        {
            HospitalizationComponent hospitalization = patient != null ? patient.GetComponent<HospitalizationComponent>() : null;
            if (hospitalization == null
                || hospitalization.m_state.m_dead
                || hospitalization.m_state.m_hospitalizationState == HospitalizationState.Dead
                || hospitalization.m_state.m_bed == null
                || hospitalization.m_state.m_bed.GetEntity() == null
                || hospitalization.m_state.m_hospitalizationTreatment == null)
            {
                return null;
            }
            return hospitalization.m_state.m_hospitalizationTreatment.Entry;
        }

        private static void Bill(Department department, Entity patient, GameDBTreatment hospitalization)
        {
            string id = hospitalization.DatabaseID.ToString();
            int pay = Value(PayPrefix + id);
            if (pay > 0)
            {
                RPHLedgerService.PayDepartment(department, pay, PaymentCategory.INSURANCE_HOSPITALIZED, patient, LedgerCategory.InsuranceInpatientDays);
            }
            int cost = Value(CostPrefix + id);
            if (cost > 0)
            {
                // The game has no expense category for care: its Budget tab shows it with fines, like machine repairs.
                RPHLedgerService.Spend(cost, ExpenseCategory.PENALTIES, RPHGameData.DepartmentId(department), id, LedgerCategory.InpatientCare);
            }
        }

        private static int Value(string id)
        {
            GameDBTweakableInt value = Database.Instance.GetEntry<GameDBTweakableInt>(id);
            return value != null ? value.Value : 0;
        }
    }
}
