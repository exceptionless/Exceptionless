<script lang="ts">
    import { ProblemDetails } from '@foundatiofx/fetchclient';
    import { createForm } from '@tanstack/svelte-form';
    import { tick } from 'svelte';

    import type { NewProjectEnvironment } from '#features/projects/models.ts';

    import ErrorMessage from '#comp/error-message.svelte';
    import * as AlertDialog from '#comp/ui/alert-dialog/index.ts';
    import * as Field from '#comp/ui/field/index.ts';
    import { Input } from '#comp/ui/input/index.ts';
    import { getEnvironmentKey, MAX_LISTED_ENVIRONMENTS, normalizeEnvironmentName } from '#features/projects/environments.ts';
    import { type NewProjectEnvironmentFormData, NewProjectEnvironmentSchema } from '#features/projects/schemas.ts';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '#features/shared/validation.ts';

    interface Props {
        add: (environment: NewProjectEnvironment) => Promise<unknown>;
        /** The names that are already listed. Names are compared case-insensitively. */
        listedNames: string[];
        /** The starting name, so a name seen in events can be corrected before it is added. */
        name?: string;
        open: boolean;
    }

    let { add, listedNames, name = '', open = $bindable() }: Props = $props();

    let nameInput = $state<HTMLInputElement | null>(null);

    const form = createForm(() => ({
        defaultValues: {
            name,
            notify: true
        } as NewProjectEnvironmentFormData,
        validators: {
            onSubmit: NewProjectEnvironmentSchema,
            onSubmitAsync: async ({ value }) => {
                const normalized = normalizeEnvironmentName(value.name);
                if (!normalized) {
                    return {
                        fields: {
                            name: 'Use 1 to 64 characters, without control characters.'
                        }
                    };
                }

                if (listedNames.some((listed) => getEnvironmentKey(listed) === getEnvironmentKey(normalized))) {
                    return {
                        fields: {
                            name: `${normalized} is already in the list.`
                        }
                    };
                }

                if (listedNames.length >= MAX_LISTED_ENVIRONMENTS) {
                    return {
                        form: `A project can have at most ${MAX_LISTED_ENVIRONMENTS} environments.`
                    };
                }

                try {
                    await add({
                        name: normalized,
                        notify: true
                    });
                    open = false;
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return {
                        form: 'An unexpected error occurred, please try again.'
                    };
                }
            }
        }
    }));

    // The dialog is mounted each time it opens, so the form starts from the given name.
    $effect(() => {
        if (open) {
            tick().then(() => nameInput?.select());
        }
    });
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content class="sm:max-w-[425px]">
        <form
            onsubmit={(event) => {
                event.preventDefault();
                event.stopPropagation();
                form.handleSubmit();
            }}
        >
            <AlertDialog.Header>
                <AlertDialog.Title>Add Environment</AlertDialog.Title>
                <AlertDialog.Description>Shown exactly as you type it. Events match it regardless of case.</AlertDialog.Description>
            </AlertDialog.Header>

            <form.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}
                    <ErrorMessage message={getFormErrorMessages(errors)} />
                {/snippet}
            </form.Subscribe>

            <div class="py-4">
                <form.Field name="name">
                    {#snippet children(field)}
                        <Field.Field data-invalid={ariaInvalid(field)}>
                            <Field.Label for={field.name}>Name</Field.Label>
                            <Input
                                autocomplete="off"
                                bind:ref={nameInput}
                                id={field.name}
                                maxlength={64}
                                name={field.name}
                                placeholder="Production"
                                value={field.state.value}
                                onblur={field.handleBlur}
                                oninput={(event) => field.handleChange(event.currentTarget.value)}
                                aria-invalid={ariaInvalid(field)}
                            />
                            <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                        </Field.Field>
                    {/snippet}
                </form.Field>
            </div>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action type="submit">Add Environment</AlertDialog.Action>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
