<script lang="ts">
    import { Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { Input } from '$comp/ui/input';
    import { Label } from '$comp/ui/label';
    import { Switch } from '$comp/ui/switch';

    import type { SavedView } from '../models';

    interface Props {
        duplicateView?: SavedView;
        onClose: () => void;
        onLoadView: (id: string) => void;
        onSave: (name: string, isPrivate: boolean, isDefault: boolean) => Promise<void>;
        open: boolean;
        saving: boolean;
    }

    let { duplicateView, onClose, onLoadView, onSave, open = $bindable(), saving }: Props = $props();

    let saveName = $state('');
    let isPrivate = $state(false);
    let isDefault = $state(false);

    $effect(() => {
        if (open) {
            saveName = '';
            isPrivate = false;
            isDefault = false;
        }
    });

    async function handleSave() {
        if (!saveName.trim()) {
            return;
        }

        await onSave(saveName.trim(), isPrivate, isDefault);
    }
</script>

<Dialog.Root bind:open>
    <Dialog.Content class="sm:max-w-100">
        <Dialog.Header>
            <Dialog.Title>Save View</Dialog.Title>
            <Dialog.Description>Save the current view configuration for quick access.</Dialog.Description>
        </Dialog.Header>
        {#if duplicateView}
            <div class="bg-muted rounded-md p-3">
                <Muted>
                    Current filters match <strong>"{duplicateView.name}"</strong>. You can
                    <Button
                        variant="link"
                        class="h-auto p-0 text-sm"
                        onclick={() => {
                            open = false;
                            onLoadView(duplicateView.id);
                        }}>load it</Button
                    > instead, or save with a different name.
                </Muted>
            </div>
        {/if}
        <form
            class="flex flex-col gap-4"
            onsubmit={(e) => {
                e.preventDefault();
                handleSave();
            }}
        >
            <div class="flex flex-col gap-2">
                <Label for="view-name">Name</Label>
                <Input id="view-name" bind:value={saveName} placeholder="e.g., Production Errors" required autofocus />
            </div>
            <div class="flex items-center justify-between">
                <div>
                    <Label for="view-private" class="text-sm">Private</Label>
                    <Muted>Only visible to you</Muted>
                </div>
                <Switch
                    id="view-private"
                    bind:checked={isPrivate}
                    onCheckedChange={(checked) => {
                        if (checked) {
                            isDefault = false;
                        }
                    }}
                />
            </div>
            {#if !isPrivate}
                <div class="flex items-center justify-between">
                    <div>
                        <Label for="view-default" class="text-sm">Set as default</Label>
                        <Muted>Auto-loads for everyone on page visit</Muted>
                    </div>
                    <Switch id="view-default" bind:checked={isDefault} />
                </div>
            {/if}
            <Dialog.Footer>
                <Button variant="outline" onclick={onClose}>Cancel</Button>
                <Button type="submit" disabled={!saveName.trim() || saving}>
                    {saving ? 'Saving...' : 'Save'}
                </Button>
            </Dialog.Footer>
        </form>
    </Dialog.Content>
</Dialog.Root>
