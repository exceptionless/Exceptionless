<script lang="ts">
    import { A, P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';

    import { ClientConfigurationSetting } from '../../models';

    interface Props {
        open: boolean;
        save: (setting: ClientConfigurationSetting) => Promise<void>;
    }
    let { open = $bindable(), save }: Props = $props();

    const form = superForm(defaults(new ClientConfigurationSetting(), classvalidatorClient(ClientConfigurationSetting)), {
        dataType: 'json',
        async onUpdate({ form }) {
            if (!form.valid) {
                return;
            }

            await save(form.data);
            open = false;
        },
        SPA: true,
        validators: classvalidatorClient(ClientConfigurationSetting)
    });

    const { enhance, form: formData } = form;
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content class="sm:max-w-[425px]">
        <form method="POST" use:enhance>
            <AlertDialog.Header>
                <AlertDialog.Title>Add New Configuration Value</AlertDialog.Title>
                <AlertDialog.Description
                    >The <A href="https://exceptionless.com/docs/project-settings/#client-configuration" target="_blank">configuration value</A> will be sent to
                    the Exceptionless clients in real time.</AlertDialog.Description
                >
            </AlertDialog.Header>

            <P class="pb-4">
                <Form.Field {form} name="key">
                    <Form.Control>
                        {#snippet children({ props })}
                            <Form.Label>Key</Form.Label>
                            <Input
                                {...props}
                                bind:value={$formData.key}
                                type="text"
                                placeholder="Please enter a valid key"
                                required
                                autocomplete="off"
                                oninput={() => ($formData.key = $formData.key.trim())}
                            />
                        {/snippet}
                    </Form.Control>
                    <Form.Description />
                    <Form.FieldErrors />
                </Form.Field>
                <Form.Field {form} name="value">
                    <Form.Control>
                        {#snippet children({ props })}
                            <Form.Label>Value</Form.Label>
                            <Input
                                {...props}
                                bind:value={$formData.value}
                                type="text"
                                placeholder="Please enter a valid value"
                                required
                                autocomplete="off"
                                oninput={() => ($formData.value = $formData.value.trim())}
                            />
                        {/snippet}
                    </Form.Control>
                    <Form.Description />
                    <Form.FieldErrors />
                </Form.Field>
            </P>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action>Add Configuration Value</AlertDialog.Action>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
