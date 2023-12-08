<script lang="ts">
	import IconEmail from '~icons/mdi/email';
	import IconOpenInNew from '~icons/mdi/open-in-new';
	import type { PersistentEvent } from '$lib/models/api';
	import * as Table from '$comp/ui/table';
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
	import { Badge } from '$comp/ui/badge';
	import { Button } from '$comp/ui/button';
	import H4 from '$comp/typography/H4.svelte';
	import A from '$comp/typography/A.svelte';

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

<Table.Root>
	<Table.Body>
		{#if isSessionStart}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Duration</Table.Head>
				<Table.Cell>
					{#if !event.data?.sessionend}
						<span
							class="inline-flex items-center w-2 h-2 bg-green-500 rounded-full animate-pulse"
							title="Online"
						></span>
					{/if}
					<Duration value={getSessionStartDuration()}></Duration>
					{#if event.data?.sessionend}
						(ended <TimeAgo value={event.data.sessionend}></TimeAgo>)
					{/if}
				</Table.Cell>
			</Table.Row>
		{/if}
		{#if event.reference_id}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Reference</Table.Head>
				<Table.Cell>
					{#if isSessionStart}
						<ClickableSessionFilter sessionId={event.reference_id}
							>{event.reference_id}</ClickableSessionFilter
						>
					{:else}
						<ClickableReferenceFilter referenceId={event.reference_id}
							>{event.reference_id}</ClickableReferenceFilter
						>
					{/if}
				</Table.Cell>
			</Table.Row>
		{/if}
		{#each references as reference (reference.id)}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">{reference.name}</Table.Head>
				<Table.Cell
					><ClickableReferenceFilter referenceId={reference.id}
						>{reference.id}</ClickableReferenceFilter
					></Table.Cell
				>
			</Table.Row>
		{/each}
		{#if level}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Level</Table.Head>
				<Table.Cell
					><ClickableStringFilter term="level" value={level}
						><LogLevel {level}></LogLevel></ClickableStringFilter
					></Table.Cell
				>
			</Table.Row>
		{/if}
		{#if event.type !== 'error'}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Event Type</Table.Head>
				<Table.Cell
					><ClickableStringFilter term="type" value={event.type}
						>{event.type}</ClickableStringFilter
					></Table.Cell
				>
			</Table.Row>
		{/if}
		{#if hasError}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Error Type</Table.Head>
				<Table.Cell
					><ClickableStringFilter term="error.type" value={errorType}
						>{errorType}</ClickableStringFilter
					></Table.Cell
				>
			</Table.Row>
		{/if}
		{#if event.source}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Source</Table.Head>
				<Table.Cell
					><ClickableStringFilter term="source" value={event.source}
						>{event.source}</ClickableStringFilter
					></Table.Cell
				>
			</Table.Row>
		{/if}
		{#if !isSessionStart && event.value}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Value</Table.Head>
				<Table.Cell
					><ClickableNumberFilter term="value" value={event.value}
						>{event.value}</ClickableNumberFilter
					></Table.Cell
				>
			</Table.Row>
		{/if}
		{#if message}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Message</Table.Head>
				<Table.Cell
					><ClickableStringFilter term="message" value={message}
						>{message}</ClickableStringFilter
					></Table.Cell
				>
			</Table.Row>
		{/if}
		{#if version}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Version</Table.Head>
				<Table.Cell
					><ClickableVersionFilter term="version" value={version}
						>{version}</ClickableVersionFilter
					></Table.Cell
				>
			</Table.Row>
		{/if}
		{#if location}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Geo</Table.Head>
				<Table.Cell>{location}</Table.Cell>
			</Table.Row>
		{/if}
		{#if event.tags?.length}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Tags</Table.Head>
				<Table.Cell class="flex flex-wrap justify-start gap-2 overflow-auto">
					{#each event.tags as tag}
						<ClickableStringFilter term="tag" value={tag}
							><Badge color="dark">{tag}</Badge></ClickableStringFilter
						>
					{/each}
				</Table.Cell>
			</Table.Row>
		{/if}
		{#if requestUrl}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">URL</Table.Head>
				<Table.Cell class="flex items-center gap-x-1">
					<ClickableStringFilter term="path" value={requestUrlPath}
						>{requestUrl}</ClickableStringFilter
					>

					<Button
						href={requestUrl}
						target="_blank"
						variant="outline"
						size="icon"
						rel="noopener noreferrer"
						title="Open in new window"><IconOpenInNew /></Button
					></Table.Cell
				>
			</Table.Row>
		{/if}
	</Table.Body>
</Table.Root>

{#if userEmail || userIdentity || userName || userDescription}
	<H4 class="mt-4 mb-2">User Info</H4>
	<Table.Root>
		<Table.Body>
			{#if userEmail}
				<Table.Row>
					<Table.Head class="whitespace-nowrap">User Email</Table.Head>
					<Table.Cell
						><ClickableStringFilter term="user.email" value={userEmail}
							>{userEmail}</ClickableStringFilter
						>
						<A href="mailto:{userEmail}" title="Send email to {userEmail}"
							><IconEmail /></A
						></Table.Cell
					>
				</Table.Row>
			{/if}
			{#if userIdentity}
				<Table.Row>
					<Table.Head class="whitespace-nowrap">User Identity</Table.Head>
					<Table.Cell
						><ClickableStringFilter term="user" value={userIdentity}
							>{userIdentity}</ClickableStringFilter
						></Table.Cell
					>
				</Table.Row>
			{/if}
			{#if userName}
				<Table.Row>
					<Table.Head class="whitespace-nowrap">User Name</Table.Head>
					<Table.Cell
						><ClickableStringFilter term="user.name" value={userName}
							>{userName}</ClickableStringFilter
						></Table.Cell
					>
				</Table.Row>
			{/if}
			{#if userDescription}
				<Table.Row>
					<Table.Head class="whitespace-nowrap">User Description</Table.Head>
					<Table.Cell
						><ClickableStringFilter term="user.description" value={userDescription}
							>{userDescription}</ClickableStringFilter
						></Table.Cell
					>
				</Table.Row>
			{/if}</Table.Body
		>
	</Table.Root>
{/if}

{#if hasError}
	<div class="flex justify-between mt-4 mb-2">
		<H4>Stack Trace</H4>
		<div class="flex justify-end">
			<CopyToClipboardButton title="Copy Stack Trace to Clipboard" value={stackTrace}
			></CopyToClipboardButton>
		</div>
	</div>
	<div class="max-h-[150px] overflow-auto p-2 mt-2 text-xs">
		{#if event.data?.['@error']}
			<StackTrace error={event.data['@error']} />
		{:else}
			<SimpleStackTrace error={event.data?.['@simple_error']} />
		{/if}
	</div>
{/if}
