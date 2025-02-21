<script lang="ts">
    import { StackStatus } from '$features/stacks/models';

    import type { SummaryModel, SummaryTemplateKeys } from './index';

    import EventErrorSummary from './event-error-summary.svelte';
    import EventFeatureSummary from './event-feature-summary.svelte';
    import EventLogSummary from './event-log-summary.svelte';
    import EventNotFoundSummary from './event-not-found-summary.svelte';
    import EventSessionSummary from './event-session-summary.svelte';
    import EventSimpleSummary from './event-simple-summary.svelte';
    import EventSummary from './event-summary.svelte';
    import StackErrorSummary from './stack-error-summary.svelte';
    import StackFeatureSummary from './stack-feature-summary.svelte';
    import StackLogSummary from './stack-log-summary.svelte';
    import StackNotFoundSummary from './stack-not-found-summary.svelte';
    import StackSessionSummary from './stack-session-summary.svelte';
    import StackSimpleSummary from './stack-simple-summary.svelte';
    import StackSummary from './stack-summary.svelte';

    interface Props {
        showStatus?: boolean;
        showType?: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { showStatus = true, showType = true, summary }: Props = $props();
    let showBadge: boolean = $derived(showStatus && 'status' in summary && summary.status !== 'open');
    let badgeStatus = $derived<StackStatus>(('status' in summary && (summary.status as StackStatus)) || StackStatus.Open);
</script>

{#if summary.template_key === 'event-summary'}
    <EventSummary {showType} {summary} />
{:else if summary.template_key === 'stack-summary'}
    <StackSummary {badgeStatus} {showBadge} {showType} {summary} />
{:else if summary.template_key === 'event-simple-summary'}
    <EventSimpleSummary {summary} />
{:else if summary.template_key === 'stack-simple-summary'}
    <StackSimpleSummary {badgeStatus} {showBadge} {summary} />
{:else if summary.template_key === 'event-error-summary'}
    <EventErrorSummary {summary} />
{:else if summary.template_key === 'stack-error-summary'}
    <StackErrorSummary {badgeStatus} {showBadge} {summary} />
{:else if summary.template_key === 'event-session-summary'}
    <EventSessionSummary {showType} {summary} />
{:else if summary.template_key === 'stack-session-summary'}
    <StackSessionSummary {badgeStatus} {showBadge} {showType} {summary} />
{:else if summary.template_key === 'event-notfound-summary'}
    <EventNotFoundSummary {showType} {summary} />
{:else if summary.template_key === 'stack-notfound-summary'}
    <StackNotFoundSummary {badgeStatus} {showBadge} {showType} {summary} />
{:else if summary.template_key === 'event-feature-summary'}
    <EventFeatureSummary {showType} {summary} />
{:else if summary.template_key === 'stack-feature-summary'}
    <StackFeatureSummary {badgeStatus} {showBadge} {showType} {summary} />
{:else if summary.template_key === 'event-log-summary'}
    <EventLogSummary {showType} {summary} />
{:else}
    <StackLogSummary {badgeStatus} {showBadge} {showType} {summary} />
{/if}
