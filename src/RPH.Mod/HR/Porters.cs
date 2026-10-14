using GLib;
using Lopital;

namespace RPH.Mod.HR
{
    /// <summary>Settings shared by the porters' code (mod/Database/HR/Porters.xml).</summary>
    internal static class Porters
    {
        public const string RoomTypeId = "ROOM_TYPE_RPH_PORTERS";
        public const string RoomTag = "porter_workspace";

        private const string EnabledId = "RPH_HR_PORTERS_ENABLED";

        public static bool Enabled
        {
            get
            {
                GameDBTweakableInt enabled = Database.Instance.GetEntry<GameDBTweakableInt>(EnabledId);
                return enabled != null && enabled.Value != 0 && !RPHCompatibility.PortersModLoaded;
            }
        }

        public static float Float(string id)
        {
            GameDBTweakableFloat value = Database.Instance.GetEntry<GameDBTweakableFloat>(id);
            return value != null ? value.Value : 0f;
        }

        public static int Int(string id)
        {
            GameDBTweakableInt value = Database.Instance.GetEntry<GameDBTweakableInt>(id);
            return value != null ? value.Value : 0;
        }
    }
}
