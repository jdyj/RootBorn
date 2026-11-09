namespace Rootborn.Core.Network
{
    public enum JoinAddressError
    {
        None,
        MissingHost,
        InvalidHost,
        InvalidPort,
    }

    /// <summary>
    /// A server address typed by a player. Unity Transport connects to an IP endpoint, so only dotted IPv4 is accepted:
    /// a host name or a malformed address would otherwise fail only after the whole connect timeout.
    /// </summary>
    public readonly struct JoinAddress
    {
        public JoinAddress(string host, ushort port)
        {
            Host = host;
            Port = port;
        }

        public string Host { get; }
        public ushort Port { get; }

        public static JoinAddressError TryParse(string host, string port, out JoinAddress address)
        {
            address = default;
            string trimmedHost = host == null ? string.Empty : host.Trim();
            if (trimmedHost.Length == 0) return JoinAddressError.MissingHost;
            if (!IsIpv4(trimmedHost)) return JoinAddressError.InvalidHost;
            if (!TryParsePort(port, out ushort value)) return JoinAddressError.InvalidPort;
            address = new JoinAddress(trimmedHost, value);
            return JoinAddressError.None;
        }

        private static bool IsIpv4(string text)
        {
            int parts = 0, value = 0, digits = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                if (i == text.Length || text[i] == '.')
                {
                    if (digits == 0 || value > 255) return false;
                    parts++;
                    value = 0;
                    digits = 0;
                    continue;
                }

                char c = text[i];
                if (c < '0' || c > '9' || digits == 3) return false;
                value = value * 10 + (c - '0');
                digits++;
            }

            return parts == 4;
        }

        private static bool TryParsePort(string text, out ushort port)
        {
            port = 0;
            string trimmed = text == null ? string.Empty : text.Trim();
            if (trimmed.Length == 0 || trimmed.Length > 5) return false;
            int value = 0;
            foreach (char c in trimmed)
            {
                if (c < '0' || c > '9') return false;
                value = value * 10 + (c - '0');
            }

            if (value < 1 || value > 65535) return false;
            port = (ushort)value;
            return true;
        }
    }
}
