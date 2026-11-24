<script lang="ts">
    import Number from '#comp/formatters/number.svelte';
    import * as AlertDialog from '#comp/ui/alert-dialog/index.ts';
    import { buttonVariants } from '#comp/ui/button/index.ts';

    interface Props {
        count?: number;
        /** The delete runs against everything matching the current filter; the count is an estimate. */
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
                {#if matchingFilter}
                    Delete Matching Stacks
                {:else}
                    Delete
                    {#if count === 1}
                        Stack
                    {:else}
                        <Number value={count} /> Stacks
                    {/if}
                {/if}
            </AlertDialog.Title>
            <AlertDialog.Description>
                {#if matchingFilter}
                    About <Number value={count} />
                    {count === 1 ? 'stack matches' : 'stacks match'} this filter. The final count may differ. This will delete them and all related events and can't
                    be undone.
                {:else}
                    Are you sure you want to delete
                    {#if count === 1}
                        stack
                    {:else}
                        <Number value={count} /> stacks
                    {/if}
                    (including all related events)?
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
                {#if matchingFilter}
                    Delete Matching Stacks
                {:else}
                    Delete
                    {#if count === 1}
                        Stack
                    {:else}
                        <Number value={count} /> Stacks
                    {/if}
                {/if}
            </AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
