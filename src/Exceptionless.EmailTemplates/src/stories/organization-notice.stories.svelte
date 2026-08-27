<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';
    import EmailPreview from './EmailPreview.svelte';
    import rawHtml from '../../../Exceptionless.Core/Mail/Templates/organization-notice.html?raw';
    import { fillTokens, monthlyOrganizationNoticeTokens, organizationNoticeTokens } from './sample-data.js';

    const hourlyHtml = fillTokens(rawHtml, organizationNoticeTokens);
    const monthlyHtml = fillTokens(rawHtml, monthlyOrganizationNoticeTokens);

    const { Story } = defineMeta({
        component: EmailPreview,
        tags: ['autodocs'],
        title: 'Email Templates/Organization Notice',
        argTypes: {
            height: { control: { type: 'range', min: 400, max: 1200, step: 50 } }
        }
    });
</script>

<Story name="Hourly Throttling" args={{ html: hourlyHtml, height: 650 }} />
<Story name="Monthly Plan Limit" args={{ html: monthlyHtml, height: 650 }} />
