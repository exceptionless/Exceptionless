<script lang="ts">
    import { toast } from 'svelte-sonner';

    import Number from '#comp/formatters/number.svelte';
    import * as AlertDialog from '#comp/ui/alert-dialog/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import { getProblemMessage } from '#shared/validation.ts';

    interface Props {
        count?: number;
        open: boolean;
        remove: () => Promise<void>;
    }

    let { count = 1, open = $bindable(), remove }: Props = $props();
    let submitting = $state(false);
    let submittedCount = $state(0);
    const displayedCount = $derived(submitting ? submittedCount : count);

    async function onSubmit() {
        if (submitting || count === 0) {
            return;
        }
        submittedCount = count;
        submitting = true;
        try {
            await remove();
            open = false;
        } catch (error) {
            toast.error(getProblemMessage(error, 'Unable to delete the selected events.'));
        } finally {
            submitting = false;
        }
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content escapeKeydownBehavior={submitting ? 'ignore' : 'close'}>
        <AlertDialog.Header>
            <AlertDialog.Title>
                Delete
                {#if displayedCount === 1}
                    Event
                {:else}
                    <Number value={displayedCount} /> Events
                {/if}
            </AlertDialog.Title>
            <AlertDialog.Description>
                Are you sure you want to delete
                {#if displayedCount === 1}
                    this event
                {:else}
                    <Number value={displayedCount} /> events
                {/if}?
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel disabled={submitting}>Cancel</AlertDialog.Cancel>
            <Button variant="destructive" disabled={submitting || count === 0} onclick={onSubmit}>
                Delete
                {#if displayedCount === 1}
                    Event
                {:else}
                    <Number value={displayedCount} /> Events
                {/if}
            </Button>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
