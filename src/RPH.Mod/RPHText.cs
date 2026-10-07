namespace RPH.Mod
{
    /// <summary>Localized strings from mod/Database/ModStringTable*.xml.</summary>
    internal static class RPHText
    {
        public static string Get(string key, params string[] parameters)
        {
            return StringTable.GetInstance().GetLocalizedText(key, parameters);
        }
    }
}
