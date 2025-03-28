<script lang="ts">
    import { P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';

    import { ReferenceLinkForm } from '../../models';

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

<AlertDialog.Root bind:open>
    <AlertDialog.Content class="sm:max-w-[425px]">
        <form method="POST" use:enhance>
            <AlertDialog.Header>
                <AlertDialog.Title>Add Reference Link</AlertDialog.Title>
                <AlertDialog.Description>Add a reference link to an external resource.</AlertDialog.Description>
            </AlertDialog.Header>

            <P class="pb-4">
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
            </P>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action>Save Reference Link</AlertDialog.Action>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
