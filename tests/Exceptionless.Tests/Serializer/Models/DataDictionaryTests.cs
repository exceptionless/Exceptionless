using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests for DataDictionary.GetValue&lt;T&gt;() extension method.
/// Verifies support for Newtonsoft.Json (JObject) and JSON strings.
/// </summary>
public class DataDictionaryTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public DataDictionaryTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void GetValue_DirectUserInfoType_ReturnsTypedValue()
    {
        // Arrange
        var userInfo = new UserInfo("test@example.com", "Test User");
        var data = new DataDictionary { { "user", userInfo } };

        // Act
        var result = data.GetValue<UserInfo>("user");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test@example.com", result.Identity);
        Assert.Equal("Test User", result.Name);
    }

    [Fact]
    public void GetValue_DirectStringType_ReturnsStringValue()
    {
        // Arrange
        var data = new DataDictionary { { "version", "1.0.0" } };

        // Act
        string? result = data.GetValue<string>("version");

        // Assert
        Assert.Equal("1.0.0", result);
    }

    [Fact]
    public void GetValue_DirectIntType_ReturnsIntValue()
    {
        // Arrange
        var data = new DataDictionary { { "count", 42 } };

        // Act
        int result = data.GetValue<int>("count");

        // Assert
        Assert.Equal(42, result);
    }


    [Fact]
    public void GetValue_JObjectWithUserInfo_ReturnsTypedUserInfo()
    {
        // Arrange
        var jObject = JObject.FromObject(new { Identity = "jobj@test.com", Name = "JObject User" });
        var data = new DataDictionary { { "user", jObject } };

        // Act
        var result = data.GetValue<UserInfo>("user");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("jobj@test.com", result.Identity);
        Assert.Equal("JObject User", result.Name);
    }

    [Fact]
    public void GetValue_JObjectWithError_ReturnsTypedError()
    {
        // Arrange
        var jObject = JObject.FromObject(new
        {
            Message = "Test error",
            Type = "System.Exception",
            StackTrace = new[]
            {
                new { Name = "TestMethod", DeclaringNamespace = "Tests", DeclaringType = "TestClass" }
            }
        });
        var data = new DataDictionary { { "@error", jObject } };

        // Act
        var result = data.GetValue<Error>("@error");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test error", result.Message);
        Assert.Equal("System.Exception", result.Type);
        Assert.NotNull(result.StackTrace);
        Assert.Single(result.StackTrace);
    }

    [Fact]
    public void GetValue_JObjectWithRequestInfo_ReturnsTypedRequestInfo()
    {
        // Arrange
        var jObject = JObject.FromObject(new
        {
            HttpMethod = "GET",
            Path = "/api/test",
            Host = "localhost",
            Port = 443,
            IsSecure = true,
            ClientIpAddress = "127.0.0.1"
        });
        var data = new DataDictionary { { "@request", jObject } };

        // Act
        var result = data.GetValue<RequestInfo>("@request");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("GET", result.HttpMethod);
        Assert.Equal("/api/test", result.Path);
        Assert.Equal("localhost", result.Host);
        Assert.Equal(443, result.Port);
        Assert.True(result.IsSecure);
    }

    [Fact]
    public void GetValue_JObjectWithEnvironmentInfo_ReturnsTypedEnvironmentInfo()
    {
        // Arrange
        var jObject = JObject.FromObject(new
        {
            MachineName = "TEST-MACHINE",
            ProcessorCount = 8,
            TotalPhysicalMemory = 16000000000L,
            OSName = "Windows",
            OSVersion = "10.0"
        });
        var data = new DataDictionary { { "@environment", jObject } };

        // Act
        var result = data.GetValue<EnvironmentInfo>("@environment");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TEST-MACHINE", result.MachineName);
        Assert.Equal(8, result.ProcessorCount);
    }

    [Fact]
    public void GetValue_JObjectWithNestedError_ReturnsNestedHierarchy()
    {
        // Arrange
        /* language=json */
        const string jsonInput = """
        {
            "Message": "Outer JObject error",
            "Type": "OuterException",
            "Inner": {
                "Message": "Inner JObject error",
                "Type": "InnerException"
            }
        }
        """;
        var jObject = JObject.Parse(jsonInput);
        var data = new DataDictionary { { "@error", jObject } };

        // Act
        var result = data.GetValue<Error>("@error");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Outer JObject error", result.Message);
        Assert.NotNull(result.Inner);
        Assert.Equal("Inner JObject error", result.Inner.Message);
    }


    [Fact]
    public void GetValue_JsonStringWithUserInfo_ReturnsTypedUserInfo()
    {
        // Arrange - Using snake_case which is the serialized format
        /* language=json */
        const string json = """{"identity":"string@test.com","name":"JSON String User"}""";
        var data = new DataDictionary { { "user", json } };

        // Act
        var result = data.GetValue<UserInfo>("user");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("string@test.com", result.Identity);
        Assert.Equal("JSON String User", result.Name);
    }

    [Fact]
    public void GetValue_JsonStringWithError_ReturnsTypedError()
    {
        // Arrange
        /* language=json */
        const string json = """{"message":"JSON string error","type":"System.ArgumentException","stack_trace":[{"name":"Method1","declaring_namespace":"Ns","declaring_type":"Class1"}]}""";
        var data = new DataDictionary { { "@error", json } };

        // Act
        var result = data.GetValue<Error>("@error");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("JSON string error", result.Message);
        Assert.Equal("System.ArgumentException", result.Type);
    }

    [Fact]
    public void GetValue_JsonStringWithRequestInfo_ReturnsTypedRequestInfo()
    {
        // Arrange - Using PascalCase as JsonConvert.DeserializeObject uses default settings
        /* language=json */
        const string json = """{"HttpMethod":"POST","Path":"/api/events","Host":"api.example.com","Port":443,"IsSecure":true}""";
        var data = new DataDictionary { { "@request", json } };

        // Act
        var result = data.GetValue<RequestInfo>("@request");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("POST", result.HttpMethod);
        Assert.Equal("/api/events", result.Path);
    }

    [Fact]
    public void GetValue_JsonStringWithEnvironmentInfo_ReturnsTypedEnvironmentInfo()
    {
        // Arrange - Using PascalCase as JsonConvert.DeserializeObject uses default settings
        /* language=json */
        const string json = """{"MachineName":"STRING-MACHINE","ProcessorCount":16}""";
        var data = new DataDictionary { { "@environment", json } };

        // Act
        var result = data.GetValue<EnvironmentInfo>("@environment");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("STRING-MACHINE", result.MachineName);
        Assert.Equal(16, result.ProcessorCount);
    }

    [Fact]
    public void GetValue_JsonStringWithSimpleError_ReturnsTypedSimpleError()
    {
        // Arrange
        /* language=json */
        const string json = """{"message":"Simple error message","type":"CustomError","stack_trace":"at Test.Method()"}""";
        var data = new DataDictionary { { "@simple_error", json } };

        // Act
        var result = data.GetValue<SimpleError>("@simple_error");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Simple error message", result.Message);
        Assert.Equal("CustomError", result.Type);
    }

    [Fact]
    public void GetValue_JsonStringWithNestedError_ReturnsNestedHierarchy()
    {
        // Arrange
        /* language=json */
        const string json = """{"message":"Outer error","type":"OuterException","inner":{"message":"Inner error","type":"InnerException"}}""";
        var data = new DataDictionary { { "@error", json } };

        // Act
        var result = data.GetValue<Error>("@error");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Outer error", result.Message);
        Assert.NotNull(result.Inner);
        Assert.Equal("Inner error", result.Inner.Message);
    }

    [Fact]
    public void GetValue_NonJsonString_ReturnsNull()
    {
        // Arrange
        var data = new DataDictionary { { "text", "not json" } };

        // Act
        var result = data.GetValue<UserInfo>("text");

        // Assert
        Assert.Null(result);
    }


    [Fact]
    public void GetValue_MissingKey_ThrowsKeyNotFoundException()
    {
        // Arrange
        var data = new DataDictionary();

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => data.GetValue<UserInfo>("nonexistent"));
    }

    [Fact]
    public void GetValue_NullValue_ReturnsNull()
    {
        // Arrange
        var data = new DataDictionary { { "nullable", null! } };

        // Act
        var result = data.GetValue<UserInfo>("nullable");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetValue_IncompatibleType_ReturnsNull()
    {
        // Arrange
        var data = new DataDictionary { { "number", 42 } };

        // Act
        var result = data.GetValue<UserInfo>("number");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetValue_MalformedJsonString_ReturnsDefaultProperties()
    {
        // Arrange - JSON string with properties that don't match UserInfo
        /* language=json */
        const string json = """{"foo":"bar"}""";
        var data = new DataDictionary { { "user", json } };

        // Act
        var result = data.GetValue<UserInfo>("user");

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Identity);
    }


    [Fact]
    public void Deserialize_DataDictionaryWithUserInfo_PreservesTypedData()
    {
        // Arrange
        var data = new DataDictionary
        {
            { "@user", new UserInfo("user@test.com", "Test User") }
        };

        // Act
        string? json = _serializer.SerializeToString(data);
        var deserialized = _serializer.Deserialize<DataDictionary>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.True(deserialized.ContainsKey("@user"));
        var userInfo = deserialized.GetValue<UserInfo>("@user");
        Assert.NotNull(userInfo);
        Assert.Equal("user@test.com", userInfo.Identity);
        Assert.Equal("Test User", userInfo.Name);
    }

    [Fact]
    public void Deserialize_DataDictionaryWithMixedTypes_PreservesAllTypes()
    {
        // Arrange
        var data = new DataDictionary
        {
            { "string_value", "hello" },
            { "int_value", 42 },
            { "bool_value", true },
            { "decimal_value", 3.14m }
        };

        // Act
        string? json = _serializer.SerializeToString(data);
        var deserialized = _serializer.Deserialize<DataDictionary>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("hello", deserialized["string_value"]);
        Assert.Equal(42L, deserialized["int_value"]);
        Assert.True(deserialized["bool_value"] as bool?);
    }

    [Fact]
    public void Deserialize_EmptyDataDictionary_PreservesEmptyState()
    {
        // Arrange
        var data = new DataDictionary();

        /* language=json */
        const string expectedJson = """{}""";

        // Act
        string? json = _serializer.SerializeToString(data);
        var deserialized = _serializer.Deserialize<DataDictionary>(json);

        // Assert
        Assert.Equal(expectedJson, json);
        Assert.NotNull(deserialized);
        Assert.Empty(deserialized);
    }

}
