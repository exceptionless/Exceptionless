---
title: "Add Reverse Geocoding to Your App"
---

# Add Reverse Geocoding to Your App

![Reverse Geocoding](/assets/img/news/reverse-geocoding-header.jpg)

We recently introduced reverse geocoding into Exceptionless and are now adding features to make full use of it.

What we'd like to do in this blog article is **walk any interested developers through the process of adding it to their own app.**

We'll talk about the resources and services we're using to pull it off, why we chose them, and give you code snippets for implementation. It's all open source, so we've also included links to all the relevant code in hopes it will make your life easier!

Lets check it out.

## What is Reverse Geocoding?



It’s the process of taking geo coordinates or an IP Address and resolving it to a physical address (E.G., city, county, state/province, country).

### Why You Need It

![Reverse Geocoding - User Location](/assets/img/news/user-event-geo-location.jpg)

Wouldn’t it be nice if you could **provide location services to your users automatically**? Maybe help them fill in a shipping form from a zip code or there current location?

With the launch of Exceptionless 2.0 we added a geo property to all events. This allows us to translate valid latitude and longitude coordinates into a physical location. Our goal was to begin capturing the data then and enable different scenarios and uses later. This also allowed us to **show end users where their customers events are being submitted from.**

### What does it cost?

One of our primary goals with Exceptionless is to be **completely open source and easy to use** (both in setting up self hosting and using the product). We had to take this into account when picking any library or service, because we want a painless setup and no additional costs for self hosters, all while adding additional value!

**Please note** that if you love the services we use, you should look into using one of their paid plans or at least promoting them with a positive review, shout out, etc (everyone needs to eat at the end of the day, right?).

After researching many different services, we ended up goin

g with [GeoLite2's free, offline, downloadable databases](http://dev.maxmind.com/geoip/geoip2/geolite2/). These databases are **free and updated once a month**, but if you require a more accurate and up-to-date database they offer a paid subscription. We also use their [open source library](https://github.com/maxmind/GeoIP2-dotnet) for interacting with the database in memory.

## Automating the GeoIP Database Download

We use our very own [Foundatio Jobs](https://github.com/FoundatioFx/Foundatio#jobs) to download the most up-to-date database. Foundatio Jobs allows us to run the job in process or out of process on a schedule in Azure.

Alternatively, you could use the [PowerShell script](https://github.com/exceptionless/Exceptionless/blob/v3.5.1/Libraries/DownloadGeoIPDatabase.ps1) we created for downloading the database.  `[DownloadGeoIPDatabaseJob](https://github.com/exceptionless/Exceptionless/blob/master/src/Exceptionless.Core/Jobs/DownloadGeoIPDatabaseJob.cs)` downloads the database over http and extracts the file contents to disk using [Foundatio Storage](https://github.com/FoundatioFx/Foundatio#file-storage).

Please feel free to **take a look out our job** for a complete sample including logging and error handling:

```cs
var client = new HttpClient();
var file = await client.GetAsync("http://geolite.maxmind.com/download/geoip/database/GeoLite2-City.mmdb.gz", context.CancellationToken);
if (!file.IsSuccessStatusCode)
    throw new Exception("Unable to download GeoIP database.");

using (GZipStream decompressionStream = new GZipStream(await file.Content.ReadAsStreamAsync(), CompressionMode.Decompress))
    await _storage.SaveFileAsync("GeoLite2-City.mmdb", decompressionStream, context.CancellationToken);
```

## Looking up a Physical Address from an IP Address

#### Resolving the geo coordinates into a location

After we automate the database download, the next step involves loading the database in memory using the [open source library](https://github.com/maxmind/GeoIP2-dotnet) provided by MaxMind and querying by the IP address. The code below will do very basic IP validation and lookup the records using the API.

```cs
private DatabaseReader _database;
public async Task<GeoResult> ResolveIpAsync(string ip, CancellationToken cancellationToken = new CancellationToken()) {
    if (String.IsNullOrWhiteSpace(ip) || (!ip.Contains(".") && !ip.Contains(":")))
        return null;

    var database = await GetDatabaseAsync(cancellationToken);
    if (database == null)
        return null;

    try {
        var city = database.City(ip);
        if (city?.Location != null) {
            return new GeoResult {
                Latitude = city.Location.Latitude,
                Longitude = city.Location.Longitude,
                Country = city.Country.IsoCode,
                Level1 = city.MostSpecificSubdivision.IsoCode,
                Locality = city.City.Name
            };
        }
    } catch (Exception) {
        // The result was not found in the database
    }

    return null;
}

private async Task<DatabaseReader> GetDatabaseAsync(CancellationToken cancellationToken) {
    if (_database != null)
        return _database;

    if (!await _storage.ExistsAsync("GeoLite2-City.mmdb")) {
        Logger.Warn().Message("No GeoIP database was found.").Write();
        return null;
    }

    try {
        using (var stream = await _storage.GetFileStreamAsync("GeoLite2-City.mmdb", cancellationToken))
            _database = new DatabaseReader(stream);
    } catch (Exception) {
        // Unable to open GeoIP database.
    }

    return _database;
}
```

Then, just call the `ResolveIPAsync` method with an IP address to look up the location details.

```cs
var location = await ResolveIPAsync("YOUR_IP_ADDRESS_HERE");
```

Feel free to take a look at `[MaxMindGeoIPService](https://github.com/exceptionless/Exceptionless/blob/master/src/Exceptionless.Insulation/Geo/MaxMindGeoIpService.cs)` for a complete sample that includes logging, error handling, caching of the results, and IP validation for higher lookup throughput. We’ve spent the time writing tests and optimizing it to ensure **its rock solid and works great**. So feel free to grab our IGeoIPService interfaces and models and use them in your app.

**It’s worth noting** that in our app, we use the IP address provided in the event. This could come from a server request or the actual machine's IP address. We also fall back to the API consumer's client IP address.

### Looking up a Physical Address from Geo Coordinates

As stated previously, every event submitted to Exceptionless has a geo property that can be set. If it’s set, we will attempt to look up your location by the geo coordinates using a third party service. We used the open source [Geocoding.net library](https://github.com/chadly/Geocoding.net), which abstracts the major different third party reverse geocode services into an easy to use API (options are always good!).

After we decided on the library, we evaluated a few API/lookup services based on cost and accuracy. We ended up going with the [Google Maps GeoCoding API](https://developers.google.com/maps/documentation/geocoding/usage-limits). They offer 2500 free requests per day and are one of the most used location services in the world.

Next, let’s write the code that will look up our location from a latitude and longitude. You can find our complete example here.

Remember to get your free api key from Google before running the code below.

```cs
public async Task<GeoResult> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = new CancellationToken()) {
    var geocoder = new GoogleGeocoder("YOUR_API_KEY");
    var addresses = await geocoder.ReverseGeocodeAsync(latitude, longitude, cancellationToken);
    var address = addresses.FirstOrDefault();
    if (address == null)
        return null;

    return new GeoResult {
        Country = address[GoogleAddressType.Country]?.ShortName,
        Level1 = address[GoogleAddressType.AdministrativeAreaLevel1]?.ShortName,
        Level2 = address[GoogleAddressType.AdministrativeAreaLevel2]?.ShortName,
        Locality = address[GoogleAddressType.Locality]?.ShortName,
        Latitude = latitude,
        Longitude = longitude
    };
}
```

Finally, just call the `ReverseGeocodeAsync` method with a latitude and longitude to look up the location details.

```cs
var location = await ResolveGeocodeAsync(44.5241, -87.9056);
```

## Final Thoughts on Reverse Geocoding

It took us a bit of work and research initially to get everything working flawlessly for location services. We hope you grab our code off of GitHub to **save yourself all that work**. Also, it’s worth noting that we use [Foundatio Caching](https://github.com/FoundatioFx/Foundatio#caching) to cache the results of location lookups. It drastically increased the performance and cut down on our limited number of requests to rate-limited third party services!

We also queue work items to look up the geo location since that can be an expensive operation. So, please take this into consideration anytime you are interacting with a third party service.

## Feedback? Questions?

Get in touch on [GitHub](https://github.com/exceptionless) or leave a comment below to let us know your thoughts or questions. We're always open to suggestions and will do what we can to help you out if you're having issues with implementation!
