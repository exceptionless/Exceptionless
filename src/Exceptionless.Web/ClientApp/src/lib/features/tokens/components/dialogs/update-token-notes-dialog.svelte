<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import * as Field from '$comp/ui/field';
    import { Textarea } from '$comp/ui/textarea';
    import { type UpdateTokenFormData, UpdateTokenSchema } from '$features/tokens/schemas';
    import { getFormErrorMessages, problemDetailsToFormErrors } from '$shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { createForm } from '@tanstack/svelte-form';

    interface Props {
        notes?: string;
        open: boolean;
        save: (notes?: string) => Promise<void>;
    }

    let { notes, open = $bindable(), save }: Props = $props();

    const form = createForm(() => ({
        defaultValues: {
            notes
        } as UpdateTokenFormData,
        validators: {
            onSubmit: UpdateTokenSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    await save(value.notes?.trim());
                    open = false;
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }
                    return { form: 'An unexpected error occurred' };
                }
            }
        }
    }));

    $effect(() => {
        if (open) {
            form.reset();
            form.setFieldValue('notes', notes);
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
                <AlertDialog.Title>API Key Notes</AlertDialog.Title>
            </AlertDialog.Header>

            <form.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}
                    <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                {/snippet}
            </form.Subscribe>

            <P class="pb-4">
                <form.Field name="notes">
                    {#snippet children(field)}
                        <Field.Field>
                            <Field.Label for={field.name}>Notes</Field.Label>
                            <Textarea
                                id={field.name}
                                name={field.name}
                                placeholder="Please enter notes"
                                autocomplete="off"
                                value={field.state.value}
                                onblur={field.handleBlur}
                                oninput={(e) => field.handleChange(e.currentTarget.value)}
                            />
                        </Field.Field>
                    {/snippet}
                </form.Field>
            </P>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <form.Subscribe selector={(state) => state.isSubmitting}>
                    {#snippet children(isSubmitting)}
                        <AlertDialog.Action type="submit" disabled={isSubmitting}>Save Notes</AlertDialog.Action>
                    {/snippet}
                </form.Subscribe>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
