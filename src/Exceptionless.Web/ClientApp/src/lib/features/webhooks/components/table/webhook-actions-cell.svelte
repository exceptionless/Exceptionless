<script lang="ts">
    import type { Webhook } from '$features/webhooks/models';

    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { deleteWebhook } from '$features/webhooks/api.svelte';
    import EllipsisIcon from '@lucide/svelte/icons/ellipsis';
    import X from '@lucide/svelte/icons/x';
    import { toast } from 'svelte-sonner';

    import RemoveWebhookDialog from '../dialogs/remove-webhook-dialog.svelte';

    interface Props {
        webhook: Webhook;
    }

    let { webhook }: Props = $props();
    let showRemoveWebhookDialog = $state(false);

    const removeWebhook = deleteWebhook({
        route: {
            get ids() {
                return [webhook.id!];
            }
        }
    });

    async function remove() {
        await removeWebhook.mutateAsync();
        toast.success('Successfully deleted webhook');
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button {...props} variant="ghost" size="icon" class="relative size-8 p-0">
                <span class="sr-only">Open menu</span>
                <EllipsisIcon />
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content align="end">
        <DropdownMenu.Item onclick={() => (showRemoveWebhookDialog = true)} disabled={removeWebhook.isPending}>
            <X class="size-4" />
            Delete
        </DropdownMenu.Item>
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if showRemoveWebhookDialog}
    <RemoveWebhookDialog bind:open={showRemoveWebhookDialog} url={webhook.url} {remove} />
{/if}
