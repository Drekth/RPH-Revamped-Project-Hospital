using System.Collections.Generic;
using System.Text;
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

        // Balance log counters, per rarity, since the last midnight.
        private const int Drawn = 0;
        private const int Refused = 1;
        private const int KeptUnchecked = 2;
        private static readonly Dictionary<string, int[]> s_drawCounts = new Dictionary<string, int[]>();

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
            int gameAmbulance = company.m_immobileSpawnedToday;
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
            bool helicopter = Renown.Stars() >= Tweakables.Float(HelicopterMinStarsId);
            company.m_helicopterSpawnedIndex1 = helicopter ? Random.Range(0, company.m_immobileSpawnedToday) : -1;
            if (company.m_isContracted)
            {
                RPHLog.Balance(BalanceSection.Patients, "insurer=" + entry.DatabaseID + " walk_in=" + company.m_patientsSpawnedToday
                    + " ambulance_game=" + gameAmbulance + " ambulance=" + company.m_immobileSpawnedToday + " helicopter_allowed=" + (helicopter ? 1 : 0));
            }
        }

        /// <summary>Draws the diagnosis again while its rarity is refused at today's renown; the last allowed draw is kept unchecked.</summary>
        public static void RedrawRareDiagnosis(BehaviorPatient patient, PatientMobility mobility)
        {
            for (int draw = 0; ; draw++)
            {
                MedicalCondition condition = patient.m_state.m_medicalCondition;
                if (condition == null)
                {
                    return;
                }
                GameDBDiagnosisOccurrence occurrence = Occurrence(condition.m_gameDBMedicalCondition.Entry);
                int[] counts = DrawCounts(occurrence);
                counts[Drawn]++;
                if (draw == MaxRedraws)
                {
                    counts[KeptUnchecked]++;
                    return;
                }
                if (Random.value < KeepChance(occurrence))
                {
                    return;
                }
                counts[Refused]++;
                patient.RandomizeMedicalCondition(mobility);
            }
        }

        /// <summary>Midnight: the renown and what it gives, then the diagnoses drawn since the last midnight.</summary>
        public static void LogDay()
        {
            if (!Enabled)
            {
                return;
            }
            var line = new StringBuilder("stars=" + RPHLog.Num(Renown.Stars()))
                .Append(" walk_in=").Append(RPHLog.Num(Renown.Ramp(WalkInPrefix)))
                .Append(" ambulance=").Append(RPHLog.Num(Renown.Ramp(AmbulancePrefix)))
                .Append(" helicopter_allowed=").Append(Renown.Stars() >= Tweakables.Float(HelicopterMinStarsId) ? 1 : 0);
            foreach (GameDBDiagnosisOccurrence occurrence in Database.Instance.GetEntries<GameDBDiagnosisOccurrence>())
            {
                line.Append(' ').Append(occurrence.DatabaseID).Append('=').Append(RPHLog.Num(KeepChance(occurrence) * 100f));
            }
            RPHLog.Balance(BalanceSection.Renown, line.ToString());
            foreach (KeyValuePair<string, int[]> counts in s_drawCounts)
            {
                RPHLog.Balance(BalanceSection.Diagnosis, "occurrence=" + counts.Key + " drawn=" + counts.Value[Drawn]
                    + " refused=" + counts.Value[Refused] + " kept_unchecked=" + counts.Value[KeptUnchecked]);
            }
            s_drawCounts.Clear();
        }

        private static GameDBDiagnosisOccurrence Occurrence(GameDBMedicalCondition diagnosis)
        {
            return diagnosis != null && diagnosis.OccurrenceRef != null ? diagnosis.OccurrenceRef.Entry : null;
        }

        /// <summary>Chance to keep a diagnosis of this rarity (OCCURRENCE_UNCOMMON, OCCURRENCE_RARE...); 1 when it has no curve.</summary>
        private static float KeepChance(GameDBDiagnosisOccurrence occurrence)
        {
            if (occurrence == null)
            {
                return 1f;
            }
            string prefix = OccurrencePrefix + occurrence.DatabaseID;
            return Renown.HasRamp(prefix) ? Renown.Ramp(prefix) / 100f : 1f;
        }

        private static int[] DrawCounts(GameDBDiagnosisOccurrence occurrence)
        {
            string key = occurrence != null ? occurrence.DatabaseID.ToString() : "-";
            int[] counts;
            if (!s_drawCounts.TryGetValue(key, out counts))
            {
                counts = new int[3];
                s_drawCounts[key] = counts;
            }
            return counts;
        }
    }
}
