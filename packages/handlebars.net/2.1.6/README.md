# Handlebars.Net

#### [![CI](https://github.com/Handlebars-Net/Handlebars.Net/workflows/CI/badge.svg)](https://github.com/Handlebars-Net/Handlebars.Net/actions?query=workflow%3ACI) [![Nuget](https://img.shields.io/nuget/vpre/Handlebars.Net)](https://www.nuget.org/packages/Handlebars.Net/) [![performance](https://img.shields.io/badge/benchmark-statistics-blue)](http://handlebars-net.github.io/Handlebars.Net/dev/bench/)

---

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=Handlebars-Net_Handlebars.Net&metric=alert_status)](https://sonarcloud.io/dashboard?id=Handlebars-Net_Handlebars.Net) [![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=Handlebars-Net_Handlebars.Net&metric=reliability_rating)](https://sonarcloud.io/dashboard?id=Handlebars-Net_Handlebars.Net) [![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=Handlebars-Net_Handlebars.Net&metric=security_rating)](https://sonarcloud.io/dashboard?id=Handlebars-Net_Handlebars.Net)

[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=Handlebars-Net_Handlebars.Net&metric=bugs)](https://sonarcloud.io/dashboard?id=Handlebars-Net_Handlebars.Net) [![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=Handlebars-Net_Handlebars.Net&metric=code_smells)](https://sonarcloud.io/dashboard?id=Handlebars-Net_Handlebars.Net) [![Coverage](https://sonarcloud.io/api/project_badges/measure?project=Handlebars-Net_Handlebars.Net&metric=coverage)](https://sonarcloud.io/dashboard?id=Handlebars-Net_Handlebars.Net) 

---

[![Stack Exchange questions](https://img.shields.io/stackexchange/stackoverflow/t/%5Bhandlebars.net%5D?label=stackoverflow)](https://stackoverflow.com/questions/tagged/handlebars.net) 
[![GitHub issues questions](https://img.shields.io/github/issues/handlebars-net/handlebars.net/question)](https://github.com/Handlebars-Net/Handlebars.Net/labels/question) 
[![GitHub issues help wanted](https://img.shields.io/github/issues/handlebars-net/handlebars.net/help%20wanted?color=green&label=help%20wanted)](https://github.com/Handlebars-Net/Handlebars.Net/labels/help%20wanted)

---

Blistering-fast [Handlebars.js templates](http://handlebarsjs.com) in your .NET application.

>Handlebars.js is an extension to the Mustache templating language created by Chris Wanstrath. Handlebars.js and Mustache are both logicless templating languages that keep the view and the code separated like we all know they should be.

Check out the [handlebars.js documentation](http://handlebarsjs.com) for how to write Handlebars templates.

Handlebars.Net doesn't use a scripting engine to run a Javascript library - it **compiles Handlebars templates directly to IL bytecode**. It also mimics the JS library's API as closely as possible.

## Install

    dotnet add package Handlebars.Net

## Extensions
The following projects are extending Handlebars.Net:
- [Handlebars.Net.Extension.Json](https://github.com/Handlebars-Net/Handlebars.Net.Extension.Json) (Adds `System.Text.Json.JsonDocument` support)
- [Handlebars.Net.Extension.NewtonsoftJson](https://github.com/Handlebars-Net/Handlebars.Net.Extension.NewtonsoftJson) (Adds `Newtonsoft.Json` support)
- [Handlebars.Net.Helpers](https://github.com/Handlebars-Net/Handlebars.Net.Helpers) (Additional helpers in the categories: 'Constants', 'Enumerable', 'Math', 'Regex', 'String', 'DateTime', 'Url' , 'DynamicLinq', 'Humanizer', 'Json', 'Random', 'Xeger' and 'XPath'.)


## Usage

```c#
string source =
@"<div class=""entry"">
  <h1>{{title}}</h1>
  <div class=""body"">
    {{body}}
  </div>
</div>";

var template = Handlebars.Compile(source);

var data = new {
    title = "My new post",
    body = "This is my first post!"
};

var result = template(data);

/* Would render:
<div class="entry">
  <h1>My New Post</h1>
  <div class="body">
    This is my first post!
  </div>
</div>
*/
```

### Registering Partials

```c#
string source =
@"<h2>Names</h2>
{{#names}}
  {{> user}}
{{/names}}";

string partialSource =
@"<strong>{{name}}</strong>";

Handlebars.RegisterTemplate("user", partialSource);

var template = Handlebars.Compile(source);

var data = new {
  names = new [] {
    new {
        name = "Karen"
    },
    new {
        name = "Jon"
    }
  }
};

var result = template(data);

/* Would render:
<h2>Names</h2>
  <strong>Karen</strong>
  <strong>Jon</strong>
*/
```

### Registering Helpers

```c#
Handlebars.RegisterHelper("link_to", (writer, context, parameters) => 
{
    writer.WriteSafeString($"<a href='{context["url"]}'>{context["text"]}</a>");
});

string source = @"Click here: {{link_to}}";

var template = Handlebars.Compile(source);

var data = new {
    url = "https://github.com/rexm/handlebars.net",
    text = "Handlebars.Net"
};

var result = template(data);

/* Would render:
Click here: <a href='https://github.com/rexm/handlebars.net'>Handlebars.Net</a>
*/
```
 
This will expect your views to be in the /Views folder like so:

```
Views\layout.hbs                |<--shared as in \Views            
Views\partials\somepartial.hbs   <--shared as in  \Views\partials
Views\{Controller}\{Action}.hbs 
Views\{Controller}\{Action}\partials\somepartial.hbs 
```
### Registering Block Helpers

```c#
Handlebars.RegisterHelper("StringEqualityBlockHelper", (output, options, context, arguments) => 
{
    if (arguments.Length != 2)
    {
        throw new HandlebarsException("{{#StringEqualityBlockHelper}} helper must have exactly two arguments");
    }

    var left = arguments.At<string>(0);
    var right = arguments[1] as string;
    if (left == right) options.Template(output, context);
    else options.Inverse(output, context);
});

var animals = new Dictionary<string, string>() 
{
	{"Fluffy", "cat" },
	{"Fido", "dog" },
	{"Chewy", "hamster" }
};

var template = "{{#each this}}The animal, {{@key}}, {{#StringEqualityBlockHelper @value 'dog'}}is a dog{{else}}is not a dog{{/StringEqualityBlockHelper}}.\r\n{{/each}}";
var compiledTemplate = Handlebars.Compile(template);
string templateOutput = compiledTemplate(animals);

/* Would render
The animal, Fluffy, is not a dog.
The animal, Fido, is a dog.
The animal, Chewy, is not a dog.
*/
```

### Registering Decorators

```c#
[Fact]
public void BasicDecorator(IHandlebars handlebars)
{
    string source = "{{#block @value-from-decorator}}{{*decorator 42}}{{@value}}{{/block}}";

    var handlebars = Handlebars.Create();
    handlebars.RegisterHelper("block", (output, options, context, arguments) =>
    {
        options.Data.CreateProperty("value", arguments[0], out _);
        options.Template(output, context);
    });
    
    handlebars.RegisterDecorator("decorator", 
        (TemplateDelegate function, in DecoratorOptions options, in Context context, in Arguments arguments) =>
    {
        options.Data.CreateProperty("value-from-decorator", arguments[0], out _);
    });
    
    var template = handlebars.Compile(source);
    
    var result = template(null);
    Assert.Equal("42", result);
}
```
For more examples see [DecoratorTests.cs](https://github.com/Handlebars-Net/Handlebars.Net/tree/master/source/Handlebars.Test/DecoratorTests.cs)

#### Known limitations:
- helpers registered inside of a decorator will not override existing registrations

### Register custom value formatter

In case you need to apply custom value formatting (e.g. `DateTime`) you can use `IFormatter` and `IFormatterProvider` interfaces:

```c#
public sealed class CustomDateTimeFormatter : IFormatter, IFormatterProvider
{
    private readonly string _format;

    public CustomDateTimeFormatter(string format) => _format = format;

    public void Format<T>(T value, in EncodedTextWriter writer)
    {
        if(!(value is DateTime dateTime)) 
            throw new ArgumentException("supposed to be DateTime");
        
        writer.Write($"{dateTime.ToString(_format)}");
    }

    public bool TryCreateFormatter(Type type, out IFormatter formatter)
    {
        if (type != typeof(DateTime))
        {
            formatter = null;
            return false;
        }

        formatter = this;
        return true;
    }
}

[Fact]
public void DateTimeFormatter(IHandlebars handlebars)
{
    var source = "{{now}}";

    var format = "d";
    var formatter = new CustomDateTimeFormatter(format);
    handlebars.Configuration.FormatterProviders.Add(formatter);

    var template = handlebars.Compile(source);
    var data = new
    {
        now = DateTime.Now
    };
    
    var result = template(data);
    Assert.Equal(data.now.ToString(format), result);
}
```
#### Notes
- Formatters are resolved in reverse order according to registration. If multiple providers can provide formatter for a type the last registered would be used.

### Shared environment

By default Handlebars will create standalone copy of environment for each compiled template. This is done in order to eliminate a chance of altering behavior of one template from inside of other one.

Unfortunately, in case runtime has a lot of compiled templates (regardless of the template size) it may have significant memory footprint. This can be solved by using `SharedEnvironment`.

Templates compiled in `SharedEnvironment` will share the same configuration.

#### Limitations

Only runtime configuration properties can be changed after the shared environment has been created. Changes to `Configuration.CompileTimeConfiguration` and other compile-time properties will have no effect. 

#### Example

```c#
[Fact]
public void BasicSharedEnvironment()
{
    var handlebars = Handlebars.CreateSharedEnvironment();
    handlebars.RegisterHelper("registerLateHelper", 
        (in EncodedTextWriter writer, in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            var configuration = options.Frame
                .GetType()
                .GetProperty("Configuration", BindingFlags.Instance | BindingFlags.NonPublic)?
                .GetValue(options.Frame) as ICompiledHandlebarsConfiguration;
            
            var helpers = configuration?.Helpers;

            const string name = "lateHelper";
            if (helpers?.TryGetValue(name, out var @ref) ?? false)
            {
                @ref.Value = new DelegateReturnHelperDescriptor(name, (c, a) => 42);
            }
        });
    
    var _0_template = "{{registerLateHelper}}";
    var _0 = handlebars.Compile(_0_template);
    var _1_template = "{{lateHelper}}";
    var _1 = handlebars.Compile(_1_template);
    
    var result = _1(null);
    Assert.Equal("", result); // `lateHelper` is not registered yet

    _0(null);
    result = _1(null);
    Assert.Equal("42", result);
}
```

### Compatibility feature toggles

Compatibility feature toggles defines a set of settings responsible for controlling compilation/rendering behavior. Each of those settings would enable certain feature that would break compatibility with canonical Handlebars.
By default all toggles are set to `false`. 

##### Legend
- Areas
  - `Compile-time`: takes affect at the time of template compilation
  - `Runtime`: takes affect at the time of template rendering

#### `RelaxedHelperNaming`
If `true` enables support for Handlebars.Net helper naming rules.
This enables helper names to be not-valid Handlebars identifiers (e.g. `{{ one.two }}`).
Such naming is not supported in Handlebarsjs and would break compatibility.

##### Areas
- `Compile-time`

##### Example
```c#
[Fact]
public void HelperWithDotSeparatedName()
{
    var source = "{{ one.two }}";
    var handlebars = Handlebars.Create();
    handlebars.Configuration.Compatibility.RelaxedHelperNaming = true;
    handlebars.RegisterHelper("one.two", (context, arguments) => 42);

    var template = handlebars.Compile(source);
    var actual = template(null);
    
    Assert.Equal("42", actual);
}
```

#### HtmlEncoder
Used to switch between the legacy Handlebars.Net and the canonical Handlebars rules (or a custom implementation).\
For Handlebars.Net 2.x.x `HtmlEncoderLegacy` is the default.

`HtmlEncoder`\
Implements the canonical Handlebars rules.

`HtmlEncoderLegacy`\
Will not encode:\
= (equals)\
&#96; (backtick)\
' (single quote)

Will encode non-ascii characters `�`, `�`, ...\
Into HTML entities (`&lt;`, `&#226;`, `&#223;`, ...).

##### Areas
- `Runtime`

##### Example
```c#
[Fact]
public void UseCanonicalHtmlEncodingRules()
{
    var handlebars = Handlebars.Create();
    handlebars.Configuration.TextEncoder = new HtmlEncoder();

    var source = "{{Text}}";
    var value = new { Text = "< �" };

    var template = handlebars.Compile(source);
    var actual = template(value);
            
    Assert.Equal("&lt; �", actual);
}
```

## Performance

### Compilation

Compared to rendering, compiling is a fairly intensive process. While both are still measured in millseconds, compilation accounts for the most of that time by far. So, it is generally ideal to compile once and cache the resulting function to be re-used for the life of your process.

### Rendering
Nearly all time spent in rendering is in the routine that resolves values against the model. Different types of objects have different performance characteristics when used as models.

#### Model Types
- The absolute fastest model is a `IDictionary<string, object>` (microseconds).
- The next fastest is a POCO (typically a few milliseconds for an average-sized template and model), which uses traditional reflection and is fairly fast.
- Rendering starts to get slower (into the tens of milliseconds or more) on dynamic objects.
- The slowest (up to hundreds of milliseconds or worse) tend to be objects with custom type implementations (such as `ICustomTypeDescriptor`) that are not optimized for heavy reflection.

## Future roadmap

TBD

## Contributing

Pull requests are welcome! The guidelines are pretty straightforward:
- Only add capabilities that are already in the Mustache / Handlebars specs
- Avoid dependencies outside of the .NET BCL
- Maintain cross-platform compatibility (.NET/Mono; Windows/OSX/Linux/etc)
- Follow the established code format
