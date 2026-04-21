---
name: frontend-testing
description: >
    Use this skill when writing or running frontend unit and component tests with Vitest and
    Testing Library. Covers render/screen/fireEvent patterns, vi.mock for mocking, and the
    AAA (Arrange-Act-Assert) test structure. Apply when adding test coverage for Svelte
    components, debugging test failures, or setting up test utilities.
---

# Frontend Testing

> **Documentation:** [vitest.dev](https://vitest.dev) | [testing-library.com](https://testing-library.com/docs/svelte-testing-library/intro)

## Framework & Location

- **Framework**: Vitest + @testing-library/svelte
- **Location**: Co-locate with code as `.test.ts` or `.spec.ts`
- **TDD workflow**: When fixing bugs or adding features, write a failing test first

## AAA Pattern

Use explicit Arrange, Act, Assert regions:

```typescript
import { describe, expect, it } from "vitest";

describe("Calculator", () => {
    it("should add two numbers correctly", () => {
        // Arrange
        const a = 5;
        const b = 3;

        // Act
        const result = add(a, b);

        // Assert
        expect(result).toBe(8);
    });
});
```

## Testing with Spies

```typescript
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CachedPersistedState } from "./cached-persisted-state.svelte";

describe("CachedPersistedState", () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it("should return cached value without reading storage repeatedly", () => {
        // Arrange
        const getItemSpy = vi.spyOn(Storage.prototype, "getItem");
        localStorage.setItem("test-key", "value1");
        const state = new CachedPersistedState("test-key", "default");
        getItemSpy.mockClear();

        // Act
        const val1 = state.current;
        const val2 = state.current;

        // Assert
        expect(val1).toBe("value1");
        expect(val2).toBe("value1");
        expect(getItemSpy).not.toHaveBeenCalled();
    });
});
```

## Query Selection Priority

Use accessible queries (not implementation details):

1. `screen.getByRole("button", { name: /submit/i })` — Role-based (preferred)
2. `screen.getByLabelText("Email address")` — Label-based
3. `screen.getByText("Welcome back")` — Text-based
4. `screen.getByTestId("complex-chart")` — Test ID (fallback)
5. ❌ `screen.getByClassName("btn-primary")` — Never use implementation details

## Mocking Modules

```typescript
import { vi, describe, it, beforeEach, expect } from "vitest";
import { render, screen } from "@testing-library/svelte";

vi.mock("$lib/api/organizations", () => ({
    getOrganizations: vi.fn(),
}));

import { getOrganizations } from "$lib/api/organizations";
import OrganizationList from "./organization-list.svelte";

describe("OrganizationList", () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it("displays organizations from API", async () => {
        // Arrange
        vi.mocked(getOrganizations).mockResolvedValue([{ id: "1", name: "Org One" }]);

        // Act
        render(OrganizationList);

        // Assert
        expect(await screen.findByText("Org One")).toBeInTheDocument();
    });
});
```
