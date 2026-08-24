import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

vi.mock('../api.svelte', () => ({
    deleteStack: vi.fn(() => ({ mutateAsync: vi.fn() })),
    postChangeStatus: vi.fn(() => ({ mutateAsync: vi.fn() })),
    postMarkFixed: vi.fn(() => ({ mutateAsync: vi.fn() })),
    postMarkSnoozed: vi.fn(() => ({ mutateAsync: vi.fn() }))
}));

import StacksBulkActionsDropdownMenu from './stacks-bulk-actions-dropdown-menu.svelte';

describe('StacksBulkActionsDropdownMenu', () => {
    afterEach(() => cleanup());

    it('requires a selected stack before opening bulk actions', async () => {
        const table = {
            getSelectedRowModel: () => ({ flatRows: [] }),
            resetRowSelection: vi.fn()
        } as never;

        render(StacksBulkActionsDropdownMenu, { props: { table } });

        const trigger = screen.getByRole('button', { name: /Bulk Actions/ }) as HTMLButtonElement;
        expect(trigger.disabled).toBe(false);
        expect(trigger.getAttribute('aria-disabled')).toBe('true');
        expect(trigger.title).toBe('Select one or more stacks to use bulk actions');
        expect(document.getElementById(trigger.getAttribute('aria-describedby')!)?.textContent).toBe('Select one or more stacks to use bulk actions.');
        trigger.focus();
        expect(document.activeElement).toBe(trigger);
        await fireEvent.click(trigger);
        expect(screen.queryByRole('menuitem', { name: 'Mark Open' })).toBeNull();
    });

    it('enables bulk actions when a stack is selected', () => {
        const table = {
            getSelectedRowModel: () => ({ flatRows: [{ id: 'stack-id' }] }),
            resetRowSelection: vi.fn()
        } as never;

        render(StacksBulkActionsDropdownMenu, { props: { table } });

        const trigger = screen.getByRole('button', { name: /Bulk Actions/ }) as HTMLButtonElement;
        expect(trigger.disabled).toBe(false);
        expect(trigger.title).toBe('Bulk Actions');
    });
});
