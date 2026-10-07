using System;
using System.Reflection;
using HarmonyLib;
using RPH.Mod.Accounting;
using RPH.Mod.Stats;

namespace RPH.Mod
{
    /// <summary>
    /// Entry point. The game's addon loader adds every HospitalMod subclass as a component
    /// on a DontDestroyOnLoad GameObject (see TitleScreenController.LoadAddons).
    /// </summary>
    public class RPHModEntry : HospitalMod
    {
        public const string HarmonyId = "com.drekth.rph";

        public static string Version
        {
            get { return typeof(RPHModEntry).Assembly.GetName().Version.ToString(3); }
        }

        public void Awake()
        {
            RPHLog.Info("loaded v" + Version);
            ApplyPatches();
            RPHCompatibility.Check();
            gameObject.AddComponent<RPHStatsWindow>();
        }

        /// <summary>One patch class at a time: a patch broken by a game update must not disable the others.</summary>
        private static void ApplyPatches()
        {
            var harmony = new Harmony(HarmonyId);
            int applied = 0;
            int failed = 0;
            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
                {
                    continue;
                }
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    applied++;
                }
                catch (Exception e)
                {
                    failed++;
                    RPHLog.Error("Patch " + type.Name + " could not be applied", e);
                }
            }
            RPHLog.Info("Harmony: " + applied + " patches applied, " + failed + " failed");
        }

        public void Update()
        {
            RPHLedgerService.Tick();
        }
    }
}
