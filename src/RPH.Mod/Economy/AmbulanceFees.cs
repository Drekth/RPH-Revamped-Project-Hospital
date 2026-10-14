using GLib;
using Lopital;
using RPH.Core.Accounting;
using RPH.Mod.Accounting;

namespace RPH.Mod.Economy
{
    /// <summary>
    /// The insurance pays a transport fee to the department of a patient brought in by the hospital's own ambulance
    /// or helicopter. City ambulances do not: the hospital did not do the transport.
    /// Fees and the on/off switch are tweakables in mod/Database/Economy/AmbulanceFees.xml.
    /// </summary>
    internal static class AmbulanceFees
    {
        private const string EnabledId = "RPH_ECONOMY_AMBULANCE_FEES_ENABLED";
        private const string AmbulanceFeeId = "RPH_ECONOMY_AMBULANCE_FEE";
        private const string HelicopterFeeId = "RPH_ECONOMY_HELICOPTER_FEE";

        /// <summary>Off when the XML switch is 0, or when PooK Hospital Economy already pays ambulance arrivals.</summary>
        public static bool Enabled
        {
            get
            {
                GameDBTweakableInt enabled = Database.Instance.GetEntry<GameDBTweakableInt>(EnabledId);
                return enabled != null && enabled.Value != 0 && !RPHCompatibility.PooKEconomyLoaded;
            }
        }

        public static void Bill(Ambulance vehicle, Entity patient)
        {
            if (vehicle.m_state.m_external || patient == null || !Enabled)
            {
                return;
            }
            GameDBTweakableInt fee = Database.Instance.GetEntry<GameDBTweakableInt>(vehicle.m_state.m_isHelicopter ? HelicopterFeeId : AmbulanceFeeId);
            BehaviorPatient behavior = patient.GetComponent<BehaviorPatient>();
            Department department = behavior != null ? behavior.GetDepartment() : null;
            if (fee == null || fee.Value <= 0 || department == null)
            {
                return;
            }
            RPHLedgerService.PayDepartment(department, fee.Value, PaymentCategory.INSURANCE_HOSPITALIZED, patient, LedgerCategory.InsuranceTransport);
            RPHLog.Balance(BalanceSection.Ambulance, "vehicle=" + (vehicle.m_state.m_isHelicopter ? "helicopter" : "ambulance")
                + " department=" + RPHGameData.DepartmentId(department) + " diagnosis=" + RPHGameData.PatientDiagnosisId(patient) + " fee=" + fee.Value);
        }
    }
}
