using GLib;
using Lopital;
using UnityEngine;

namespace RPH.Mod.Economy
{
    /// <summary>
    /// Patients follow the hospital's renown (see <see cref="Renown"/>): a little-known hospital gets few patients,
    /// almost only common diseases, few ambulance cases and no helicopter. All curves are tweakables in
    /// mod/Database/Economy/RenownPatients.xml.
    /// </summary>
    internal static class RenownPatients
    {
        private const string EnabledId = "RPH_ECONOMY_RENOWN_PATIENTS_ENABLED";
        private const string WalkInPrefix = "RPH_ECONOMY_RENOWN_WALK_IN";
        private const string AmbulancePrefix = "RPH_ECONOMY_RENOWN_AMBULANCE";
        private const string HelicopterMinStarsId = "RPH_ECONOMY_RENOWN_HELICOPTER_MIN_STARS";
        private const string OccurrencePrefix = "RPH_ECONOMY_RENOWN_";

        /// <summary>Enough draws to make an unwanted rarity unlikely; the last draw is kept anyway.</summary>
        private const int MaxRedraws = 10;

        public static bool Enabled
        {
            get { return Tweakables.Int(EnabledId) != 0; }
        }

        /// <summary>Walk-in patients, in % of what the insurance contracts send. Replaces the game's prestige modifier.</summary>
        public static int WalkInPercent()
        {
            return (int)Renown.Ramp(WalkInPrefix);
        }

        /// <summary>
        /// After the game counted today's patients of one insurer: patients brought by ambulance follow their own,
        /// steeper curve, and the helicopter only flies for a renowned hospital.
        /// </summary>
        public static void AdjustArrivals(InsuranceCompany company)
        {
            GameDBInsuranceCompany entry = company.m_gameDBInsuranceCompany.Entry;
            // Same planned count as the game (InsuranceCompany.CalculatePatientCounts), with our percentage.
            // Scenarios that grow patients every day (ImmobilePerDayIncrease) keep the game's numbers.
            if (company.m_isContracted
                && entry.ImmobilePerDayIncrease == 0
                && Hospital.Instance.GetRandomDepartmentWithDiagnoses(PatientMobility.IMOBILE) != null)
            {
                int planned = company.m_currentImmobilePatients > 0
                    ? company.m_currentImmobilePatients
                    : entry.ImmobilePerDay + (company.m_unlockedExtraImmobilePatients ? entry.UnlockedExtraImmobilePerDay : 0);
                float percent = Renown.Ramp(AmbulancePrefix) + WorldEventManager.Instance.GetIncomingPatientModifier();
                company.m_immobileSpawnedToday = Mathf.Max(0, (int)(planned * percent / 100f));
            }
            // The game turns one of today's ambulance patients into a helicopter patient by index.
            company.m_helicopterSpawnedIndex1 = Renown.Stars() >= Tweakables.Float(HelicopterMinStarsId)
                ? Random.Range(0, company.m_immobileSpawnedToday)
                : -1;
        }

        /// <summary>Draws the diagnosis again while its rarity is refused at today's renown.</summary>
        public static void RedrawRareDiagnosis(BehaviorPatient patient, PatientMobility mobility)
        {
            for (int i = 0; i < MaxRedraws; i++)
            {
                MedicalCondition condition = patient.m_state.m_medicalCondition;
                if (condition == null || Random.value < KeepChance(condition.m_gameDBMedicalCondition.Entry))
                {
                    return;
                }
                patient.RandomizeMedicalCondition(mobility);
            }
        }

        /// <summary>Chance to keep a diagnosis of this rarity (OCCURRENCE_UNCOMMON, OCCURRENCE_RARE...); 1 when it has no curve.</summary>
        private static float KeepChance(GameDBMedicalCondition diagnosis)
        {
            GameDBDiagnosisOccurrence occurrence = diagnosis != null && diagnosis.OccurrenceRef != null ? diagnosis.OccurrenceRef.Entry : null;
            if (occurrence == null)
            {
                return 1f;
            }
            string prefix = OccurrencePrefix + occurrence.DatabaseID;
            return Renown.HasRamp(prefix) ? Renown.Ramp(prefix) / 100f : 1f;
        }
    }
}
