<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';

    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import Duration from '$comp/formatters/duration.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import Live from '$comp/live.svelte';
    import { A, H3 } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Table from '$comp/ui/table';
    import * as EventsFacetedFilter from '$features/events/components/filters';
    import {
        getErrorType,
        getLocation,
        getMessage,
        getRequestInfoPath,
        getRequestInfoUrl,
        getStackTrace,
        hasErrorOrSimpleError
    } from '$features/events/persistent-event';
    import ExternalLink from 'lucide-svelte/icons/external-link';
    import Filter from 'lucide-svelte/icons/filter';
    import Email from 'lucide-svelte/icons/mail';

    import type { PersistentEvent } from '../../models/index';

    import LogLevel from '../log-level.svelte';
    import SimpleStackTrace from '../simple-stack-trace/simple-stack-trace.svelte';
    import StackTrace from '../stack-trace/stack-trace.svelte';

    interface Props {
        event: PersistentEvent;
        filterChanged: (filter: IFilter) => void;
    }

    let { event, filterChanged }: Props = $props();

    let hasError = $derived(hasErrorOrSimpleError(event));
    let errorType = $derived(hasError ? getErrorType(event) : null);
    let stackTrace = $derived(hasError ? getStackTrace(event) : null);

    let isSessionStart = $derived(event.type === 'session');
    let message = $derived(getMessage(event));

    const referencePrefix = '@ref:';
    let references = $derived.by(() => {
        let refs: { id: string; name: string }[] = [];
        Object.entries(event.data || {}).forEach(([key, value]) => {
            if (key.startsWith(referencePrefix)) {
                refs.push({ id: value as string, name: key.slice(5) });
            }
        });
        return refs;
    });

    let level = $derived(event.data?.['@level']?.toLowerCase());
    let location = $derived(getLocation(event));

    let userInfo = $derived(event.data?.['@user']);
    let userIdentity = $derived(userInfo?.identity);
    let userName = $derived(userInfo?.name);
    let userDescriptionInfo = $derived(event.data?.['@user_description']);
    let userEmail = $derived(userDescriptionInfo?.email_address);
    let userDescription = $derived(userDescriptionInfo?.description);

    let requestUrl = $derived(getRequestInfoUrl(event));
    let requestUrlPath = $derived(getRequestInfoPath(event));
    let version = $derived(event.data?.['@version']);

    function getSessionStartDuration(event: PersistentEvent): Date | number | string | undefined {
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
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Duration</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell>
                    <Live live={!event.data?.sessionend} liveTitle="Online" notLiveTitle="Ended" />
                    <Duration value={getSessionStartDuration(event)}></Duration>
                    {#if event.data?.sessionend}
                        (ended <TimeAgo value={event.data.sessionend}></TimeAgo>)
                    {/if}
                </Table.Cell>
            </Table.Row>
        {/if}
        {#if event.reference_id}
            <Table.Row class="group">
                {#if isSessionStart}
                    <Table.Head class="w-40 font-semibold whitespace-nowrap">Session</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><EventsFacetedFilter.SessionTrigger changed={filterChanged} value={event.reference_id} /></Table.Cell
                    >
                {:else}
                    <Table.Head class="w-40 font-semibold whitespace-nowrap">Reference</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><EventsFacetedFilter.ReferenceTrigger changed={filterChanged} value={event.reference_id} /></Table.Cell
                    >
                {/if}
                <Table.Cell>{event.reference_id}</Table.Cell>
            </Table.Row>
        {/if}
        {#each references as reference (reference.id)}
            <Table.Row class="group">
                {#if reference.name === 'session'}
                    <Table.Head class="w-40 font-semibold whitespace-nowrap">Session</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><EventsFacetedFilter.SessionTrigger changed={filterChanged} value={reference.id} /></Table.Cell
                    >
                {:else}
                    <Table.Head class="w-40 font-semibold whitespace-nowrap">{reference.name}</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><EventsFacetedFilter.ReferenceTrigger changed={filterChanged} value={reference.id} /></Table.Cell
                    >
                {/if}
                <Table.Cell>{reference.id}</Table.Cell>
            </Table.Row>
        {/each}
        {#if level}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Level</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.LevelTrigger changed={filterChanged} value={[level]} /></Table.Cell
                >
                <Table.Cell class="flex items-center"><LogLevel {level}></LogLevel></Table.Cell>
            </Table.Row>
        {/if}
        {#if event.type !== 'error'}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Event Type</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.TypeTrigger changed={filterChanged} value={[event.type as string]} /></Table.Cell
                >
                <Table.Cell>{event.type}</Table.Cell>
            </Table.Row>
        {/if}
        {#if hasError}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Error Type</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.StringTrigger changed={filterChanged} term="error.type" value={errorType} /></Table.Cell
                >
                <Table.Cell>{errorType}</Table.Cell>
            </Table.Row>
        {/if}
        {#if event.source}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Source</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.StringTrigger changed={filterChanged} term="source" value={event.source} /></Table.Cell
                >
                <Table.Cell>{event.source}</Table.Cell>
            </Table.Row>
        {/if}
        {#if !isSessionStart && event.value}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Value</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.NumberTrigger changed={filterChanged} term="value" value={event.value} /></Table.Cell
                >
                <Table.Cell>{event.value}</Table.Cell>
            </Table.Row>
        {/if}
        {#if message}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Message</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.StringTrigger changed={filterChanged} term="message" value={message} /></Table.Cell
                >
                <Table.Cell>{message}</Table.Cell>
            </Table.Row>
        {/if}
        {#if version}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Version</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.VersionTrigger changed={filterChanged} term="version" value={version} /></Table.Cell
                >
                <Table.Cell>{version}</Table.Cell>
            </Table.Row>
        {/if}
        {#if location}
            <Table.Row>
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Geo</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell>{location}</Table.Cell>
            </Table.Row>
        {/if}
        {#if event.tags?.length}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Tags</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell class="flex flex-wrap items-center justify-start gap-2 overflow-auto">
                    {#each event.tags as tag (tag)}
                        <Badge color="dark"
                            ><EventsFacetedFilter.TagTrigger changed={filterChanged} class="mr-1" value={[tag]}
                                ><Filter class="text-muted-foreground text-opacity-80 hover:text-secondary size-5" /></EventsFacetedFilter.TagTrigger
                            >{tag}</Badge
                        >
                    {/each}
                </Table.Cell>
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
        {/if}
    </Table.Body>
</Table.Root>

{#if userEmail || userIdentity || userName || userDescription}
    <H3 class="mt-4 mb-2">User Info</H3>
    <Table.Root>
        <Table.Body>
            {#if userEmail}
                <Table.Row class="group">
                    <Table.Head class="w-40 font-semibold whitespace-nowrap">User Email</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><EventsFacetedFilter.StringTrigger changed={filterChanged} term="user.email" value={userEmail} /></Table.Cell
                    >
                    <Table.Cell class="flex items-center">{userEmail}<A href="mailto:{userEmail}" title="Send email to {userEmail}"><Email /></A></Table.Cell>
                </Table.Row>
            {/if}
            {#if userIdentity}
                <Table.Row class="group">
                    <Table.Head class="w-40 font-semibold whitespace-nowrap">User Identity</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><EventsFacetedFilter.StringTrigger changed={filterChanged} term="user" value={userIdentity} /></Table.Cell
                    >
                    <Table.Cell>{userIdentity}</Table.Cell>
                </Table.Row>
            {/if}
            {#if userName}
                <Table.Row class="group">
                    <Table.Head class="w-40 font-semibold whitespace-nowrap">User Name</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><EventsFacetedFilter.StringTrigger changed={filterChanged} term="user.name" value={userName} /></Table.Cell
                    >
                    <Table.Cell>{userName}</Table.Cell>
                </Table.Row>
            {/if}
            {#if userDescription}
                <Table.Row class="group">
                    <Table.Head class="w-40 font-semibold whitespace-nowrap">User Description</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><EventsFacetedFilter.StringTrigger changed={filterChanged} term="user.description" value={userDescription} /></Table.Cell
                    >
                    <Table.Cell>{userDescription}</Table.Cell>
                </Table.Row>
            {/if}</Table.Body
        >
    </Table.Root>
{/if}

{#if hasError}
    <div class="mt-4 flex justify-between">
        <H3>Stack Trace</H3>
        <div class="flex justify-end">
            <CopyToClipboardButton title="Copy Stack Trace to Clipboard" value={stackTrace}></CopyToClipboardButton>
        </div>
    </div>
    <div class="mt-2 max-h-[300px] grow overflow-auto text-xs">
        {#if event.data?.['@error']}
            <StackTrace error={event.data['@error']} />
        {:else if event.data?.['@simple_error']}
            <SimpleStackTrace error={event.data?.['@simple_error']} />
        {/if}
    </div>
{/if}
