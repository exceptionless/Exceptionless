import type { Preview } from '@storybook/svelte-vite';

const preview: Preview = {
    parameters: {
        layout: 'fullscreen',
        backgrounds: {
            default: 'light',
            values: [
                { name: 'light', value: '#f7f7f7' },
                { name: 'white', value: '#ffffff' }
            ]
        }
    }
};

export default preview;
