using System;
using GLib;
using HarmonyLib;
using Lopital;

namespace RPH.Mod.Cleaning
{
    /// <summary>
    /// The janitor's choice of the next room (private BehaviorJanitor.TryToSelectTileInARoom) calls two dirt searches that
    /// know the janitor's department but not the janitor: say whose choice is running, so the searches can use his floor.
    /// </summary>
    [HarmonyPatch(typeof(BehaviorJanitor), "TryToSelectTileInARoom")]
    internal static class JanitorChoosingPatch
    {
        private static void Prefix(BehaviorJanitor __instance)
        {
            try
            {
                JanitorZones.BeginChoice(__instance);
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(JanitorChoosingPatch), e);
            }
        }

        // A finalizer runs even if the game throws: the choice must never leak to other callers of the searches.
        private static Exception Finalizer(Exception __exception)
        {
            JanitorZones.Choosing = null;
            return __exception;
        }
    }

    /// <summary>First search of the choice: rooms assigned to the janitor by hand, else rooms nobody is assigned to.</summary>
    [HarmonyPatch(typeof(MapScriptInterface), nameof(MapScriptInterface.FindDirtiestTileInRoomWithMatchingAssignmentAnyFloor))]
    internal static class DirtiestAssignedTilePatch
    {
        private static bool Prefix(ref Vector3i __result, int threshold)
        {
            try
            {
                if (JanitorZones.Choosing != null)
                {
                    __result = JanitorZones.FindDirtiest(JanitorZones.Choosing, true, threshold);
                    return false;
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(DirtiestAssignedTilePatch), e);
            }
            return true;
        }
    }

    /// <summary>Second search of the choice: any room that nobody reserved.</summary>
    [HarmonyPatch(typeof(MapScriptInterface), nameof(MapScriptInterface.FindDirtiestTileInAnyUnreservedRoomAnyFloor))]
    internal static class DirtiestAnyTilePatch
    {
        private static bool Prefix(ref Vector3i __result, int threshold)
        {
            try
            {
                if (JanitorZones.Choosing != null)
                {
                    __result = JanitorZones.FindDirtiest(JanitorZones.Choosing, false, threshold);
                    return false;
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(DirtiestAnyTilePatch), e);
            }
            return true;
        }
    }

    /// <summary>A janitor on his way to a room where a procedure has started waits in front of it instead of walking in.</summary>
    [HarmonyPatch(typeof(BehaviorJanitor), nameof(BehaviorJanitor.Update))]
    internal static class JanitorWaitPatch
    {
        private static bool Prefix(BehaviorJanitor __instance, float deltaTime)
        {
            try
            {
                return !JanitorZones.Enabled || JanitorZones.BeforeUpdate(__instance, deltaTime);
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(JanitorWaitPatch), e);
                return true;
            }
        }
    }
}
