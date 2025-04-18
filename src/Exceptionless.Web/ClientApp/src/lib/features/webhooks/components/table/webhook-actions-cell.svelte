<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { deleteWebhook } from '$features/webhooks/api.svelte';
    import { Webhook } from '$features/webhooks/models';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
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
        <Button class="h-8 w-8 p-0" variant="ghost">
            <ChevronDown class="size-4" />
        </Button>
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
