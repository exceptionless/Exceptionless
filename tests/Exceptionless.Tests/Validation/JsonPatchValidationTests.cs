using System.Text.Json;
using Exceptionless.Web.Api.Infrastructure;
using Foundatio.Mediator;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson.Operations;
using Xunit;

namespace Exceptionless.Tests.Validation;

public sealed class JsonPatchValidationTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public void ValidateOperations_EmptyPatch_ReturnsSuccess()
    {
        var patch = new JsonPatchDocument<TestDto>([], _options);
        var result = JsonPatchValidation.ValidateOperations(patch);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateOperations_ValidReplace_ReturnsSuccess()
    {
        var patch = new JsonPatchDocument<TestDto>(
            [new Operation<TestDto>("replace", "/name", null, "new-name")],
            _options);

        var result = JsonPatchValidation.ValidateOperations(patch);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateOperations_ValidTest_ReturnsSuccess()
    {
        var patch = new JsonPatchDocument<TestDto>(
            [new Operation<TestDto>("test", "/name", null, "expected-value")],
            _options);

        var result = JsonPatchValidation.ValidateOperations(patch);
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("move")]
    [InlineData("copy")]
    public void ValidateOperations_DisallowedOp_ReturnsInvalid(string op)
    {
        var patch = new JsonPatchDocument<TestDto>(
            [new Operation<TestDto>(op, "/name", null, "val")],
            _options);

        var result = JsonPatchValidation.ValidateOperations(patch);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains("not supported", GetErrorMessage(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("  ")]
    public void ValidateOperations_RootOrEmptyPath_ReturnsInvalid(string path)
    {
        var patch = new JsonPatchDocument<TestDto>(
            [new Operation<TestDto>("replace", path, null, "val")],
            _options);

        var result = JsonPatchValidation.ValidateOperations(patch);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains("root path", GetErrorMessage(result));
    }

    [Theory]
    [InlineData("//name")]
    [InlineData("/a/b")]
    [InlineData("/nested/deep/path")]
    public void ValidateOperations_NestedOrMalformedPath_ReturnsInvalid(string path)
    {
        var patch = new JsonPatchDocument<TestDto>(
            [new Operation<TestDto>("replace", path, null, "val")],
            _options);

        var result = JsonPatchValidation.ValidateOperations(patch);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains("not valid", GetErrorMessage(result));
    }

    [Fact]
    public void ValidateOperations_ImmutablePath_ReturnsInvalid()
    {
        var patch = new JsonPatchDocument<TestDto>(
            [new Operation<TestDto>("replace", "/id", null, "new-id")],
            _options);

        var result = JsonPatchValidation.ValidateOperations(patch, "/id");
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains("cannot be modified", GetErrorMessage(result));
    }

    [Fact]
    public void ValidateOperations_ImmutablePathCaseInsensitive_ReturnsInvalid()
    {
        var patch = new JsonPatchDocument<TestDto>(
            [new Operation<TestDto>("replace", "/Id", null, "new-id")],
            _options);

        var result = JsonPatchValidation.ValidateOperations(patch, "/id");
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains("cannot be modified", GetErrorMessage(result));
    }

    [Fact]
    public void ValidateOperations_ExceedsMaxOperations_ReturnsInvalid()
    {
        var ops = Enumerable.Range(0, 51)
            .Select(i => new Operation<TestDto>("replace", "/name", null, $"val-{i}"))
            .ToList();
        var patch = new JsonPatchDocument<TestDto>(ops, _options);

        var result = JsonPatchValidation.ValidateOperations(patch);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains("exceeds maximum", GetErrorMessage(result));
    }

    [Fact]
    public void ApplyPatch_ValidReplace_MutatesTarget()
    {
        var target = new TestDto { Name = "old", Description = "desc" };
        var patch = new JsonPatchDocument<TestDto>(
            [new Operation<TestDto>("replace", "/name", null, "new-name")],
            _options);

        var result = JsonPatchValidation.ApplyPatch(patch, target);
        Assert.True(result.IsSuccess);
        Assert.Equal("new-name", target.Name);
        Assert.Equal("desc", target.Description);
    }

    [Fact]
    public void AffectsPath_MatchingPath_ReturnsTrue()
    {
        var patch = new JsonPatchDocument<TestDto>(
            [new Operation<TestDto>("replace", "/name", null, "val")],
            _options);

        Assert.True(patch.AffectsPath("/name"));
    }

    [Fact]
    public void AffectsPath_NonMatchingPath_ReturnsFalse()
    {
        var patch = new JsonPatchDocument<TestDto>(
            [new Operation<TestDto>("replace", "/name", null, "val")],
            _options);

        Assert.False(patch.AffectsPath("/description"));
    }

    [Fact]
    public void IsEmpty_EmptyPatch_ReturnsTrue()
    {
        var patch = new JsonPatchDocument<TestDto>([], _options);
        Assert.True(patch.IsEmpty());
    }

    [Fact]
    public void IsEmpty_NonEmptyPatch_ReturnsFalse()
    {
        var patch = new JsonPatchDocument<TestDto>(
            [new Operation<TestDto>("replace", "/name", null, "val")],
            _options);
        Assert.False(patch.IsEmpty());
    }

    [Fact]
    public void FromPartialObject_ValidObject_CreatesReplaceOps()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("""{"name":"test","description":"hello"}""");
        var patch = JsonPatchValidation.FromPartialObject<TestDto>(json, _options);

        Assert.NotNull(patch);
        Assert.Equal(2, patch!.Operations.Count);
        Assert.All(patch.Operations, op => Assert.Equal(OperationType.Replace, op.OperationType));
    }

    [Fact]
    public void FromPartialObject_NonObject_ReturnsNull()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("""[]""");
        var patch = JsonPatchValidation.FromPartialObject<TestDto>(json, _options);
        Assert.Null(patch);
    }

    [Fact]
    public void FromPartialObject_EmptyObject_ReturnsEmptyPatch()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("""{}""");
        var patch = JsonPatchValidation.FromPartialObject<TestDto>(json, _options);

        Assert.NotNull(patch);
        Assert.Empty(patch!.Operations);
    }

    private static string GetErrorMessage(Result result)
    {
        // Result.Invalid populates ValidationErrors, not Message
        var errors = result.ValidationErrors?.ToList();
        if (errors is { Count: > 0 })
            return string.Join("; ", errors.Select(e => e.ErrorMessage ?? ""));

        return result.Message ?? string.Empty;
    }

    private sealed class TestDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
