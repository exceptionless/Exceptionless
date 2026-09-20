import { render } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import DateTime from './date-time.svelte';

describe('DateTime', () => {
    it('renders midnight with seconds and the requested timezone', () => {
        // Arrange
        const value = new Date('2026-01-02T00:00:00Z');

        // Act
        const { container } = render(DateTime, {
            formatOptions: { timeZone: 'UTC', timeZoneName: 'short' },
            value
        });
        const text = container.textContent?.trim() ?? '';

        // Assert
        expect(text).toContain('Jan 2, 2026');
        expect(text).toContain('12:00:00 AM');
        expect(text).toContain('UTC');
    });

    it('preserves the local day boundary for a requested timezone', () => {
        // Arrange
        const value = new Date('2026-01-02T00:04:05Z');

        // Act
        const { container } = render(DateTime, {
            formatOptions: { timeZone: 'America/Los_Angeles', timeZoneName: 'short' },
            value
        });
        const text = container.textContent?.trim() ?? '';

        // Assert
        expect(text).toContain('Jan 1, 2026');
        expect(text).toContain('4:04:05 PM');
        expect(text).toContain('PST');
    });

    it('renders the previous calendar day across a timezone boundary', () => {
        // Arrange
        const value = new Date('2026-01-02T00:04:05Z');

        // Act
        const { container } = render(DateTime, {
            formatOptions: { timeZone: 'America/Los_Angeles', timeZoneName: 'short' },
            value
        });

        // Assert
        expect(container.textContent).toContain('Jan 1, 2026');
        expect(container.textContent).toContain('4:04:05 PM');
        expect(container.textContent).toContain('PST');
    });

    it.each([undefined, 'not a timestamp'] as const)('omits missing and invalid value %s', (value) => {
        // Arrange
        const props = { value };

        // Act
        const { container } = render(DateTime, props);

        // Assert
        expect(container.textContent).toBe('');
    });
});
