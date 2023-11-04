<script lang="ts">
	import IconOpenInNew from '~icons/mdi/open-in-new';
	import type { PersistentEvent } from '$lib/models/api';
	import DateTime from '$comp/formatters/DateTime.svelte';
	import TimeAgo from '$comp/formatters/TimeAgo.svelte';
	import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
	import ClickableDateFilter from '$comp/filters/ClickableDateFilter.svelte';
	import ExtendedDataItem from '../ExtendedDataItem.svelte';
	import { getRequestInfoPath, getRequestInfoUrl } from '$lib/helpers/persistent-event';

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

<table class="table table-zebra table-xs border border-base-300">
	<tbody>
		<tr>
			<th class="border border-base-300 whitespace-nowrap">Occurred On</th>
			<td class="border border-base-300"
				><ClickableDateFilter term="date" value={event.date}
					><DateTime value={event.date}></DateTime> (<TimeAgo value={event.date}
					></TimeAgo>)</ClickableDateFilter
				></td
			>
		</tr>
		{#if request.http_method}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">HTTP Method</th>
				<td class="border border-base-300">{request.http_method}</td>
			</tr>
		{/if}
		{#if requestUrl}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">URL</th>
				<td class="border border-base-300">
					<ClickableStringFilter term="path" value={requestUrlPath}
						>{requestUrl}</ClickableStringFilter
					>

					<a href={requestUrl} target="_blank" class="link" title="Open in new window"
						><IconOpenInNew /></a
					></td
				>
			</tr>
		{:else if requestUrlPath}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">URL</th>
				<td class="border border-base-300">
					<ClickableStringFilter term="path" value={requestUrlPath}
						>{requestUrlPath}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if request.referrer}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Referrer</th>
				<td class="border border-base-300 flex items-center gap-x-1"
					>{request.referrer}
					<a
						href={request.referrer}
						target="_blank"
						class="link"
						title="Open in new window"><IconOpenInNew /></a
					></td
				>
			</tr>
		{/if}
		{#if request.client_ip_address}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Client IP Address</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="ip" value={request.client_ip_address}
						>{request.client_ip_address}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if request.user_agent}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">User Agent</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="useragent" value={request.user_agent}
						>{request.user_agent}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if device}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Device</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="device" value={device}
						>{device}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if browser}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Browser</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="browser" value={browser}
						>{browser}</ClickableStringFilter
					>
					{#if browserMajorVersion}
						<abbr title={browserVersion}>
							<ClickableStringFilter term="browser.major" value={browserMajorVersion}
								>{browserMajorVersion}</ClickableStringFilter
							>
						</abbr>
					{/if}</td
				>
			</tr>
		{/if}
		{#if os}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Browser OS</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="os" value={os}>{os}</ClickableStringFilter>
					{#if osMajorVersion}
						<abbr title={osVersion}>
							<ClickableStringFilter term="os.major" value={osMajorVersion}
								>{osMajorVersion}</ClickableStringFilter
							>
						</abbr>
					{/if}</td
				>
			</tr>
		{/if}
	</tbody>
</table>

{#if request.post_data}
	<div class="mt-2">
		<ExtendedDataItem canPromote={false} title="Post Data" data={request.post_data}
		></ExtendedDataItem>
	</div>
{/if}

{#if hasHeaders}
	<h4 class="text-lg mt-4 mb-2">Headers</h4>
	<table class="table table-zebra table-xs border border-base-300">
		<thead>
			<tr>
				<th class="border border-base-300">Name</th>
				<th class="border border-base-300">Value</th>
			</tr>
		</thead>
		<tbody>
			{#each Object.entries(sortedHeaders) as [key, value]}
				<tr>
					<td class="border border-base-300">{key}</td>
					<td class="border border-base-300"
						><span class="inline line-clamp-3">{value}</span></td
					>
				</tr>
			{/each}
		</tbody>
	</table>
{/if}

{#if hasCookies}
	<h4 class="text-lg mt-4 mb-2">Cookie Values</h4>
	<table class="table table-zebra table-xs border border-base-300">
		<thead>
			<tr>
				<th class="border border-base-300">Name</th>
				<th class="border border-base-300">Value</th>
			</tr>
		</thead>
		<tbody>
			{#each Object.entries(request.cookies || {}) as [key, value]}
				<tr>
					<td class="border border-base-300">{key}</td>
					<td class="border border-base-300"
						><span class="inline line-clamp-3">{value}</span></td
					>
				</tr>
			{/each}
		</tbody>
	</table>
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
