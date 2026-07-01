<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';
    import EmailPreview from './EmailPreview.svelte';
    import rawHtml from '../../../Exceptionless.Core/Mail/Templates/user-email-verify.html?raw';
    import { fillTokens, emailVerifyTokens } from './sample-data.js';

    const html = fillTokens(rawHtml, emailVerifyTokens);

    const { Story } = defineMeta({
        component: EmailPreview,
        tags: ['autodocs'],
        title: 'Email Templates/Email Verify',
        argTypes: {
            height: { control: { type: 'range', min: 400, max: 1200, step: 50 } }
        }
    });
</script>

<Story name="Default" args={{ html, height: 500 }} />
