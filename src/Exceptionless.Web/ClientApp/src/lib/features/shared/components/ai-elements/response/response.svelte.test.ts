import { render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

vi.mock('katex/dist/katex.min.css', () => ({}));

import Response from './response.svelte';

describe('Response', () => {
    it('renders a compact responsive table without a separate controls row', () => {
        const { container } = render(Response, {
            props: {
                content: `| Status | Meaning | Recommended action |
| --- | --- | --- |
| Open | Still occurring | Investigate the latest event |
| Fixed | Resolved in a release | Confirm the deployed version |
| Discarded | Intentionally ignored | Revisit if impact changes |`
            }
        });

        const table = screen.getByRole('table');
        expect(table.textContent).toContain('Recommended action');
        expect(table.className).toContain('table-auto');
        expect(table.className).not.toContain('min-w-[28rem]');
        expect(screen.getByRole('columnheader', { name: 'Recommended action' }).className).toContain('whitespace-normal');
        expect(screen.getByRole('cell', { name: 'Investigate the latest event' }).className).toContain('text-xs');
        expect(container.querySelector('[data-streamdown="table-toolbar"]')).toBeNull();
        const tableWrapper = container.querySelector('[data-streamdown="table-wrapper"]');
        const tableContainer = container.querySelector('[data-streamdown-table]');
        expect(tableWrapper?.className.split(/\s+/)).toEqual(expect.arrayContaining(['border-0', 'p-0', 'shadow-none']));
        expect(tableWrapper?.className.split(/\s+/)).not.toContain('border');
        expect(tableContainer?.className.split(/\s+/)).toContain('border');
        expect(container.firstElementChild?.className).toContain('w-full');
        expect(container.firstElementChild?.className).not.toContain('size-full');
    });
});
