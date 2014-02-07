using System;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace CodeSmith.Core.Services
{
    public class GeoLocationInfo
    {
        public string City { get; set; }
        public string RegionCode { get; set; }
        public string RegionName { get; set; }
        public string MetroCode { get; set; }
        public string ZipCode { get; set; }
        public decimal Longitude { get; set; }
        public decimal Latitude { get; set; }
        public string CountryName { get; set; }
        public string CountryCode { get; set; }
        public string IpAddress { get; set; }
    }
    
    public class GeoIpService
    {
        public static GeoLocationInfo GetLocationInfo(string ipaddress)
        {
            var request = (HttpWebRequest)WebRequest.Create(String.Format("http://freegeoip.net/json/{0}", ipaddress));
            request.Method = "GET";
            request.Accept = "application/json";

#if PFX_LEGACY_3_5
            var response = request.GetResponse();
            using (var sr = new StreamReader(response.GetResponseStream()))
#else
            var response = request.GetResponseAsync();
            if (!response.Wait(TimeSpan.FromSeconds(5)))
                return null;
            
            if (!response.IsCompleted || response.Result == null)
                return null;

            using (var sr = new StreamReader(response.Result.GetResponseStream()))
#endif
            {
                var data = sr.ReadToEnd();
                var s = new System.Web.Script.Serialization.JavaScriptSerializer();
                var dict = s.Deserialize<Dictionary<string, string>>(data);
                if (dict == null)
                    return null;

                var location = new GeoLocationInfo
                {
                    City = dict["city"],
                    RegionName = dict["region_name"],
                    RegionCode = dict["region_code"],
                    CountryCode = dict["country_code"],
                    CountryName = dict["country_name"]
                };

                return location;
            }
        }
    }
}
