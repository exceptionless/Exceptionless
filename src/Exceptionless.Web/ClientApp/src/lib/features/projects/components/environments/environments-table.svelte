<script lang="ts">
    import Plus from '@lucide/svelte/icons/plus';
    import Trash from '@lucide/svelte/icons/trash-2';

    import type { EnvironmentRow } from '#features/projects/environments.ts';

    import ErrorMessage from '#comp/error-message.svelte';
    import TimeAgo from '#comp/formatters/time-ago.svelte';
    import { Badge } from '#comp/ui/badge/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import { Switch } from '#comp/ui/switch/index.ts';
    import * as Table from '#comp/ui/table/index.ts';

    interface Props {
        add: (row: EnvironmentRow) => Promise<unknown> | unknown;
        busy?: boolean;
        isError?: boolean;
        isLoading?: boolean;
        /** What unlisted environments do, shown for rows that follow the project setting. */
        notifyOtherEnvironments: boolean;
        remove: (row: EnvironmentRow) => void;
        rows: EnvironmentRow[];
        toggleNotify: (row: EnvironmentRow, notify: boolean) => Promise<unknown> | unknown;
    }

    let { add, busy = false, isError = false, isLoading = false, notifyOtherEnvironments, remove, rows, toggleNotify }: Props = $props();
</script>

<div class="rounded-md border">
    <Table.Root>
        <Table.Header>
            <Table.Row>
                <Table.Head>Environment</Table.Head>
                <Table.Head>Listed</Table.Head>
                <Table.Head>Notifications</Table.Head>
                <Table.Head>Last seen</Table.Head>
                <Table.Head class="text-right">Events (30 days)</Table.Head>
                <Table.Head class="w-32"><span class="sr-only">Actions</span></Table.Head>
            </Table.Row>
        </Table.Header>
        <Table.Body>
            {#if isLoading}
                <Table.Row><Table.Cell colspan={6} class="h-24 text-center"><Spinner /></Table.Cell></Table.Row>
            {:else if isError}
                <Table.Row><Table.Cell colspan={6} class="h-24 text-center"><ErrorMessage message="Unable to load environments." /></Table.Cell></Table.Row>
            {:else if rows.length === 0}
                <Table.Row
                    ><Table.Cell colspan={6} class="text-muted-foreground h-24 text-center"
                        >No environments are listed and none have been seen in the last 30 days.</Table.Cell
                    ></Table.Row
                >
            {:else}
                {#each rows as row (row.key)}
                    <Table.Row>
                        <Table.Cell class="font-medium">{row.name}</Table.Cell>
                        <Table.Cell>
                            <Badge variant={row.isListed ? 'secondary' : 'outline'}>{row.isListed ? 'Listed' : 'Not listed'}</Badge>
                        </Table.Cell>
                        <Table.Cell>
                            {#if row.isListed}
                                <div class="flex items-center gap-2">
                                    <Switch
                                        aria-label={`Send notifications for ${row.name}`}
                                        checked={row.notify}
                                        disabled={busy}
                                        onCheckedChange={(checked) => toggleNotify(row, checked)}
                                    />
                                    <span class="text-muted-foreground text-xs">{row.notify ? 'On' : 'Off'}</span>
                                </div>
                            {:else}
                                <span class="text-muted-foreground text-xs">{notifyOtherEnvironments ? 'On' : 'Off'} (other environments)</span>
                            {/if}
                        </Table.Cell>
                        <Table.Cell>
                            {#if row.lastSeen}
                                <TimeAgo value={row.lastSeen} />
                            {:else}
                                <span class="text-muted-foreground">No recent events</span>
                            {/if}
                        </Table.Cell>
                        <Table.Cell class="text-right tabular-nums">{row.count.toLocaleString()}</Table.Cell>
                        <Table.Cell class="text-right">
                            {#if row.isListed}
                                <Button aria-label={`Remove ${row.name}`} disabled={busy} onclick={() => remove(row)} size="icon" variant="ghost">
                                    <Trash class="size-4" />
                                </Button>
                            {:else}
                                <Button aria-label={`Add ${row.name}`} disabled={busy} onclick={() => add(row)} size="sm" variant="outline">
                                    <Plus class="size-4" /> Add
                                </Button>
                            {/if}
                        </Table.Cell>
                    </Table.Row>
                {/each}
            {/if}
        </Table.Body>
    </Table.Root>
</div>
