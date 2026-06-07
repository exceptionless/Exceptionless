<script lang="ts">
    import type { UpdateProject } from '$features/projects/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import { Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
    import * as Tooltip from '$comp/ui/tooltip';
    import { deleteProject, generateSampleData, getProjectQuery, resetData, updateProject } from '$features/projects/api.svelte';
    import RemoveProjectDialog from '$features/projects/components/dialogs/remove-project-dialog.svelte';
    import ResetProjectDataDialog from '$features/projects/components/dialogs/reset-project-data-dialog.svelte';
    import { type UpdateProjectFormData, UpdateProjectSchema } from '$features/projects/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import AlertTriangle from '@lucide/svelte/icons/alert-triangle';
    import Database from '@lucide/svelte/icons/database';
    import NotificationSettings from '@lucide/svelte/icons/mail';
    import Send from '@lucide/svelte/icons/send';
    import Stacks from '@lucide/svelte/icons/layers';
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

        await goto(resolve('/(app)/project/list'));
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

    const generateSampleDataMutation = generateSampleData({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    async function generateProjectSampleData() {
        toast.dismiss(toastId);

        try {
            await generateSampleDataMutation.mutateAsync();
            toastId = toast.success('Sample data generation has been queued. Events will appear shortly.');
        } catch (error) {
            toastId = toast.error('Failed to generate sample data. Please try again.');
            throw error;
        }
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
    <Muted>General project settings</Muted>

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
            <Button variant="secondary" href={`${resolve('/(app)/stack')}?filter=project:${projectId}`}>
                <Stacks class="mr-2 size-4" /> Go To Stacks
            </Button>
            <Button variant="secondary" href={resolve('/(app)/project/[projectId]/configure', { projectId })}>
                <Send class="mr-2 size-4" /> Send Events
            </Button>
            <Button variant="secondary" href={`${resolve('/(app)/account/notifications')}?project=${projectId}`}>
                <NotificationSettings class="mr-2 size-4" /> Notifications
            </Button>
            <Button variant="secondary" onclick={generateProjectSampleData} disabled={generateSampleDataMutation.isPending}>
                {#if generateSampleDataMutation.isPending}
                    <Spinner />
                    <span>Generating...</span>
                {:else}
                    <Database class="mr-2 size-4" />
                    <span>Generate Sample Data</span>
                {/if}
            </Button>
        </div>

        <div class="flex items-center gap-2">
            <Tooltip.Root>
                <Tooltip.Trigger>
                    {#snippet child({ props })}
                        <Button
                            {...props}
                            variant="destructive"
                            size="icon"
                            aria-label="Reset project data"
                            onclick={() => (showResetDialog = true)}
                            disabled={resetProject.isPending}
                        >
                            {#if resetProject.isPending}
                                <Spinner />
                            {:else}
                                <AlertTriangle class="size-4" />
                            {/if}
                        </Button>
                    {/snippet}
                </Tooltip.Trigger>
                <Tooltip.Content>Reset project data</Tooltip.Content>
            </Tooltip.Root>

            <Tooltip.Root>
                <Tooltip.Trigger>
                    {#snippet child({ props })}
                        <Button
                            {...props}
                            variant="destructive"
                            size="icon"
                            aria-label="Delete project"
                            onclick={() => (showRemoveDialog = true)}
                            disabled={removeProject.isPending}
                        >
                            {#if removeProject.isPending}
                                <Spinner />
                            {:else}
                                <X class="size-4" />
                            {/if}
                        </Button>
                    {/snippet}
                </Tooltip.Trigger>
                <Tooltip.Content>Delete project</Tooltip.Content>
            </Tooltip.Root>
        </div>
    </div>
</div>

{#if projectQuery.isSuccess}
    <ResetProjectDataDialog bind:open={showResetDialog} name={projectQuery.data.name} {reset} />
    <RemoveProjectDialog bind:open={showRemoveDialog} name={projectQuery.data.name} {remove} />
{/if}
