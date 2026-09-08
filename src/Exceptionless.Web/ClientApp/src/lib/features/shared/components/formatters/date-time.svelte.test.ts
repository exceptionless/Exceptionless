import { cleanup, render } from '@testing-library/svelte';
import { afterEach, describe, expect, it } from 'vitest';

import DateTime from './date-time.svelte';

afterEach(cleanup);

describe('DateTime', () => {
    const value = new Date('2026-08-01T00:00:00Z');

    it('preserves the existing default date and time format', () => {
        // Act
        const { container } = render(DateTime, { value });

        // Assert
        expect(container.textContent).toBe(
            value.toLocaleString(undefined, {
                day: 'numeric',
                hour: 'numeric',
                hour12: true,
                minute: '2-digit',
                month: 'short',
                second: '2-digit',
                year: 'numeric'
            })
        );
    });

    it('supports an explicit timezone for aggregation bucket labels', () => {
        // Arrange
        const options: Intl.DateTimeFormatOptions = { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' };

        // Act
        const { container } = render(DateTime, { options, value });

        // Assert
        expect(container.textContent).toBe(value.toLocaleString(undefined, options));
    });
});
