import { afterEach, describe, expect, it } from 'vitest';

import { installSvelteEffectDepthDiagnostics } from './svelte-effect-depth-diagnostics';

describe('Svelte effect-depth diagnostics', () => {
    afterEach(() => {
        delete globalThis.__exceptionlessSvelteEffectDepthDiagnostics;
    });

    it('attaches repeated state-write locations to the Svelte error', () => {
        installSvelteEffectDepthDiagnostics();
        const diagnostics = globalThis.__exceptionlessSvelteEffectDepthDiagnostics!;
        const source = {};

        diagnostics.startFlush();
        for (let index = 0; index < 8; index++) {
            diagnostics.recordStateUpdate(source);
        }

        const error = new Error('effect_update_depth_exceeded');
        diagnostics.attachEffectDepthError(error, {
            f: 4,
            fn: function effectOwner() {},
            parent: { f: 8, fn: function parentOwner() {}, parent: null }
        });

        expect(error).toHaveProperty('svelte_effect_depth');
        expect(error.stack).toContain('Reactive state update call sites:');
        expect(error.stack).toContain('recordStateUpdate');
        expect(Object.keys(error)).toContain('svelte_effect_depth');

        const attached = (error as Error & { svelte_effect_depth: { effectChain: { functionName?: string }[]; stateSources: { count: number }[] } })
            .svelte_effect_depth;
        expect(attached.effectChain.map((entry) => entry.functionName)).toEqual(['effectOwner', 'parentOwner']);
        expect(attached.stateSources).toEqual([expect.objectContaining({ count: 8 })]);
    });

    it('clears state-write data after the flush finishes', () => {
        installSvelteEffectDepthDiagnostics();
        const diagnostics = globalThis.__exceptionlessSvelteEffectDepthDiagnostics!;
        const source = {};

        diagnostics.startFlush();
        for (let index = 0; index < 8; index++) {
            diagnostics.recordStateUpdate(source);
        }

        diagnostics.endFlush();

        const error = new Error('effect_update_depth_exceeded');
        diagnostics.attachEffectDepthError(error, null);

        expect(error).not.toHaveProperty('svelte_effect_depth');
    });

    it('bounds captured stacks without recording state values', () => {
        installSvelteEffectDepthDiagnostics();
        const diagnostics = globalThis.__exceptionlessSvelteEffectDepthDiagnostics!;
        const source = { v: 'secret-state-value' };

        diagnostics.startFlush();
        for (let index = 0; index < 1_500; index++) {
            diagnostics.recordStateUpdate(source);
        }

        const error = new Error('effect_update_depth_exceeded');
        diagnostics.attachEffectDepthError(error, null);

        const attached = (error as Error & { svelte_effect_depth: { stateSources: { stacks: { stack: string }[] }[]; totalStateUpdates: number } })
            .svelte_effect_depth;
        expect(attached.totalStateUpdates).toBe(1_500);
        expect(attached.stateSources[0]?.stacks.length).toBeLessThanOrEqual(4);
        expect(JSON.stringify(attached)).not.toContain('secret-state-value');
    });

    it('bounds state source collection during high-fan-out updates', () => {
        installSvelteEffectDepthDiagnostics();
        const diagnostics = globalThis.__exceptionlessSvelteEffectDepthDiagnostics!;

        diagnostics.startFlush();
        for (let index = 0; index < 100; index++) {
            diagnostics.recordStateUpdate({ index });
        }

        const overflowSource = {};
        for (let index = 0; index < 100; index++) {
            diagnostics.recordStateUpdate(overflowSource);
        }

        const error = new Error('effect_update_depth_exceeded');
        diagnostics.attachEffectDepthError(error, null);

        const attached = (error as Error & { svelte_effect_depth: { stateSources: { count: number }[]; totalStateUpdates: number } }).svelte_effect_depth;
        expect(attached.totalStateUpdates).toBe(200);
        expect(attached.stateSources).toHaveLength(8);
        expect(attached.stateSources.every((source) => source.count === 1)).toBe(true);
    });
});
