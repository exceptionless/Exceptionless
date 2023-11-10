<script lang="ts">
	import IconEmail from '~icons/mdi/email';
	import IconOpenInNew from '~icons/mdi/open-in-new';
	import type { PersistentEvent } from '$lib/models/api';
	import Duration from '$comp/formatters/Duration.svelte';
	import TimeAgo from '$comp/formatters/TimeAgo.svelte';
	import {
		getErrorType,
		getLocation,
		getMessage,
		getRequestInfoPath,
		getRequestInfoUrl,
		getStackTrace,
		hasErrorOrSimpleError
	} from '$lib/helpers/persistent-event';
	import SimpleStackTrace from '../SimpleStackTrace.svelte';
	import StackTrace from '../StackTrace.svelte';
	import LogLevel from '../LogLevel.svelte';
	import ClickableSessionFilter from '$comp/filters/ClickableSessionFilter.svelte';
	import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
	import ClickableReferenceFilter from '$comp/filters/ClickableReferenceFilter.svelte';
	import ClickableNumberFilter from '$comp/filters/ClickableNumberFilter.svelte';
	import ClickableVersionFilter from '$comp/filters/ClickableVersionFilter.svelte';
	import CopyToClipboardButton from '$comp/CopyToClipboardButton.svelte';

	export let event: PersistentEvent;

	const hasError = hasErrorOrSimpleError(event);
	const errorType = hasError ? getErrorType(event) : null;
	const stackTrace = hasError ? getStackTrace(event) : null;

	const isSessionStart = event.type === 'session';

	const message = getMessage(event);
	let references: { id: string; name: string }[] = [];
	const referencePrefix = '@ref:';
	Object.entries(event.data || {}).forEach(([key, value]) => {
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

	function getSessionStartDuration(): Date | number | string | undefined {
		if (event.data?.sessionend) {
			if (event.value) {
				return event.value * 1000;
			}

			if (event.date) {
				return new Date(event.data.sessionend).getTime() - new Date(event.date).getTime();
			}

			throw new Error('Completed session start event has no value or date');
		}

		return event.date;
	}
</script>

<table class="table table-zebra table-xs border border-base-300">
	<tbody>
		{#if isSessionStart}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Duration</th>
				<td class="border border-base-300">
					{#if !event.data?.sessionend}
						<span
							class="bg-green-500 rounded-full inline-flex items-center w-2 h-2 animate-pulse"
							title="Online"
						></span>
					{/if}
					<Duration value={getSessionStartDuration()}></Duration>
					{#if event.data?.sessionend}
						(ended <TimeAgo value={event.data.sessionend}></TimeAgo>)
					{/if}
				</td>
			</tr>
		{/if}
		{#if event.reference_id}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Reference</th>
				<td class="border border-base-300">
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
				<th class="border border-base-300 whitespace-nowrap">{reference.name}</th>
				<td class="border border-base-300"
					><ClickableReferenceFilter referenceId={reference.id}
						>{reference.id}</ClickableReferenceFilter
					></td
				>
			</tr>
		{/each}
		{#if level}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Level</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="level" value={level}
						><LogLevel {level}></LogLevel></ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if event.type !== 'error'}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Event Type</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="type" value={event.type}
						>{event.type}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if hasError}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Error Type</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="error.type" value={errorType}
						>{errorType}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if event.source}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Source</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="source" value={event.source}
						>{event.source}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if !isSessionStart && event.value}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Value</th>
				<td class="border border-base-300"
					><ClickableNumberFilter term="value" value={event.value}
						>{event.value}</ClickableNumberFilter
					></td
				>
			</tr>
		{/if}
		{#if message}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Message</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="message" value={message}
						>{message}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if version}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Version</th>
				<td class="border border-base-300"
					><ClickableVersionFilter term="version" value={version}
						>{version}</ClickableVersionFilter
					></td
				>
			</tr>
		{/if}
		{#if location}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Geo</th>
				<td class="border border-base-300">{location}</td>
			</tr>
		{/if}
		{#if event.tags?.length}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Tags</th>
				<td class="border border-base-300 flex flex-wrap justify-start gap-2 overflow-auto">
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
				<th class="border border-base-300 whitespace-nowrap">URL</th>
				<td class="border border-base-300 flex items-center gap-x-1">
					<ClickableStringFilter term="path" value={requestUrlPath}
						>{requestUrl}</ClickableStringFilter
					>

					<a href={requestUrl} target="_blank" class="link" title="Open in new window"
						><IconOpenInNew /></a
					></td
				>
			</tr>
		{/if}
	</tbody>
</table>

{#if userEmail || userIdentity || userName || userDescription}
	<h4 class="text-lg mt-4 mb-2">User Info</h4>
	<table class="table table-zebra table-xs border border-base-300">
		<tbody>
			{#if userEmail}
				<tr>
					<th class="border border-base-300 whitespace-nowrap">User Email</th>
					<td class="border border-base-300"
						><ClickableStringFilter term="user.email" value={userEmail}
							>{userEmail}</ClickableStringFilter
						>
						<a href="mailto:{userEmail}" title="Send email to {userEmail}"
							><IconEmail /></a
						></td
					>
				</tr>
			{/if}
			{#if userIdentity}
				<tr>
					<th class="border border-base-300 whitespace-nowrap">User Identity</th>
					<td class="border border-base-300"
						><ClickableStringFilter term="user" value={userIdentity}
							>{userIdentity}</ClickableStringFilter
						></td
					>
				</tr>
			{/if}
			{#if userName}
				<tr>
					<th class="border border-base-300 whitespace-nowrap">User Name</th>
					<td class="border border-base-300"
						><ClickableStringFilter term="user.name" value={userName}
							>{userName}</ClickableStringFilter
						></td
					>
				</tr>
			{/if}
			{#if userDescription}
				<tr>
					<th class="border border-base-300 whitespace-nowrap">User Description</th>
					<td class="border border-base-300"
						><ClickableStringFilter term="user.description" value={userDescription}
							>{userDescription}</ClickableStringFilter
						></td
					>
				</tr>
			{/if}</tbody
		>
	</table>
{/if}

{#if hasError}
	<div class="flex justify-between mt-4 mb-2">
		<h4 class="text-lg">Stack Trace</h4>
		<div class="flex justify-end">
			<CopyToClipboardButton title="Copy Stack Trace to Clipboard" value={stackTrace}
			></CopyToClipboardButton>
		</div>
	</div>
	<div class="max-h-[150px] overflow-auto p-2 mt-2 border border-base-300 text-xs">
		{#if event.data?.['@error']}
			<StackTrace error={event.data['@error']} />
		{:else}
			<SimpleStackTrace error={event.data?.['@simple_error']} />
		{/if}
	</div>
{/if}
