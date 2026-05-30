# MiniValidation
A minimalistic validation library built atop the existing features in .NET's `System.ComponentModel.DataAnnotations` namespace. Adds support for single-line validation calls and recursion with cycle detection.

Supports .NET Standard 2.0 compliant runtimes.

## Installation
[![Nuget](https://img.shields.io/nuget/v/MiniValidation)](https://www.nuget.org/packages/MiniValidation/)

Install the library from [NuGet](https://www.nuget.org/packages/MiniValidation):
``` console
❯ dotnet add package MiniValidation
```

### ASP.NET Core 8+ Projects
If installing into an ASP.NET Core 8+ project, consider using the [MinimalApis.Extensions](https://www.nuget.org/packages/MinimalApis.Extensions) package instead, which adds extensions specific to ASP.NET Core, including a validation endpoint filter:
``` console
❯ dotnet add package MinimalApis.Extensions
```

## Example usage

### Validate an object

```csharp
var widget = new Widget { Name = "" };

var isValid = MiniValidator.TryValidate(widget, out var errors);

class Widget
{
    [Required, MinLength(3)]
    public string Name { get; set; }

    public override string ToString() => Name;
}
```

### Use services from validators

```csharp
var widget = new Widget { Name = "" };

// Get your serviceProvider from wherever makes sense
var serviceProvider = ...
var isValid = MiniValidator.TryValidate(widget, serviceProvider, out var errors);

class Widget : IValidatableObject
{
    [Required, MinLength(3)]
    public string Name { get; set; }

    public override string ToString() => Name;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var disallowedNamesService = validationContext.GetService(typeof(IDisallowedNamesService)) as IDisallowedNamesService;

        if (disallowedNamesService is null)
        {
            throw new InvalidOperationException($"Validation of {nameof(Widget)} requires an {nameof(IDisallowedNamesService)} instance.");
        }

        if (disallowedNamesService.IsDisallowedName(Name))
        {
            yield return new($"Cannot name a widget '{Name}'.", new[] { nameof(Name) });
        }
    }
}
```

### Console app

```csharp
using System.ComponentModel.DataAnnotations;
using MiniValidation;

var title = args.Length > 0 ? args[0] : "";

var widgets = new List<Widget>
{
    new Widget { Name = title },
    new WidgetWithCustomValidation { Name = title }
};

foreach (var widget in widgets)
{
    if (!MiniValidator.TryValidate(widget, out var errors))
    {
        Console.WriteLine($"{nameof(Widget)} has errors!");
        foreach (var entry in errors)
        {
            Console.WriteLine($"  {entry.Key}:");
            foreach (var error in entry.Value)
            {
                Console.WriteLine($"  - {error}");
            }
        }
    }
    else
    {
        Console.WriteLine($"{nameof(Widget)} '{widget}' is valid!");
    }
}

class Widget
{
    [Required, MinLength(3)]
    public string Name { get; set; }

    public override string ToString() => Name;
}

class WidgetWithCustomValidation : Widget, IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(Name, "Widget", StringComparison.OrdinalIgnoreCase))
        {
            yield return new($"Cannot name a widget '{Name}'.", new[] { nameof(Name) });
        }
    }
}
```

``` console
❯ widget.exe
Widget 'widget' is valid!
Widget has errors!
  Name:
  - Cannot name a widget 'widget'.

❯ widget.exe Ok
Widget has errors!
  Name:
  - The field Name must be a string or array type with a minimum length of '3'.
Widget has errors!
  Name:
  - The field Name must be a string or array type with a minimum length of '3'.

❯ widget.exe Widget
Widget 'Widget' is valid!
Widget has errors!
  Name:
  - Cannot name a widget 'Widget'.

❯ widget.exe MiniValidation
Widget 'MiniValidation' is valid!
Widget 'MiniValidation' is valid!
```

### Web app (.NET 8+)
```csharp
using System.ComponentModel.DataAnnotations;
using MiniValidation;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World");

app.MapGet("/widgets", () =>
    new[] {
        new Widget { Name = "Shinerizer" },
        new Widget { Name = "Sparklizer" }
    });

app.MapGet("/widgets/{name}", (string name) =>
    new Widget { Name = name });

app.MapPost("/widgets", (Widget widget) =>
    !MiniValidator.TryValidate(widget, out var errors)
        ? Results.ValidationProblem(errors)
        : Results.Created($"/widgets/{widget.Name}", widget));

app.MapPost("/widgets/custom-validation", (WidgetWithCustomValidation widget) =>
    !MiniValidator.TryValidate(widget, out var errors)
        ? Results.ValidationProblem(errors)
        : Results.Created($"/widgets/{widget.Name}", widget));

app.Run();

class Widget
{
    [Required, MinLength(3)]
    public string? Name { get; set; }

    public override string? ToString() => Name;
}

class WidgetWithCustomValidation : Widget, IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(Name, "Widget", StringComparison.OrdinalIgnoreCase))
        {
            yield return new($"Cannot name a widget '{Name}'.", new[] { nameof(Name) });
        }
    }
}
```
