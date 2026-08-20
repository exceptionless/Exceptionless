<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';
    import EmailPreview from './EmailPreview.svelte';
    import rawHtml from '../../../Exceptionless.Core/Mail/Templates/organization-invited.html?raw';
    import { fillTokens, organizationInvitedTokens } from './sample-data.js';

    const html = fillTokens(rawHtml, organizationInvitedTokens);

    const { Story } = defineMeta({
        component: EmailPreview,
        tags: ['autodocs'],
        title: 'Email Templates/Organization Invited',
        argTypes: {
            height: { control: { type: 'range', min: 400, max: 1200, step: 50 } }
        }
    });
</script>

<Story name="Default" args={{ html, height: 700 }} />
