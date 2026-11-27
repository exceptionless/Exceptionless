import { flushSync } from 'svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useFirstEventToast } from './first-event-toast.svelte';

const mocks = vi.hoisted(() => ({
    goto: vi.fn(),
    toastSuccess: vi.fn()
}));

vi.mock('$app/navigation', () => ({ goto: mocks.goto }));
vi.mock('svelte-sonner', () => ({ toast: { success: mocks.toastSuccess } }));

describe('useFirstEventToast', () => {
    let cleanup: (() => void) | undefined;
    const state = $state({
        awaiting: false,
        eventsHref: '/event' as string | undefined,
        onEmptyStatePage: true,
        organizationId: 'organization-id' as string | undefined,
        received: false
    });

    beforeEach(() => {
        mocks.goto.mockReset();
        mocks.toastSuccess.mockReset();
        Object.assign(state, { awaiting: false, eventsHref: '/event', onEmptyStatePage: true, organizationId: 'organization-id', received: false });
        cleanup = $effect.root(() => {
            useFirstEventToast({
                getEventsHref: () => state.eventsHref,
                getOrganizationId: () => state.organizationId,
                hasReceivedEvents: () => state.received,
                isAwaitingFirstEvent: () => state.awaiting,
                isOnEmptyStatePage: () => state.onEmptyStatePage
            });
        });
        flushSync();
    });

    afterEach(() => cleanup?.());

    function update(changes: Partial<typeof state>) {
        Object.assign(state, changes);
        flushSync();
    }

    it('celebrates when the first event arrives while the empty state is showing', () => {
        // Arrange
        update({ awaiting: true });

        // Act
        update({ awaiting: false, received: true });

        // Assert
        expect(mocks.toastSuccess).toHaveBeenCalledExactlyOnceWith('Your first event just landed!', expect.objectContaining({ action: expect.anything() }));
        mocks.toastSuccess.mock.calls[0]![1].action.onClick();
        expect(mocks.goto).toHaveBeenCalledWith('/event');
    });

    it('offers no action when the user is already on the events page', () => {
        // Arrange
        update({ awaiting: true, eventsHref: undefined });

        // Act
        update({ awaiting: false, received: true });

        // Assert
        expect(mocks.toastSuccess).toHaveBeenCalledExactlyOnceWith('Your first event just landed!', expect.objectContaining({ action: undefined }));
    });

    it('stays quiet when the page already had events', () => {
        // Arrange & Act
        update({ awaiting: false, received: true });
        update({ received: false });
        update({ received: true });

        // Assert
        expect(mocks.toastSuccess).not.toHaveBeenCalled();
    });

    it('stays quiet when the user leaves the empty state page before events arrive', () => {
        // Arrange
        update({ awaiting: true });
        update({ onEmptyStatePage: false });

        // Act
        update({ awaiting: false, onEmptyStatePage: true, received: true });

        // Assert
        expect(mocks.toastSuccess).not.toHaveBeenCalled();
    });

    it('stays quiet when switching to an organization that has events', () => {
        // Arrange
        update({ awaiting: true });

        // Act
        update({ awaiting: false, organizationId: 'other-organization-id', received: true });

        // Assert
        expect(mocks.toastSuccess).not.toHaveBeenCalled();
    });

    it('stays quiet when the empty state clears without events', () => {
        // Arrange
        update({ awaiting: true });

        // Act
        update({ awaiting: false });

        // Assert
        expect(mocks.toastSuccess).not.toHaveBeenCalled();
    });
});
