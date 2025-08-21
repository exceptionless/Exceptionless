import type { StorybookConfig } from '@storybook/sveltekit';

const config: StorybookConfig = {
    addons: ['@storybook/addon-svelte-csf', '@chromatic-com/storybook', '@storybook/addon-a11y', '@storybook/addon-docs'],
    framework: '@storybook/sveltekit',
    stories: ['../src/**/*.mdx', '../src/**/*.stories.@(js|ts|svelte)']
};
export default config;
