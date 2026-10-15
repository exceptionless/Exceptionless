import type { HandleClientError } from '@sveltejs/kit/hooks';

import { beforeEach, describe, expect, it, vi } from 'vitest';

import { handleError } from './hooks.client';

const telemetry = vi.hoisted(() => {
    const builder = {
        setProperty: vi.fn(() => builder),
        setReferenceId: vi.fn(() => builder),
        submit: vi.fn(() => Promise.resolve())
    };

    return {
        builder,
        createException: vi.fn(() => builder),
        createNotFound: vi.fn(() => builder)
    };
});

vi.mock('$app/env', () => ({ browser: false, dev: false }));
vi.mock('#lib/telemetry/svelte-effect-depth-diagnostics.ts', () => ({ installSvelteEffectDepthDiagnostics: vi.fn() }));
vi.mock('@exceptionless/browser', () => ({
    Exceptionless: { createException: telemetry.createException, createNotFound: telemetry.createNotFound },
    guid: () => 'client-error-0001',
    toError: (error: unknown) => (error instanceof Error ? error : new Error(String(error)))
}));

type HandleErrorInput = Parameters<HandleClientError>[0];

const event = { params: {}, route: { id: '/(app)/stream' }, url: new URL('http://localhost/stream') } as HandleErrorInput['event'];

describe('handleError', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it.each<{ input: HandleErrorInput; name: string; submittedWith: typeof telemetry.createException }>([
        { input: { error: new Error('Render failed'), event, kind: 'unknown' }, name: 'an unexpected error', submittedWith: telemetry.createException },
        { input: { error: { message: 'Not Found', status: 404 }, event, kind: 'framework' }, name: 'a missing route', submittedWith: telemetry.createNotFound }
    ])('returns an error id at once for $name and tags the submitted event with it', ({ input, submittedWith }) => {
        // Act
        const result = handleError(input);

        // Assert
        expect(result).toEqual({ errorId: 'client-error-0001' });
        expect(submittedWith).toHaveBeenCalledOnce();
        expect(telemetry.builder.setReferenceId).toHaveBeenCalledExactlyOnceWith('client-error-0001');
        expect(telemetry.builder.submit).toHaveBeenCalledOnce();
    });

    it('returns expected errors without submitting them', () => {
        // Arrange
        const error = { message: 'The saved view could not be found.', status: 404 };

        // Act
        const result = handleError({ error, event, kind: 'app' });

        // Assert
        expect(result).toBe(error);
        expect(telemetry.builder.submit).not.toHaveBeenCalled();
    });
});
