<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';
    import EmailPreview from './EmailPreview.svelte';
    import rawHtml from '../../../Exceptionless.Core/Mail/Templates/project-daily-summary.html?raw';
    import { fillTokens, dailySummaryTokens } from './sample-data.js';

    const html = fillTokens(rawHtml, dailySummaryTokens);

    const { Story } = defineMeta({
        component: EmailPreview,
        tags: ['autodocs'],
        title: 'Email Templates/Daily Summary',
        argTypes: {
            height: { control: { type: 'range', min: 400, max: 1400, step: 50 } }
        }
    });
</script>

<Story name="Default" args={{ html, height: 900 }} />
