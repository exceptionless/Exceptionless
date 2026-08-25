import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

describe('organization', () => {
    beforeEach(() => {
        localStorage.clear();
        vi.resetModules();
    });

    afterEach(() => {
        localStorage.clear();
    });

    it('current_WhenCleared_RemovesTheSelectedOrganization', async () => {
        // Arrange
        localStorage.setItem('organization', JSON.stringify('existing-organization'));
        const { organization } = await import('./context.svelte');
        expect(organization.current).toBe('existing-organization');

        // Act
        organization.current = undefined;

        // Assert
        expect(organization.current).toBeUndefined();
        expect(localStorage.getItem('organization')).toBe(JSON.stringify(''));
    });
});
