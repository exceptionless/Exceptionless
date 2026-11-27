import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }));
const mutations = vi.hoisted(() => ({ changeStatus: vi.fn(), markFixed: vi.fn(), snooze: vi.fn() }));

vi.mock('svelte-sonner', () => ({ toast }));

vi.mock('../api.svelte', () => ({
    postChangeStatus: vi.fn(() => ({ mutateAsync: mutations.changeStatus })),
    postMarkFixed: vi.fn(() => ({ mutateAsync: mutations.markFixed })),
    postMarkSnoozed: vi.fn(() => ({ mutateAsync: mutations.snooze }))
}));

import type { Stack } from '../models';

import { StackStatus } from '../models';
import StackStatusDropdownMenu from './stack-status-dropdown-menu.svelte';

const stack = { id: 'stack-id', status: StackStatus.Open } as Stack;

async function openSnoozeSubmenu() {
    await fireEvent.click(screen.getByRole('button', { name: 'Open' }));
    const trigger = screen.getByRole('menuitem', { name: 'Snoozed' });
    trigger.focus();
    await fireEvent.keyDown(trigger, { key: 'ArrowRight' });
}

describe('StackStatusDropdownMenu', () => {
    beforeEach(() => {
        Object.values(mutations).forEach((m) => m.mockReset());
        toast.error.mockClear();
    });

    afterEach(async () => {
        cleanup();
        // Bits UI defers body-scroll restoration by 24 ms after an overlay unmounts.
        await new Promise((resolve) => window.setTimeout(resolve, 30));
    });

    it('only opens the snooze durations when the submenu is opened with the keyboard', async () => {
        // Arrange
        render(StackStatusDropdownMenu, { props: { stack } });

        // Act
        await openSnoozeSubmenu();

        // Assert
        expect(await screen.findByRole('menuitem', { name: '6 Hours' })).toBeTruthy();
        expect(mutations.snooze).not.toHaveBeenCalled();
    });

    it('snoozes for the chosen duration', async () => {
        // Arrange
        render(StackStatusDropdownMenu, { props: { stack } });
        await openSnoozeSubmenu();

        // Act
        await fireEvent.click(await screen.findByRole('menuitem', { name: '6 Hours' }));

        // Assert
        await waitFor(() => expect(mutations.snooze).toHaveBeenCalledOnce());
        const snoozeUntilUtc = mutations.snooze.mock.calls[0]![0] as Date;
        const hours = (snoozeUntilUtc.getTime() - Date.now()) / 3600000;
        expect(hours).toBeGreaterThan(5.9);
        expect(hours).toBeLessThan(6.1);
    });
});
