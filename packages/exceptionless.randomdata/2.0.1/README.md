# Exceptionless.RandomData

[![Build status](https://github.com/Exceptionless/Exceptionless.RandomData/workflows/Build/badge.svg)](https://github.com/Exceptionless/Exceptionless.RandomData/actions)
[![NuGet Version](http://img.shields.io/nuget/v/Exceptionless.RandomData.svg?style=flat)](https://www.nuget.org/packages/Exceptionless.RandomData/)
[![Discord](https://img.shields.io/discord/715744504891703319)](https://discord.gg/6HxgFCx)
[![Donate](https://img.shields.io/badge/donorbox-donate-blue.svg)](https://donorbox.org/exceptionless?recurring=true)

A utility library for generating random data in .NET. Makes generating realistic test data a breeze. Targets **net8.0** and **net10.0**.

## Getting Started

[This package](https://www.nuget.org/packages/Exceptionless.RandomData/) can be installed via the [NuGet package manager](https://docs.nuget.org/consume/Package-Manager-Dialog). If you need help, please contact us via in-app support or [open an issue](https://github.com/exceptionless/Exceptionless.RandomData/issues/new). We're always here to help if you have any questions!

```
dotnet add package Exceptionless.RandomData
```

## Usage

All methods are on the static `RandomData` class in the `Exceptionless` namespace.

### Numbers

```csharp
using Exceptionless;

int value = RandomData.GetInt(1, 100);
long big = RandomData.GetLong(0, 1_000_000);
double d = RandomData.GetDouble(0.0, 1.0);
decimal m = RandomData.GetDecimal(1, 500);
```

### Booleans

```csharp
using Exceptionless;

bool coin = RandomData.GetBool();
bool likely = RandomData.GetBool(chance: 80); // 80% chance of true
```

### Strings

```csharp
using Exceptionless;

string random = RandomData.GetString(minLength: 5, maxLength: 20);
string alpha = RandomData.GetAlphaString(10, 10);
string alphaNum = RandomData.GetAlphaNumericString(8, 16);
```

### Words, Sentences, and Paragraphs

```csharp
using Exceptionless;

string word = RandomData.GetWord();
string title = RandomData.GetTitleWords(minWords: 3, maxWords: 6);
string sentence = RandomData.GetSentence(minWords: 5, maxWords: 15);
string text = RandomData.GetParagraphs(count: 2, minSentences: 3, maxSentences: 10);
string html = RandomData.GetParagraphs(count: 2, html: true);
```

### Dates and Times

```csharp
using Exceptionless;

DateTime date = RandomData.GetDateTime();
DateTime recent = RandomData.GetDateTime(start: DateTime.UtcNow.AddDays(-30), end: DateTime.UtcNow);
DateTimeOffset dto = RandomData.GetDateTimeOffset();
TimeSpan span = RandomData.GetTimeSpan(min: TimeSpan.FromMinutes(1), max: TimeSpan.FromHours(2));
```

### Enums

```csharp
using Exceptionless;

DayOfWeek day = RandomData.GetEnum<DayOfWeek>();
```

### Network and Versioning

```csharp
using Exceptionless;

string ip = RandomData.GetIp4Address();           // e.g. "192.168.4.12"
string coord = RandomData.GetCoordinate();         // e.g. "45.123,-90.456"
string version = RandomData.GetVersion("1.0", "5.0");
```

### Pick Random from Collection

The `Random<T>()` extension method picks a random element from any `IEnumerable<T>`:

```csharp
using Exceptionless;

int[] numbers = [1, 2, 3, 4, 5];
int picked = numbers.Random();

string[] names = ["Alice", "Bob", "Charlie"];
string? name = names.Random();
```

## Thanks to all the people who have contributed

[![contributors](https://contributors-img.web.app/image?repo=exceptionless/Exceptionless.RandomData)](https://github.com/exceptionless/Exceptionless.RandomData/graphs/contributors)
