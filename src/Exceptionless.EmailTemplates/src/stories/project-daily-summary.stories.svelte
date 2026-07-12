<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';
    import EmailPreview from './EmailPreview.svelte';
    import rawHtml from '../../../Exceptionless.Core/Mail/Templates/project-daily-summary.html?raw';
    import {
        blockedDailySummaryTokens,
        dailySummaryTokens,
        fillTokens,
        unconfiguredDailySummaryTokens
    } from './sample-data.js';

    const defaultHtml = fillTokens(rawHtml, dailySummaryTokens);
    const blockedHtml = fillTokens(rawHtml, blockedDailySummaryTokens);
    const unconfiguredHtml = fillTokens(rawHtml, unconfiguredDailySummaryTokens);

    const { Story } = defineMeta({
        component: EmailPreview,
        tags: ['autodocs'],
        title: 'Email Templates/Daily Summary',
        argTypes: {
            height: { control: { type: 'range', min: 400, max: 1400, step: 50 } }
        }
    });
</script>

<Story name="Default" args={{ html: defaultHtml, height: 900 }} />
<Story name="Discarded Events and Free Plan" args={{ html: blockedHtml, height: 1500 }} />
<Story name="Unconfigured Project" args={{ html: unconfiguredHtml, height: 650 }} />
