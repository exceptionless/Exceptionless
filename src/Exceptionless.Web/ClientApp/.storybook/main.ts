import type { StorybookConfig } from '@storybook/sveltekit';

import { mergeConfig } from 'vite';

const config: StorybookConfig = {
    addons: ['@storybook/addon-svelte-csf', '@chromatic-com/storybook', '@storybook/addon-a11y', '@storybook/addon-docs'],
    framework: '@storybook/sveltekit',
    stories: ['../src/**/*.mdx', '../src/**/*.stories.@(js|ts|svelte)'],

    async viteFinal(config) {
        return mergeConfig(config, {
            envPrefix: ['PUBLIC_'],
            resolve: {
                alias: {
                    // Mock SvelteKit's $env/dynamic/public for Storybook
                    '$env/dynamic/public': new URL('./mocks/env.js', import.meta.url).pathname
                }
            }
        });
    }
};
export default config;
