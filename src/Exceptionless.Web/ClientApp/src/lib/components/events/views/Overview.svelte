<script lang="ts">
    import IconEmail from '~icons/mdi/email';
    import IconFilter from '~icons/mdi/filter';
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
    import { A, H4 } from '$comp/typography';
    import ClickableTypeFilter from '$comp/filters/ClickableTypeFilter.svelte';
    import type { IFilter } from '$comp/filters/filters';

    interface Props {
        event: PersistentEvent;
        changed: (filter: IFilter) => void;
    }

    let { event, changed }: Props = $props();

    let hasError = $derived(hasErrorOrSimpleError(event));
    let errorType = $derived(hasError ? getErrorType(event) : null);
    let stackTrace = $derived(hasError ? getStackTrace(event) : null);

    let isSessionStart = $derived(event.type === 'session');
    let message = $derived(getMessage(event));

    const referencePrefix = '@ref:';
    let references: { id: string; name: string }[] = $derived.by(() => {
        let refs = [];
        Object.entries(event.data || {}).forEach(([key, value]) => {
            if (key.startsWith(referencePrefix)) {
                references.push({ id: value as string, name: key.slice(5) });
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
                <Table.Head class="w-40 whitespace-nowrap">Duration</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell>
                    {#if !event.data?.sessionend}
                        <span class="inline-flex h-2 w-2 animate-pulse items-center rounded-full bg-green-500" title="Online"></span>
                    {/if}
                    <Duration value={getSessionStartDuration(event)}></Duration>
                    {#if event.data?.sessionend}
                        (ended <TimeAgo value={event.data.sessionend}></TimeAgo>)
                    {/if}
                </Table.Cell>
            </Table.Row>
        {/if}
        {#if event.reference_id}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Reference</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    >{#if isSessionStart}
                        <ClickableSessionFilter value={event.reference_id} {changed} />
                    {:else}
                        <ClickableReferenceFilter value={event.reference_id} {changed} />
                    {/if}</Table.Cell
                >
                <Table.Cell>{event.reference_id}</Table.Cell>
            </Table.Row>
        {/if}
        {#each references as reference (reference.id)}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">{reference.name}</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"><ClickableReferenceFilter value={reference.id} {changed} /></Table.Cell>
                <Table.Cell>{reference.id}</Table.Cell>
            </Table.Row>
        {/each}
        {#if level}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Level</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"><ClickableStringFilter term="level" value={level} {changed} /></Table.Cell>
                <Table.Cell class="flex items-center"><LogLevel {level}></LogLevel></Table.Cell>
            </Table.Row>
        {/if}
        {#if event.type !== 'error'}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Event Type</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"><ClickableTypeFilter value={[event.type]} {changed} /></Table.Cell>
                <Table.Cell>{event.type}</Table.Cell>
            </Table.Row>
        {/if}
        {#if hasError}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Error Type</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><ClickableStringFilter term="error.type" value={errorType} {changed} /></Table.Cell
                >
                <Table.Cell>{errorType}</Table.Cell>
            </Table.Row>
        {/if}
        {#if event.source}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Source</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><ClickableStringFilter term="source" value={event.source} {changed} /></Table.Cell
                >
                <Table.Cell>{event.source}</Table.Cell>
            </Table.Row>
        {/if}
        {#if !isSessionStart && event.value}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Value</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"><ClickableNumberFilter term="value" value={event.value} {changed} /></Table.Cell>
                <Table.Cell>{event.value}</Table.Cell>
            </Table.Row>
        {/if}
        {#if message}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Message</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"><ClickableStringFilter term="message" value={message} {changed} /></Table.Cell>
                <Table.Cell>{message}</Table.Cell>
            </Table.Row>
        {/if}
        {#if version}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Version</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"><ClickableVersionFilter term="version" value={version} {changed} /></Table.Cell>
                <Table.Cell>{version}</Table.Cell>
            </Table.Row>
        {/if}
        {#if location}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Geo</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell>{location}</Table.Cell>
            </Table.Row>
        {/if}
        {#if event.tags?.length}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Tags</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell class="flex flex-wrap items-center justify-start gap-2 overflow-auto">
                    {#each event.tags as tag (tag)}
                        <Badge color="dark"
                            ><ClickableStringFilter term="tag" value={tag} {changed} class="mr-1"
                                ><IconFilter class="text-muted-foreground text-opacity-80 hover:text-secondary" /></ClickableStringFilter
                            >{tag}</Badge
                        >
                    {/each}
                </Table.Cell>
            </Table.Row>
        {/if}
        {#if requestUrl}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">URL</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><ClickableStringFilter term="path" value={requestUrlPath} {changed} /></Table.Cell
                >
                <Table.Cell class="flex items-center gap-x-1"
                    >{requestUrl}<Button href={requestUrl} target="_blank" variant="outline" size="icon" rel="noopener noreferrer" title="Open in new window"
                        ><IconOpenInNew /></Button
                    ></Table.Cell
                >
            </Table.Row>
        {/if}
    </Table.Body>
</Table.Root>

{#if userEmail || userIdentity || userName || userDescription}
    <H4 class="mb-2 mt-4">User Info</H4>
    <Table.Root>
        <Table.Body>
            {#if userEmail}
                <Table.Row class="group">
                    <Table.Head class="w-40 whitespace-nowrap">User Email</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><ClickableStringFilter term="user.email" value={userEmail} {changed} /></Table.Cell
                    >
                    <Table.Cell class="flex items-center"
                        >{userEmail}<A href="mailto:{userEmail}" title="Send email to {userEmail}"><IconEmail /></A></Table.Cell
                    >
                </Table.Row>
            {/if}
            {#if userIdentity}
                <Table.Row class="group">
                    <Table.Head class="w-40 whitespace-nowrap">User Identity</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><ClickableStringFilter term="user" value={userIdentity} {changed} /></Table.Cell
                    >
                    <Table.Cell>{userIdentity}</Table.Cell>
                </Table.Row>
            {/if}
            {#if userName}
                <Table.Row class="group">
                    <Table.Head class="w-40 whitespace-nowrap">User Name</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><ClickableStringFilter term="user.name" value={userName} {changed} /></Table.Cell
                    >
                    <Table.Cell>{userName}</Table.Cell>
                </Table.Row>
            {/if}
            {#if userDescription}
                <Table.Row class="group">
                    <Table.Head class="w-40 whitespace-nowrap">User Description</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><ClickableStringFilter term="user.description" value={userDescription} {changed} /></Table.Cell
                    >
                    <Table.Cell>{userDescription}</Table.Cell>
                </Table.Row>
            {/if}</Table.Body
        >
    </Table.Root>
{/if}

{#if hasError}
    <div class="mb-2 mt-4 flex justify-between">
        <H4>Stack Trace</H4>
        <div class="flex justify-end">
            <CopyToClipboardButton title="Copy Stack Trace to Clipboard" value={stackTrace}></CopyToClipboardButton>
        </div>
    </div>
    <div class="mt-2 max-h-[150px] overflow-auto p-2 text-xs">
        {#if event.data?.['@error']}
            <StackTrace error={event.data['@error']} />
        {:else}
            <SimpleStackTrace error={event.data?.['@simple_error']} />
        {/if}
    </div>
{/if}
