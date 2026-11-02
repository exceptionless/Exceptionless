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
        /** Events from environments that are not in the list are being discarded. */
        discardUnlisted: boolean;
        isError?: boolean;
        isLoading?: boolean;
        notifyOtherEnvironments: boolean;
        remove: (row: EnvironmentRow) => void;
        rows: EnvironmentRow[];
        toggleNotify: (row: EnvironmentRow, notify: boolean) => Promise<unknown> | unknown;
        toggleNotifyOther: (notify: boolean) => Promise<unknown> | unknown;
    }

    let {
        add,
        busy = false,
        discardUnlisted,
        isError = false,
        isLoading = false,
        notifyOtherEnvironments,
        remove,
        rows,
        toggleNotify,
        toggleNotifyOther
    }: Props = $props();
</script>

<div class="rounded-md border">
    <Table.Root>
        <Table.Header>
            <Table.Row>
                <Table.Head>Environment</Table.Head>
                <Table.Head class="w-32">Notifications</Table.Head>
                <Table.Head class="w-36 text-right">Events (30 days)</Table.Head>
                <Table.Head class="w-40 pl-6">Last seen</Table.Head>
                <Table.Head class="w-24"><span class="sr-only">Actions</span></Table.Head>
            </Table.Row>
        </Table.Header>
        <Table.Body>
            {#if isLoading}
                <Table.Row><Table.Cell colspan={5} class="h-24 text-center"><Spinner /></Table.Cell></Table.Row>
            {:else if isError}
                <Table.Row><Table.Cell colspan={5} class="h-24 text-center"><ErrorMessage message="Unable to load environments." /></Table.Cell></Table.Row>
            {:else}
                {#each rows as row (row.key)}
                    <Table.Row>
                        <Table.Cell>
                            <span class={row.isListed ? 'font-medium' : 'text-muted-foreground'}>{row.name}</span>
                            {#if !row.isListed}
                                <Badge class="ml-2" variant={discardUnlisted ? 'destructive' : 'outline'}>{discardUnlisted ? 'Discarded' : 'Not added'}</Badge>
                            {/if}
                        </Table.Cell>
                        <Table.Cell>
                            {#if row.isListed}
                                <Switch
                                    aria-label={`Send notifications for ${row.name}`}
                                    checked={row.notify}
                                    disabled={busy}
                                    onCheckedChange={(checked) => toggleNotify(row, checked)}
                                />
                            {:else}
                                {#if discardUnlisted}
                                    <span class="text-muted-foreground">—</span>
                                {:else}
                                    <span class="text-muted-foreground text-xs" title="Follows the Other environments setting below">Default</span>
                                {/if}
                            {/if}
                        </Table.Cell>
                        <Table.Cell class="text-right tabular-nums">{row.count.toLocaleString()}</Table.Cell>
                        <Table.Cell class="text-muted-foreground pl-6">
                            {#if row.lastSeen}
                                <TimeAgo value={row.lastSeen} />
                            {:else}
                                Not in 30 days
                            {/if}
                        </Table.Cell>
                        <Table.Cell class="text-right">
                            {#if row.isListed}
                                <Button
                                    aria-label={`Remove ${row.name}`}
                                    disabled={busy}
                                    onclick={() => remove(row)}
                                    size="icon"
                                    title="Remove"
                                    variant="ghost"
                                >
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
                <Table.Row class="bg-muted/30 hover:bg-muted/30">
                    <Table.Cell>
                        <div class="font-medium">Other environments</div>
                        <div class="text-muted-foreground text-xs">Not in this list, or none sent</div>
                    </Table.Cell>
                    <Table.Cell>
                        <Switch
                            aria-label="Send notifications for other environments"
                            checked={notifyOtherEnvironments}
                            disabled={busy}
                            onCheckedChange={(checked) => toggleNotifyOther(checked)}
                        />
                    </Table.Cell>
                    <Table.Cell colspan={3}></Table.Cell>
                </Table.Row>
            {/if}
        </Table.Body>
    </Table.Root>
</div>
