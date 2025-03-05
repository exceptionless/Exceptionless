<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';

    import { H3 } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Table from '$comp/ui/table';
    import * as EventsFacetedFilter from '$features/events/components/filters';
    import { getRequestInfoPath, getRequestInfoUrl } from '$features/events/persistent-event';
    import ExternalLink from 'lucide-svelte/icons/external-link';

    import type { PersistentEvent } from '../../models/index';

    import ExtendedDataItem from '../extended-data-item.svelte';

    interface Props {
        event: PersistentEvent;
        filterChanged: (filter: IFilter) => void;
    }

    let { event, filterChanged }: Props = $props();
    let request = $derived(event.data?.['@request'] ?? {});
    let requestUrl = $derived(getRequestInfoUrl(event));
    let requestUrlPath = $derived(getRequestInfoPath(event));

    let device = $derived(request.data?.['@device']);
    let browser = $derived(request.data?.['@browser']);
    let browserMajorVersion = $derived(request.data?.['@browser_major_version']);
    let browserVersion = $derived(request.data?.['@browser_version']);
    let os = $derived(request.data?.['@os']);
    let osMajorVersion = $derived(request.data?.['@os_major_version']);
    let osVersion = $derived(request.data?.['@os_version']);

    const excludedAdditionalData = ['@browser', '@browser_version', '@browser_major_version', '@device', '@os', '@os_version', '@os_major_version', '@is_bot'];

    let hasCookies = $derived(Object.keys(request.cookies ?? {}).length > 0);
    let hasHeaders = $derived(Object.keys(request.headers ?? {}).length > 0);
    let sortedHeaders = $derived(
        Object.keys(request.headers || {})
            .sort()
            .reduce(
                (acc, key) => {
                    acc[key] = request.headers?.[key]?.join(',') ?? '';
                    return acc;
                },
                {} as Record<string, string>
            )
    );
</script>

<Table.Root>
    <Table.Body>
        {#if request.http_method}
            <Table.Row>
                <Table.Head class="w-40 font-semibold whitespace-nowrap">HTTP Method</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell>{request.http_method}</Table.Cell>
            </Table.Row>
        {/if}
        {#if requestUrl}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">URL</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.StringTrigger changed={filterChanged} term="path" value={requestUrlPath} /></Table.Cell
                >
                <Table.Cell class="flex items-center gap-x-1"
                    >{requestUrl}<Button href={requestUrl} rel="noopener noreferrer" size="sm" target="_blank" title="Open in new window" variant="ghost"
                        ><ExternalLink /></Button
                    ></Table.Cell
                >
            </Table.Row>
        {:else if requestUrlPath}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">URL</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.StringTrigger changed={filterChanged} term="path" value={requestUrlPath} /></Table.Cell
                >
                <Table.Cell>{requestUrlPath}</Table.Cell>
            </Table.Row>
        {/if}
        {#if request.referrer}
            <Table.Row>
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Referrer</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell class="flex items-center gap-x-1"
                    >{request.referrer}<Button
                        href={request.referrer}
                        rel="noopener noreferrer"
                        size="sm"
                        target="_blank"
                        title="Open in new window"
                        variant="ghost"><ExternalLink /></Button
                    ></Table.Cell
                >
            </Table.Row>
        {/if}
        {#if request.client_ip_address}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Client IP Address</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.StringTrigger changed={filterChanged} term="ip" value={request.client_ip_address} /></Table.Cell
                >
                <Table.Cell class="flex items-center">{request.client_ip_address}</Table.Cell>
            </Table.Row>
        {/if}
        {#if request.user_agent}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">User Agent</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.StringTrigger changed={filterChanged} term="useragent" value={request.user_agent} /></Table.Cell
                >
                <Table.Cell>{request.user_agent}</Table.Cell>
            </Table.Row>
        {/if}
        {#if device}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Device</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.StringTrigger changed={filterChanged} term="device" value={device} /></Table.Cell
                >
                <Table.Cell>{device}</Table.Cell>
            </Table.Row>
        {/if}
        {#if browser}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Browser</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.StringTrigger changed={filterChanged} term="browser" value={browser} /></Table.Cell
                >
                <Table.Cell class="flex items-center"
                    >{browser}
                    {#if browserMajorVersion}&nbsp;
                        <EventsFacetedFilter.StringTrigger changed={filterChanged} term="browser.major" value={browserMajorVersion} class="decoration-dotted"
                            >{browserVersion}</EventsFacetedFilter.StringTrigger
                        >
                    {/if}</Table.Cell
                >
            </Table.Row>
        {/if}
        {#if os}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Browser OS</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.StringTrigger changed={filterChanged} term="os" value={os} /></Table.Cell
                >
                <Table.Cell class="flex items-center"
                    >{os}
                    {#if osMajorVersion}&nbsp;
                        <EventsFacetedFilter.StringTrigger changed={filterChanged} term="os.major" value={osMajorVersion} class="decoration-dotted"
                            >{osVersion}</EventsFacetedFilter.StringTrigger
                        >
                    {/if}</Table.Cell
                >
            </Table.Row>
        {/if}
    </Table.Body>
</Table.Root>

{#if request.post_data}
    <div class="mt-2">
        <ExtendedDataItem canPromote={false} data={request.post_data} title="Post Data"></ExtendedDataItem>
    </div>
{/if}

{#if hasHeaders}
    <H3 class="mt-4 mb-2">Headers</H3>
    <Table.Root>
        <Table.Header>
            <Table.Row>
                <Table.Head>Name</Table.Head>
                <Table.Head>Value</Table.Head>
            </Table.Row>
        </Table.Header>
        <Table.Body>
            {#each Object.entries(sortedHeaders) as [key, value] (key)}
                <Table.Row>
                    <Table.Cell>{key}</Table.Cell>
                    <Table.Cell><span class="line-clamp-3 inline">{value}</span></Table.Cell>
                </Table.Row>
            {/each}
        </Table.Body>
    </Table.Root>
{/if}

{#if hasCookies}
    <H3 class="mt-4 mb-2">Cookie Values</H3>
    <Table.Root>
        <Table.Header>
            <Table.Row>
                <Table.Head>Name</Table.Head>
                <Table.Head>Value</Table.Head>
            </Table.Row>
        </Table.Header>
        <Table.Body>
            {#each Object.entries(request.cookies || {}) as [key, value] (key)}
                <Table.Row>
                    <Table.Cell>{key}</Table.Cell>
                    <Table.Cell><span class="line-clamp-3 inline">{value}</span></Table.Cell>
                </Table.Row>
            {/each}
        </Table.Body>
    </Table.Root>
{/if}

{#if request.data}
    <div class="mt-2">
        <ExtendedDataItem canPromote={false} data={request.data} excludedKeys={excludedAdditionalData} title="Additional Data"></ExtendedDataItem>
    </div>
{/if}
