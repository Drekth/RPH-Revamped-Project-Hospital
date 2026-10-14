using System;
using HarmonyLib;
using Lopital;

namespace RPH.Mod.Economy
{
    /// <summary>
    /// The game's patient count modifier, from yesterday's prestige: ours follows the renown instead.
    /// Also shown by the game in the prestige tooltip.
    /// </summary>
    [HarmonyPatch(typeof(Hospital), nameof(Hospital.GetPrestigePatientsModifier), new[] { typeof(float) })]
    internal static class RenownWalkInPatch
    {
        private static void Postfix(ref int __result)
        {
            try
            {
                if (RenownPatients.Enabled)
                {
                    __result = RenownPatients.WalkInPercent();
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(RenownWalkInPatch), e);
            }
        }
    }

    /// <summary>Each insurer counts its patients for the day here: adjust those who arrive by ambulance or helicopter.</summary>
    [HarmonyPatch(typeof(InsuranceCompany), nameof(InsuranceCompany.CalculatePatientCounts))]
    internal static class RenownArrivalsPatch
    {
        private static void Postfix(InsuranceCompany __instance)
        {
            try
            {
                if (RenownPatients.Enabled && DayTime.Instance != null)
                {
                    RenownPatients.AdjustArrivals(__instance);
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(RenownArrivalsPatch), e);
            }
        }
    }

    /// <summary>A new patient got a diagnosis: draw again if its rarity does not fit the renown.</summary>
    [HarmonyPatch(typeof(BehaviorPatient), nameof(BehaviorPatient.RandomizeMedicalCondition))]
    internal static class RenownDiagnosisPatch
    {
        /// <summary>The redraw calls the patched method again: only the outer call redraws.</summary>
        private static bool s_redrawing;

        private static void Postfix(BehaviorPatient __instance, PatientMobility allowedMobility)
        {
            if (s_redrawing)
            {
                return;
            }
            s_redrawing = true;
            try
            {
                if (RenownPatients.Enabled)
                {
                    RenownPatients.RedrawRareDiagnosis(__instance, allowedMobility);
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(RenownDiagnosisPatch), e);
            }
            finally
            {
                s_redrawing = false;
            }
        }
    }
}
