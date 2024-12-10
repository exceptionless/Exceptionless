<script lang="ts">
    import * as Dialog from '$comp/ui/dialog';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';

    import { ReferenceLinkForm } from '../models';

    interface Props {
        open: boolean;
        save: (url: string) => Promise<void>;
    }

    let { open = $bindable(), save }: Props = $props();

    const form = superForm(defaults(new ReferenceLinkForm(), classvalidatorClient(ReferenceLinkForm)), {
        dataType: 'json',
        async onUpdate({ form }) {
            if (!form.valid) {
                return;
            }

            await save(form.data.url);
            open = false;
        },
        SPA: true,
        validators: classvalidatorClient(ReferenceLinkForm)
    });

    const { enhance, form: formData } = form;
</script>

<Dialog.Root bind:open onOpenChange={() => form.reset()}>
    <Dialog.Content class="sm:max-w-[425px]">
        <form method="POST" use:enhance>
            <Dialog.Header>
                <Dialog.Title>Add Reference Link</Dialog.Title>
                <Dialog.Description>Add a reference link to an external resource.</Dialog.Description>
            </Dialog.Header>

            <div class="py-4">
                <Form.Field {form} name="url">
                    <Form.Control>
                        {#snippet children({ props })}
                            <Form.Label>Reference Link</Form.Label>
                            <Input {...props} bind:value={$formData.url} type="url" placeholder="Please enter a valid URL" autocomplete="url" required />
                        {/snippet}
                    </Form.Control>
                    <Form.Description />
                    <Form.FieldErrors />
                </Form.Field>
            </div>

            <Dialog.Footer>
                <Form.Button>Save Reference Link</Form.Button>
            </Dialog.Footer>
        </form>
    </Dialog.Content>
</Dialog.Root>
