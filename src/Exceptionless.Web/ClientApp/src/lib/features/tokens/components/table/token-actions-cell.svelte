<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { deleteToken, patchToken } from '$features/tokens/api.svelte';
    import { ViewToken } from '$features/tokens/models';
    import { UseClipboard } from '$lib/hooks/use-clipboard.svelte';
    import Disable from '@lucide/svelte/icons/ban';
    import Enable from '@lucide/svelte/icons/check';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import Copy from '@lucide/svelte/icons/copy';
    import Edit from '@lucide/svelte/icons/pen';
    import X from '@lucide/svelte/icons/x';
    import { toast } from 'svelte-sonner';

    import DisableTokenDialog from '../dialogs/disable-token-dialog.svelte';
    import EnableTokenDialog from '../dialogs/enable-token-dialog.svelte';
    import RemoveTokenDialog from '../dialogs/remove-token-dialog.svelte';
    import UpdateTokenNotesDialog from '../dialogs/update-token-notes-dialog.svelte';

    interface Props {
        token: ViewToken;
    }

    let { token }: Props = $props();
    let showRemoveTokenDialog = $state(false);
    let showUpdateNotesDialog = $state(false);
    let showEnableTokenDialog = $state(false);
    let showDisableTokenDialog = $state(false);

    const clipboard = new UseClipboard();

    async function copyToClipboard() {
        await clipboard.copy(token.id); // Assuming token.apiKey holds the API key to copy
        if (clipboard.copied) {
            toast.success('Copy to clipboard succeeded');
        } else {
            toast.error('Copy to clipboard failed');
        }
    }

    const updateToken = patchToken({
        route: {
            get id() {
                return token.id;
            }
        }
    });

    async function updateDisabled(is_disabled: boolean) {
        await updateToken.mutateAsync({ is_disabled, notes: token.notes });
        toast.success(`Successfully ${is_disabled ? 'disabled' : 'enabled'} token`);
    }

    function onEnableDisableClick() {
        if (token.is_disabled) {
            showEnableTokenDialog = true;
        } else {
            showDisableTokenDialog = true;
        }
    }

    async function updateNotes(notes?: string) {
        await updateToken.mutateAsync({ is_disabled: token.is_disabled, notes });
        toast.success('Successfully updated notes');
    }

    const removeToken = deleteToken({
        route: {
            get ids() {
                return [token.id!];
            }
        }
    });

    async function remove() {
        await removeToken.mutateAsync();
        toast.success('Successfully deleted token');
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        <Button class="h-8 w-8 p-0" variant="ghost">
            <ChevronDown class="size-4" />
        </Button>
    </DropdownMenu.Trigger>
    <DropdownMenu.Content align="end">
        <DropdownMenu.Item onclick={copyToClipboard}>
            <Copy class="size-4" />
            Copy Api Key
        </DropdownMenu.Item>
        <DropdownMenu.Item onclick={() => (showUpdateNotesDialog = true)}>
            <Edit />
            Edit Notes
        </DropdownMenu.Item>
        <DropdownMenu.Item onclick={onEnableDisableClick} disabled={updateToken.isPending}>
            {#if token.is_disabled}
                <Enable class="size-4" />
                Enable API Key
            {:else}
                <Disable class="size-4" />
                Disable API Key
            {/if}
        </DropdownMenu.Item>
        <DropdownMenu.Item onclick={() => (showRemoveTokenDialog = true)} disabled={removeToken.isPending}>
            <X class="size-4" />
            Delete
        </DropdownMenu.Item>
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if showEnableTokenDialog}
    <EnableTokenDialog bind:open={showEnableTokenDialog} id={token.id} notes={token.notes} enable={() => updateDisabled(false)} />
{/if}
{#if showDisableTokenDialog}
    <DisableTokenDialog bind:open={showDisableTokenDialog} id={token.id} notes={token.notes} disable={() => updateDisabled(true)} />
{/if}
{#if showUpdateNotesDialog}
    <UpdateTokenNotesDialog bind:open={showUpdateNotesDialog} save={updateNotes} />
{/if}
{#if showRemoveTokenDialog}
    <RemoveTokenDialog bind:open={showRemoveTokenDialog} id={token.id} notes={token.notes} {remove} />
{/if}
