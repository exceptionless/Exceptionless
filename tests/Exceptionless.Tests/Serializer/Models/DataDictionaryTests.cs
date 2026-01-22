using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests for DataDictionary.GetValue extension method.
/// Verifies deserialization from typed objects, JObject (Elasticsearch), JSON strings, and round-trips.
/// </summary>
public class DataDictionaryTests : TestWithServices
{
    private readonly ITextSerializer _serializer;
    private readonly JsonSerializerOptions _jsonOptions;

    public DataDictionaryTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
        _jsonOptions = GetService<JsonSerializerOptions>();
    }

    [Fact]
    public void GetValue_DirectUserInfoType_ReturnsTypedValue()
    {
        // Arrange
        var userInfo = new UserInfo("test@example.com", "Test User");
        var data = new DataDictionary { { "user", userInfo } };

        // Act
        var result = data.GetValue<UserInfo>("user", _jsonOptions);

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
        string? result = data.GetValue<string>("version", _jsonOptions);

        // Assert
        Assert.Equal("1.0.0", result);
    }

    [Fact]
    public void GetValue_DirectIntType_ReturnsIntValue()
    {
        // Arrange
        var data = new DataDictionary { { "count", 42 } };

        // Act
        int result = data.GetValue<int>("count", _jsonOptions);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void GetValue_JObjectWithUserInfo_ReturnsTypedUserInfo()
    {
        // Arrange - JObject comes from Elasticsearch via NEST/JSON.NET
        var jObject = JObject.FromObject(new { Identity = "jobj@test.com", Name = "JObject User" });
        var data = new DataDictionary { { "user", jObject } };

        // Act
        var result = data.GetValue<UserInfo>("user", _jsonOptions);

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
        var result = data.GetValue<Error>("@error", _jsonOptions);

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
        var result = data.GetValue<RequestInfo>("@request", _jsonOptions);

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
        var result = data.GetValue<EnvironmentInfo>("@environment", _jsonOptions);

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
        var result = data.GetValue<Error>("@error", _jsonOptions);

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
        var result = data.GetValue<UserInfo>("user", _jsonOptions);

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
        var result = data.GetValue<Error>("@error", _jsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("JSON string error", result.Message);
        Assert.Equal("System.ArgumentException", result.Type);
    }

    [Fact]
    public void GetValue_JsonStringWithRequestInfo_ReturnsTypedRequestInfo()
    {
        // Arrange
        /* language=json */
        const string json = """{"http_method":"POST","path":"/api/events","host":"api.example.com","port":443,"is_secure":true}""";
        var data = new DataDictionary { { "@request", json } };

        // Act
        var result = data.GetValue<RequestInfo>("@request", _jsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("POST", result.HttpMethod);
        Assert.Equal("/api/events", result.Path);
    }

    [Fact]
    public void GetValue_JsonStringWithEnvironmentInfo_ReturnsTypedEnvironmentInfo()
    {
        // Arrange
        /* language=json */
        const string json = """{"machine_name":"STRING-MACHINE","processor_count":16}""";
        var data = new DataDictionary { { "@environment", json } };

        // Act
        var result = data.GetValue<EnvironmentInfo>("@environment", _jsonOptions);

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
        var result = data.GetValue<SimpleError>("@simple_error", _jsonOptions);

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
        var result = data.GetValue<Error>("@error", _jsonOptions);

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
        var result = data.GetValue<UserInfo>("text", _jsonOptions);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetValue_MissingKey_ThrowsKeyNotFoundException()
    {
        // Arrange
        var data = new DataDictionary();

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => data.GetValue<UserInfo>("nonexistent", _jsonOptions));
    }

    [Fact]
    public void GetValue_NullValue_ReturnsNull()
    {
        // Arrange
        var data = new DataDictionary { { "nullable", null! } };

        // Act
        var result = data.GetValue<UserInfo>("nullable", _jsonOptions);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetValue_IncompatibleType_ReturnsNull()
    {
        // Arrange
        var data = new DataDictionary { { "number", 42 } };

        // Act
        var result = data.GetValue<UserInfo>("number", _jsonOptions);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetValue_MalformedJsonString_ReturnsDefaultProperties()
    {
        // Arrange
        /* language=json */
        const string json = """{"foo":"bar"}""";
        var data = new DataDictionary { { "user", json } };

        // Act
        var result = data.GetValue<UserInfo>("user", _jsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Identity);
    }

    [Fact]
    public void Deserialize_DataDictionaryWithUserInfoAfterRoundTrip_PreservesTypedData()
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
        var userInfo = deserialized.GetValue<UserInfo>("@user", _jsonOptions);
        Assert.NotNull(userInfo);
        Assert.Equal("user@test.com", userInfo.Identity);
        Assert.Equal("Test User", userInfo.Name);
    }

    [Fact]
    public void Deserialize_DataDictionaryWithMixedTypesAfterRoundTrip_PreservesAllTypes()
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
    public void Deserialize_EmptyDataDictionaryAfterRoundTrip_PreservesEmptyState()
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

    [Fact]
    public void Deserialize_UserInfoAfterRoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new UserInfo("stj@test.com", "STJ Test User");
        var data = new DataDictionary { { "@user", original } };

        // Act
        string? json = _serializer.SerializeToString(data);
        var deserialized = _serializer.Deserialize<DataDictionary>(json);

        // Assert
        Assert.NotNull(deserialized);
        var result = deserialized.GetValue<UserInfo>("@user", _jsonOptions);
        Assert.NotNull(result);
        Assert.Equal("stj@test.com", result.Identity);
        Assert.Equal("STJ Test User", result.Name);
    }

    [Fact]
    public void Deserialize_ErrorAfterRoundTrip_PreservesComplexStructure()
    {
        // Arrange
        var original = new Error
        {
            Message = "Test Exception",
            Type = "System.InvalidOperationException",
            Code = "ERR001",
            StackTrace =
            [
                new StackFrame
                {
                    Name = "TestMethod",
                    DeclaringNamespace = "TestNamespace",
                    DeclaringType = "TestClass",
                    LineNumber = 42
                }
            ]
        };
        var data = new DataDictionary { { "@error", original } };

        // Act
        string? json = _serializer.SerializeToString(data);
        var deserialized = _serializer.Deserialize<DataDictionary>(json);

        // Assert
        Assert.NotNull(deserialized);
        var result = deserialized.GetValue<Error>("@error", _jsonOptions);
        Assert.NotNull(result);
        Assert.Equal("Test Exception", result.Message);
        Assert.Equal("System.InvalidOperationException", result.Type);
        Assert.Equal("ERR001", result.Code);
        Assert.NotNull(result.StackTrace);
        Assert.Single(result.StackTrace);
        Assert.Equal("TestMethod", result.StackTrace[0].Name);
        Assert.Equal(42, result.StackTrace[0].LineNumber);
    }

    [Fact]
    public void Deserialize_RequestInfoAfterRoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new RequestInfo
        {
            HttpMethod = "POST",
            Path = "/api/events",
            Host = "api.example.com",
            Port = 443,
            IsSecure = true,
            ClientIpAddress = "192.168.1.1"
        };
        var data = new DataDictionary { { "@request", original } };

        // Act
        string? json = _serializer.SerializeToString(data);
        var deserialized = _serializer.Deserialize<DataDictionary>(json);

        // Assert
        Assert.NotNull(deserialized);
        var result = deserialized.GetValue<RequestInfo>("@request", _jsonOptions);
        Assert.NotNull(result);
        Assert.Equal("POST", result.HttpMethod);
        Assert.Equal("/api/events", result.Path);
        Assert.Equal("api.example.com", result.Host);
        Assert.Equal(443, result.Port);
        Assert.True(result.IsSecure);
        Assert.Equal("192.168.1.1", result.ClientIpAddress);
    }

    [Fact]
    public void Deserialize_EnvironmentInfoAfterRoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new EnvironmentInfo
        {
            MachineName = "TEST-MACHINE",
            ProcessorCount = 16,
            TotalPhysicalMemory = 32000000000L,
            OSName = "Windows",
            OSVersion = "10.0.19041"
        };
        var data = new DataDictionary { { "@environment", original } };

        // Act
        string? json = _serializer.SerializeToString(data);
        var deserialized = _serializer.Deserialize<DataDictionary>(json);

        // Assert
        Assert.NotNull(deserialized);
        var result = deserialized.GetValue<EnvironmentInfo>("@environment", _jsonOptions);
        Assert.NotNull(result);
        Assert.Equal("TEST-MACHINE", result.MachineName);
        Assert.Equal(16, result.ProcessorCount);
        Assert.Equal(32000000000L, result.TotalPhysicalMemory);
        Assert.Equal("Windows", result.OSName);
    }

    [Fact]
    public void Deserialize_NestedErrorAfterRoundTrip_PreservesInnerError()
    {
        // Arrange
        var original = new Error
        {
            Message = "Outer exception",
            Type = "OuterException",
            Inner = new InnerError
            {
                Message = "Inner exception",
                Type = "InnerException"
            }
        };
        var data = new DataDictionary { { "@error", original } };

        // Act
        string? json = _serializer.SerializeToString(data);
        var deserialized = _serializer.Deserialize<DataDictionary>(json);

        // Assert
        Assert.NotNull(deserialized);
        var result = deserialized.GetValue<Error>("@error", _jsonOptions);
        Assert.NotNull(result);
        Assert.Equal("Outer exception", result.Message);
        Assert.NotNull(result.Inner);
        Assert.Equal("Inner exception", result.Inner.Message);
        Assert.Equal("InnerException", result.Inner.Type);
    }

    [Fact]
    public void Deserialize_MixedDataTypesAfterRoundTrip_PreservesAllTypes()
    {
        // Arrange
        var data = new DataDictionary
        {
            { "@user", new UserInfo("user@test.com", "Test") },
            { "@version", "1.0.0" },
            { "count", 42 },
            { "enabled", true }
        };

        // Act
        string? json = _serializer.SerializeToString(data);
        var deserialized = _serializer.Deserialize<DataDictionary>(json);

        // Assert
        Assert.NotNull(deserialized);

        var userInfo = deserialized.GetValue<UserInfo>("@user", _jsonOptions);
        Assert.NotNull(userInfo);
        Assert.Equal("user@test.com", userInfo.Identity);

        Assert.Equal("1.0.0", deserialized["@version"]);
        Assert.Equal(42L, deserialized["count"]);
        Assert.Equal(true, deserialized["enabled"]);
    }

    [Fact]
    public void Deserialize_NestedDataDictionaryAfterRoundTrip_PreservesNestedData()
    {
        // Arrange
        var original = new UserInfo("user@test.com", "Test User")
        {
            Data = new DataDictionary
            {
                { "custom_field", "custom_value" },
                { "score", 100 }
            }
        };
        var data = new DataDictionary { { "@user", original } };

        // Act
        string? json = _serializer.SerializeToString(data);
        var deserialized = _serializer.Deserialize<DataDictionary>(json);

        // Assert
        Assert.NotNull(deserialized);
        var result = deserialized.GetValue<UserInfo>("@user", _jsonOptions);
        Assert.NotNull(result);
        Assert.Equal("user@test.com", result.Identity);
        Assert.NotNull(result.Data);
        Assert.Equal("custom_value", result.Data["custom_field"]);
        Assert.Equal(100L, result.Data["score"]);
    }

    [Fact]
    public void GetValue_DictionaryOfStringObject_DeserializesToTypedObject()
    {
        // Arrange - Simulates what ObjectToInferredTypesConverter produces after deserialization
        var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { "identity", "dict@test.com" },
            { "name", "Dictionary User" }
        };
        var data = new DataDictionary { { "@user", dictionary } };

        // Act
        var result = data.GetValue<UserInfo>("@user", _jsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("dict@test.com", result.Identity);
        Assert.Equal("Dictionary User", result.Name);
    }

    [Fact]
    public void GetValue_ListOfObjects_DeserializesToTypedCollection()
    {
        // Arrange - Simulates array from ObjectToInferredTypesConverter
        var list = new List<object?>
        {
            new Dictionary<string, object?> { { "name", "Frame1" }, { "line_number", 10L } },
            new Dictionary<string, object?> { { "name", "Frame2" }, { "line_number", 20L } }
        };
        var data = new DataDictionary { { "frames", list } };

        // Act
        var result = data.GetValue<List<StackFrame>>("frames", _jsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Frame1", result[0].Name);
        Assert.Equal(10, result[0].LineNumber);
        Assert.Equal("Frame2", result[1].Name);
        Assert.Equal(20, result[1].LineNumber);
    }
}
