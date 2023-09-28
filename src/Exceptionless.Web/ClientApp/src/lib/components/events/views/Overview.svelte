<script lang="ts">
	import type { PersistentEvent, ViewProject } from '$lib/models/api';
	import TimeAgo from '$comp/time/TimeAgo.svelte';
	import Duration from '$comp/time/Duration.svelte';
	import DateTime from '$comp/time/DateTime.svelte';

	export let event: PersistentEvent;
	let project: ViewProject = {}; // TODO

	const hasError = !!event.data?.['@error'] || !!event.data?.['@simple_error'];

	const isSessionStart = event.type === 'session';
	let referenceId = isSessionStart ? event.reference_id : null;

	let references: { id: string; name: string }[] = [];
	const referencePrefix = '@ref:';
	Object.entries(event.data || {}).forEach(([key, value]) => {
		if (key === '@ref:session') {
			referenceId = value as string;
		}

		if (key.startsWith(referencePrefix)) {
			references.push({ id: value as string, name: key.slice(5) });
		}
	});

	const level = event.data?.['@level']?.toLowerCase();

	function activateSessionEventsTab() {}
</script>

<table class="table table-zebra table-xs border">
	<tr>
		<th>Occurred On</th>
		<td><DateTime date={event.date}></DateTime> (<TimeAgo date={event.date}></TimeAgo>)</td>
	</tr>
	{#if isSessionStart}
		<tr>
			<th>Duration</th>
			<td>
				{#if !event.data?.sessionend}
					<span class="text-green-500">•</span>
				{/if}
				<Duration value={event.date}></Duration>
				{#if event.data?.sessionend}
					(ended <TimeAgo date={event.data.sessionend}></TimeAgo>)
				{/if}
			</td>
		</tr>
	{/if}
	<!-- <tr>
		<th>Project</th>
		<td><a href="/app.project-frequent/{event.project_id}">{project.name}</a></td>
	</tr> -->
	{#if event.reference_id}
		<tr>
			<th>Reference</th>
			<td>
				{#if isSessionStart}
					<button on:click|preventDefault={activateSessionEventsTab}
						>{event.reference_id}</button
					>
				{:else}
					{event.reference_id}
				{/if}
			</td>
		</tr>
	{/if}
	{#each references as reference (reference.id)}
		<tr>
			<th>{reference.name}</th>
			<td><a href="/next/event/by-ref/{reference.id}">{reference.id}</a></td>
		</tr>
	{/each}
	{#if level}
		<tr>
			<th>Level</th>
			<td><span class="label-default label">{level}</span></td>
		</tr>
	{/if}
	{#if event.type !== 'error'}
		<tr>
			<th>Event Type</th>
			<td>{event.type}</td>
		</tr>
	{/if}
	<!-- {#if hasError}
		<tr>
			<th>Error Type</th>
			<td>{errorType}</td>
		</tr>
	{/if} -->
	{#if event.source}
		<tr>
			<th>Source</th>
			<td>{event.source}</td>
		</tr>
	{/if}
	{#if !isSessionStart && event.value}
		<tr>
			<th>Value</th>
			<td>{event.value}</td>
		</tr>
	{/if}
	<!-- {#if event.message || getMessage()}
		<tr>
			<th>Message</th>
			<td>{event.message || message}</td>
		</tr>
	{/if}
	{#if version}
		<tr>
			<th>Version</th>
			<td>{version}</td>
		</tr>
	{/if}
	{#if location}
		<tr>
			<th>Geo</th>
			<td>{location}</td>
		</tr>
	{/if}
	{#if event.tags.length > 0}
		<tr>
			<th>Tags</th>
			<td><span class="label label-info" each={tag in event.tags}>{tag}</span></td>
		</tr>
	{/if}
	{#if requestUrl}
		<tr>
			<th>URL</th>
			<td><a href={requestUrl} target="_blank">{requestUrl}</a></td>
		</tr>
	{/if} -->
</table>

<!-- {#if userEmail || userDescription || userIdentity || userEmail}
	<h4>User Info</h4>
	<table class="table-auto border-collapse border border-gray-800">
		{#if userEmail}
			<tr>
				<th>User Email</th>
				<td><a href="mailto:{userEmail}">{userEmail}</a></td>
			</tr>
		{/if}
		{#if userIdentity}
			<tr>
				<th>User Identity</th>
				<td>{userIdentity}</td>
			</tr>
		{/if}
		{#if userName}
			<tr>
				<th>User Name</th>
				<td>{userName}</td>
			</tr>
		{/if}
		{#if userDescription}
			<tr>
				<th>User Description</th>
				<td>{userDescription}</td>
			</tr>
		{/if}
	</table>
{/if}

{#if hasError}
	<div class="flex justify-end">
		<a
			class="btn btn-default btn-xs fa fa-code hidden-xs"
			role="button"
			title="Copy Stack Trace to Clipboard"
		></a>
	</div>
	<h4>Stack Trace</h4>
	<StackTrace class="stack-trace-mini" exception={event.data['@error']} />
	<SimpleStackTrace class="stack-trace-mini" exception={event.data['@simple_error']} />
{/if} -->
