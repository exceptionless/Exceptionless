import { cleanup, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
    getProjectQuery: vi.fn()
}));

vi.mock('#features/projects/api.svelte.ts', () => ({
    getProjectQuery: mocks.getProjectQuery
}));

import EnvironmentTrigger from '../filters/environment-faceted-filter-trigger.svelte';
import EventEnvironmentCell from './event-environment-cell.svelte';

describe('environment display casing', () => {
    afterEach(() => {
        cleanup();
        vi.resetAllMocks();
    });

    it('shows the listed casing in the cell and in its filter title', () => {
        // Arrange
        mocks.getProjectQuery.mockReturnValue({ data: { environments: [{ name: 'Production', notify: true }] } });

        // Act
        render(EventEnvironmentCell, { changed: vi.fn(), environment: 'production', projectId: 'project-id' });

        // Assert
        const button = screen.getByRole('button', { name: 'Production' });
        expect(button.getAttribute('title')).toBe('Filter by environment: Production');
    });

    it('keeps the reported casing for an unlisted environment', () => {
        // Arrange
        mocks.getProjectQuery.mockReturnValue({ data: { environments: [] } });

        // Act
        render(EventEnvironmentCell, { changed: vi.fn(), environment: 'qa-West', projectId: 'project-id' });

        // Assert
        expect(screen.getByRole('button', { name: 'qa-West' }).getAttribute('title')).toBe('Filter by environment: qa-West');
    });

    it('titles the filter trigger with the label it is given', () => {
        // Act
        render(EnvironmentTrigger, { changed: vi.fn(), label: 'Production', value: 'production' });

        // Assert
        expect(screen.getByRole('button').getAttribute('title')).toBe('Filter by environment: Production');
    });
});
