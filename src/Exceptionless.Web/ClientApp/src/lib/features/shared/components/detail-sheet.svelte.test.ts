import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { shallowRouting } from '../shallow-routing.test-fake.svelte';
import DetailSheetTestHarness from './detail-sheet.test-harness.svelte';

const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }));

vi.mock('svelte-sonner', () => ({ toast }));
vi.mock('$app/env', () => ({ browser: true }));
vi.mock('$app/navigation', async () => {
    const { shallowRouting } = await import('../shallow-routing.test-fake.svelte');
    return { beforeNavigate: shallowRouting.beforeNavigate, goto: shallowRouting.goto };
});
vi.mock('$app/state', async () => {
    const { shallowRouting } = await import('../shallow-routing.test-fake.svelte');
    return { navigating: shallowRouting.navigating, page: shallowRouting.page };
});

const { goto } = shallowRouting;
const detailSheetHistoryStateKey = '__exceptionlessDetailSheet';
const listUrl = '/events?filter=open';

function getWrittenUrl(call: number): string {
    const url = new URL(goto.mock.calls[call - 1]![0], window.location.origin);
    return `${url.pathname}${url.search}`;
}

async function openDetails(): Promise<void> {
    await fireEvent.click(screen.getByRole('button', { name: 'Open details' }));
    await shallowRouting.flush();
}

describe('DetailSheet history', () => {
    const writeText = vi.fn(() => Promise.resolve());

    beforeEach(() => {
        toast.error.mockClear();
        toast.success.mockClear();
        writeText.mockClear();
        Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });
        shallowRouting.reset(listUrl);
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

    it('adds a same-page history entry when details open', async () => {
        render(DetailSheetTestHarness);

        await openDetails();

        expect(goto).toHaveBeenCalledOnce();
        expect(getWrittenUrl(1)).toBe(listUrl);
        expect(goto.mock.calls[0]![1]).toEqual({
            persistState: true,
            replace: false,
            shallow: true,
            state: { [detailSheetHistoryStateKey]: { key: 'event', value: 'abc123' } }
        });
        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('open:abc123');
        expect(document.querySelector('[data-slot="sheet-content"]')?.classList.contains('w-[calc(100%-1rem)]!')).toBe(true);
    });

    it('copies the absolute full-page details link', async () => {
        render(DetailSheetTestHarness);
        await openDetails();

        await fireEvent.click(screen.getByRole('button', { name: 'Copy event link' }));

        expect(writeText).toHaveBeenCalledWith(new URL('/event/abc123', window.location.href).href);
        expect(toast.success).toHaveBeenCalledWith('Event link copied');
    });

    it('closes details during Back navigation without traversing twice', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        render(DetailSheetTestHarness);
        await openDetails();

        await shallowRouting.traverse(listUrl);

        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('closed');
        expect(historyBack).not.toHaveBeenCalled();
    });

    it('restores the same details during Forward navigation without pushing another entry', async () => {
        render(DetailSheetTestHarness);
        await openDetails();
        const detailState = shallowRouting.page.state;

        await shallowRouting.traverse(listUrl);
        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('closed');

        await shallowRouting.traverse(listUrl, detailState);

        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('open:abc123');
        expect(goto).toHaveBeenCalledOnce();
    });

    it('restores details when returning to a sheet history entry after the component remounts', async () => {
        const firstRender = render(DetailSheetTestHarness);
        await openDetails();
        const detailState = shallowRouting.page.state;
        firstRender.unmount();

        shallowRouting.visit(listUrl, detailState);
        render(DetailSheetTestHarness);
        await shallowRouting.flush();

        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('open:abc123');
        expect(goto).toHaveBeenCalledOnce();
    });

    it('lets shallow history writes from other components through while it owns an entry', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        render(DetailSheetTestHarness);
        await openDetails();

        await goto('/events?filter=regressed', { replace: true, shallow: true, state: shallowRouting.page.state });
        await shallowRouting.flush();

        expect(historyBack).not.toHaveBeenCalled();
        expect(window.location.search).toBe('?filter=regressed');
        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('open:abc123');
    });

    it('consumes the sheet entry before continuing normal navigation', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        render(DetailSheetTestHarness);
        await openDetails();

        const cancel = shallowRouting.beforeLeaving('link', '/event/abc123');

        expect(cancel).toHaveBeenCalledOnce();
        expect(historyBack).toHaveBeenCalledOnce();

        await shallowRouting.traverse(listUrl);

        await waitFor(() => expect(goto).toHaveBeenLastCalledWith(new URL('/event/abc123', window.location.origin)));
        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('closed');
    });

    it('consumes the sheet entry when a parent close immediately starts navigation', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        render(DetailSheetTestHarness);
        await openDetails();

        await fireEvent.click(screen.getByRole('button', { name: 'Close details externally' }));
        const cancel = shallowRouting.beforeLeaving('goto', '/events?stack=def456');

        expect(cancel).toHaveBeenCalledOnce();
        expect(historyBack).toHaveBeenCalledOnce();

        await shallowRouting.traverse(listUrl);

        await waitFor(() => expect(goto).toHaveBeenLastCalledWith(new URL('/events?stack=def456', window.location.origin)));
        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('closed');
    });

    it('preserves SvelteKit navigation options without replaying link click handlers', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        render(DetailSheetTestHarness);
        await openDetails();
        const link = screen.getByRole('link', { name: 'Navigate with link options' }) as HTMLAnchorElement;
        const preventNavigation = (event: MouseEvent) => event.preventDefault();
        link.addEventListener('click', preventNavigation);
        link.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
        const linkClick = vi.spyOn(link, 'click');

        const cancel = shallowRouting.beforeLeaving('link', link.href);
        expect(cancel).toHaveBeenCalledOnce();
        expect(historyBack).toHaveBeenCalledOnce();

        await shallowRouting.traverse(listUrl);

        await waitFor(() => expect(goto).toHaveBeenLastCalledWith(new URL(link.href), { replace: undefined, reset: false }));
        expect(link.dataset.sveltekitReset).toBe('false');
        expect(linkClick).not.toHaveBeenCalled();
        link.removeEventListener('click', preventNavigation);
    });

    it('consumes its same-page history entry when the sheet requests close', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        render(DetailSheetTestHarness);
        await openDetails();

        await fireEvent.click(screen.getByRole('button', { name: 'Close' }));
        await shallowRouting.flush();

        expect(historyBack).toHaveBeenCalledOnce();
        expect(screen.getByTestId('detail-sheet-state').textContent).toBe('closed');
    });

    it('consumes a same-page history entry when details close externally', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        render(DetailSheetTestHarness);
        await openDetails();

        await fireEvent.click(screen.getByRole('button', { name: 'Close details externally' }));

        await waitFor(() => expect(historyBack).toHaveBeenCalledOnce());
        expect(goto).toHaveBeenCalledOnce();
    });

    it('preserves a newer URL when details close externally after a query update', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        render(DetailSheetTestHarness);
        await openDetails();
        window.history.replaceState(window.history.state, '', '/events?filter=updated');

        await fireEvent.click(screen.getByRole('button', { name: 'Close details externally' }));

        await waitFor(() => expect(goto).toHaveBeenCalledTimes(2));
        expect(goto.mock.calls[1]![1]).toEqual({ persistState: true, replace: true, shallow: true, state: {} });
        expect(historyBack).not.toHaveBeenCalled();
        expect(window.location.pathname + window.location.search).toBe('/events?filter=updated');
        expect(shallowRouting.historyEntryState()).not.toHaveProperty(detailSheetHistoryStateKey);
    });

    it('updates the sheet history entry when navigating within details', async () => {
        render(DetailSheetTestHarness);
        await openDetails();

        await fireEvent.click(screen.getByRole('button', { name: 'Navigate within details' }));
        await shallowRouting.flush();

        expect(goto).toHaveBeenCalledTimes(2);
        expect(goto.mock.calls[1]![1]).toMatchObject({ replace: true, shallow: true });
        expect(shallowRouting.historyEntryState()).toEqual({ [detailSheetHistoryStateKey]: { key: 'event', value: 'def456' } });
    });

    it('preserves a newer query history entry when the sheet requests close after the URL changes', async () => {
        const historyBack = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
        render(DetailSheetTestHarness);
        await openDetails();
        window.history.pushState({}, '', '/events?filter=regressed');

        await fireEvent.click(screen.getByRole('button', { name: 'Close' }));
        await shallowRouting.flush();

        expect(historyBack).not.toHaveBeenCalled();
        expect(goto).toHaveBeenCalledOnce();
    });
});
