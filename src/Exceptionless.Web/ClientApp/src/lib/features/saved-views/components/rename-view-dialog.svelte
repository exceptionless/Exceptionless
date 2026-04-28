<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { Input } from '$comp/ui/input';
    import { Label } from '$comp/ui/label';

    interface Props {
        name: string;
        onClose: () => void;
        onRename: (newName: string) => Promise<void>;
        open: boolean;
        saving: boolean;
    }

    let { name, onClose, onRename, open = $bindable(), saving }: Props = $props();

    let renameName = $state('');

    $effect(() => {
        if (open) {
            renameName = name;
        }
    });

    async function handleRename() {
        if (!renameName.trim()) {
            return;
        }

        await onRename(renameName.trim());
    }
</script>

<Dialog.Root bind:open>
    <Dialog.Content class="sm:max-w-100">
        <Dialog.Header>
            <Dialog.Title>Rename View</Dialog.Title>
            <Dialog.Description>Change the display name for this saved view.</Dialog.Description>
        </Dialog.Header>
        <form
            class="flex flex-col gap-4"
            onsubmit={(e) => {
                e.preventDefault();
                handleRename();
            }}
        >
            <div class="flex flex-col gap-2">
                <Label for="rename-view">Name</Label>
                <Input id="rename-view" bind:value={renameName} placeholder="View name" required autofocus />
            </div>
            <Dialog.Footer>
                <Button variant="outline" onclick={onClose}>Cancel</Button>
                <Button type="submit" disabled={!renameName.trim() || saving}>
                    {saving ? 'Saving...' : 'Rename'}
                </Button>
            </Dialog.Footer>
        </form>
    </Dialog.Content>
</Dialog.Root>
