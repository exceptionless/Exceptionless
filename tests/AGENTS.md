# Backend Testing Guidelines (C#)

## Running Tests

```bash
dotnet test
```

### Specific Tests

```bash
# By test name
dotnet test --filter "FullyQualifiedName~CanCreateOrganization"

# By class name
dotnet test --filter "ClassName~OrganizationTests"

# By category/trait
dotnet test --filter "Category=Integration"
```

## Test-First Workflow

1. **Search for existing tests**: Find tests covering the code you're modifying
2. **Extend existing test classes**: Add new `[Fact]` or `[Theory]` cases to existing files for maintainability
3. **Write the failing test first**: Verify it fails for the right reason
4. **Implement minimal code**: Just enough to pass the test
5. **Add edge case tests**: Null inputs, empty collections, boundary values
6. **Run full test suite**: Ensure no regressions

**Why extend existing tests?** Consolidates related test logic, reduces duplication, improves discoverability.

## Framework & Best Practices

- Use xUnit as the primary testing framework
- Follow Microsoft's [unit testing best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

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
