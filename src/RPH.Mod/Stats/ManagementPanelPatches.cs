using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace RPH.Mod.Stats
{
    /// <summary>Adds an "advanced statistics" button after the tabs of the hospital management panel, cloned from the game's statistics tab button.</summary>
    [HarmonyPatch(typeof(HospitalManagementPanelController), nameof(HospitalManagementPanelController.Start))]
    internal static class ManagementPanelStartPatch
    {
        // Icon of the game's statistics tab (inactive state), see HospitalManagementPanelController.SetTab.
        private const int StatisticsIcon = 230;

        private static void Postfix(HospitalManagementPanelController __instance)
        {
            try
            {
                GameObject[] tabs =
                {
                    __instance.m_tabButtonDepartments, __instance.m_tabButtonInsurance, __instance.m_tabButtonStatistics,
                    __instance.m_tabButtonBudget, __instance.m_tabButtonAmbulances,
                };
                RectTransform[] sorted = tabs.Select(t => (RectTransform)t.transform).OrderBy(t => t.anchoredPosition.x).ToArray();
                float spacing = sorted[1].anchoredPosition.x - sorted[0].anchoredPosition.x;
                RectTransform last = sorted[sorted.Length - 1];

                GameObject button = UnityEngine.Object.Instantiate(__instance.m_tabButtonStatistics, last.parent, false);
                button.name = "RPHStatsTabButton";
                ((RectTransform)button.transform).anchoredPosition = last.anchoredPosition + new Vector2(spacing, 0f);
                IconButtonController controller = button.GetComponent<IconButtonController>();
                controller.RemoveOnClickDelegate();
                controller.SetIcon(StatisticsIcon);
                controller.m_toolTipTextLocalizationID = "RPH_STATS_TOOLTIP";
                controller.SetOnClickedDelegate(() =>
                {
                    if (RPHStatsWindow.Instance != null)
                    {
                        RPHStatsWindow.Instance.Open();
                    }
                });
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(ManagementPanelStartPatch), e);
            }
        }
    }
}
