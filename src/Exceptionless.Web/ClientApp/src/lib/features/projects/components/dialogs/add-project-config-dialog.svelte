<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { A, P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { createForm } from '@tanstack/svelte-form';

    import type { ClientConfigurationSetting } from '../../models';

    import { type ClientConfigurationSettingFormData, ClientConfigurationSettingSchema } from '../../schemas';

    interface Props {
        open: boolean;
        save: (setting: ClientConfigurationSetting) => Promise<void>;
    }
    let { open = $bindable(), save }: Props = $props();

    const form = createForm(() => ({
        defaultValues: {
            key: '',
            value: ''
        } as ClientConfigurationSettingFormData,
        validators: {
            onSubmit: ClientConfigurationSettingSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    await save({
                        key: value.key.trim(),
                        value: value.value.trim()
                    });
                    open = false;
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return { form: 'An unexpected error occurred.' };
                }
            }
        }
    }));

    $effect(() => {
        if (open) {
            form.reset();
        }
    });
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content class="sm:max-w-[425px]">
        <form
            onsubmit={(e) => {
                e.preventDefault();
                e.stopPropagation();
                form.handleSubmit();
            }}
        >
            <AlertDialog.Header>
                <AlertDialog.Title>Add New Configuration Value</AlertDialog.Title>
                <AlertDialog.Description
                    >The <A href="https://exceptionless.com/docs/project-settings/#client-configuration" target="_blank">configuration value</A> will be sent to the
                    Exceptionless clients in real time.</AlertDialog.Description
                >
            </AlertDialog.Header>

            <form.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}
                    <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                {/snippet}
            </form.Subscribe>

            <P class="pb-4">
                <form.Field name="key">
                    {#snippet children(field)}
                        <Field.Field data-invalid={ariaInvalid(field)}>
                            <Field.Label for={field.name}>Key</Field.Label>
                            <Input
                                id={field.name}
                                name={field.name}
                                type="text"
                                placeholder="Please enter a valid key"
                                required
                                autocomplete="off"
                                value={field.state.value}
                                onblur={field.handleBlur}
                                oninput={(e) => field.handleChange(e.currentTarget.value.trim())}
                                aria-invalid={ariaInvalid(field)}
                            />
                            <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                        </Field.Field>
                    {/snippet}
                </form.Field>
                <form.Field name="value">
                    {#snippet children(field)}
                        <Field.Field data-invalid={ariaInvalid(field)}>
                            <Field.Label for={field.name}>Value</Field.Label>
                            <Input
                                id={field.name}
                                name={field.name}
                                type="text"
                                placeholder="Please enter a valid value"
                                required
                                autocomplete="off"
                                value={field.state.value}
                                onblur={field.handleBlur}
                                oninput={(e) => field.handleChange(e.currentTarget.value.trim())}
                                aria-invalid={ariaInvalid(field)}
                            />
                            <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                        </Field.Field>
                    {/snippet}
                </form.Field>
            </P>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action type="submit">Add Configuration Value</AlertDialog.Action>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
