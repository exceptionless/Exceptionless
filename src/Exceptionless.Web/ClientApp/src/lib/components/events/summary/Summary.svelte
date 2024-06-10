<script lang="ts">
    import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
    import EventSimpleSummary from './EventSimpleSummary.svelte';
    import EventErrorSummary from './EventErrorSummary.svelte';
    import EventFeatureSummary from './EventFeatureSummary.svelte';
    import EventNotFoundSummary from './EventNotFoundSummary.svelte';
    import EventLogSummary from './EventLogSummary.svelte';
    import EventSummary from './EventSummary.svelte';
    import EventSessionSummary from './EventSessionSummary.svelte';
    import StackNotFoundSummary from './StackNotFoundSummary.svelte';
    import StackLogSummary from './StackLogSummary.svelte';
    import StackSummary from './StackSummary.svelte';
    import StackErrorSummary from './StackErrorSummary.svelte';
    import StackSimpleSummary from './StackSimpleSummary.svelte';
    import StackSessionSummary from './StackSessionSummary.svelte';
    import StackFeatureSummary from './StackFeatureSummary.svelte';

    interface Props {
        summary: SummaryModel<SummaryTemplateKeys>;
        showStatus: boolean;
        showType: boolean;
    }

    let { summary, showStatus = true, showType = true }: Props = $props();
    let showBadge: boolean = $derived(showStatus && 'status' in summary && summary.status !== 'open');
    let badgeClass = $derived('label-' + (('status' in summary && summary.status) || 'open'));
</script>

{#if summary.template_key === 'event-summary'}
    <EventSummary {summary} {showType} />
{:else if summary.template_key === 'stack-summary'}
    <StackSummary {summary} {showBadge} {showType} {badgeClass} />
{:else if summary.template_key === 'event-simple-summary'}
    <EventSimpleSummary {summary} />
{:else if summary.template_key === 'stack-simple-summary'}
    <StackSimpleSummary {summary} {showBadge} {badgeClass} />
{:else if summary.template_key === 'event-error-summary'}
    <EventErrorSummary {summary} />
{:else if summary.template_key === 'stack-error-summary'}
    <StackErrorSummary {summary} {showBadge} {badgeClass} />
{:else if summary.template_key === 'event-session-summary'}
    <EventSessionSummary {summary} {showType} />
{:else if summary.template_key === 'stack-session-summary'}
    <StackSessionSummary {summary} {showBadge} {showType} {badgeClass} />
{:else if summary.template_key === 'event-notfound-summary'}
    <EventNotFoundSummary {summary} {showType} />
{:else if summary.template_key === 'stack-notfound-summary'}
    <StackNotFoundSummary {summary} {showBadge} {showType} {badgeClass} />
{:else if summary.template_key === 'event-feature-summary'}
    <EventFeatureSummary {summary} {showType} />
{:else if summary.template_key === 'stack-feature-summary'}
    <StackFeatureSummary {summary} {showBadge} {showType} {badgeClass} />
{:else if summary.template_key === 'event-log-summary'}
    <EventLogSummary {summary} {showType} />
{:else}
    <StackLogSummary {summary} {showBadge} {showType} {badgeClass} />
{/if}
