<script lang="ts">
	import IconEmail from '~icons/mdi/email';
	import IconOpenInNew from '~icons/mdi/open-in-new';
	import type { PersistentEvent } from '$lib/models/api';
	import Duration from '$comp/formatters/Duration.svelte';
	import DateTime from '$comp/formatters/DateTime.svelte';
	import TimeAgo from '$comp/formatters/TimeAgo.svelte';
	import {
		getErrorType,
		getLocation,
		getMessage,
		getRequestInfoPath,
		getRequestInfoUrl,
		getStackTrace
	} from '$lib/helpers/persistent-event';
	import SimpleStackTrace from '../SimpleStackTrace.svelte';
	import StackTrace from '../StackTrace.svelte';
	import LogLevel from '../LogLevel.svelte';
	import ClickableSessionFilter from '$comp/filters/ClickableSessionFilter.svelte';
	import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
	import ClickableReferenceFilter from '$comp/filters/ClickableReferenceFilter.svelte';
	import ClickableNumberFilter from '$comp/filters/ClickableNumberFilter.svelte';
	import ClickableVersionFilter from '$comp/filters/ClickableVersionFilter.svelte';
	import ClickableDateFilter from '$comp/filters/ClickableDateFilter.svelte';
	import CopyToClipboardButton from '$comp/CopyToClipboardButton.svelte';

	export let event: PersistentEvent;
	//let project: ViewProject = {}; // TODO

	const hasError = !!event.data?.['@error'] || !!event.data?.['@simple_error'];
	const errorType = hasError ? getErrorType(event) : null;
	const stackTrace = hasError ? getStackTrace(event) : null;

	const isSessionStart = event.type === 'session';
	//let referenceId = isSessionStart ? event.reference_id : null;

	const message = event.message || getMessage(event);
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
	const requestUrlPath = getRequestInfoPath(event);
	const version = event.data?.['@version'];
</script>

<table class="table table-zebra table-xs border">
	<tr>
		<th class="whitespace-nowrap">Occurred On</th>
		<td
			><ClickableDateFilter term="date" value={event.date}
				><DateTime value={event.date}></DateTime> (<TimeAgo value={event.date}
				></TimeAgo>)</ClickableDateFilter
			></td
		>
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
				{#if isSessionStart}
					<ClickableSessionFilter sessionId={event.reference_id}
						>{event.reference_id}</ClickableSessionFilter
					>
				{:else}
					<ClickableReferenceFilter referenceId={event.reference_id}
						>{event.reference_id}</ClickableReferenceFilter
					>
				{/if}
			</td>
		</tr>
	{/if}
	{#each references as reference (reference.id)}
		<tr>
			<th class="whitespace-nowrap">{reference.name}</th>
			<td
				><ClickableReferenceFilter referenceId={reference.id}
					>{reference.id}</ClickableReferenceFilter
				></td
			>
		</tr>
	{/each}
	{#if level}
		<tr>
			<th class="whitespace-nowrap">Level</th>
			<td
				><ClickableStringFilter term="level" value={level}
					><LogLevel {level}></LogLevel></ClickableStringFilter
				></td
			>
		</tr>
	{/if}
	{#if event.type !== 'error'}
		<tr>
			<th class="whitespace-nowrap">Event Type</th>
			<td
				><ClickableStringFilter term="type" value={event.type}
					>{event.type}</ClickableStringFilter
				></td
			>
		</tr>
	{/if}
	{#if hasError}
		<tr>
			<th class="whitespace-nowrap">Error Type</th>
			<td
				><ClickableStringFilter term="error.type" value={errorType}
					>{errorType}</ClickableStringFilter
				></td
			>
		</tr>
	{/if}
	{#if event.source}
		<tr>
			<th class="whitespace-nowrap">Source</th>
			<td
				><ClickableStringFilter term="source" value={event.source}
					>{event.source}</ClickableStringFilter
				></td
			>
		</tr>
	{/if}
	{#if !isSessionStart && event.value}
		<tr>
			<th class="whitespace-nowrap">Value</th>
			<td
				><ClickableNumberFilter term="value" value={event.value}
					>{event.value}</ClickableNumberFilter
				></td
			>
		</tr>
	{/if}
	{#if message}
		<tr>
			<th class="whitespace-nowrap">Message</th>
			<td
				><ClickableStringFilter term="message" value={message}
					>{message}</ClickableStringFilter
				></td
			>
		</tr>
	{/if}
	{#if version}
		<tr>
			<th class="whitespace-nowrap">Version</th>
			<td
				><ClickableVersionFilter term="version" value={version}
					>{version}</ClickableVersionFilter
				></td
			>
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
					<ClickableStringFilter term="tag" value={tag}
						><div class="badge badge-neutral">{tag}</div></ClickableStringFilter
					>
				{/each}
			</td>
		</tr>
	{/if}
	{#if requestUrl}
		<tr>
			<th class="whitespace-nowrap">URL</th>
			<td>
				<ClickableStringFilter term="path" value={requestUrlPath}
					>{requestUrl}</ClickableStringFilter
				>

				<a href={requestUrl} target="_blank" class="link" title="Open in new window"
					><IconOpenInNew /></a
				></td
			>
		</tr>
	{/if}
</table>

{#if userEmail || userIdentity || userName || userDescription}
	<h4 class="text-lg">User Info</h4>
	<table class="table table-zebra table-xs border">
		{#if userEmail}
			<tr>
				<th class="whitespace-nowrap">User Email</th>
				<td
					><ClickableStringFilter term="user.email" value={userEmail}
						>{userEmail}</ClickableStringFilter
					>
					<a href="mailto:{userEmail}" title="Send email to {userEmail}"><IconEmail /></a
					></td
				>
			</tr>
		{/if}
		{#if userIdentity}
			<tr>
				<th class="whitespace-nowrap">User Identity</th>
				<td
					><ClickableStringFilter term="user" value={userIdentity}
						>{userIdentity}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if userName}
			<tr>
				<th class="whitespace-nowrap">User Name</th>
				<td
					><ClickableStringFilter term="user.name" value={userName}
						>{userName}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if userDescription}
			<tr>
				<th class="whitespace-nowrap">User Description</th>
				<td
					><ClickableStringFilter term="user.description" value={userDescription}
						>{userDescription}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
	</table>
{/if}

{#if hasError}
	<div class="flex justify-between items-center">
		<h4 class="text-lg">Stack Trace</h4>
		<CopyToClipboardButton title="Copy Stack Trace to Clipboard" value={stackTrace}
		></CopyToClipboardButton>
	</div>
	<div class="max-h-[150px] overflow-auto p-2 mt-2 border border-info text-xs">
		{#if event.data?.['@error']}
			<StackTrace error={event.data['@error']} />
		{:else}
			<SimpleStackTrace error={event.data?.['@simple_error']} />
		{/if}
	</div>
{/if}
