<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { Input } from '$comp/ui/input';
    import { Label } from '$comp/ui/label';

    interface Props {
        open: boolean;
        save: (url: string) => Promise<void>;
    }

    // TODO: Use form validation
    let { open = $bindable(), save }: Props = $props();
    let value = $state('');

    async function onSubmit() {
        await save(value);
        open = false;
    }
</script>

<Dialog.Root bind:open>
    <Dialog.Content class="sm:max-w-[425px]">
        <Dialog.Header>
            <Dialog.Title>Add Reference Link</Dialog.Title>
            <Dialog.Description>Add a reference link to an external resource.</Dialog.Description>
        </Dialog.Header>
        <div class="grid gap-4 py-4">
            <div class="grid grid-cols-5 items-center gap-6">
                <Label for="url" class="text-right">Url</Label>
                <Input id="url" name="url" type="url" bind:value placeholder="Please enter a valid URL" class="col-span-4" required />
            </div>
        </div>
        <Dialog.Footer>
            <Button type="submit" onclick={onSubmit}>Save Reference Link</Button>
        </Dialog.Footer>
    </Dialog.Content>
</Dialog.Root>
