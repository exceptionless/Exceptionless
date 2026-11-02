import { cleanup, render, screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
    addEnvironment: vi.fn(),
    countData: undefined as unknown,
    project: undefined as unknown,
    updateSettings: vi.fn()
}));

vi.mock('$app/state', () => ({
    page: { params: { projectId: 'project-id' } }
}));

vi.mock('#features/events/api.svelte.ts', () => ({
    getProjectCountQuery: () => ({
        get data() {
            return mocks.countData;
        },
        isError: false,
        isLoading: false,
        isSuccess: true
    })
}));

vi.mock('#features/projects/api.svelte.ts', () => ({
    deleteProjectEnvironment: () => ({ isPending: false, mutateAsync: vi.fn() }),
    getProjectDiscardedEnvironments: () => ({
        data: { environments: [], other_events: 0, other_names: 0, other_names_capped: false, retention_days: 7 },
        isError: false,
        isLoading: false
    }),
    getProjectQuery: () => ({
        get data() {
            return mocks.project;
        },
        isError: false,
        isLoading: false
    }),
    patchProjectEnvironment: () => ({ isPending: false, mutateAsync: vi.fn() }),
    postProjectEnvironment: () => ({ isPending: false, mutateAsync: mocks.addEnvironment }),
    putProjectEnvironmentSettings: () => ({ isPending: false, mutateAsync: mocks.updateSettings })
}));

import EnvironmentsPage from './+page.svelte';

const project = (environments: { created_utc: string; name: string; notify: boolean }[]) => ({
    discard_events_without_environment: false,
    discard_unlisted_environments: false,
    environments,
    id: 'project-id',
    notify_other_environments: true
});

const observed = (...environments: { count: number; key: string }[]) => ({
    aggregations: {
        terms_environment: {
            items: environments.map((environment) => ({
                aggregations: { max_date: { value: '2026-11-02T10:00:00Z' } },
                key: environment.key,
                total: environment.count
            }))
        }
    }
});

describe('Environments page', () => {
    beforeEach(() => {
        mocks.addEnvironment.mockResolvedValue({});
        mocks.updateSettings.mockResolvedValue({});
        mocks.countData = observed();
        mocks.project = project([]);
    });

    afterEach(() => {
        cleanup();
        vi.clearAllMocks();
    });

    it('asks for confirmation before discarding unlisted environments when no environment is listed', async () => {
        // Arrange
        render(EnvironmentsPage);

        // Act
        await userEvent.click(screen.getByRole('switch', { name: 'Discard events from environments that are not listed' }));

        // Assert
        expect(await screen.findByText(/every event that has an environment will be discarded/)).toBeTruthy();
        expect(mocks.updateSettings).not.toHaveBeenCalled();
    });

    it('saves the setting once the warning is confirmed', async () => {
        // Arrange
        render(EnvironmentsPage);
        await userEvent.click(screen.getByRole('switch', { name: 'Discard events from environments that are not listed' }));

        // Act
        await userEvent.click(await screen.findByRole('button', { name: 'Discard Unlisted Environments' }));

        // Assert
        await waitFor(() => expect(mocks.updateSettings).toHaveBeenCalledWith({ discard_unlisted_environments: true }));
    });

    it('does not save the setting when the warning is cancelled and puts the switch back', async () => {
        // Arrange
        render(EnvironmentsPage);
        const discard = () => screen.getByRole('switch', { name: 'Discard events from environments that are not listed' });
        await userEvent.click(discard());

        // Act
        await userEvent.click(await screen.findByRole('button', { name: 'Cancel' }));

        // Assert
        await waitFor(() => expect(discard().getAttribute('aria-checked')).toBe('false'));
        expect(mocks.updateSettings).not.toHaveBeenCalled();
    });

    it('warns when none of the listed environments has had events recently', async () => {
        // Arrange
        mocks.project = project([{ created_utc: '2026-11-01T00:00:00Z', name: 'Production', notify: true }]);
        mocks.countData = observed({ count: 4, key: 'qa-west' });
        render(EnvironmentsPage);

        // Act
        await userEvent.click(screen.getByRole('switch', { name: 'Discard events from environments that are not listed' }));

        // Assert
        expect(await screen.findByText(/None of the listed environments has had events in the last 30 days/)).toBeTruthy();
        expect(mocks.updateSettings).not.toHaveBeenCalled();
    });

    it('saves immediately when a listed environment has recent events', async () => {
        // Arrange
        mocks.project = project([{ created_utc: '2026-11-01T00:00:00Z', name: 'Production', notify: true }]);
        mocks.countData = observed({ count: 4, key: 'production' });
        render(EnvironmentsPage);

        // Act
        await userEvent.click(screen.getByRole('switch', { name: 'Discard events from environments that are not listed' }));

        // Assert
        await waitFor(() => expect(mocks.updateSettings).toHaveBeenCalledWith({ discard_unlisted_environments: true }));
        expect(screen.queryByRole('button', { name: 'Discard Unlisted Environments' })).toBeNull();
    });

    it('fills the add form with the observed name so the casing can be confirmed instead of adding it', async () => {
        // Arrange
        mocks.countData = observed({ count: 4, key: 'qa-west' });
        render(EnvironmentsPage);

        // Act
        await userEvent.click(await screen.findByRole('button', { name: 'Add qa-west' }));

        // Assert
        await waitFor(() => expect((screen.getByLabelText('Environment name') as HTMLInputElement).value).toBe('qa-west'));
        expect(mocks.addEnvironment).not.toHaveBeenCalled();
    });
});
