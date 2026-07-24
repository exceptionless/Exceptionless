import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import LogLevel from './log-level.svelte';

describe('LogLevel', () => {
    it.each(['debug', 'error', 'info', 'warn'])('renders %s with the shared tag shape and a visible border', (level) => {
        render(LogLevel, { level });

        const badge = screen.getByText(level);
        expect(badge.classList).toContain('rounded-md');
        expect(badge.classList).toContain('border');
        expect(badge.classList).toContain('border-current/20');
        expect(badge.classList).toContain('dark:border-current/40');
    });
});
