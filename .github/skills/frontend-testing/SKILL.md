---
name: Frontend Testing
description: |
  Unit and component testing for the frontend with Vitest and Testing Library.
  Keywords: Vitest, @testing-library/svelte, component tests, vi.mock, render, screen,
  fireEvent, userEvent, test.ts, spec.ts, describe, it, AAA pattern
---

# Frontend Testing

> **Documentation:** [vitest.dev](https://vitest.dev) | [testing-library.com](https://testing-library.com/docs/svelte-testing-library/intro)

## Running Tests

```bash
npm run test:unit
```

## Framework & Location

- **Framework**: Vitest + @testing-library/svelte
- **Location**: Co-locate with code as `.test.ts` or `.spec.ts`
- **TDD workflow**: When fixing bugs or adding features, write a failing test first

## AAA Pattern

Use explicit Arrange, Act, Assert regions:

```typescript
import { describe, expect, it } from 'vitest';

describe('Calculator', () => {
    it('should add two numbers correctly', () => {
        // Arrange
        const a = 5;
        const b = 3;

        // Act
        const result = add(a, b);

        // Assert
        expect(result).toBe(8);
    });

    it('should handle negative numbers', () => {
        // Arrange
        const a = -5;
        const b = 3;

        // Act
        const result = add(a, b);

        // Assert
        expect(result).toBe(-2);
    });
});
```

## Test Patterns from Codebase

### Unit Tests with AAA

From [dates.test.ts](src/Exceptionless.Web/ClientApp/src/lib/features/shared/dates.test.ts):

```typescript
import { describe, expect, it } from 'vitest';
import { getDifferenceInSeconds, getRelativeTimeFormatUnit } from './dates';

describe('getDifferenceInSeconds', () => {
    it('should calculate difference in seconds correctly', () => {
        // Arrange
        const now = new Date();
        const past = new Date(now.getTime() - 5000);

        // Act
        const result = getDifferenceInSeconds(past);

        // Assert
        expect(result).toBeCloseTo(5, 0);
    });
});

describe('getRelativeTimeFormatUnit', () => {
    it('should return correct unit for given seconds', () => {
        // Arrange & Act & Assert (simple value tests)
        expect(getRelativeTimeFormatUnit(30)).toBe('seconds');
        expect(getRelativeTimeFormatUnit(1800)).toBe('minutes');
        expect(getRelativeTimeFormatUnit(7200)).toBe('hours');
    });

    it('should handle boundary cases correctly', () => {
        // Arrange & Act & Assert
        expect(getRelativeTimeFormatUnit(59)).toBe('seconds');
        expect(getRelativeTimeFormatUnit(60)).toBe('minutes');
    });
});
```

### Testing with Spies

From [cached-persisted-state.svelte.test.ts](src/Exceptionless.Web/ClientApp/src/lib/features/shared/utils/cached-persisted-state.svelte.test.ts):

```typescript
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CachedPersistedState } from './cached-persisted-state.svelte';

describe('CachedPersistedState', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('should initialize with default value when storage is empty', () => {
        // Arrange & Act
        const state = new CachedPersistedState('test-key', 'default');

        // Assert
        expect(state.current).toBe('default');
    });

    it('should return cached value without reading storage repeatedly', () => {
        // Arrange
        const getItemSpy = vi.spyOn(Storage.prototype, 'getItem');
        localStorage.setItem('test-key', 'value1');
        const state = new CachedPersistedState('test-key', 'default');
        getItemSpy.mockClear();

        // Act
        const val1 = state.current;
        const val2 = state.current;

        // Assert
        expect(val1).toBe('value1');
        expect(val2).toBe('value1');
        expect(getItemSpy).not.toHaveBeenCalled();
    });
});
```

### Testing String Transformations

From [helpers.svelte.test.ts](src/Exceptionless.Web/ClientApp/src/lib/features/events/components/filters/helpers.svelte.test.ts):

```typescript
import { describe, expect, it } from 'vitest';
import { quoteIfSpecialCharacters } from './helpers.svelte';

describe('helpers.svelte', () => {
    it('quoteIfSpecialCharacters handles tabs and newlines', () => {
        // Arrange & Act & Assert
        expect(quoteIfSpecialCharacters('foo\tbar')).toBe('"foo\tbar"');
        expect(quoteIfSpecialCharacters('foo\nbar')).toBe('"foo\nbar"');
    });

    it('quoteIfSpecialCharacters handles empty string and undefined/null', () => {
        // Arrange & Act & Assert
        expect(quoteIfSpecialCharacters('')).toBe('');
        expect(quoteIfSpecialCharacters(undefined)).toBeUndefined();
        expect(quoteIfSpecialCharacters(null)).toBeNull();
    });

    it('quoteIfSpecialCharacters quotes all Lucene special characters', () => {
        // Arrange
        const luceneSpecials = ['+', '-', '!', '(', ')', '{', '}', '[', ']', '^', '"', '~', '*', '?', ':', '\\', '/'];

        // Act & Assert
        for (const char of luceneSpecials) {
            expect(quoteIfSpecialCharacters(char)).toBe(`"${char}"`);
        }
    });
});
```

## Query Selection Priority

Use accessible queries (not implementation details):

```typescript
// ✅ Role-based
screen.getByRole('button', { name: /submit/i });
screen.getByRole('textbox', { name: /email/i });

// ✅ Label-based
screen.getByLabelText('Email address');

// ✅ Text-based
screen.getByText('Welcome back');

// ⚠️ Fallback: Test ID
screen.getByTestId('complex-chart');

// ❌ Avoid: Implementation details
screen.getByClassName('btn-primary');
```

## Mocking Modules

```typescript
import { vi, describe, it, beforeEach, expect } from 'vitest';
import { render, screen } from '@testing-library/svelte';

vi.mock('$lib/api/organizations', () => ({
    getOrganizations: vi.fn()
}));

import { getOrganizations } from '$lib/api/organizations';
import OrganizationList from './organization-list.svelte';

describe('OrganizationList', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('displays organizations from API', async () => {
        // Arrange
        const mockOrganizations = [{ id: '1', name: 'Org One' }];
        vi.mocked(getOrganizations).mockResolvedValue(mockOrganizations);

        // Act
        render(OrganizationList);

        // Assert
        expect(await screen.findByText('Org One')).toBeInTheDocument();
    });
});
```

## Snapshot Testing (Use Sparingly)

```typescript
it('matches snapshot', () => {
    // Arrange & Act
    const { container } = render(StaticComponent);

    // Assert
    expect(container).toMatchSnapshot();
});
```

Use snapshots only for stable, static components. Prefer explicit assertions for dynamic content.
