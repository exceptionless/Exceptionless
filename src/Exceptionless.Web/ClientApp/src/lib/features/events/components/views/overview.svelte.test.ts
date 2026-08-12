import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

vi.mock('$env/dynamic/public', () => ({ env: {} }));

import { ReferenceFilter, SessionFilter } from '$features/events/components/filters';

import type { PersistentEvent } from '../../models';

import Overview from './overview.svelte';

describe('Overview', () => {
    it.each([
        ['Parent', 'reference', ReferenceFilter],
        ['SESSION', 'session', SessionFilter]
    ])('uses the built-in %s filter case-insensitively', async (referenceName, filterName, FilterType) => {
        const filterChanged = vi.fn();
        const event = {
            data: { [`@ref:${referenceName}`]: 'reference-id' }
        } as PersistentEvent;

        render(Overview, { event, filterChanged });
        await fireEvent.click(screen.getByTitle(`Filter ${filterName}:reference-id`));

        expect(filterChanged).toHaveBeenCalledOnce();
        expect(filterChanged).toHaveBeenCalledWith(expect.any(FilterType));
    });
});
