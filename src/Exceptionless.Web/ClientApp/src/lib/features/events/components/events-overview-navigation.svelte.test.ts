import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { fromStore, writable } from 'svelte/store';
import { afterEach, expect, it, vi } from 'vitest';

import type { PersistentEvent } from '../models';

import EventsOverview from './events-overview.svelte';

const mocks = vi.hoisted(() => ({ getEventWithNavigationQuery: vi.fn() }));

vi.mock('$env/dynamic/public', () => ({ env: {} }));
vi.mock('$features/events/api.svelte', () => ({ getEventWithNavigationQuery: mocks.getEventWithNavigationQuery }));
vi.mock('$features/projects/api.svelte', () => ({ getProjectQuery: () => ({ isPending: false }), updateProject: () => ({}) }));
vi.mock('$features/organizations/api.svelte', () => ({ getOrganizationQuery: () => ({ isSuccess: true }) }));
vi.mock('$comp/formatters/date-time.svelte', () => ({ default: () => ({}) }));
vi.mock('$comp/formatters/time-ago.svelte', () => ({ default: () => ({}) }));
vi.mock('./tours/investigation-detail.svelte', () => ({ default: () => ({}) }));
vi.mock('./views/overview.svelte', () => ({ default: () => ({}) }));

afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
});

it.each(['0000000000000000000000aa', '0000000000000000000000AA'])(
    'waits for the selected event before using retained neighbour links (%s)',
    async (initialId) => {
        vi.stubGlobal(
            'ResizeObserver',
            class {
                public disconnect() {}
                public observe() {}
            }
        );
        const result = (id: string, previousId: string, nextId: string) => ({
            event: { data: {}, id } as PersistentEvent,
            navigation: { nextId, previousId }
        });
        const eventState = writable(result('0000000000000000000000aa', '0000000000000000000000bb', '0000000000000000000000cc'));
        const eventQuery = fromStore(eventState);
        mocks.getEventWithNavigationQuery.mockReturnValue({
            get data() {
                return eventQuery.current;
            }
        });
        const onNavigate = vi.fn();
        const props = { filterChanged: vi.fn(), handleError: vi.fn(), id: initialId, onNavigate };
        const { rerender } = render(EventsOverview, props);
        const older = screen.getByRole('button', { name: 'Older event' });
        const newer = screen.getByRole('button', { name: 'Newer event' });

        await fireEvent.click(older);
        expect(onNavigate).toHaveBeenLastCalledWith('0000000000000000000000bb');
        onNavigate.mockClear();
        await rerender({ ...props, id: '0000000000000000000000bb' });

        // keepPreviousData retains the first event while the selected event's transport is pending.
        // Its next link points past the first event, which must not be usable from the selected event.
        expect((newer as HTMLButtonElement).disabled).toBe(true);
        expect((older as HTMLButtonElement).disabled).toBe(true);
        await fireEvent.click(newer);
        expect(onNavigate).not.toHaveBeenCalled();

        eventState.set(result('0000000000000000000000bb', '0000000000000000000000dd', '0000000000000000000000aa'));
        await waitFor(() => expect((newer as HTMLButtonElement).disabled).toBe(false));
        await fireEvent.click(newer);
        expect(onNavigate).toHaveBeenLastCalledWith('0000000000000000000000aa');
        onNavigate.mockClear();
        await fireEvent.click(older);
        expect(onNavigate).toHaveBeenLastCalledWith('0000000000000000000000dd');
    }
);
