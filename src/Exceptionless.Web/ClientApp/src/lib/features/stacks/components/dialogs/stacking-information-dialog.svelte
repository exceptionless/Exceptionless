<script lang="ts">
    import * as Dialog from '$comp/ui/dialog';
    import * as Table from '$comp/ui/table';

    import type { Stack } from '../../models';

    interface Props {
        open: boolean;
        stack: Stack;
    }

    let { open = $bindable(), stack }: Props = $props();

    const signatureEntries = $derived(Object.entries(stack.signature_info ?? {}).sort(([keyA], [keyB]) => keyA.localeCompare(keyB)));
</script>

<Dialog.Root bind:open>
    <Dialog.Content class="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-2xl">
        <Dialog.Header>
            <Dialog.Title>Stacking Information</Dialog.Title>
            <Dialog.Description>Values used to group events into this stack.</Dialog.Description>
        </Dialog.Header>

        {#if signatureEntries.length > 0}
            <Table.Root>
                <Table.Body>
                    {#each signatureEntries as [key, value] (key)}
                        <Table.Row>
                            <Table.Head class="w-48 font-semibold whitespace-nowrap">{key}</Table.Head>
                            <Table.Cell class="break-all">{value}</Table.Cell>
                        </Table.Row>
                    {/each}
                </Table.Body>
            </Table.Root>
        {:else}
            <p class="text-muted-foreground text-sm">No stacking information is available for this stack.</p>
        {/if}
    </Dialog.Content>
</Dialog.Root>
