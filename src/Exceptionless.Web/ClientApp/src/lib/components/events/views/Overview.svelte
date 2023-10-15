<script lang="ts">
	import type { PersistentEvent } from '$lib/models/api';
	import Duration from '$comp/formatters/Duration.svelte';
	import DateTime from '$comp/formatters/DateTime.svelte';
	import TimeAgo from '$comp/formatters/TimeAgo.svelte';
	import {
		getErrorType,
		getLocation,
		getMessage,
		getRequestInfoUrl
	} from '$lib/helpers/persistent-event';
	import SimpleStackTrace from '../SimpleStackTrace.svelte';
	import StackTrace from '../StackTrace.svelte';
	import FilterableItem from '$comp/filters/FilterableItem.svelte';
	import LogLevel from '../LogLevel.svelte';
	import { quote } from '$comp/filters/filters';
	//import { buildUrl } from '$lib/helpers/url';

	export let event: PersistentEvent;
	//let project: ViewProject = {}; // TODO

	const hasError = !!event.data?.['@error'] || !!event.data?.['@simple_error'];
	const errorType = hasError ? getErrorType(event) : null;

	const isSessionStart = event.type === 'session';
	//let referenceId = isSessionStart ? event.reference_id : null;

	const message = getMessage(event);
	let references: { id: string; name: string }[] = [];
	const referencePrefix = '@ref:';
	Object.entries(event.data || {}).forEach(([key, value]) => {
		if (key === '@ref:session') {
			//referenceId = value as string;
		}

		if (key.startsWith(referencePrefix)) {
			references.push({ id: value as string, name: key.slice(5) });
		}
	});

	const level = event.data?.['@level']?.toLowerCase();
	const location = getLocation(event);

	const userInfo = event.data?.['@user'];
	const userIdentity = userInfo?.identity;
	const userName = userInfo?.name;
	const userDescriptionInfo = event.data?.['@user_description'];
	const userEmail = userDescriptionInfo?.email_address;
	const userDescription = userDescriptionInfo?.description;

	const requestUrl = getRequestInfoUrl(event);
	const version = event.data?.['@version'];
</script>

<table class="table table-zebra table-xs border">
	<tr>
		<th class="whitespace-nowrap">Occurred On</th>
		<td><DateTime value={event.date}></DateTime> (<TimeAgo value={event.date}></TimeAgo>)</td>
	</tr>
	{#if isSessionStart}
		<tr>
			<th class="whitespace-nowrap">Duration</th>
			<td>
				{#if !event.data?.sessionend}
					<span class="text-green-500">•</span>
				{/if}
				<Duration value={event.date}></Duration>
				{#if event.data?.sessionend}
					(ended <TimeAgo value={event.data.sessionend}></TimeAgo>)
				{/if}
			</td>
		</tr>
	{/if}
	<!-- <tr>
		<th class="whitespace-nowrap">Project</th>
		<td><a href="/app.project-frequent/{event.project_id}">{project.name}</a></td>
	</tr> -->
	{#if event.reference_id}
		<tr>
			<th class="whitespace-nowrap">Reference</th>
			<td>
				<!-- TODO: support group filters (reference:{sessionId} OR ref.session:{sessionId}) -->
				{#if isSessionStart}
					<FilterableItem term="ref.session" value={quote(event.reference_id)}
						>{event.reference_id}</FilterableItem
					>
				{:else}
					{event.reference_id}
				{/if}
			</td>
		</tr>
	{/if}
	{#each references as reference (reference.id)}
		<tr>
			<th class="whitespace-nowrap">{reference.name}</th>
			<td
				><FilterableItem term="reference" value={quote(reference.id)}
					>{reference.id}</FilterableItem
				></td
			>
		</tr>
	{/each}
	{#if level}
		<tr>
			<th class="whitespace-nowrap">Level</th>
			<td
				><FilterableItem term="level" value={quote(level)}
					><LogLevel {level}></LogLevel></FilterableItem
				></td
			>
		</tr>
	{/if}
	{#if event.type !== 'error'}
		<tr>
			<th class="whitespace-nowrap">Event Type</th>
			<td
				><FilterableItem term="type" value={quote(event.type)}>{event.type}</FilterableItem
				></td
			>
		</tr>
	{/if}
	{#if hasError}
		<tr>
			<th class="whitespace-nowrap">Error Type</th>
			<td
				><FilterableItem term="error.type" value={quote(errorType)}
					>{errorType}</FilterableItem
				></td
			>
		</tr>
	{/if}
	{#if event.source}
		<tr>
			<th class="whitespace-nowrap">Source</th>
			<td
				><FilterableItem term="source" value={quote(event.source)}
					>{event.source}</FilterableItem
				></td
			>
		</tr>
	{/if}
	{#if !isSessionStart && event.value}
		<tr>
			<th class="whitespace-nowrap">Value</th>
			<td><FilterableItem term="value" value={event.value}>{event.value}</FilterableItem></td>
		</tr>
	{/if}
	{#if message}
		<tr>
			<th class="whitespace-nowrap">Message</th>
			<td><FilterableItem term="message" value={quote(message)}>{message}</FilterableItem></td
			>
		</tr>
	{/if}
	{#if version}
		<tr>
			<th class="whitespace-nowrap">Version</th>
			<td><FilterableItem term="version" value={version}>{version}</FilterableItem></td>
		</tr>
	{/if}
	{#if location}
		<tr>
			<th class="whitespace-nowrap">Geo</th>
			<td>{location}</td>
		</tr>
	{/if}
	{#if event.tags?.length}
		<tr>
			<th class="whitespace-nowrap">Tags</th>
			<td class="flex flex-wrap justify-start gap-2 overflow-auto">
				{#each event.tags as tag}
					<FilterableItem term="tag" value={quote(tag)}
						><div class="badge badge-neutral">{tag}</div></FilterableItem
					>
				{/each}
			</td>
		</tr>
	{/if}
	{#if requestUrl}
		<tr>
			<th class="whitespace-nowrap">URL</th>
			<td><a href={requestUrl} target="_blank" class="link">{requestUrl}></a></td>
		</tr>
	{/if}
</table>

{#if userEmail || userDescription || userIdentity}
	<h4 class="text-lg">User Info</h4>
	<table class="table table-zebra table-xs border">
		{#if userEmail}
			<tr>
				<th class="whitespace-nowrap">User Email</th>
				<td><a href="mailto:{userEmail}">{userEmail}</a></td>
			</tr>
		{/if}
		{#if userIdentity}
			<tr>
				<th class="whitespace-nowrap">User Identity</th>
				<td>{userIdentity}</td>
			</tr>
		{/if}
		{#if userName}
			<tr>
				<th class="whitespace-nowrap">User Name</th>
				<td>{userName}</td>
			</tr>
		{/if}
		{#if userDescriptionInfo}
			<tr>
				<th class="whitespace-nowrap">User Description</th>
				<td>{userDescriptionInfo}</td>
			</tr>
		{/if}
	</table>
{/if}

{#if hasError}
	<div class="flex justify-end">
		<button
			class="btn btn-default btn-xs fa fa-code hidden-xs"
			title="Copy Stack Trace to Clipboard"
		></button>
	</div>

	<h4 class="text-lg">Stack Trace</h4>
	<div class="max-h-[150px] overflow-auto p-2 mt-2 border border-info text-xs">
		{#if event.data?.['@error']}
			<StackTrace error={event.data['@error']} />
		{:else}
			<SimpleStackTrace error={event.data?.['@simple_error']} />
		{/if}
	</div>
{/if}
