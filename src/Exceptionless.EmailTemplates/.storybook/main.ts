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
        // Allow Vite to read generated templates and preview assets from sibling projects.
        config.server = config.server ?? {};
        config.server.fs = {
            allow: [
                resolve(__dirname, '..'),
                resolve(__dirname, '../../Exceptionless.Core/Mail/Templates'),
                resolve(__dirname, '../../Exceptionless.Web/ClientApp.angular/img')
            ]
        };
        return config;
    }
};

export default config;
