<script module>
    import { defineMeta } from '@storybook/addon-svelte-csf';
    import { expect, userEvent, waitFor, within } from '@storybook/test';

    import Page from './Page.svelte';

    // More on how to set up stories at: https://storybook.js.org/docs/writing-stories
    const { Story } = defineMeta({
        component: Page,
        parameters: {
            // More on how to position stories at: https://storybook.js.org/docs/configure/story-layout
            layout: 'fullscreen'
        },
        title: 'Example/Page'
    });
</script>

<Story
    name="Logged In"
    play={async ({ canvasElement }) => {
        const canvas = within(canvasElement);
        const loginButton = canvas.getByRole('button', { name: /Log in/i });
        await expect(loginButton).toBeInTheDocument();
        await userEvent.click(loginButton);
        await waitFor(() => expect(loginButton).not.toBeInTheDocument());

        const logoutButton = canvas.getByRole('button', { name: /Log out/i });
        await expect(logoutButton).toBeInTheDocument();
    }}
/>

<Story name="Logged Out" />
