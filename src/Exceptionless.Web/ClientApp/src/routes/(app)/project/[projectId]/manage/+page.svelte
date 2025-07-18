<script lang="ts">
    import { goto } from '$app/navigation';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import Loading from '$comp/loading.svelte';
    import { H3, Muted } from '$comp/typography';
    import { Button, buttonVariants } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
    import { Separator } from '$comp/ui/separator';
    import { deleteProject, getProjectQuery, resetData, updateProject } from '$features/projects/api.svelte';
    import RemoveProjectDialog from '$features/projects/components/dialogs/remove-project-dialog.svelte';
    import ResetProjectDataDialog from '$features/projects/components/dialogs/reset-project-data-dialog.svelte';
    import { UpdateProject } from '$features/projects/models';
    import { applyServerSideErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import AlertTriangle from '@lucide/svelte/icons/alert-triangle';
    import Issues from '@lucide/svelte/icons/bug';
    import X from '@lucide/svelte/icons/x';
    import { toast } from 'svelte-sonner';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';
    import { debounce } from 'throttle-debounce';

    let toastId = $state<number | string>();

    const projectId = page.params.projectId || '';
    const projectQuery = getProjectQuery({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    const update = updateProject({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    let showRemoveDialog = $state(false);
    const removeProject = deleteProject({
        route: {
            get ids() {
                return [projectId];
            }
        }
    });

    async function remove() {
        await removeProject.mutateAsync();

        toast.dismiss(toastId);
        toastId = toast.success('Successfully queued the project for deletion.');

        await goto('/next/project/list');
    }

    let showResetDialog = $state(false);
    const resetProject = resetData({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    async function reset() {
        await resetProject.mutateAsync();

        toast.dismiss(toastId);
        toastId = toast.success('Successfully queued the project for data reset.');
    }

    const form = superForm(defaults(projectQuery.data ?? new UpdateProject(), classvalidatorClient(UpdateProject)), {
        dataType: 'json',
        async onUpdate({ form, result }) {
            if (!form.valid) {
                return;
            }

            try {
                await update.mutateAsync(form.data);

                toast.dismiss(toastId);
                toastId = toast.success('Successfully updated Project name');
            } catch (error) {
                if (error instanceof ProblemDetails) {
                    applyServerSideErrors(form, error);
                    result.status = error.status ?? 500;
                } else {
                    result.status = 500;
                }

                toastId = toast.error(form.message ?? 'Error saving project name. Please try again.');
            }
        },
        SPA: true,
        validators: classvalidatorClient(UpdateProject)
    });

    const { enhance, form: formData, message, submit, submitting, tainted } = form;
    const debouncedFormSubmit = debounce(1000, submit);

    $effect(() => {
        if (!projectQuery.isSuccess) {
            return;
        }

        if (!$submitting && !$tainted) {
            form.reset({ data: projectQuery.data, keepMessage: true });
        }
    });

    // TODO: Add Skeleton
</script>

<div class="space-y-6">
    <div>
        <H3>Manage Project</H3>
        <Muted>Manage your project name.</Muted>
    </div>
    <Separator />

    <form method="POST" use:enhance>
        <ErrorMessage message={$message}></ErrorMessage>
        <Form.Field {form} name="name">
            <Form.Control>
                {#snippet children({ props })}
                    <Form.Label>Project name</Form.Label>
                    <Input {...props} bind:value={$formData.name} type="text" placeholder="Enter project name" required oninput={debouncedFormSubmit} />
                {/snippet}
            </Form.Control>
            <Form.Description />
            <Form.FieldErrors />
        </Form.Field>
    </form>

    <div class="flex w-full items-center justify-between">
        <div class="flex gap-2">
            <Button variant="secondary" href="/next/issues?filter=project:{projectId}">
                <Issues class="mr-2 size-4" /> Go To Issues
            </Button>
        </div>

        <div>
            <DropdownMenu.Root>
                <DropdownMenu.Trigger class={buttonVariants({ variant: 'destructive' })}>
                    <X class="size-4" />
                </DropdownMenu.Trigger>
                <DropdownMenu.Content align="end" class="w-56">
                    <DropdownMenu.Group>
                        <DropdownMenu.GroupHeading>Actions</DropdownMenu.GroupHeading>
                        <DropdownMenu.Separator />
                        <DropdownMenu.Item onclick={() => (showResetDialog = true)} disabled={resetProject.isPending}>
                            {#if resetProject.isPending}
                                <Loading class="mr-2" variant="secondary"></Loading>
                                <span>Resetting...</span>
                            {:else}
                                <AlertTriangle class="mr-2 size-4" />
                                <span>Reset Project Data</span>
                            {/if}
                        </DropdownMenu.Item>
                        <DropdownMenu.Item onclick={() => (showRemoveDialog = true)} disabled={removeProject.isPending}>
                            {#if removeProject.isPending}
                                <Loading class="mr-2" variant="secondary"></Loading>
                                <span>Deleting Project...</span>
                            {:else}
                                <X class="mr-2 size-4" />
                                <span>Delete Project</span>
                            {/if}
                        </DropdownMenu.Item>
                    </DropdownMenu.Group>
                </DropdownMenu.Content>
            </DropdownMenu.Root>
        </div>
    </div>
</div>

{#if projectQuery.isSuccess}
    <ResetProjectDataDialog bind:open={showResetDialog} name={projectQuery.data.name} {reset} />
    <RemoveProjectDialog bind:open={showRemoveDialog} name={projectQuery.data.name} {remove} />
{/if}
