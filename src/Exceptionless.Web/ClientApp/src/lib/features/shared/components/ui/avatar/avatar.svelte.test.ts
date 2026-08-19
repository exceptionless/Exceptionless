import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import AvatarTestHarness from './avatar.test-harness.svelte';

describe('Avatar', () => {
    it('defaults to a circle and supports consistently square content', () => {
        render(AvatarTestHarness);

        const circle = screen.getByTestId('circle-avatar');
        const square = screen.getByTestId('square-avatar');

        expect(circle.dataset.shape).toBe('circle');
        expect(square.dataset.shape).toBe('square');
        expect(square.classList).toContain('data-[shape=square]:after:rounded-lg');
        expect(square.querySelector('[data-slot="avatar-image"]')?.classList).toContain('group-data-[shape=square]/avatar:rounded-lg');
        expect(square.querySelector('[data-slot="avatar-fallback"]')?.classList).toContain('group-data-[shape=square]/avatar:rounded-lg');
    });
});
