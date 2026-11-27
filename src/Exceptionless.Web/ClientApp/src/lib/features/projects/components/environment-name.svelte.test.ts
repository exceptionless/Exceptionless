import { cleanup, render } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
    getProjectQuery: vi.fn()
}));

vi.mock('#features/projects/api.svelte.ts', () => ({
    getProjectQuery: mocks.getProjectQuery
}));

import EnvironmentName from './environment-name.svelte';

describe('EnvironmentName', () => {
    afterEach(() => {
        cleanup();
        vi.resetAllMocks();
    });

    it.each([
        ['production', 'Production'],
        ['PRODUCTION', 'Production'],
        ['qa-West', 'qa-West']
    ])('shows %s as %s using the listed casing', (environment, expected) => {
        // Arrange
        mocks.getProjectQuery.mockReturnValue({ data: { environments: [{ name: 'Production', notify: true }] } });

        // Act
        const { container } = render(EnvironmentName, { environment, projectId: 'project-id' });

        // Assert
        expect(container.textContent?.trim()).toBe(expected);
    });

    it('shows the placeholder without loading the project when there is no environment', () => {
        // Arrange
        mocks.getProjectQuery.mockReturnValue({ data: undefined });

        // Act
        const { container } = render(EnvironmentName, { projectId: 'project-id' });
        const request = mocks.getProjectQuery.mock.calls[0]![0] as { route: { id: string | undefined } };

        // Assert
        expect(container.textContent?.trim()).toBe('Unspecified');
        expect(request.route.id).toBeUndefined();
    });

    it('keeps the event casing until the project has loaded', () => {
        // Arrange
        mocks.getProjectQuery.mockReturnValue({ data: undefined });

        // Act
        const { container } = render(EnvironmentName, { environment: 'production', projectId: 'project-id' });

        // Assert
        expect(container.textContent?.trim()).toBe('production');
    });
});
