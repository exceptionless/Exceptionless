<script lang="ts">
    import type { IFilter } from '$comp/filters/filters.svelte';

    import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
    import { H4 } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Table from '$comp/ui/table';
    import { getRequestInfoPath, getRequestInfoUrl } from '$features/events/persistent-event';
    import IconOpenInNew from '~icons/mdi/open-in-new';

    import type { PersistentEvent } from '../../models/index';

    import ExtendedDataItem from '../ExtendedDataItem.svelte';

    interface Props {
        changed: (filter: IFilter) => void;
        event: PersistentEvent;
    }

    let { changed, event }: Props = $props();
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
                <Table.Head class="w-40 whitespace-nowrap">HTTP Method</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell>{request.http_method}</Table.Cell>
            </Table.Row>
        {/if}
        {#if requestUrl}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">URL</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><ClickableStringFilter {changed} term="path" value={requestUrlPath} /></Table.Cell
                >
                <Table.Cell class="flex items-center gap-x-1"
                    >{requestUrl}<Button href={requestUrl} rel="noopener noreferrer" size="sm" target="_blank" title="Open in new window" variant="ghost"
                        ><IconOpenInNew /></Button
                    ></Table.Cell
                >
            </Table.Row>
        {:else if requestUrlPath}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">URL</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><ClickableStringFilter {changed} term="path" value={requestUrlPath} /></Table.Cell
                >
                <Table.Cell>{requestUrlPath}</Table.Cell>
            </Table.Row>
        {/if}
        {#if request.referrer}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Referrer</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell class="flex items-center gap-x-1"
                    >{request.referrer}<Button
                        href={request.referrer}
                        rel="noopener noreferrer"
                        size="sm"
                        target="_blank"
                        title="Open in new window"
                        variant="ghost"><IconOpenInNew /></Button
                    ></Table.Cell
                >
            </Table.Row>
        {/if}
        {#if request.client_ip_address}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Client IP Address</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><ClickableStringFilter {changed} term="ip" value={request.client_ip_address} /></Table.Cell
                >
                <Table.Cell class="flex items-center">{request.client_ip_address}</Table.Cell>
            </Table.Row>
        {/if}
        {#if request.user_agent}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">User Agent</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><ClickableStringFilter {changed} term="useragent" value={request.user_agent} /></Table.Cell
                >
                <Table.Cell>{request.user_agent}</Table.Cell>
            </Table.Row>
        {/if}
        {#if device}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Device</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"><ClickableStringFilter {changed} term="device" value={device} /></Table.Cell>
                <Table.Cell>{device}</Table.Cell>
            </Table.Row>
        {/if}
        {#if browser}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Browser</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"><ClickableStringFilter {changed} term="browser" value={browser} /></Table.Cell>
                <Table.Cell class="flex items-center"
                    >{browser}
                    {#if browserMajorVersion}&nbsp;
                        <ClickableStringFilter {changed} term="browser.major" value={browserMajorVersion} class="decoration-dotted"
                            >{browserVersion}</ClickableStringFilter
                        >
                    {/if}</Table.Cell
                >
            </Table.Row>
        {/if}
        {#if os}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Browser OS</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"><ClickableStringFilter {changed} term="os" value={os} /></Table.Cell>
                <Table.Cell class="flex items-center"
                    >{os}
                    {#if osMajorVersion}&nbsp;
                        <ClickableStringFilter {changed} term="os.major" value={osMajorVersion} class="decoration-dotted">{osVersion}</ClickableStringFilter>
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
    <H4 class="mb-2 mt-4">Headers</H4>
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
    <H4 class="mb-2 mt-4">Cookie Values</H4>
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
