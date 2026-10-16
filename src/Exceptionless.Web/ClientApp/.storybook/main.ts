import type { StorybookConfig } from '@storybook/sveltekit';
import type { Plugin } from 'vite';

import { mergeConfig } from 'vite';

// SvelteKit resolves `$app/env/public` itself, ahead of any alias, so the mock replaces the
// module that the import resolves to.
function mockPublicEnvironment(): Plugin {
    const mock = new URL('./mocks/env.js', import.meta.url).pathname;
    return {
        enforce: 'pre',
        load: {
            filter: { id: /\/@sveltejs\/kit\/src\/runtime\/app\/env\/public\/index\.js(\?|$)/ },
            handler: () => `export * from ${JSON.stringify(mock)};`
        },
        name: 'exceptionless:storybook-public-env'
    };
}

const config: StorybookConfig = {
    addons: ['@storybook/addon-svelte-csf', '@chromatic-com/storybook', '@storybook/addon-a11y', '@storybook/addon-docs'],
    framework: '@storybook/sveltekit',
    stories: ['../src/**/*.mdx', '../src/**/*.stories.@(js|ts|svelte)'],

    async viteFinal(config, { configType }) {
        return mergeConfig(config, {
            // Storybook 10 removes SvelteKit's compile plugin, which defines the payload that
            // SvelteKit 3's `$app/env` and `$app/paths` read in builds. Storybook 11 defines it:
            // https://github.com/storybookjs/storybook/blob/next/code/frameworks/sveltekit/src/plugins/config-overrides.ts
            define: configType === 'PRODUCTION' ? { __SVELTEKIT_PAYLOAD__: JSON.stringify({}) } : {},
            envPrefix: ['PUBLIC_'],
            plugins: [mockPublicEnvironment()]
        });
    }
};
export default config;
