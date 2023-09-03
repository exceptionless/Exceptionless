<script lang="ts">
	import EventSummaries from '$comp/events/EventsTable.svelte';
</script>

<svelte:head>
	<title>Exceptionless</title>
</svelte:head>

<div class="stats shadow">
	<div class="stat place-items-center">
		<div class="stat-title">Events</div>
		<div class="stat-value">31K</div>
		<div class="stat-desc">Jan 1st - Feb 1st</div>
	</div>

	<div class="stat place-items-center">
		<div class="stat-title">Stacks</div>
		<div class="stat-value">4,200</div>
		<div class="stat-desc">↗︎ 400 (22%)</div>
	</div>

	<div class="stat place-items-center">
		<div class="stat-title">New Stacks</div>
		<div class="stat-value">1,200</div>
		<div class="stat-desc">↘︎ 90 (14%)</div>
	</div>

	<div class="stat place-items-center">
		<div class="stat-title">Events Per Hour</div>
		<div class="stat-value">1,200</div>
		<div class="stat-desc">↘︎ 90 (14%)</div>
	</div>
</div>

<h1 class="text-xl mt-5">Events</h1>

<EventSummaries mode="summary"></EventSummaries>
