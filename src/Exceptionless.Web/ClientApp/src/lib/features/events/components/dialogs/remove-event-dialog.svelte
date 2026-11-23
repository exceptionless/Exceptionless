<script lang="ts">
    import Number from '#comp/formatters/number.svelte';
    import * as AlertDialog from '#comp/ui/alert-dialog/index.ts';
    import { buttonVariants } from '#comp/ui/button/index.ts';

    interface Props {
        count?: number;
        /** The delete runs against everything matching the current filter instead of the selected rows. */
        matchingFilter?: boolean;
        open: boolean;
        remove: () => Promise<void>;
    }

    let { count = 1, matchingFilter = false, open = $bindable(), remove }: Props = $props();

    let isSubmitting = $state(false);

    async function onSubmit() {
        if (isSubmitting) {
            return;
        }

        isSubmitting = true;
        try {
            await remove();
            open = false;
        } finally {
            isSubmitting = false;
        }
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>
                {#if matchingFilter}
                    Delete Matching Events
                {:else}
                    Delete
                    {#if count === 1}
                        Event
                    {:else}
                        <Number value={count} /> Events
                    {/if}
                {/if}
            </AlertDialog.Title>
            <AlertDialog.Description>
                {#if matchingFilter}
                    About <Number value={count} />
                    {count === 1 ? 'event matches' : 'events match'} this filter. The final count may differ. This can't be undone.
                {:else}
                    Are you sure you want to delete
                    {#if count === 1}
                        this event
                    {:else}
                        <Number value={count} /> events
                    {/if}?
                {/if}
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action
                class={buttonVariants({
                    variant: 'destructive'
                })}
                disabled={isSubmitting}
                onclick={onSubmit}
            >
                {#if matchingFilter}
                    Delete Matching Events
                {:else}
                    Delete
                    {#if count === 1}
                        Event
                    {:else}
                        <Number value={count} /> Events
                    {/if}
                {/if}
            </AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
