<script lang="ts">
    import { goto } from '$app/navigation';
    import ErrorMessage from '$comp/error-message.svelte';
    import Loading from '$comp/loading.svelte';
    import { H3, Muted } from '$comp/typography';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
    import { organization } from '$features/organizations/context.svelte';
    import { postProject } from '$features/projects/api.svelte';
    import { applyServerSideErrors } from '$features/shared/validation';
    import { NewProject } from '$generated/api';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { toast } from 'svelte-sonner';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';

    let toastId = $state<number | string>();
    const createProject = postProject();

    const project = new NewProject();
    project.organization_id = organization.current!;
    project.delete_bot_data_enabled = true;

    const form = superForm(defaults(project, classvalidatorClient(NewProject)), {
        dataType: 'json',
        id: 'post-project',
        async onUpdate({ form, result }) {
            if (!form.valid) {
                return;
            }

            toast.dismiss(toastId);
            try {
                const { id } = await createProject.mutateAsync(form.data);
                toastId = toast.success('Project added successfully');
                await goto(`/next/project/${id}/configure?redirect=true`);

                // HACK: This is to prevent sveltekit from stealing focus
                result.type = 'failure';
            } catch (error: unknown) {
                if (error instanceof ProblemDetails) {
                    applyServerSideErrors(form, error);
                    result.status = error.status ?? 500;
                } else {
                    result.status = 500;
                }

                toastId = toast.error(form.message ?? 'Error creating project. Please try again.');
            }
        },
        SPA: true,
        validators: classvalidatorClient(NewProject)
    });

    const { enhance, form: formData, message, submitting } = form;
</script>

<div class="flex flex-col gap-4">
    <div class="flex flex-col gap-1">
        <H3>Add Project</H3>
        <Muted>Add a new project to start tracking errors and events.</Muted>
    </div>
    <form method="POST" use:enhance>
        <ErrorMessage message={$message} />
        <Form.Field {form} name="name">
            <Form.Control>
                {#snippet children({ props })}
                    <Form.Label>Project Name</Form.Label>
                    <Input {...props} bind:value={$formData.name} placeholder="Enter project name" />
                {/snippet}
            </Form.Control>
            <Form.FieldErrors />
        </Form.Field>

        <Form.Button>
            {#if $submitting}
                <Loading class="mr-2" variant="secondary"></Loading> Adding Project...
            {:else}
                Add Project
            {/if}</Form.Button
        >
    </form>
</div>
