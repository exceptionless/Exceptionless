<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';
    import EmailPreview from './EmailPreview.svelte';
    import rawHtml from '../../../Exceptionless.Core/Mail/Templates/organization-notice.html?raw';
    import { fillTokens, organizationNoticeTokens } from './sample-data.js';

    const html = fillTokens(rawHtml, organizationNoticeTokens);

    const { Story } = defineMeta({
        component: EmailPreview,
        tags: ['autodocs'],
        title: 'Email Templates/Organization Notice',
        argTypes: {
            height: { control: { type: 'range', min: 400, max: 1200, step: 50 } }
        }
    });
</script>

<Story name="Default" args={{ html, height: 650 }} />
