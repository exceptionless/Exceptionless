import type { StorybookConfig } from '@storybook/sveltekit';

const config: StorybookConfig = {
    addons: [
        '@storybook/addon-svelte-csf',
        '@storybook/addon-essentials',
        '@chromatic-com/storybook',
        '@storybook/addon-interactions',
        '@storybook/addon-a11y'
    ],
    framework: {
        name: '@storybook/sveltekit',
        options: {}
    },
    stories: ['../src/**/*.mdx', '../src/**/*.stories.@(js|ts|svelte)']
};
export default config;
