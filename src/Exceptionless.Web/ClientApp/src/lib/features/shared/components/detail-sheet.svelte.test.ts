import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { tick } from 'svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import DetailSheetTestHarness from './detail-sheet.test-harness.svelte';

const navigation = vi.hoisted(() => ({
    beforeNavigate: vi.fn(),
    goto: vi.fn(),
    pushState: vi.fn(),
    replaceState: vi.fn()
}));
const pageState = vi.hoisted(() => ({}));

function createSvelteKitHistoryState(state: App.PageState): Record<string, App.PageState> {
    return { 'sveltekit:states': state };
}

vi.mock('$app/environment', () => ({ browser: true }));
vi.mock('$app/navigation', () => navigation);
vi.mock('$app/state', () => ({
    page: {
        state: pageState,
        get url() {
            return new URL(window.location.href);
        }
    }
}));

describe('DetailSheet history', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        window.history.replaceState(createSvelteKitHistoryState(pageState), '', '/events?filter=open');
        navigation.goto.mockResolvedValue(undefined);
        navigation.pushState.mockImplementation((url: string | URL, state: App.PageState) =>
            window.history.pushState(createSvelteKitHistoryState(state), '', url)
        );
        navigation.replaceState.mockImplementation((url: string | URL, state: App.PageState) =>
            window.history.replaceState(createSvelteKitHistoryState(state), '', url)
        );
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

    it('adds a same-page history entry when details open', async () => {
        render(DetailSheetTestHarness);

        await fireEvent.click(screen.getByRole('button', { name: 'Open details' }));
        await tick();

        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.pushState).toHaveBeenCalledWith('/events?filter=open', {
            __exceptionlessDetailSheet: { key: 'event', value: 'abc123' }
        });
        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('open:abc123');
    });

    it('closes details during Back navigation without traversing twice', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        render(DetailSheetTestHarness);
        const originalState = window.history.state;
        await fireEvent.click(screen.getByRole('button', { name: 'Open details' }));
        await tick();

        window.history.replaceState(originalState, '', '/events?filter=open');
        window.dispatchEvent(new PopStateEvent('popstate', { state: originalState }));
        await tick();

        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('closed');
        expect(historyBack).not.toHaveBeenCalled();
    });

    it('restores the same details during Forward navigation without pushing another entry', async () => {
        render(DetailSheetTestHarness);
        const originalState = window.history.state;
        await fireEvent.click(screen.getByRole('button', { name: 'Open details' }));
        await tick();
        const detailState = window.history.state;

        window.history.replaceState(originalState, '', '/events?filter=open');
        window.dispatchEvent(new PopStateEvent('popstate', { state: originalState }));
        await tick();
        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('closed');

        window.history.replaceState(detailState, '', '/events?filter=open');
        window.dispatchEvent(new PopStateEvent('popstate', { state: detailState }));
        await tick();

        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('open:abc123');
        expect(navigation.pushState).toHaveBeenCalledOnce();
    });

    it('restores details when returning to a sheet history entry after the component remounts', async () => {
        const firstRender = render(DetailSheetTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'Open details' }));
        await tick();
        const detailState = window.history.state;
        firstRender.unmount();

        window.history.replaceState(detailState, '', '/events?filter=open');
        render(DetailSheetTestHarness);
        await tick();

        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('open:abc123');
        expect(navigation.pushState).toHaveBeenCalledOnce();
    });

    it('consumes the sheet entry before continuing normal navigation', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        const cancel = vi.fn();
        render(DetailSheetTestHarness);
        const originalState = window.history.state;
        await fireEvent.click(screen.getByRole('button', { name: 'Open details' }));
        await tick();
        const beforeNavigation = navigation.beforeNavigate.mock.calls[0]?.[0] as
            ((navigation: { cancel: () => void; to: { url: URL }; type: string }) => void) | undefined;

        const destination = new URL('/event/abc123', window.location.origin);
        beforeNavigation?.({ cancel, to: { url: destination }, type: 'link' });

        expect(cancel).toHaveBeenCalledOnce();
        expect(historyBack).toHaveBeenCalledOnce();

        window.history.replaceState(originalState, '', '/events?filter=open');
        window.dispatchEvent(new PopStateEvent('popstate', { state: originalState }));
        await tick();

        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('closed');
        await waitFor(() => expect(navigation.goto).toHaveBeenCalledWith(destination));
    });

    it('consumes its same-page history entry when the sheet requests close', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        render(DetailSheetTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'Open details' }));
        await tick();

        await fireEvent.click(screen.getByRole('button', { name: 'Close' }));
        await tick();

        expect(historyBack).toHaveBeenCalledOnce();
        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('closed');
    });

    it('does not traverse history when details close externally', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        render(DetailSheetTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'Open details' }));
        await tick();

        await fireEvent.click(screen.getByRole('button', { name: 'Close details externally' }));
        await tick();

        expect(historyBack).not.toHaveBeenCalled();
        expect(navigation.replaceState).toHaveBeenCalledOnce();
        expect(window.history.state['sveltekit:states'].__exceptionlessDetailSheet).toBeUndefined();
    });

    it('updates the sheet history entry when navigating within details', async () => {
        render(DetailSheetTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'Open details' }));
        await tick();

        await fireEvent.click(screen.getByRole('button', { name: 'Navigate within details' }));
        await tick();

        expect(navigation.replaceState).toHaveBeenCalledOnce();
        expect(window.history.state['sveltekit:states'].__exceptionlessDetailSheet).toEqual({ key: 'event', value: 'def456' });
    });

    it('preserves a newer query history entry when the sheet requests close after the URL changes', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        render(DetailSheetTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'Open details' }));
        await tick();
        window.history.pushState({}, '', '/events?filter=regressed');

        await fireEvent.click(screen.getByRole('button', { name: 'Close' }));
        await tick();

        expect(historyBack).not.toHaveBeenCalled();
    });
});
