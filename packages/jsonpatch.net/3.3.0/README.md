## Summary

_JsonPatch.Net_ implements [JSON Patch](https://jsonpatch.com/), [RFC 6902](https://datatracker.ietf.org/doc/html/rfc6902), a JSON document structure for expressing a sequence of operations to apply to another JSON document.

## Links

- [Documentation](https://docs.json-everything.net/patch/basics/)
- [API Reference](https://docs.json-everything.net/api/JsonPatch.Net/JsonPatch/)
- [Release Notes](https://docs.json-everything.net/rn-json-patch/)

## Usage

Deserialize and apply immediately:

```c#
var patch = JsonSerializer.Deserialize<JsonPatch>(patchString);
var doc = JsonNode.Parse(docString);
var result = patch.Apply(doc);
```

Or you can build a patch inline:

```c#
var patch = new JsonPatch(PatchOperation.Add("/foo/bar", "baz"),
                          PatchOperation.Test("/foo/biz", false));
```

There is also limited patch generation support:

```c#
// parse your data
var start = JsonNode.Parse("[{\"test\":\"test123\"},{\"test\":\"test321\"},{\"test\":[1,2,3]},{\"test\":[1,2,4]}]");
// or build it inline
var target = new JsonArray{
  new JsonObject { ["test"] = "test123" },
  new JsonObject { ["test"] = "test32132" },
  new JsonObject { ["test1"] = "test321" },
  new JsonObject { ["test"] = new JsonArray{ 1, 2, 3 } },
  new JsonObject { ["test"] = new JsonArray{ 1, 2, 3 } },
}

var patch = start.CreatePatch(target);

/*
Result:
[
  {"op":"replace","path":"/1/test","value":"test32132"},
  {"op":"remove","path":"/2/test"},
  {"op":"add","path":"/2/test1","value":"test321"},
  {"op":"replace","path":"/3/test/2","value":3},
  {"op":"add","path":"/4","value":{"test":[1,2,3]}}
]
*/
```

## Sponsorship

If you found this library helpful and would like to promote continued development, please consider [sponsoring the maintainers](https://github.com/sponsors/gregsdennis).