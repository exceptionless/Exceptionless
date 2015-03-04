using System;
using System.Net;
using System.Text.RegularExpressions;

namespace Exceptionless.EventMigration {
    public static class Util {
        public static IPAddress GetExternalIP() {
            try {
                var client = new WebClient();
                string content = client.DownloadString("http://checkip.dyndns.org/");
                var match = Regex.Matches(content, "Current IP Address: (.+?)<");
                string ip = match[0].Groups[1].Value;
                return IPAddress.Parse(ip);
            } catch {
                return null;
            }
        }
    }
}