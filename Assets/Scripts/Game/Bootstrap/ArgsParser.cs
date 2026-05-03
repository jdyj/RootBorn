using System;

namespace Rootborn.Game.Bootstrap
{
    public static class ArgsParser
    {
        public static AppConfig Parse(string[] args)
        {
            var config = new AppConfig();
            if (args == null) return config;

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "-mode":
                        if (TryNext(args, i, out var modeStr))
                            config.Mode = ParseMode(modeStr);
                        i++;
                        break;
                    case "-port":
                        if (TryNext(args, i, out var portStr) && ushort.TryParse(portStr, out var port))
                            config.Port = port;
                        i++;
                        break;
                    case "-maxPlayers":
                        if (TryNext(args, i, out var maxStr) && int.TryParse(maxStr, out var max))
                            config.MaxPlayers = max;
                        i++;
                        break;
                    case "-saveSlot":
                        if (TryNext(args, i, out var slot))
                            config.SaveSlot = slot;
                        i++;
                        break;
                    case "-joinIp":
                        if (TryNext(args, i, out var ip))
                            config.JoinIp = ip;
                        i++;
                        break;
                }
            }
            return config;
        }

        private static bool TryNext(string[] args, int i, out string value)
        {
            if (i + 1 < args.Length)
            {
                value = args[i + 1];
                return true;
            }
            value = string.Empty;
            return false;
        }

        private static SessionMode ParseMode(string s)
        {
            if (string.IsNullOrEmpty(s)) return SessionMode.None;
            return s.ToLowerInvariant() switch
            {
                "single" => SessionMode.Single,
                "host" => SessionMode.Host,
                "client" => SessionMode.Client,
                "server" => SessionMode.Server,
                _ => SessionMode.None
            };
        }
    }
}
