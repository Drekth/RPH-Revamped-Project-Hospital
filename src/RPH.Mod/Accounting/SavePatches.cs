using System;
using HarmonyLib;
using Lopital;

namespace RPH.Mod.Accounting
{
    // Manual saves and autosaves both write "<save>.xml" through GameSave.Save before zipping it,
    // and loading extracts it and reads it with GameSave.Load: the XML path names our sidecar file.

    [HarmonyPatch(typeof(GameSave), nameof(GameSave.Save))]
    internal static class GameSaveSavePatch
    {
        private static void Postfix(string path)
        {
            try
            {
                RPHLedgerService.OnGameSaved(path);
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(GameSaveSavePatch), e);
            }
        }
    }

    [HarmonyPatch(typeof(GameSave), nameof(GameSave.Load))]
    internal static class GameSaveLoadPatch
    {
        private static void Postfix(string filepath)
        {
            try
            {
                RPHLedgerService.OnGameLoading(filepath);
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(GameSaveLoadPatch), e);
            }
        }
    }
}
