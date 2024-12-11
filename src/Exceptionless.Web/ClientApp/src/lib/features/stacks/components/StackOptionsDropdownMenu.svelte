<script lang="ts">
    import { goto } from '$app/navigation';
    import Button from '$comp/ui/button/button.svelte';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { toast } from 'svelte-sonner';
    import IconSettings from '~icons/mdi/gear';
    import IconReference from '~icons/mdi/link';
    import IconDelete from '~icons/mdi/trash-can';
    import IconPromoteToExternal from '~icons/mdi/trending-up';

    import { mutateAddStackReference, mutateMarkStackAsCritical, mutateMarkStackAsNotCritical, promoteStackToExternal, removeStack } from '../api.svelte';
    import { Stack } from '../models';
    import AddStackReferenceDialog from './dialogs/AddStackReferenceDialog.svelte';
    import RemoveStackDialog from './dialogs/RemoveStackDialog.svelte';

    interface Props {
        stack: Stack;
    }

    let { stack }: Props = $props();
    let openAddStackReferenceDialog = $state<boolean>(false);
    let openRemoveStackDialog = $state<boolean>(false);

    const addStackReference = mutateAddStackReference({
        get id() {
            return stack?.id;
        }
    });

    const deleteStack = removeStack({
        get id() {
            return stack?.id;
        }
    });

    const markStackAsCritical = mutateMarkStackAsCritical({
        get id() {
            return stack?.id;
        }
    });

    const markStackAsNotCritical = mutateMarkStackAsNotCritical({
        get id() {
            return stack?.id;
        }
    });

    const promoteStack = promoteStackToExternal({
        get id() {
            return stack?.id;
        }
    });

    async function promoteToExternal() {
        const response = await promoteStack.mutateAsync();
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
            toast.error('No promoted web hooks are configured for this project. Please add a promoted web hook to use this feature.');
            // confirm dialog  .confirm(response.data.message, translateService.T("Manage Integrations"))
            await goto(`/next/account/manage/notifications?project=${stack.project_id}`);
            return;
        }
    }

    async function updateCritical() {
        if (stack.occurrences_are_critical) {
            await markStackAsNotCritical.mutateAsync();
        } else {
            await markStackAsCritical.mutateAsync();
        }
    }

    async function addReference(url: string) {
        if (!stack.references?.includes(url)) {
            await addStackReference.mutateAsync(url);
        }
    }

    async function remove() {
        await deleteStack.mutateAsync();
        toast.success('Successfully queued the stack for deletion.');
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        <Button variant="ghost" size="icon">
            <IconSettings class="size-4" />
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
                <IconPromoteToExternal class="mr-2 size-4" />
                Promote To External
            </DropdownMenu.Item>
            <DropdownMenu.Item onclick={() => (openAddStackReferenceDialog = true)} title="Add a reference link to an external resource">
                <IconReference class="mr-2 size-4" />
                Add Reference Link
            </DropdownMenu.Item>
            <DropdownMenu.Separator />
            <DropdownMenu.Item onclick={() => (openRemoveStackDialog = true)} class="text-destructive" title="Delete this stack">
                <IconDelete class="mr-2 size-4" />
                Delete
            </DropdownMenu.Item>
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>

<AddStackReferenceDialog bind:open={openAddStackReferenceDialog} save={addReference} />
<RemoveStackDialog bind:open={openRemoveStackDialog} {remove} />
