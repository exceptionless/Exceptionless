<script lang="ts">
    import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

    import EventErrorSummary from './EventErrorSummary.svelte';
    import EventFeatureSummary from './EventFeatureSummary.svelte';
    import EventLogSummary from './EventLogSummary.svelte';
    import EventNotFoundSummary from './EventNotFoundSummary.svelte';
    import EventSessionSummary from './EventSessionSummary.svelte';
    import EventSimpleSummary from './EventSimpleSummary.svelte';
    import EventSummary from './EventSummary.svelte';
    import StackErrorSummary from './StackErrorSummary.svelte';
    import StackFeatureSummary from './StackFeatureSummary.svelte';
    import StackLogSummary from './StackLogSummary.svelte';
    import StackNotFoundSummary from './StackNotFoundSummary.svelte';
    import StackSessionSummary from './StackSessionSummary.svelte';
    import StackSimpleSummary from './StackSimpleSummary.svelte';
    import StackSummary from './StackSummary.svelte';

    interface Props {
        showStatus?: boolean;
        showType?: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { showStatus = true, showType = true, summary }: Props = $props();
    let showBadge: boolean = $derived(showStatus && 'status' in summary && summary.status !== 'open');
    let badgeClass = $derived('label-' + (('status' in summary && summary.status) || 'open'));
</script>

{#if summary.template_key === 'event-summary'}
    <EventSummary {showType} {summary} />
{:else if summary.template_key === 'stack-summary'}
    <StackSummary {badgeClass} {showBadge} {showType} {summary} />
{:else if summary.template_key === 'event-simple-summary'}
    <EventSimpleSummary {summary} />
{:else if summary.template_key === 'stack-simple-summary'}
    <StackSimpleSummary {badgeClass} {showBadge} {summary} />
{:else if summary.template_key === 'event-error-summary'}
    <EventErrorSummary {summary} />
{:else if summary.template_key === 'stack-error-summary'}
    <StackErrorSummary {badgeClass} {showBadge} {summary} />
{:else if summary.template_key === 'event-session-summary'}
    <EventSessionSummary {showType} {summary} />
{:else if summary.template_key === 'stack-session-summary'}
    <StackSessionSummary {badgeClass} {showBadge} {showType} {summary} />
{:else if summary.template_key === 'event-notfound-summary'}
    <EventNotFoundSummary {showType} {summary} />
{:else if summary.template_key === 'stack-notfound-summary'}
    <StackNotFoundSummary {badgeClass} {showBadge} {showType} {summary} />
{:else if summary.template_key === 'event-feature-summary'}
    <EventFeatureSummary {showType} {summary} />
{:else if summary.template_key === 'stack-feature-summary'}
    <StackFeatureSummary {badgeClass} {showBadge} {showType} {summary} />
{:else if summary.template_key === 'event-log-summary'}
    <EventLogSummary {showType} {summary} />
{:else}
    <StackLogSummary {badgeClass} {showBadge} {showType} {summary} />
{/if}
