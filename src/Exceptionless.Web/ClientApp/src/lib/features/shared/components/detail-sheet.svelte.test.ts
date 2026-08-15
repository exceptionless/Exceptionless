import { fireEvent, render, screen } from '@testing-library/svelte';
import { tick } from 'svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import DetailSheetTestHarness from './detail-sheet.test-harness.svelte';

const navigation = vi.hoisted(() => ({
    pushState: vi.fn()
}));
const pageState = vi.hoisted(() => ({}));

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
        window.history.replaceState({}, '', '/events?filter=open');
        navigation.pushState.mockImplementation((url: string | URL, state: App.PageState) => window.history.pushState(state, '', url));
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

    it('adds a same-page history entry when details open', async () => {
        render(DetailSheetTestHarness);

        await fireEvent.click(screen.getByRole('button', { name: 'Open details' }));
        await tick();

        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.pushState).toHaveBeenCalledWith('/events?filter=open', pageState);
        expect(screen.getByText('Event details')).toBeTruthy();
    });

    it('closes details during Back navigation without traversing twice', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        render(DetailSheetTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'Open details' }));
        await tick();

        window.dispatchEvent(new PopStateEvent('popstate'));
        await tick();

        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('closed');
        expect(historyBack).not.toHaveBeenCalled();
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
