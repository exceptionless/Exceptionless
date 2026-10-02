import type { SourceCoverageData } from '@vitest/istanbul-lib-instrument';
import type { Plugin } from 'vite';

import { transformSync } from '@babel/core';
import { defaultOpts, programVisitor } from '@vitest/istanbul-lib-instrument';
import { relative } from 'node:path';

import { browserSource } from './policy.ts';

// Use the same pinned instrumenter/options as Vitest. The Vite adapter is small
// enough to explicitly handle Svelte and reject CSS/SSR virtual modules.
export function frontendCoverage(onCoverage?: (file: SourceCoverageData) => void): Plugin {
    let root: string;

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
            let file: SourceCoverageData | undefined;
            const instrumented = transformSync(code, {
                ast: false,
                babelrc: false,
                comments: defaultOpts.preserveComments,
                compact: false,
                configFile: false,
                filename: id,
                // Vite composes this generated-to-input map with prior transforms.
                // Only the coverage visitor needs the original source map. Keeping
                // them separate avoids parsing and instrumenting every module twice.
                inputSourceMap: false,
                parserOpts: { allowReturnOutsideFunction: false, plugins: defaultOpts.parserPlugins, sourceType: 'module' },
                plugins: [
                    ({ types }) => {
                        const visitor = programVisitor(types, id, {
                            coverageGlobalScope: 'globalThis',
                            coverageGlobalScopeFunc: false,
                            coverageVariable: '__coverage__',
                            ignoreLines: true,
                            inputSourceMap: { ...sourceMap, version: 3 }
                        });
                        return {
                            visitor: {
                                Program: {
                                    enter: visitor.enter,
                                    exit(path) {
                                        file = visitor.exit(path)?.fileCoverage;
                                    }
                                }
                            }
                        };
                    }
                ],
                sourceMaps: true
            });
            if (!instrumented?.code || !instrumented.map || !file) throw new Error(`Missing instrumented code or source map: ${id}`);
            onCoverage?.(file);
            return { code: instrumented.code, map: JSON.stringify(instrumented.map) };
        }
    };
}
