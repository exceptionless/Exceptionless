<script lang="ts">
    import type { NewProject } from '$features/projects/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import ErrorMessage from '$comp/error-message.svelte';
    import Logo from '$comp/logo.svelte';
    import { Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing';
    import { postOrganization } from '$features/organizations/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { useHideOrganizationNotifications } from '$features/organizations/hooks/use-hide-organization-notifications.svelte';
    import { postProject } from '$features/projects/api.svelte';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';
    import { type infer as Infer, object, string } from 'zod';

    const SetupSchema = object({
        organization_name: string().min(1, 'Organization name is required'),
        project_name: string().min(1, 'Project name is required')
    });

    type SetupFormData = Infer<typeof SetupSchema>;

    let toastId = $state<number | string>();
    const createOrganization = postOrganization();
    const createProject = postProject();
    const CREATE_ERROR_MESSAGE = 'Error creating setup. Please try again.';

    useHideOrganizationNotifications();

    const form = createForm(() => ({
        defaultValues: {
            organization_name: '',
            project_name: ''
        } as SetupFormData,
        validators: {
            onSubmit: SetupSchema,
            onSubmitAsync: async ({ value }) => {
                toast.dismiss(toastId);
                try {
                    const createdOrganization = await createOrganization.mutateAsync({ name: value.organization_name });
                    organization.current = createdOrganization.id;

                    const createdProject = await createProject.mutateAsync({
                        delete_bot_data_enabled: true,
                        name: value.project_name,
                        organization_id: createdOrganization.id
                    } as NewProject);

                    toastId = toast.success('Project added successfully');
                    await goto(resolve('/(app)/project/[projectId]/configure', { projectId: createdProject.id }) + '?redirect=true');
                    return null;
                } catch (error: unknown) {
                    if (showBillingDialogOnUpgradeProblem(error, organization.current, () => form.handleSubmit())) {
                        return null;
                    }

                    if (error instanceof ProblemDetails) {
                        toastId = toast.error(error.title || CREATE_ERROR_MESSAGE);
                        return problemDetailsToFormErrors(error);
                    }

                    toastId = toast.error(CREATE_ERROR_MESSAGE);
                    return { form: 'An unexpected error occurred, please try again.' };
                }
            }
        }
    }));
</script>

<Card.Root class="mx-auto w-sm">
    <Card.Header>
        <Logo />
        <Card.Title class="text-center text-2xl">Set Up Exceptionless</Card.Title>
        <Muted class="text-center">Create an organization and project. Next, we'll connect your app.</Muted>
    </Card.Header>
    <Card.Content>
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
            <form.Field name="organization_name">
                {#snippet children(field)}
                    <Field.Field data-invalid={ariaInvalid(field)}>
                        <Field.Label for={field.name}>Organization Name</Field.Label>
                        <Input
                            id={field.name}
                            name={field.name}
                            placeholder="Enter organization name"
                            value={field.state.value}
                            onblur={field.handleBlur}
                            oninput={(e) => field.handleChange(e.currentTarget.value)}
                            aria-invalid={ariaInvalid(field)}
                        />
                        <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                    </Field.Field>
                {/snippet}
            </form.Field>
            <form.Field name="project_name">
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
                    <Button type="submit" class="mt-4 w-full" disabled={isSubmitting}>
                        {#if isSubmitting}
                            <Spinner /> Creating Setup...
                        {:else}
                            Continue
                        {/if}
                    </Button>
                {/snippet}
            </form.Subscribe>
        </form>
    </Card.Content>
</Card.Root>
