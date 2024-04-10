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
    import H4 from '$comp/typography/H4.svelte';
    import A from '$comp/typography/A.svelte';
    import ClickableTypeFilter from '$comp/filters/ClickableTypeFilter.svelte';

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
                <Table.Head class="w-40 whitespace-nowrap">Duration</Table.Head>
                <Table.Cell>
                    {#if !event.data?.sessionend}
                        <span class="inline-flex h-2 w-2 animate-pulse items-center rounded-full bg-green-500" title="Online"></span>
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
                <Table.Head class="w-40 whitespace-nowrap">Reference</Table.Head>
                <Table.Cell class="flex items-center">
                    {#if isSessionStart}
                        <ClickableSessionFilter value={event.reference_id} />{event.reference_id}
                    {:else}
                        <ClickableReferenceFilter value={event.reference_id} />{event.reference_id}
                    {/if}
                </Table.Cell>
            </Table.Row>
        {/if}
        {#each references as reference (reference.id)}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">{reference.name}</Table.Head>
                <Table.Cell class="flex items-center"><ClickableReferenceFilter value={reference.id} />{reference.id}</Table.Cell>
            </Table.Row>
        {/each}
        {#if level}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Level</Table.Head>
                <Table.Cell class="flex items-center"><ClickableStringFilter term="level" value={level} /><LogLevel {level}></LogLevel></Table.Cell>
            </Table.Row>
        {/if}
        {#if event.type !== 'error'}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Event Type</Table.Head>
                <Table.Cell class="flex items-center"><ClickableTypeFilter value={[event.type]} />{event.type}</Table.Cell>
            </Table.Row>
        {/if}
        {#if hasError}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Error Type</Table.Head>
                <Table.Cell class="flex items-center"><ClickableStringFilter term="error.type" value={errorType} />{errorType}</Table.Cell>
            </Table.Row>
        {/if}
        {#if event.source}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Source</Table.Head>
                <Table.Cell class="flex items-center"><ClickableStringFilter term="source" value={event.source} />{event.source}</Table.Cell>
            </Table.Row>
        {/if}
        {#if !isSessionStart && event.value}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Value</Table.Head>
                <Table.Cell class="flex items-center"><ClickableNumberFilter term="value" value={event.value} />{event.value}</Table.Cell>
            </Table.Row>
        {/if}
        {#if message}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Message</Table.Head>
                <Table.Cell class="flex items-center"><ClickableStringFilter term="message" value={message} />{message}</Table.Cell>
            </Table.Row>
        {/if}
        {#if version}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Version</Table.Head>
                <Table.Cell class="flex items-center"><ClickableVersionFilter term="version" value={version} />{version}</Table.Cell>
            </Table.Row>
        {/if}
        {#if location}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Geo</Table.Head>
                <Table.Cell>{location}</Table.Cell>
            </Table.Row>
        {/if}
        {#if event.tags?.length}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Tags</Table.Head>
                <Table.Cell class="flex flex-wrap items-center justify-start gap-2 overflow-auto">
                    {#each event.tags as tag (tag)}
                        <Badge color="dark"
                            ><ClickableStringFilter term="tag" value={tag} class="mr-1"
                                ><IconFilter class="text-muted-foreground text-opacity-80 hover:text-secondary" /></ClickableStringFilter
                            >{tag}</Badge
                        >
                    {/each}
                </Table.Cell>
            </Table.Row>
        {/if}
        {#if requestUrl}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">URL</Table.Head>
                <Table.Cell class="flex items-center gap-x-1">
                    <ClickableStringFilter term="path" value={requestUrlPath} />{requestUrl}

                    <Button href={requestUrl} target="_blank" variant="outline" size="icon" rel="noopener noreferrer" title="Open in new window"
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
                <Table.Row>
                    <Table.Head class="w-40 whitespace-nowrap">User Email</Table.Head>
                    <Table.Cell class="flex items-center"
                        ><ClickableStringFilter term="user.email" value={userEmail} />{userEmail}
                        <A href="mailto:{userEmail}" title="Send email to {userEmail}"><IconEmail /></A></Table.Cell
                    >
                </Table.Row>
            {/if}
            {#if userIdentity}
                <Table.Row>
                    <Table.Head class="w-40 whitespace-nowrap">User Identity</Table.Head>
                    <Table.Cell class="flex items-center"><ClickableStringFilter term="user" value={userIdentity} />{userIdentity}</Table.Cell>
                </Table.Row>
            {/if}
            {#if userName}
                <Table.Row>
                    <Table.Head class="w-40 whitespace-nowrap">User Name</Table.Head>
                    <Table.Cell class="flex items-center"><ClickableStringFilter term="user.name" value={userName} />{userName}</Table.Cell>
                </Table.Row>
            {/if}
            {#if userDescription}
                <Table.Row>
                    <Table.Head class="w-40 whitespace-nowrap">User Description</Table.Head>
                    <Table.Cell class="flex items-center"><ClickableStringFilter term="user.description" value={userDescription} />{userDescription}</Table.Cell
                    >
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
