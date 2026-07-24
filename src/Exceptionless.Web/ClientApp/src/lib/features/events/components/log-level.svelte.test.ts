import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import LogLevel from './log-level.svelte';

describe('LogLevel', () => {
    it.each([
        ['debug', ['dark:border-white/20', 'dark:bg-white/5', 'dark:text-zinc-200']],
        ['error', ['dark:border-red-400/30', 'dark:bg-red-500/10', 'dark:text-red-300']],
        ['fatal', ['dark:border-red-400/30', 'dark:bg-red-500/10', 'dark:text-red-300']],
        ['info', ['dark:border-green-400/30', 'dark:bg-green-500/10', 'dark:text-green-200']],
        ['warn', ['dark:border-yellow-400/30', 'dark:bg-yellow-500/10', 'dark:text-yellow-200']]
    ])('renders %s as a compact tag with restrained dark-theme colors', (level, darkThemeClasses) => {
        render(LogLevel, { level });

        const badge = screen.getByText(level);
        expect(badge.classList).toContain('w-12');
        expect(badge.classList).toContain('rounded-md');
        expect(badge.classList).toContain('border');
        expect(badge.classList).toContain('border-current/20');
        darkThemeClasses.forEach((className) => expect(badge.classList).toContain(className));
    });
});
