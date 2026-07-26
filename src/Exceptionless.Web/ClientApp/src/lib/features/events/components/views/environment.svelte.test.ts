import { render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

vi.mock('$features/events/components/filters', () => ({ StringTrigger: null }));

import type { PersistentEvent } from '../../models';

import Environment from './environment.svelte';

describe('Environment', () => {
    it('shows memory values with the legacy byte formatting', () => {
        const event = {
            data: {
                '@environment': {
                    available_physical_memory: 11 * 1024 ** 3,
                    process_memory_size: 193.5 * 1024 ** 2,
                    total_physical_memory: 16 * 1024 ** 3
                }
            }
        } as PersistentEvent;

        render(Environment, { event, filterChanged: vi.fn() });

        expect(screen.getByText('Total Memory')).toBeTruthy();
        expect(screen.getByText('17 GB')).toBeTruthy();
        expect(screen.getByText('Available Memory')).toBeTruthy();
        expect(screen.getByText('12 GB')).toBeTruthy();
        expect(screen.getByText('Process Memory')).toBeTruthy();
        expect(screen.getByText('203 MB')).toBeTruthy();
    });
});
