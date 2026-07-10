import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import StackSortHeader from './stack-sort-header.svelte';

describe('StackSortHeader', () => {
    it('exposes the active descending sort and handles selection', async () => {
        const onclick = vi.fn();
        render(StackSortHeader, { active: true, label: 'Events', onclick });

        const button = screen.getByRole('button', { name: 'Sort by Events descending' });
        expect(button.getAttribute('aria-pressed')).toBe('true');

        await fireEvent.click(button);

        expect(onclick).toHaveBeenCalledOnce();
    });

    it('does not mark inactive sort modes as selected', () => {
        render(StackSortHeader, { active: false, label: 'First', onclick: vi.fn() });

        expect(screen.getByRole('button', { name: 'Sort by First descending' }).getAttribute('aria-pressed')).toBe('false');
    });
});
