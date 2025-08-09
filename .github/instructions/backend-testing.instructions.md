---
description: "C# backend testing guidelines"
applyTo: "tests/**/*.cs"
---

# Backend Testing Guidelines (C#)

## Framework & Best Practices

- Follow Microsoft's [unit testing best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices).
- Use xUnit as the primary testing framework.

## Test Principles

- **Fast & Isolated**: Tests should execute quickly and not depend on external factors or the order of execution.
- **Repeatable & Self-Checking**: Tests must be consistent and validate their own outcomes without manual checks.
- **Timely**: Write tests alongside your code to ensure relevance and improve design.

## Test Structure & Naming

- Write complete, runnable tests—no placeholders or TODOs.
- Use clear, descriptive naming conventions for test methods:
    - `MethodName_StateUnderTest_ExpectedBehavior`
- Follow AAA pattern (Arrange, Act, Assert).

## Test Organization

- Use `[Theory]` and `[InlineData]` for parameterized tests.
- Implement proper setup and teardown using constructors and `IDisposable`.

## Integration Testing

- Use the custom `AppWebHostFactory`, which inherits from `WebApplicationFactory<Startup>`, for integration tests. This factory manages the Aspire distributed application lifecycle.
- Inject `ITestOutputHelper` and `AppWebHostFactory` into the test class constructor to get access to the test output and the application factory.
- Isolate dependencies using test containers, in-memory providers, or stubs to ensure reliable test execution.
- Test the full request/response cycle, including model validation and HTTP status codes.
- Verify data persistence and side effects by inspecting the state of the database or other services after the test runs.
