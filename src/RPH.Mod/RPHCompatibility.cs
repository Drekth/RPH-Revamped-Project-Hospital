using System;

namespace RPH.Mod
{
    /// <summary>Detects other code mods that touch the same systems.</summary>
    internal static class RPHCompatibility
    {
        public static bool PooKEconomyLoaded { get; private set; }
        public static bool HospitalEmsLoaded { get; private set; }
        public static bool ShiftHandoverModLoaded { get; private set; }

        public static void Check()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (name.StartsWith("PooK", StringComparison.OrdinalIgnoreCase))
                {
                    PooKEconomyLoaded = true;
                }
                else if (name.IndexOf("EMS", StringComparison.Ordinal) >= 0 && name.IndexOf("Hospital", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    HospitalEmsLoaded = true;
                }
                else if (name.IndexOf("Handover", StringComparison.OrdinalIgnoreCase) >= 0 && !name.StartsWith("RPH", StringComparison.Ordinal))
                {
                    ShiftHandoverModLoaded = true;
                }
            }
            if (PooKEconomyLoaded)
            {
                RPHLog.Warn("PooK Hospital Economy detected: its own costs and payments are recorded like the game's. Statistics stay correct; RPH exam billing, inpatient days and ambulance fees are off so nothing is paid twice.");
            }
            if (HospitalEmsLoaded)
            {
                RPHLog.Info("Hospital EMS detected.");
            }
            if (ShiftHandoverModLoaded)
            {
                RPHLog.Warn("A shift handover mod is loaded: RPH shift handover is off.");
            }
        }
    }
}
