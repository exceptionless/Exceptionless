import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import TagList from './tag-list.svelte';

describe('TagList', () => {
    it('renders colored tag badges without filter icons and summarizes overflow', () => {
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
