<script lang="ts">
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Button, buttonVariants } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { Input } from '$comp/ui/input';
    import { getMigrationRerunQuery, postMigrationRerunMutation, queryKeys } from '$features/admin/api.svelte';
    import { getProblemMessage } from '$features/shared/validation';
    import EllipsisIcon from '@lucide/svelte/icons/ellipsis';
    import RefreshCcw from '@lucide/svelte/icons/refresh-ccw';
    import { useQueryClient } from '@tanstack/svelte-query';
    import { toast } from 'svelte-sonner';

    import type { MigrationStateRow } from './migrations-options.svelte';

    interface Props {
        migration: MigrationStateRow;
    }

    let { migration }: Props = $props();

    const queryClient = useQueryClient();
    const rerunMutation = postMigrationRerunMutation();
    let confirmation = $state('');
    let dialogOpen = $state(false);
    let operationId = $state<string>();
    let reportedStatus = $state<string>();
    const operationQuery = getMigrationRerunQuery(() => operationId);
    const expectedConfirmation = $derived(`RERUN ${migration.version}`);
    const operationActive = $derived(operationQuery.data?.status === 'Queued' || operationQuery.data?.status === 'Running');

    $effect(() => {
        const operation = operationQuery.data;
        if (!operation || operation.status === 'Queued' || operation.status === 'Running' || operation.status === reportedStatus) {
            return;
        }

        reportedStatus = operation.status;
        void queryClient.invalidateQueries({
            queryKey: queryKeys.migrations
        });
        if (operation.status === 'Completed') {
            toast.success(`Migration ${migration.version} rerun completed.`);
        } else {
            toast.error(operation.error_message || `Migration ${migration.version} rerun ${operation.status.toLowerCase()}.`);
        }
    });

    async function rerunMigration() {
        try {
            const result = await rerunMutation.mutateAsync({
                confirmation,
                version: migration.version
            });
            operationId = result.workers[0];
            reportedStatus = undefined;
            dialogOpen = false;
            confirmation = '';
            toast.success(`Migration ${migration.version} rerun queued.`);
        } catch (error) {
            toast.error(getProblemMessage(error, `Failed to queue migration ${migration.version} rerun.`));
        }
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button {...props} variant="ghost" size="icon" class="relative size-8 p-0" disabled={operationActive}>
                <span class="sr-only">Open actions for migration {migration.version}</span>
                <EllipsisIcon class="size-4" aria-hidden="true" />
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content align="end">
        <DropdownMenu.Item
            disabled={rerunMutation.isPending || operationActive}
            onclick={() => {
                confirmation = '';
                dialogOpen = true;
            }}
        >
            <RefreshCcw class="size-4" aria-hidden="true" />
            {operationActive ? 'Rerun in progress' : 'Rerun migration'}
        </DropdownMenu.Item>
    </DropdownMenu.Content>
</DropdownMenu.Root>

<AlertDialog.Root bind:open={dialogOpen}>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Rerun Migration {migration.version}</AlertDialog.Title>
            <AlertDialog.Description>
                This migration can rewrite Elasticsearch data. Take a snapshot and stop older Exceptionless instances before continuing. The rerun executes in
                the background and does not change the current migration version.
            </AlertDialog.Description>
        </AlertDialog.Header>

        <div class="space-y-2">
            <label class="text-sm font-medium" for={`migration-rerun-confirmation-${migration.version}`}>
                Enter <span class="font-mono">{expectedConfirmation}</span> to confirm
            </label>
            <Input id={`migration-rerun-confirmation-${migration.version}`} bind:value={confirmation} autocomplete="off" placeholder={expectedConfirmation} />
        </div>

        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action
                class={buttonVariants({
                    variant: 'destructive'
                })}
                disabled={rerunMutation.isPending || confirmation !== expectedConfirmation}
                onclick={() => void rerunMigration()}
            >
                {rerunMutation.isPending ? 'Queuing...' : 'Rerun Migration'}
            </AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
