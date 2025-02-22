<script module>
    import { defineMeta } from '@storybook/addon-svelte-csf';
    import { fn } from '@storybook/test';

    import Header from './Header.svelte';

    // More on how to set up stories at: https://storybook.js.org/docs/writing-stories
    const { Story } = defineMeta({
        args: {
            onCreateAccount: fn(),
            onLogin: fn(),
            onLogout: fn()
        },
        component: Header,
        parameters: {
            // More on how to position stories at: https://storybook.js.org/docs/configure/story-layout
            layout: 'fullscreen'
        },
        // This component will have an automatically generated Autodocs entry: https://storybook.js.org/docs/writing-docs/autodocs
        tags: ['autodocs'],
        title: 'Example/Header'
    });
</script>

<Story name="Logged In" args={{ user: { name: 'Jane Doe' } }} />

<Story name="Logged Out" />
