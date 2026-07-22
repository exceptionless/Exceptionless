import { render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import SourceMapsPage from './+page.svelte';

vi.mock('$app/paths', () => ({
    resolve: (path: string) => path
}));

vi.mock('$app/state', () => ({
    page: { params: { projectId: 'project-id' } }
}));

vi.mock('$features/projects/api.svelte', () => ({
    deleteSourceMapMutation: () => ({ isPending: false, mutateAsync: vi.fn() }),
    getSourceMapsQuery: () => ({ data: [], isError: false, isLoading: false }),
    postSourceMapMutation: () => ({ mutateAsync: vi.fn() })
}));

describe('Source Maps page', () => {
    it('renders the source map upload form', () => {
        render(SourceMapsPage);

        expect(screen.getByRole('heading', { name: 'Upload Source Map' })).toBeTruthy();
        expect(screen.getByLabelText('Source map').getAttribute('type')).toBe('file');
    });
});
