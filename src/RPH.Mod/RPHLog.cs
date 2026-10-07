using System;
using UnityEngine;

namespace RPH.Mod
{
    /// <summary>The only logger of the mod. Every line starts with [RPH] in Player.log.</summary>
    internal static class RPHLog
    {
        private const string Prefix = "[RPH] ";

        public static void Info(string message)
        {
            Debug.Log(Prefix + message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(Prefix + message);
        }

        public static void Error(string message, Exception exception)
        {
            Debug.LogError(Prefix + message + (exception != null ? ": " + exception : string.Empty));
        }

        /// <summary>Logs an exception thrown inside a Harmony patch, once per patch, so a broken patch cannot flood the log.</summary>
        public static void PatchError(string patch, Exception exception)
        {
            if (s_reported.Contains(patch))
            {
                return;
            }
            s_reported.Add(patch);
            Error("Patch " + patch + " failed (further errors from it are hidden)", exception);
        }

        private static readonly System.Collections.Generic.HashSet<string> s_reported = new System.Collections.Generic.HashSet<string>();
    }
}
