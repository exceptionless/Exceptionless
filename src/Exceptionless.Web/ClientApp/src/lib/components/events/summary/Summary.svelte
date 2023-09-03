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

	export let summary: SummaryModel<SummaryTemplateKeys>;
	export let showStatus: boolean = true;
	export let showType: boolean = true;
	const showBadge: boolean = showStatus && 'status' in summary && summary.status !== 'open';
	const badgeClass = 'label-' + (('status' in summary && summary.status) || 'open');

	const components: {
		templateKey: SummaryTemplateKeys;
		component: ConstructorOfATypedSvelteComponent;
	}[] = [
		{ templateKey: 'event-summary', component: EventSummary },
		{ templateKey: 'stack-summary', component: StackSummary },
		{ templateKey: 'event-simple-summary', component: EventSimpleSummary },
		{ templateKey: 'stack-simple-summary', component: StackSimpleSummary },
		{ templateKey: 'event-error-summary', component: EventErrorSummary },
		{ templateKey: 'stack-error-summary', component: StackErrorSummary },
		{ templateKey: 'event-session-summary', component: EventSessionSummary },
		{ templateKey: 'stack-session-summary', component: StackSessionSummary },
		{ templateKey: 'event-notfound-summary', component: EventNotFoundSummary },
		{ templateKey: 'stack-notfound-summary', component: StackNotFoundSummary },
		{ templateKey: 'event-feature-summary', component: EventFeatureSummary },
		{ templateKey: 'stack-feature-summary', component: StackFeatureSummary },
		{ templateKey: 'event-log-summary', component: EventLogSummary },
		{ templateKey: 'stack-log-summary', component: StackLogSummary }
	];

	const component = components.find(
		(type) => type.templateKey == summary.template_key
	)?.component;
</script>

<svelte:component this={component} {summary} {showBadge} {showStatus} {showType} {badgeClass} />
