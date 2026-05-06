<script lang="ts">
    import * as AlertDialog from '$comp/ui/alert-dialog';

    import type { SavedView } from '../models';

    interface Props {
        onDelete: () => Promise<void>;
        open: boolean;
        viewToDelete: null | SavedView;
    }

    let { onDelete, open = $bindable(), viewToDelete }: Props = $props();
</script>

{#if viewToDelete}
    <AlertDialog.Root bind:open>
        <AlertDialog.Content>
            <AlertDialog.Header>
                <AlertDialog.Title>Delete Saved View</AlertDialog.Title>
                <AlertDialog.Description class="break-all">
                    Are you sure you want to delete "{viewToDelete.name}"? This action cannot be undone.
                </AlertDialog.Description>
            </AlertDialog.Header>
            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action variant="destructive" onclick={onDelete}>Delete</AlertDialog.Action>
            </AlertDialog.Footer>
        </AlertDialog.Content>
    </AlertDialog.Root>
{/if}
