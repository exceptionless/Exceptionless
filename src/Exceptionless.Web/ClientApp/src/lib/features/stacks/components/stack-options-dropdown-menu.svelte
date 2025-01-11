<script lang="ts">
    import { goto } from '$app/navigation';
    import Button from '$comp/ui/button/button.svelte';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import Reference from 'lucide-svelte/icons/link-2';
    import Settings from 'lucide-svelte/icons/settings';
    import Delete from 'lucide-svelte/icons/trash';
    import PromoteToExternal from 'lucide-svelte/icons/trending-up';
    import { toast } from 'svelte-sonner';

    import { deleteMarkCritical, deleteStack, postAddLink, postMarkCritical, postPromote } from '../api.svelte';
    import { Stack } from '../models';
    import AddStackReferenceDialog from './dialogs/add-stack-reference-dialog.svelte';
    import RemoveStackDialog from './dialogs/remove-stack-dialog.svelte';
    import RequiresPromotedWebHookDialog from './dialogs/requires-promoted-web-hook-dialog.svelte';

    interface Props {
        stack: Stack;
    }

    let { stack }: Props = $props();
    let openAddStackReferenceDialog = $state<boolean>(false);
    let openRemoveStackDialog = $state<boolean>(false);
    let openRequiresPromotedWebHookDialog = $state<boolean>(false);

    const addLink = postAddLink({
        route: {
            get id() {
                return stack?.id;
            }
        }
    });

    const removeStacks = deleteStack({
        route: {
            get ids() {
                return [stack?.id].filter(Boolean);
            }
        }
    });

    const markCritical = postMarkCritical({
        route: {
            get ids() {
                return [stack?.id].filter(Boolean);
            }
        }
    });

    const markNotCritical = deleteMarkCritical({
        route: {
            get ids() {
                return [stack?.id].filter(Boolean);
            }
        }
    });

    const promote = postPromote({
        route: {
            get ids() {
                return [stack?.id].filter(Boolean);
            }
        }
    });

    async function promoteToExternal() {
        const response = await promote.mutateAsync();
        if (response.status === 200) {
            toast.success('Successfully promoted stack!');
            return;
        }

        if (response.status === 426) {
            toast.error(
                'Promote to External is a premium feature used to promote an error stack to an external system. Please upgrade your plan to enable this feature.'
            );
            //await confirmUpgradePlan(message, tack.organization_id);
            //await promoteToExternal();
            return;
        }

        if (response.status === 501) {
            openRequiresPromotedWebHookDialog = true;
            return;
        }
    }

    async function navigateToProjectIntegrations() {
        await goto(`/project/${stack.project_id}/manage`);
    }

    async function updateCritical() {
        if (stack.occurrences_are_critical) {
            await markNotCritical.mutateAsync();
        } else {
            await markCritical.mutateAsync();
        }
    }

    async function addReference(url: string) {
        if (!stack.references?.includes(url)) {
            await addLink.mutateAsync(url);
        }
    }

    async function remove() {
        await removeStacks.mutateAsync();
        toast.success('Successfully queued the stack for deletion.');
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        <Button variant="ghost" size="icon">
            <Settings class="size-4" />
        </Button>
    </DropdownMenu.Trigger>
    <DropdownMenu.Content align="end">
        <DropdownMenu.Group>
            <DropdownMenu.GroupHeading>Stack Options</DropdownMenu.GroupHeading>
            <DropdownMenu.Separator />
            <DropdownMenu.CheckboxItem
                checked={stack.occurrences_are_critical}
                onclick={() => updateCritical()}
                title="All future occurrences will be marked as critical"
            >
                Future Occurrences Are Critical
            </DropdownMenu.CheckboxItem>
            <DropdownMenu.Separator />
            <DropdownMenu.Item onclick={() => promoteToExternal()} title="Used to promote stacks to external systems">
                <PromoteToExternal class="mr-2 size-4" />
                Promote To External
            </DropdownMenu.Item>
            <DropdownMenu.Item onclick={() => (openAddStackReferenceDialog = true)} title="Add a reference link to an external resource">
                <Reference class="mr-2 size-4" />
                Add Reference Link
            </DropdownMenu.Item>
            <DropdownMenu.Separator />
            <DropdownMenu.Item onclick={() => (openRemoveStackDialog = true)} class="text-destructive" title="Delete this stack">
                <Delete class="mr-2 size-4" />
                Delete
            </DropdownMenu.Item>
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if openAddStackReferenceDialog}
    <AddStackReferenceDialog bind:open={openAddStackReferenceDialog} save={addReference} />
{/if}
{#if openRemoveStackDialog}
    <RemoveStackDialog bind:open={openRemoveStackDialog} {remove} />
{/if}
{#if openRequiresPromotedWebHookDialog}
    <RequiresPromotedWebHookDialog bind:open={openRequiresPromotedWebHookDialog} navigate={navigateToProjectIntegrations} />
{/if}
