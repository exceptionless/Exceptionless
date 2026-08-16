<script lang="ts">
    import { A, Small } from '$comp/typography';
    import Button from '$comp/ui/button/button.svelte';
    import * as Table from '$comp/ui/table';
    import ExternalLink from '@lucide/svelte/icons/external-link';
    import Delete from '@lucide/svelte/icons/trash';

    import type { Stack } from '../models';

    import { postRemoveLink } from '../api.svelte';
    import RemoveStackReferenceDialog from './dialogs/remove-stack-reference-dialog.svelte';

    interface Props {
        stack: Stack;
    }

    let { stack }: Props = $props();
    let openRemoveStackReferenceDialog = $state<boolean>(false);
    let referenceToRemove = $state<string>('');

    const removeLink = postRemoveLink({
        route: {
            get id() {
                return stack?.id;
            }
        }
    });

    function onOpenRemoveStackReferenceDialog(reference: string) {
        referenceToRemove = reference;
        openRemoveStackReferenceDialog = true;
    }

    async function removeReference(reference: string) {
        await removeLink.mutateAsync(reference);
    }
</script>

{#if stack.references?.length > 0}
    <Table.Row>
        <Table.Head class="w-36 align-top font-semibold whitespace-nowrap">Reference</Table.Head>
        <Table.Cell class="w-4 pr-0"></Table.Cell>
        <Table.Cell>
            <ul class="space-y-1.5">
                {#each stack.references as reference (reference)}
                    <li class="flex min-w-0 items-center gap-1">
                        <A
                            href={reference}
                            target="_blank"
                            rel="noopener noreferrer"
                            class="flex min-w-0 items-center gap-1.5"
                            title={reference}
                            variant="secondary"
                        >
                            <Small class="min-w-0 truncate">{reference}</Small>
                            <ExternalLink aria-hidden="true" class="size-3.5 shrink-0" />
                        </A>
                        <Button
                            aria-label="Delete reference link"
                            title="Delete reference link"
                            variant="destructive"
                            size="icon-xs"
                            onclick={() => onOpenRemoveStackReferenceDialog(reference)}
                        >
                            <Delete aria-hidden="true" />
                        </Button>
                    </li>
                {/each}
            </ul>
        </Table.Cell>
    </Table.Row>
{/if}

{#if openRemoveStackReferenceDialog}
    <RemoveStackReferenceDialog bind:open={openRemoveStackReferenceDialog} reference={referenceToRemove} remove={removeReference} />
{/if}
