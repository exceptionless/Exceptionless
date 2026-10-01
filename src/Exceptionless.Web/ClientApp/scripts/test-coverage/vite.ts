import type { SourceCoverageData } from '@vitest/istanbul-lib-instrument';
import type { Plugin } from 'vite';

import { createInstrumenter } from '@vitest/istanbul-lib-instrument';
import MagicString from 'magic-string';
import { relative } from 'node:path';

import { browserSource } from './policy.ts';

// Use the same pinned instrumenter/options as Vitest. The Vite adapter is small
// enough to explicitly handle Svelte and reject CSS/SSR virtual modules.
export function frontendCoverage(onCoverage?: (file: SourceCoverageData) => void): Plugin {
    let root: string;
    const instrumenter = createInstrumenter({
        autoWrap: false,
        compact: false,
        coverageGlobalScope: 'globalThis',
        coverageGlobalScopeFunc: false,
        coverageVariable: '__coverage__',
        esModules: true,
        ignoreLines: true,
        produceSourceMap: true
    });

    return {
        apply: 'serve',
        configResolved(config) {
            root = config.root;
        },
        enforce: 'post',
        name: 'exceptionless-frontend-coverage',
        transform(code, id, options) {
            if (options?.ssr || id.includes('?') || !browserSource(relative(root, id).replaceAll('\\', '/'))) {
                return;
            }

            const sourceMap = this.getCombinedSourcemap();
            if (sourceMap.version !== 3) throw new Error(`Unsupported coverage source map: ${id}`);
            const instrumented = instrumenter.instrumentSync(code, id, { ...sourceMap, version: 3 });
            const file = instrumenter.lastFileCoverage();
            if (file) onCoverage?.(file);
            // Vite composes the returned map with the preceding transforms.
            // As in Vitest, return an instrumented-to-JavaScript map here; the
            // counters themselves retain the original Svelte/TypeScript map.
            const identity = new MagicString(code).generateMap({ hires: true, includeContent: true, source: id });
            instrumenter.instrumentSync(code, id, { ...identity, version: 3 });
            const map = instrumenter.lastSourceMap();
            if (!map) throw new Error(`Missing instrumented source map: ${id}`);
            return { code: instrumented, map: JSON.stringify(map) };
        }
    };
}
