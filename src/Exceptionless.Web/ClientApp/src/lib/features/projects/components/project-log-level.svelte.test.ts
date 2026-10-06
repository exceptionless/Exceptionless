import { cleanup, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

vi.mock('$features/projects/api.svelte', () => ({
    deleteProjectConfig: vi.fn(() => ({ isPending: false, mutateAsync: vi.fn() })),
    getProjectConfig: vi.fn(() => ({ data: { settings: {} }, isSuccess: true })),
    postProjectConfig: vi.fn(() => ({ isPending: false, mutateAsync: vi.fn() }))
}));

import ProjectLogLevel from './project-log-level.svelte';

describe('ProjectLogLevel', () => {
    afterEach(() => cleanup());

    it('renders an icon-only trigger when requested', () => {
        render(ProjectLogLevel, { iconOnly: true, projectId: 'project-id', source: 'source' });

        const trigger = screen.getByRole('button', { name: 'Select a Default Log Level' });
        expect(trigger.classList.contains('size-8')).toBe(true);
        expect(trigger.title).toBe('Select a Default Log Level');
        expect(trigger.textContent?.trim()).toBe('');
        expect(trigger.querySelector('svg')).toBeTruthy();
    });

    it('keeps the descriptive trigger for project settings', () => {
        render(ProjectLogLevel, { projectId: 'project-id', source: '*' });

        const trigger = screen.getByRole('button', { name: 'Select a Default Log Level' });
        expect(trigger.classList.contains('size-8')).toBe(false);
        expect(trigger.textContent).toContain('Select a Default Log Level');
    });
});
