using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Reflection.Emit;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Results;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class AutoValidationEndpointFilterTests
{
    [Fact]
    public async Task InvokeAsync_InvalidSemanticValue_ReturnsUnprocessableEntityValidationProblem()
    {
        // Arrange
        var filter = new AutoValidationEndpointFilter();
        var httpContext = new DefaultHttpContext();
        var context = new TestEndpointFilterInvocationContext(httpContext, [new RequestWithSemanticValidation { DisplayName = "x" }]);
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("next");
        });

        // Assert
        Assert.False(nextCalled);
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(valueResult.Value);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, statusCodeResult.StatusCode);
        Assert.True(problemDetails.Errors.TryGetValue("display_name", out var displayNameErrors));
        Assert.Contains(displayNameErrors, error => error.Contains("minimum length of 3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvokeAsync_MissingRequiredProperty_ReturnsUnprocessableEntityValidationProblem()
    {
        // Arrange
        var filter = new AutoValidationEndpointFilter();
        var httpContext = new DefaultHttpContext();
        var context = new TestEndpointFilterInvocationContext(httpContext, [new RequestWithValidationMetadata()]);
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("next");
        });

        // Assert
        Assert.False(nextCalled);
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(valueResult.Value);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, statusCodeResult.StatusCode);
        Assert.True(problemDetails.Errors.TryGetValue("display_name", out var displayNameErrors));
        Assert.Contains(displayNameErrors, error => error == "The DisplayName field is required.");
    }

    [Fact]
    public async Task InvokeAsync_ServiceArgumentWithoutValidationMetadata_CallsNext()
    {
        // Arrange
        var filter = new AutoValidationEndpointFilter();
        var context = new TestEndpointFilterInvocationContext(
            new DefaultHttpContext(),
            [null, "text", 42, new ServiceWithoutValidationMetadata()]);
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("next");
        });

        // Assert
        Assert.True(nextCalled);
        Assert.Equal("next", result);
    }

    [Fact]
    public async Task InvokeAsync_ValidModel_CallsNext()
    {
        // Arrange
        var filter = new AutoValidationEndpointFilter();
        var context = new TestEndpointFilterInvocationContext(new DefaultHttpContext(), [new RequestWithValidationMetadata { DisplayName = "Valid" }]);
        var expectedResult = new object();
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(context, invocationContext =>
        {
            nextCalled = true;
            Assert.Same(context, invocationContext);
            return ValueTask.FromResult<object?>(expectedResult);
        });

        // Assert
        Assert.True(nextCalled);
        Assert.Same(expectedResult, result);
    }

    [Fact]
    public void ShouldValidate_ModelImplementingValidatableObject_ReturnsTrue()
    {
        // Arrange
        var type = typeof(ValidatableRequest);

        // Act
        bool result = ShouldValidate(type);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldValidate_ModelWithTypeValidationMetadata_ReturnsTrue()
    {
        // Arrange
        var type = typeof(RequestWithTypeValidationMetadata);

        // Act
        bool result = ShouldValidate(type);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldValidate_ModelWithValidationMetadata_ReturnsTrue()
    {
        // Arrange
        var type = typeof(RequestWithValidationMetadata);

        // Act
        bool result = ShouldValidate(type);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(Uri))]
    [InlineData(typeof(IMediator))]
    [InlineData(typeof(DefaultHttpContext))]
    [InlineData(typeof(AbstractService))]
    [InlineData(typeof(ApiResultMapper))]
    [InlineData(typeof(ServiceWithoutValidationMetadata))]
    public void ShouldValidate_ServiceArgumentWithoutValidationMetadata_ReturnsFalse(Type type)
    {
        // Arrange is provided by InlineData.

        // Act
        bool result = ShouldValidate(type);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldValidate_TypeWithoutNamespace_ReturnsFalse()
    {
        // Arrange
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("AutoValidationEndpointFilterTestsDynamic"), AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("Main");
        var type = moduleBuilder.DefineType("TypeWithoutNamespace", TypeAttributes.Public | TypeAttributes.Class).CreateType();

        // Act
        bool result = ShouldValidate(type);

        // Assert
        Assert.Null(type.Namespace);
        Assert.False(result);
    }

    private static bool ShouldValidate(Type type)
    {
        var method = typeof(AutoValidationEndpointFilter).GetMethod("ShouldValidate", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return (bool)method.Invoke(null, [type])!;
    }

    private sealed class RequestWithValidationMetadata
    {
        [Required]
        public string? DisplayName { get; set; }
    }

    private sealed class RequestWithSemanticValidation
    {
        [StringLength(10, MinimumLength = 3)]
        public string? DisplayName { get; set; }
    }

    [TypeValidation]
    private sealed class RequestWithTypeValidationMetadata;

    [AttributeUsage(AttributeTargets.Class)]
    private sealed class TypeValidationAttribute : ValidationAttribute;

    private abstract class AbstractService
    {
        [Required]
        public string? Name { get; set; }
    }

    private sealed class ValidatableRequest : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            yield break;
        }
    }

    private sealed class ServiceWithoutValidationMetadata
    {
        public string Name { get; set; } = String.Empty;
    }

    private sealed class TestEndpointFilterInvocationContext(HttpContext httpContext, IList<object?> arguments) : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext { get; } = httpContext;

        public override IList<object?> Arguments { get; } = arguments;

        public override T GetArgument<T>(int index) => (T)Arguments[index]!;
    }
}
