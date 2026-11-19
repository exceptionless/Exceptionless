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

    async function onSubmit() {
        await remove();
        open = false;
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>
                Delete
                {#if count === 1}
                    Event
                {:else}
                    <Number value={count} /> Events
                {/if}
            </AlertDialog.Title>
            <AlertDialog.Description>
                {#if matchingFilter}
                    Delete <Number value={count} />
                    {count === 1 ? 'event' : 'events'} matching this filter? This can't be undone.
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
                onclick={onSubmit}
            >
                Delete
                {#if count === 1}
                    Event
                {:else}
                    <Number value={count} /> Events
                {/if}
            </AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
