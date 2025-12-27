<script lang="ts">
    import type { UpdateProject } from '$features/projects/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import { H3, Muted } from '$comp/typography';
    import { Button, buttonVariants } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Separator } from '$comp/ui/separator';
    import { Spinner } from '$comp/ui/spinner';
    import { organization } from '$features/organizations/context.svelte';
    import { deleteProject, getProjectQuery, resetData, updateProject } from '$features/projects/api.svelte';
    import RemoveProjectDialog from '$features/projects/components/dialogs/remove-project-dialog.svelte';
    import ResetProjectDataDialog from '$features/projects/components/dialogs/reset-project-data-dialog.svelte';
    import { type UpdateProjectFormData, UpdateProjectSchema } from '$features/projects/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import AlertTriangle from '@lucide/svelte/icons/alert-triangle';
    import Issues from '@lucide/svelte/icons/bug';
    import X from '@lucide/svelte/icons/x';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';
    import { debounce } from 'throttle-debounce';

    let toastId = $state<number | string>();

    const projectId = $derived(page.params.projectId || '');
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

        if (organization.current) {
            await goto(resolve('/(app)/organization/[organizationId]/projects', { organizationId: organization.current }));
        } else {
            goto(resolve('/(app)/organization/list'));
        }
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

    const form = createForm(() => ({
        defaultValues: {
            name: projectQuery.data?.name
        } as UpdateProjectFormData,
        validators: {
            onSubmit: UpdateProjectSchema,
            onSubmitAsync: async ({ value }) => {
                toast.dismiss(toastId);
                try {
                    await update.mutateAsync(value as UpdateProject);
                    toastId = toast.success('Successfully updated project name');
                    return null;
                } catch (error: unknown) {
                    toastId = toast.error('Error saving project name. Please try again.');
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return { form: 'An unexpected error occurred.' };
                }
            }
        }
    }));

    const debouncedFormSubmit = debounce(1000, () => form.handleSubmit());

    // TODO: Add Skeleton
</script>

<div class="space-y-6">
    <div>
        <H3>General</H3>
        <Muted>Manage your project name.</Muted>
    </div>
    <Separator />

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
                    <Field.Label for={field.name}>Project name</Field.Label>
                    <Input
                        id={field.name}
                        name={field.name}
                        type="text"
                        placeholder="Enter project name"
                        required
                        value={field.state.value}
                        onblur={field.handleBlur}
                        oninput={(e) => {
                            field.handleChange(e.currentTarget.value);
                            debouncedFormSubmit();
                        }}
                        aria-invalid={ariaInvalid(field)}
                    />
                    <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                </Field.Field>
            {/snippet}
        </form.Field>
    </form>

    <div class="flex w-full items-center justify-between">
        <div class="flex gap-2">
            <Button variant="secondary" href={`${resolve('/(app)/issues')}?filter=project:${projectId}`}>
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
                                <Spinner />
                                <span>Resetting...</span>
                            {:else}
                                <AlertTriangle class="mr-2 size-4" />
                                <span>Reset Project Data</span>
                            {/if}
                        </DropdownMenu.Item>
                        <DropdownMenu.Item onclick={() => (showRemoveDialog = true)} disabled={removeProject.isPending}>
                            {#if removeProject.isPending}
                                <Spinner />
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
