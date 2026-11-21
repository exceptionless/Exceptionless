import { render, screen, within } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import type { PersistentEvent } from '../../models';

import ErrorView from './error.svelte';

vi.mock('#features/events/components/filters/index.ts', () => ({ StringTrigger: null, VersionTrigger: null }));

describe('Error view', () => {
    it('renders every module when IDs repeat or are omitted', () => {
        // Arrange
        const event: PersistentEvent = {
            created_utc: '2026-01-01T00:00:00Z',
            data: {
                '@error': {
                    modules: [
                        { module_id: 3, name: 'Example.App', version: '1.0.0' },
                        { name: 'Example.Tools', version: '2.0.0' },
                        { module_id: 3, name: 'Example.App', version: '1.0.0' },
                        { module_id: 3, name: 'Example.App', version: '2.0.0' },
                        { name: 'Example.Worker', version: '1.0.0' }
                    ],
                    type: 'InvalidOperationException'
                }
            },
            date: '2026-01-01T00:00:00Z',
            id: 'event-id',
            is_first_occurrence: false,
            organization_id: 'organization-id',
            project_id: 'project-id',
            stack_id: 'stack-id',
            type: 'error'
        };

        // Act
        render(ErrorView, { event, filterChanged: vi.fn() });

        // Assert
        expect(screen.getByRole('heading', { name: 'Stack Trace' })).toBeVisible();
        const modules = screen.getByRole('columnheader', { name: 'Version' }).closest('table')!;
        const rows = within(modules)
            .getAllByRole('row')
            .slice(1)
            .map((row) =>
                within(row)
                    .getAllByRole('cell')
                    .map((cell) => cell.textContent)
            );
        expect(rows).toEqual([
            ['Example.App', '1.0.0'],
            ['Example.Tools', '2.0.0'],
            ['Example.App', '1.0.0'],
            ['Example.App', '2.0.0'],
            ['Example.Worker', '1.0.0']
        ]);
    });
});
