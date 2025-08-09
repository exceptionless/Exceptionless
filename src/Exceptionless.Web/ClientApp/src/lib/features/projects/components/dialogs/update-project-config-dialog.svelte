<script lang="ts">
    import { A, P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
    import { applyServerSideErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';

    import { ClientConfigurationSetting } from '../../models';

    interface Props {
        open: boolean;
        save: (value: string) => Promise<void>;
        setting: ClientConfigurationSetting; // Change type from string to ClientConfigurationSetting
    }
    let { open = $bindable(), save, setting }: Props = $props();

    const defaultValue = new ClientConfigurationSetting();
    defaultValue.key = setting.key;
    defaultValue.value = setting.value;

    const form = superForm(defaults(defaultValue, classvalidatorClient(ClientConfigurationSetting)), {
        dataType: 'json',
        id: 'update-project-config',
        async onUpdate({ form, result }) {
            if (!form.valid) {
                return;
            }

            try {
                await save(form.data.value.trim());
                open = false;

                // HACK: This is to prevent sveltekit from stealing focus
                result.type = 'failure';
            } catch (error: unknown) {
                if (error instanceof ProblemDetails) {
                    applyServerSideErrors(form, error);
                    result.status = error.status ?? 500;
                } else {
                    result.status = 500;
                }
            }
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
                <AlertDialog.Title>Update {setting.key} Configuration Value</AlertDialog.Title>
                <AlertDialog.Description
                    >The <A href="https://exceptionless.com/docs/project-settings/#client-configuration" target="_blank">configuration value</A> will be sent to
                    the Exceptionless clients in real time.</AlertDialog.Description
                >
            </AlertDialog.Header>

            <P class="pb-4">
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
                <AlertDialog.Action>Save Configuration Value</AlertDialog.Action>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
