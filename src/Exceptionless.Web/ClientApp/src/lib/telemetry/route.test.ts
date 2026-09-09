import { beforeEach, describe, expect, it, vi } from 'vitest';

const paths = vi.hoisted(() => ({ base: '/next' }));

vi.mock('$app/paths', () => ({ resolve: (path: string) => `${paths.base}${path}` }));

import { normalizePath } from './route';

describe('normalizePath', () => {
    beforeEach(() => {
        paths.base = '/next';
    });

    it.each(['/next', ''])('normalizes telemetry under the app base %j', (base) => {
        paths.base = base;

        expect(normalizePath(`${base}/stack/507f1f77bcf86cd799439011/`)).toBe('/stack/:id');
        expect(normalizePath(`${base}/event/123/`)).toBe('/event/:id');
        expect(normalizePath(`${base}/event/550e8400-e29b-41d4-a716-446655440000`)).toBe('/event/:id');
        expect(normalizePath(`${base}/`)).toBe('/');
    });

    it('does not strip a partial base segment', () => {
        expect(normalizePath('/nextdoor/event/123')).toBe('/nextdoor/event/:id');
        expect(normalizePath('/next')).toBe('/');
    });

    it('preserves support for an explicit normalization base', () => {
        expect(normalizePath('/custom/event/123', '/custom')).toBe('/event/:id');
        expect(normalizePath('/next/event/123', '')).toBe('/next/event/:id');
    });
});
