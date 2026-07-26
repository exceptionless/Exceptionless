import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import TagList from './tag-list.svelte';

describe('TagList', () => {
    it('renders neutral tag badges without filter icons and summarizes overflow', () => {
        const { container } = render(TagList, {
            maxVisible: 2,
            tags: ['api', 'production', 'critical', 'customer']
        });

        expect(screen.getByText('api')).toBeTruthy();
        expect(screen.getByText('production')).toBeTruthy();
        expect(screen.getByText('+2')).toBeTruthy();
        expect(screen.queryByText('critical')).toBeNull();
        expect(screen.getByLabelText('Tags: api, production, critical, customer').getAttribute('title')).toBe('api, production, critical, customer');
        expect(container.querySelector('svg')).toBeNull();

        for (const badge of container.querySelectorAll('[data-slot="badge"]')) {
            expect(badge.classList).toContain('border-border');
            expect(badge.classList).toContain('dark:border-muted-foreground/50');
            expect(badge.classList).toContain('bg-muted');
            expect(badge.classList).toContain('text-muted-foreground');
            expect(badge.classList).toContain('rounded-md');
        }
    });

    it('filters when a tag is clicked', async () => {
        const onTagClick = vi.fn();
        render(TagList, { onTagClick, tags: ['api'] });

        await fireEvent.click(screen.getByRole('button', { name: 'api' }));

        expect(onTagClick).toHaveBeenCalledOnce();
        expect(onTagClick).toHaveBeenCalledWith('api');
    });

    it('shows an empty value when there are no tags', () => {
        render(TagList, { tags: [] });

        expect(screen.getByText('—')).toBeTruthy();
    });
});
