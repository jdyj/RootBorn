using System;
using UnityEngine;

namespace Rootborn.Game.Common
{
    public static class DirectValidationTrace
    {
        private const string Flag = "-directValidationTrace";
        private static bool? s_enabled;

        public static bool Enabled
        {
            get
            {
                if (!s_enabled.HasValue)
                {
                    s_enabled = HasFlag();
                }

                return s_enabled.Value;
            }
        }

        public static void Log(string message)
        {
            if (Enabled)
            {
                Debug.Log("[ROOTBORN/DIRECT-TRACE] " + message);
            }
        }

        private static bool HasFlag()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}