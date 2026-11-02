<script lang="ts">
    import Check from '@lucide/svelte/icons/check';

    import type { DiscardedEnvironment } from '#features/projects/models.ts';

    import ErrorMessage from '#comp/error-message.svelte';
    import TimeAgo from '#comp/formatters/time-ago.svelte';
    import { Button } from '#comp/ui/button/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import * as Table from '#comp/ui/table/index.ts';

    interface Props {
        /** Adds the name to the list so its events are accepted from now on. */
        allow: (environment: DiscardedEnvironment) => Promise<unknown> | unknown;
        busy?: boolean;
        isError?: boolean;
        isLoading?: boolean;
        named: DiscardedEnvironment[];
        /** Events from names beyond the tracking limit. They are counted but cannot be allowed individually. */
        otherEvents?: number;
        otherNames?: number;
        otherNamesCapped?: boolean;
        retentionDays: number;
        /** Turns off discarding events that have no environment. */
        stopDiscardingUnspecified: () => Promise<unknown> | unknown;
        unspecified?: DiscardedEnvironment;
    }

    let {
        allow,
        busy = false,
        isError = false,
        isLoading = false,
        named,
        otherEvents = 0,
        otherNames = 0,
        otherNamesCapped = false,
        retentionDays,
        stopDiscardingUnspecified,
        unspecified
    }: Props = $props();

    const hasRows = $derived(named.length > 0 || !!unspecified || otherEvents > 0);
</script>

<div class="rounded-md border">
    <Table.Root>
        <Table.Header>
            <Table.Row>
                <Table.Head>Environment</Table.Head>
                <Table.Head class="w-36 text-right">Events</Table.Head>
                <Table.Head class="w-40 pl-6">Last seen</Table.Head>
                <Table.Head class="w-24"><span class="sr-only">Actions</span></Table.Head>
            </Table.Row>
        </Table.Header>
        <Table.Body>
            {#if isLoading}
                <Table.Row><Table.Cell colspan={4} class="h-24 text-center"><Spinner /></Table.Cell></Table.Row>
            {:else if isError}
                <Table.Row
                    ><Table.Cell colspan={4} class="h-24 text-center"><ErrorMessage message="Unable to load discarded environments." /></Table.Cell></Table.Row
                >
            {:else if !hasRows}
                <Table.Row
                    ><Table.Cell colspan={4} class="text-muted-foreground h-24 text-center">Nothing discarded in the last {retentionDays} days.</Table.Cell
                    ></Table.Row
                >
            {:else}
                {#each named as environment (environment.key)}
                    <Table.Row>
                        <Table.Cell class="font-medium">{environment.name ?? environment.key}</Table.Cell>
                        <Table.Cell class="text-right tabular-nums">{environment.count.toLocaleString()}</Table.Cell>
                        <Table.Cell class="text-muted-foreground pl-6"><TimeAgo value={environment.last_seen_utc} /></Table.Cell>
                        <Table.Cell class="text-right">
                            <Button
                                aria-label={`Allow ${environment.name ?? environment.key}`}
                                disabled={busy}
                                onclick={() => allow(environment)}
                                size="sm"
                                variant="outline"
                            >
                                <Check class="size-4" /> Allow
                            </Button>
                        </Table.Cell>
                    </Table.Row>
                {/each}
                {#if otherEvents > 0}
                    <Table.Row>
                        <Table.Cell class="text-muted-foreground italic" colspan={4}>
                            Plus {otherEvents.toLocaleString()} events from {otherNamesCapped ? 'at least ' : ''}{otherNames.toLocaleString()} more names, too many
                            to list here. Check the environment your clients send.
                        </Table.Cell>
                    </Table.Row>
                {/if}
                {#if unspecified}
                    <Table.Row>
                        <Table.Cell class="text-muted-foreground font-medium italic">No environment</Table.Cell>
                        <Table.Cell class="text-right tabular-nums">{unspecified.count.toLocaleString()}</Table.Cell>
                        <Table.Cell class="text-muted-foreground pl-6"><TimeAgo value={unspecified.last_seen_utc} /></Table.Cell>
                        <Table.Cell class="text-right">
                            <Button
                                aria-label="Stop discarding events without an environment"
                                disabled={busy}
                                onclick={() => stopDiscardingUnspecified()}
                                size="sm"
                                variant="outline"
                            >
                                <Check class="size-4" /> Allow
                            </Button>
                        </Table.Cell>
                    </Table.Row>
                {/if}
            {/if}
        </Table.Body>
    </Table.Root>
</div>
