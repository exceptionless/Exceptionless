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
    import * as Select from '$comp/ui/select';
    import { Spinner } from '$comp/ui/spinner';
    import * as Tooltip from '$comp/ui/tooltip';
    import { deleteProject, generateSampleData, getProjectQuery, resetData, updateProject } from '$features/projects/api.svelte';
    import RemoveProjectDialog from '$features/projects/components/dialogs/remove-project-dialog.svelte';
    import ResetProjectDataDialog from '$features/projects/components/dialogs/reset-project-data-dialog.svelte';
    import { type UpdateProjectFormData, UpdateProjectSchema } from '$features/projects/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import AlertTriangle from '@lucide/svelte/icons/alert-triangle';
    import Configure from '@lucide/svelte/icons/cloud-download';
    import Database from '@lucide/svelte/icons/database';
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

    // Ingest limit state
    type LimitType = 'none' | 'fixed' | 'percent';
    const projectData = $derived(projectQuery.data);
    let limitType = $state<LimitType>('none');
    let fixedLimit = $state('');
    let percentLimit = $state('');
    let limitSaving = $state(false);
    let limitError = $state('');

    $effect(() => {
        const limit = projectData?.ingest_limit;
        if (!limit) {
            limitType = 'none';
            fixedLimit = '';
            percentLimit = '';
        } else if (limit.type === 0) {
            limitType = 'fixed';
            fixedLimit = limit.fixed_limit?.toString() ?? '';
            percentLimit = '';
        } else {
            limitType = 'percent';
            percentLimit = limit.percent_of_organization_limit?.toString() ?? '';
            fixedLimit = '';
        }
    });

    const limitTypeItems = [
        { value: 'none', label: 'No limit' },
        { value: 'fixed', label: 'Fixed event count' },
        { value: 'percent', label: 'Percentage of org limit' }
    ];

    async function saveIngestLimit() {
        limitError = '';
        toast.dismiss(toastId);
        limitSaving = true;

        try {
            let ingest_limit: { type: number; fixed_limit?: number | null; percent_of_organization_limit?: number | null } | null = null;

            if (limitType === 'fixed') {
                const val = parseInt(fixedLimit, 10);
                if (isNaN(val) || val < 1) {
                    limitError = 'Fixed limit must be a positive integer.';
                    limitSaving = false;
                    return;
                }
                ingest_limit = { type: 0, fixed_limit: val };
            } else if (limitType === 'percent') {
                const val = parseFloat(percentLimit);
                if (isNaN(val) || val <= 0) {
                    limitError = 'Percentage must be a positive number.';
                    limitSaving = false;
                    return;
                }
                ingest_limit = { type: 1, percent_of_organization_limit: val };
            }

            await update.mutateAsync({ ingest_limit } as UpdateProject);
            toastId = toast.success('Event ingest limit saved.');
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`Error saving ingest limit: ${message}`);
        } finally {
            limitSaving = false;
        }
    }

    // TODO: Add Skeleton
</script>

<div class="space-y-8">
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

    <section class="space-y-4" aria-labelledby="ingest-limit-heading">
        <div class="space-y-1">
            <h2 id="ingest-limit-heading" class="text-sm font-medium">Event ingest limit</h2>
            <Muted class="text-xs">Restrict how many events this project can ingest per month.</Muted>
        </div>

        <div class="bg-card space-y-4 rounded-lg border p-4">
            <Field.Field>
                <Field.Label for="limit-type">Limit type</Field.Label>
                <Select.Root
                    type="single"
                    value={limitType}
                    onValueChange={(v) => {
                        limitType = v as LimitType;
                        limitError = '';
                    }}
                >
                    <Select.Trigger id="limit-type" class="w-56">
                        {limitTypeItems.find((i) => i.value === limitType)?.label ?? 'No limit'}
                    </Select.Trigger>
                    <Select.Content>
                        {#each limitTypeItems as item (item.value)}
                            <Select.Item value={item.value}>{item.label}</Select.Item>
                        {/each}
                    </Select.Content>
                </Select.Root>
            </Field.Field>

            {#if limitType === 'fixed'}
                <Field.Field>
                    <Field.Label for="fixed-limit">Max events per month</Field.Label>
                    <Input id="fixed-limit" type="number" min="1" placeholder="e.g. 100000" class="w-48" bind:value={fixedLimit} />
                    <Field.Description>Events beyond this count will be blocked.</Field.Description>
                </Field.Field>
            {:else if limitType === 'percent'}
                <Field.Field>
                    <Field.Label for="percent-limit">Percentage of organization limit (%)</Field.Label>
                    <Input id="percent-limit" type="number" min="1" max="999" step="0.1" placeholder="e.g. 50" class="w-48" bind:value={percentLimit} />
                    <Field.Description>
                        Percentage of the organization's monthly event allowance. Values above 100% allow this project to use more than its equal share.
                        {#if projectData?.effective_ingest_limit != null}
                            <span class="ml-1 font-medium">Current effective limit: {projectData.effective_ingest_limit.toLocaleString()} events.</span>
                        {/if}
                    </Field.Description>
                </Field.Field>
            {/if}

            {#if limitError}
                <p class="text-destructive text-xs">{limitError}</p>
            {/if}

            {#if projectData?.is_smart_throttled}
                <div class="bg-muted rounded-md p-3 text-xs">
                    <span class="font-medium">Smart throttle active</span> — this project is accepting {Math.round((projectData.smart_throttle_sample_rate ?? 1) * 100)}% of events due to disproportionate resource usage.
                </div>
            {/if}

            <div class="flex justify-end border-t pt-3">
                <Button type="button" onclick={saveIngestLimit} disabled={limitSaving || update.isPending} size="sm">
                    {#if limitSaving || update.isPending}
                        <Spinner class="mr-2 size-4" />
                    {/if}
                    Save ingest limit
                </Button>
            </div>
        </div>
    </section>

    <div class="flex w-full items-center justify-between">
        <div class="flex gap-2">
            <Button variant="secondary" href={`${resolve('/(app)/stacks')}?filter=project:${projectId}`}>
                <Stacks class="mr-2 size-4" /> Go To Stacks
            </Button>
            <Button variant="secondary" href={resolve('/(app)/project/[projectId]/configure', { projectId })}>
                <Configure class="mr-2 size-4" /> Configure Project
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
