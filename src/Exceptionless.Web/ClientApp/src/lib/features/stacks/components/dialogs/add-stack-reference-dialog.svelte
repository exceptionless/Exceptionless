<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { ReferenceLinkSchema } from '$features/stacks/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { createForm } from '@tanstack/svelte-form';

    interface Props {
        open: boolean;
        save: (url: string) => Promise<void>;
    }

    let { open = $bindable(), save }: Props = $props();

    const form = createForm(() => ({
        defaultValues: {
            url: ''
        },
        validators: {
            onSubmit: ReferenceLinkSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    await save(value.url);
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
                <AlertDialog.Title>Add Reference Link</AlertDialog.Title>
                <AlertDialog.Description>Add a reference link to an external resource.</AlertDialog.Description>
            </AlertDialog.Header>

            <form.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}
                    <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                {/snippet}
            </form.Subscribe>

            <P class="pb-4">
                <form.Field name="url">
                    {#snippet children(field)}
                        <Field.Field data-invalid={ariaInvalid(field)}>
                            <Field.Label for={field.name}>Reference Link</Field.Label>
                            <Input
                                id={field.name}
                                name={field.name}
                                type="url"
                                placeholder="Please enter a valid URL"
                                autocomplete="url"
                                required
                                value={field.state.value}
                                onblur={field.handleBlur}
                                oninput={(e) => field.handleChange(e.currentTarget.value)}
                                aria-invalid={ariaInvalid(field)}
                            />
                            <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                        </Field.Field>
                    {/snippet}
                </form.Field>
            </P>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <form.Subscribe selector={(state) => state.isSubmitting}>
                    {#snippet children(isSubmitting)}
                        <AlertDialog.Action type="submit" disabled={isSubmitting}>Save Reference Link</AlertDialog.Action>
                    {/snippet}
                </form.Subscribe>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
