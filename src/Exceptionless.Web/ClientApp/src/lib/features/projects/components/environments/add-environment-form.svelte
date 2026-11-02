<script lang="ts">
    import { ProblemDetails } from '@foundatiofx/fetchclient';
    import Plus from '@lucide/svelte/icons/plus';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';

    import type { NewProjectEnvironment } from '#features/projects/models.ts';

    import ErrorMessage from '#comp/error-message.svelte';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Field from '#comp/ui/field/index.ts';
    import { Input } from '#comp/ui/input/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import { Switch } from '#comp/ui/switch/index.ts';
    import { getEnvironmentKey, MAX_LISTED_ENVIRONMENTS } from '#features/projects/environments.ts';
    import { type AddEnvironmentFormData, AddEnvironmentSchema } from '#features/projects/schemas.ts';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '#features/shared/validation.ts';

    interface Props {
        add: (environment: NewProjectEnvironment) => Promise<unknown>;
        /** The names that are already listed. Names are compared case-insensitively. */
        listedNames: string[];
    }

    let { add, listedNames }: Props = $props();

    const form = createForm(() => ({
        defaultValues: {
            name: '',
            notify: true
        } as AddEnvironmentFormData,
        validators: {
            onSubmit: AddEnvironmentSchema,
            onSubmitAsync: async ({ value }) => {
                const name = value.name.trim();
                if (listedNames.some((listed) => getEnvironmentKey(listed) === getEnvironmentKey(name))) {
                    return {
                        fields: {
                            name: `${name} is already listed. Names are not case-sensitive.`
                        }
                    };
                }

                if (listedNames.length >= MAX_LISTED_ENVIRONMENTS) {
                    return {
                        form: `A project can list at most ${MAX_LISTED_ENVIRONMENTS} environments.`
                    };
                }

                try {
                    await add({
                        name,
                        notify: value.notify
                    });
                    toast.success(`Added ${name}.`);
                    form.reset();
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return {
                        form: 'An unexpected error occurred.'
                    };
                }
            }
        }
    }));
</script>

<form
    class="grid gap-4 sm:grid-cols-[minmax(0,1fr)_auto_auto] sm:items-end"
    onsubmit={(event) => {
        event.preventDefault();
        event.stopPropagation();
        form.handleSubmit();
    }}
>
    <form.Subscribe selector={(state) => state.errors}>
        {#snippet children(errors)}
            <div class="sm:col-span-3"><ErrorMessage message={getFormErrorMessages(errors)} /></div>
        {/snippet}
    </form.Subscribe>

    <form.Field name="name">
        {#snippet children(field)}
            <Field.Field data-invalid={ariaInvalid(field)}>
                <Field.Label for={field.name}>Environment name</Field.Label>
                <Input
                    autocomplete="off"
                    id={field.name}
                    maxlength={64}
                    name={field.name}
                    placeholder="Example: Production"
                    value={field.state.value}
                    onblur={field.handleBlur}
                    oninput={(event) => field.handleChange(event.currentTarget.value)}
                    aria-invalid={ariaInvalid(field)}
                />
                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
            </Field.Field>
        {/snippet}
    </form.Field>

    <form.Field name="notify">
        {#snippet children(field)}
            <Field.Field orientation="horizontal" class="sm:pb-2">
                <Switch id={field.name} checked={field.state.value} onCheckedChange={(checked) => field.handleChange(checked)} />
                <Field.Label for={field.name}>Send notifications</Field.Label>
            </Field.Field>
        {/snippet}
    </form.Field>

    <form.Subscribe selector={(state) => state.isSubmitting}>
        {#snippet children(isSubmitting)}
            <Button type="submit" disabled={isSubmitting}>
                {#if isSubmitting}<Spinner />{:else}<Plus class="size-4" />{/if}
                Add environment
            </Button>
        {/snippet}
    </form.Subscribe>
</form>
