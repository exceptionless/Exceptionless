<script lang="ts">
    import { A, Small } from '$comp/typography';
    import Button from '$comp/ui/button/button.svelte';
    import IconReference from '~icons/mdi/link';
    import IconOpenInNew from '~icons/mdi/open-in-new';
    import IconRemove from '~icons/mdi/remove';

    import { postRemoveLink } from '../api.svelte';
    import { Stack } from '../models';
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
    <ul class="space-y-2">
        {#each stack.references as reference (reference)}
            <li class="flex items-center gap-2">
                <IconReference />
                <A href={reference} target="_blank" rel="noopener noreferrer" class="flex items-center gap-2" variant="secondary">
                    <Small class="truncate">{reference}</Small>
                    <IconOpenInNew />
                </A>
                <Button variant="destructive" size="xs" onclick={() => onOpenRemoveStackReferenceDialog(reference)}>
                    <IconRemove />
                </Button>
            </li>
        {/each}
    </ul>
{/if}

{#if openRemoveStackReferenceDialog}
    <RemoveStackReferenceDialog bind:open={openRemoveStackReferenceDialog} reference={referenceToRemove} remove={removeReference} />
{/if}
