using System;

namespace Exceptionless.Core.Utility {
    public static class IPHelper {
        public static bool IsPrivateNetwork(string ip) {
            if (String.IsNullOrEmpty(ip))
                return false;

            if (String.Equals(ip, "::1") || String.Equals(ip, "127.0.0.1"))
                return true;

            // 10.0.0.0 – 10.255.255.255 (Class A)
            if (ip.StartsWith("10."))
                return true;

            // 172.16.0.0 – 172.31.255.255 (Class B)
            if (ip.StartsWith("172.")) {
                for (var range = 16; range < 32; range++) {
                    if (ip.StartsWith("172." + range + "."))
                        return true;
                }
            }

            // 192.168.0.0 – 192.168.255.255 (Class C)
            return ip.StartsWith("192.168.");
        }
    }
}