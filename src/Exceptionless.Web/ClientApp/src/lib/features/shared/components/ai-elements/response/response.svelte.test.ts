import { render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

vi.mock('katex/dist/katex.min.css', () => ({}));

import Response from './response.svelte';

describe('Response', () => {
    it('renders a compact scrollable table with copy and fullscreen controls', () => {
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
        expect(table.className).toContain('min-w-[28rem]');
        expect(container.querySelector('[data-streamdown="table-wrapper"]')?.className).toContain('rounded-xl');
        expect(container.querySelector('[data-streamdown="table-toolbar"]')?.querySelectorAll('button')).toHaveLength(2);
        expect(container.firstElementChild?.className).toContain('w-full');
        expect(container.firstElementChild?.className).not.toContain('size-full');
    });
});
