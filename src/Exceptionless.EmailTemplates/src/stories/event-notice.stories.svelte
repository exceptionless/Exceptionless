<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';
    import EmailPreview from './EmailPreview.svelte';
    import rawHtml from '../../../Exceptionless.Core/Mail/Templates/event-notice.html?raw';
    import {
        eventNoticeTokens,
        fillTokens,
        reoccurredEventNoticeTokens,
        regressedEventNoticeTokens
    } from './sample-data.js';

    const newEventHtml = fillTokens(rawHtml, eventNoticeTokens);
    const regressedEventHtml = fillTokens(rawHtml, regressedEventNoticeTokens);
    const reoccurredEventHtml = fillTokens(rawHtml, reoccurredEventNoticeTokens);

    const { Story } = defineMeta({
        component: EmailPreview,
        tags: ['autodocs'],
        title: 'Email Templates/Event Notice',
        argTypes: {
            height: { control: { type: 'range', min: 400, max: 1200, step: 50 } }
        }
    });
</script>

<Story name="New Critical Event" args={{ html: newEventHtml, height: 700 }} />
<Story name="Critical Regression" args={{ html: regressedEventHtml, height: 700 }} />
<Story name="Reoccurred Without Details" args={{ html: reoccurredEventHtml, height: 550 }} />
