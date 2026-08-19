import { fireEvent, render, screen } from '@testing-library/svelte';
import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest';

import OverlayInteractionTestHarness from './overlay-interaction.test-harness.svelte';

class ResizeObserverMock {
    disconnect() {}
    observe() {}
    unobserve() {}
}

describe('overlay interaction', () => {
    beforeAll(() => {
        vi.stubGlobal('ResizeObserver', ResizeObserverMock);
    });

    afterAll(() => {
        vi.unstubAllGlobals();
    });

    it('dismisses an open tooltip when a popover opens', async () => {
        render(OverlayInteractionTestHarness);

        const tagTrigger = screen.getByText('Tag');
        await fireEvent.pointerEnter(tagTrigger);
        await fireEvent.pointerMove(tagTrigger);
        expect(screen.getByTestId('tooltip-state').textContent).toBe('open');

        await fireEvent.click(screen.getByText('Filter'));

        expect(screen.getByTestId('popover-state').textContent).toBe('open');
        expect(screen.getByTestId('tooltip-state').textContent).toBe('closed');
    });

    it('keeps a suppressed tooltip closed while a popover is already open', async () => {
        render(OverlayInteractionTestHarness);

        await fireEvent.click(screen.getByText('Filter'));
        expect(screen.getByTestId('popover-state').textContent).toBe('open');

        const tagTrigger = screen.getByText('Tag');
        await fireEvent.pointerEnter(tagTrigger);
        await fireEvent.pointerMove(tagTrigger);

        expect(screen.getByTestId('tooltip-state').textContent).toBe('closed');
    });

    it('keeps a suppressed tooltip closed while a dropdown menu is already open', async () => {
        render(OverlayInteractionTestHarness);

        await fireEvent.click(screen.getByText('Views'));
        expect(screen.getByTestId('dropdown-state').textContent).toBe('open');

        const tagTrigger = screen.getByText('Tag');
        await fireEvent.pointerEnter(tagTrigger);
        await fireEvent.pointerMove(tagTrigger);

        expect(screen.getByTestId('tooltip-state').textContent).toBe('closed');
    });
});
