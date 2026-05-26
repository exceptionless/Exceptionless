<script lang="ts">
    import type { NewProject } from '$features/projects/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import ErrorMessage from '$comp/error-message.svelte';
    import { H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing';
    import { organization } from '$features/organizations/context.svelte';
    import { postProject } from '$features/projects/api.svelte';
    import { type NewProjectFormData, NewProjectSchema } from '$features/projects/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { createForm } from '@tanstack/svelte-form';
    import { untrack } from 'svelte';
    import { toast } from 'svelte-sonner';

    let toastId = $state<number | string>();
    const createProject = postProject();

    const form = createForm(() => ({
        defaultValues: {
            delete_bot_data_enabled: true,
            name: '',
            organization_id: organization.current ?? ''
        } as NewProjectFormData,
        validators: {
            onSubmit: NewProjectSchema,
            onSubmitAsync: async ({ value }) => {
                toast.dismiss(toastId);
                try {
                    const { id } = await createProject.mutateAsync({ ...value, organization_id: organization.current ?? value.organization_id } as NewProject);
                    toastId = toast.success('Project added successfully');
                    await goto(resolve('/(app)/project/[projectId]/configure', { projectId: id }) + '?redirect=true');
                    return null;
                } catch (error: unknown) {
                    if (showBillingDialogOnUpgradeProblem(error, organization.current, () => form.handleSubmit())) {
                        return null;
                    }

                    if (error instanceof ProblemDetails) {
                        toastId = toast.error(error.title || 'Error creating project. Please try again.');
                        return problemDetailsToFormErrors(error);
                    }

                    toastId = toast.error('Error creating project. Please try again.');
                    return { form: 'An unexpected error occurred, please try again.' };
                }
            }
        }
    }));

    $effect(() => {
        const organizationId = organization.current ?? '';
        untrack(() => form.setFieldValue('organization_id', organizationId));
    });
</script>

<div class="flex flex-col gap-4">
    <div class="flex flex-col gap-1">
        <H3>Add Project</H3>
        <Muted>Create a project, then configure a client to send your first event.</Muted>
    </div>
    <form
        onsubmit={(e) => {
            e.preventDefault();
            e.stopPropagation();
            form.handleSubmit();
        }}
    >
        <form.Subscribe selector={(state) => state.errors}>
            {#snippet children(errors)}
                <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
            {/snippet}
        </form.Subscribe>
        <form.Field name="name">
            {#snippet children(field)}
                <Field.Field data-invalid={ariaInvalid(field)}>
                    <Field.Label for={field.name}>Project Name</Field.Label>
                    <Input
                        id={field.name}
                        name={field.name}
                        placeholder="Enter project name"
                        value={field.state.value}
                        onblur={field.handleBlur}
                        oninput={(e) => field.handleChange(e.currentTarget.value)}
                        aria-invalid={ariaInvalid(field)}
                    />
                    <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                </Field.Field>
            {/snippet}
        </form.Field>
        <form.Subscribe selector={(state) => state.isSubmitting}>
            {#snippet children(isSubmitting)}
                <Button type="submit" class="mt-4" disabled={isSubmitting}>
                    {#if isSubmitting}
                        <Spinner /> Adding Project...
                    {:else}
                        Continue to Client Setup
                    {/if}
                </Button>
            {/snippet}
        </form.Subscribe>
    </form>
</div>
