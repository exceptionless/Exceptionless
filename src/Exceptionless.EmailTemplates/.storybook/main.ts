import type { StorybookConfig } from '@storybook/svelte-vite';
import { resolve } from 'path';
import { fileURLToPath } from 'url';

const __dirname = fileURLToPath(new URL('.', import.meta.url));

const config: StorybookConfig = {
    addons: ['@storybook/addon-svelte-csf', '@storybook/addon-docs'],
    framework: '@storybook/svelte-vite',
    stories: ['../src/**/*.stories.@(js|ts|svelte)'],
    docs: {},
    async viteFinal(config) {
        // Allow Vite to read HTML template files from the sibling Core project
        config.server = config.server ?? {};
        config.server.fs = {
            allow: [resolve(__dirname, '..'), resolve(__dirname, '../../Exceptionless.Core/Mail/Templates')]
        };
        return config;
    }
};

export default config;
