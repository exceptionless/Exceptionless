## Summary

_JsonPointer.Net_ implements the JSON Pointer specification [RFC 6901](https://www.rfc-editor.org/rfc/rfc6901.html), a string syntax for identifying a specific value within a JavaScript Object Notation (JSON) document.

## Links

- [Documentation](https://docs.json-everything.net/pointer/basics/)
- [API Reference](https://docs.json-everything.net/api/JsonPointer.Net/JsonPointer/)
- [Release Notes](https://docs.json-everything.net/rn-json-pointer/)

## Usage

Parse a pointer:

```c#
var pointer = JsonPointer.Parse("/objects/and/3/arrays");
```

Build it manually:

```c#
var pointer = JsonPointer.Create("object", "and", 3, "arrays");
```

Or generate using an LINQ expression:

```c#
var pointer = JsonPointer.Create<MyObject>(x => x.objects.and[3].arrays);
```

Use the pointer to query `JsonElement`:

```c#
using var element = JsonDocument.Parse("{\"objects\":{\"and\":[\"item zero\",null,2,{\"arrays\":\"found me\"}]}}");
var result = pointer.Evaluate(element.RootElement);
// result: "found me"
```

or `JsonNode`:

```c#
var element = JsonNode.Parse("{\"objects\":{\"and\":[\"item zero\",null,2,{\"arrays\":\"found me\"}]}}");
var success = pointer.TryEvaluate(element, out var result);
// success: true
// result: "found me"
```

## Sponsorship

If you found this library helpful and would like to promote continued development, please consider [sponsoring the maintainers](https://github.com/sponsors/gregsdennis).