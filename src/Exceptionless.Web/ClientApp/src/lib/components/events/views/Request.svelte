<script lang="ts">
	import IconOpenInNew from '~icons/mdi/open-in-new';
	import * as Table from '$comp/ui/table';
	import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
	import type { PersistentEvent } from '$lib/models/api';
	import ExtendedDataItem from '../ExtendedDataItem.svelte';
	import { getRequestInfoPath, getRequestInfoUrl } from '$lib/helpers/persistent-event';
	import { Button } from '$comp/ui/button';
	import H4 from '$comp/typography/H4.svelte';

	export let event: PersistentEvent;

	const request = event.data?.['@request'] ?? {};
	const requestUrl = getRequestInfoUrl(event);
	const requestUrlPath = getRequestInfoPath(event);

	const device = request.data?.['@device'];
	const browser = request.data?.['@browser'];
	const browserMajorVersion = request.data?.['@browser_major_version'];
	const browserVersion = request.data?.['@browser_version'];
	const os = request.data?.['@os'];
	const osMajorVersion = request.data?.['@os_major_version'];
	const osVersion = request.data?.['@os_version'];

	const excludedAdditionalData = [
		'@browser',
		'@browser_version',
		'@browser_major_version',
		'@device',
		'@os',
		'@os_version',
		'@os_major_version',
		'@is_bot'
	];

	const hasCookies = Object.keys(request.cookies ?? {}).length > 0;
	const hasHeaders = Object.keys(request.headers ?? {}).length > 0;
	const sortedHeaders = Object.keys(request.headers || {})
		.sort()
		.reduce(
			(acc, key) => {
				acc[key] = request.headers?.[key].join(',') ?? '';
				return acc;
			},
			<Record<string, string>>{}
		);
</script>

<Table.Root>
	<Table.Body>
		{#if request.http_method}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">HTTP Method</Table.Head>
				<Table.Cell>{request.http_method}</Table.Cell>
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
		{:else if requestUrlPath}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">URL</Table.Head>
				<Table.Cell>
					<ClickableStringFilter term="path" value={requestUrlPath}
						>{requestUrlPath}</ClickableStringFilter
					></Table.Cell
				>
			</Table.Row>
		{/if}
		{#if request.referrer}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Referrer</Table.Head>
				<Table.Cell class="flex items-center gap-x-1"
					>{request.referrer}
					<a
						href={request.referrer}
						target="_blank"
						class="link"
                        rel="noopener noreferrer"
						title="Open in new window"><IconOpenInNew /></a
					></Table.Cell
				>
			</Table.Row>
		{/if}
		{#if request.client_ip_address}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Client IP Address</Table.Head>
				<Table.Cell
					><ClickableStringFilter term="ip" value={request.client_ip_address}
						>{request.client_ip_address}</ClickableStringFilter
					></Table.Cell
				>
			</Table.Row>
		{/if}
		{#if request.user_agent}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">User Agent</Table.Head>
				<Table.Cell
					><ClickableStringFilter term="useragent" value={request.user_agent}
						>{request.user_agent}</ClickableStringFilter
					></Table.Cell
				>
			</Table.Row>
		{/if}
		{#if device}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Device</Table.Head>
				<Table.Cell
					><ClickableStringFilter term="device" value={device}
						>{device}</ClickableStringFilter
					></Table.Cell
				>
			</Table.Row>
		{/if}
		{#if browser}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Browser</Table.Head>
				<Table.Cell
					><ClickableStringFilter term="browser" value={browser}
						>{browser}</ClickableStringFilter
					>
					{#if browserMajorVersion}
						<abbr title={browserVersion}>
							<ClickableStringFilter term="browser.major" value={browserMajorVersion}
								>{browserMajorVersion}</ClickableStringFilter
							>
						</abbr>
					{/if}</Table.Cell
				>
			</Table.Row>
		{/if}
		{#if os}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Browser OS</Table.Head>
				<Table.Cell
					><ClickableStringFilter term="os" value={os}>{os}</ClickableStringFilter>
					{#if osMajorVersion}
						<abbr title={osVersion}>
							<ClickableStringFilter term="os.major" value={osMajorVersion}
								>{osMajorVersion}</ClickableStringFilter
							>
						</abbr>
					{/if}</Table.Cell
				>
			</Table.Row>
		{/if}
	</Table.Body>
</Table.Root>

{#if request.post_data}
	<div class="mt-2">
		<ExtendedDataItem canPromote={false} title="Post Data" data={request.post_data}
		></ExtendedDataItem>
	</div>
{/if}

{#if hasHeaders}
	<H4 class="mt-4 mb-2">Headers</H4>
	<Table.Root>
		<Table.Header>
			<Table.Row>
				<Table.Head>Name</Table.Head>
				<Table.Head>Value</Table.Head>
			</Table.Row>
		</Table.Header>
		<Table.Body>
			{#each Object.entries(sortedHeaders) as [key, value]}
				<Table.Row>
					<Table.Cell>{key}</Table.Cell>
					<Table.Cell><span class="inline line-clamp-3">{value}</span></Table.Cell>
				</Table.Row>
			{/each}
		</Table.Body>
	</Table.Root>
{/if}

{#if hasCookies}
	<H4 class="mt-4 mb-2">Cookie Values</H4>
	<Table.Root>
		<Table.Header>
			<Table.Row>
				<Table.Head>Name</Table.Head>
				<Table.Head>Value</Table.Head>
			</Table.Row>
		</Table.Header>
		<Table.Body>
			{#each Object.entries(request.cookies || {}) as [key, value]}
				<Table.Row>
					<Table.Cell>{key}</Table.Cell>
					<Table.Cell><span class="inline line-clamp-3">{value}</span></Table.Cell>
				</Table.Row>
			{/each}
		</Table.Body>
	</Table.Root>
{/if}

{#if request.data}
	<div class="mt-2">
		<ExtendedDataItem
			canPromote={false}
			title="Additional Data"
			data={request.data}
			excludedKeys={excludedAdditionalData}
		></ExtendedDataItem>
	</div>
{/if}
