<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import { Muted } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import { Button, buttonVariants } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
    import { Switch } from '$comp/ui/switch';
    import { deleteOrganization, getOrganizationQuery, patchOrganization } from '$features/organizations/api.svelte';
    import RemoveOrganizationDialog from '$features/organizations/components/dialogs/remove-organization-dialog.svelte';
    import { type NewOrganizationFormData, NewOrganizationSchema } from '$features/organizations/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import Stacks from '@lucide/svelte/icons/layers';
    import Plus from '@lucide/svelte/icons/plus';
    import X from '@lucide/svelte/icons/x';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';
    import { debounce } from 'throttle-debounce';

    let toastId = $state<number | string>();

    const organizationId = $derived(page.params.organizationId || '');
    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organizationId;
            }
        }
    });

    const update = patchOrganization({
        route: {
            get id() {
                return organizationId;
            }
        }
    });

    let showRemoveDialog = $state(false);
    const removeOrganization = deleteOrganization({
        route: {
            get ids() {
                return [organizationId];
            }
        }
    });

    async function remove() {
        toast.dismiss(toastId);
        try {
            await removeOrganization.mutateAsync();
            toastId = toast.success('Successfully queued the organization for deletion.');

            await goto(resolve('/(app)/organization/list'));
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while trying to delete the organization: ${message}`);
        }
    }

    const form = createForm(() => ({
        defaultValues: {
            name: organizationQuery.data?.name
        } as NewOrganizationFormData,
        validators: {
            onSubmit: NewOrganizationSchema,
            onSubmitAsync: async ({ value }) => {
                toast.dismiss(toastId);
                try {
                    await update.mutateAsync({ name: value.name });
                    toastId = toast.success('Successfully updated Organization name');
                    return null;
                } catch (error: unknown) {
                    toastId = toast.error('Error saving organization name. Please try again.');
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return { form: 'An unexpected error occurred.' };
                }
            }
        }
    }));

    const debouncedFormSubmit = debounce(1000, () => form.handleSubmit());

    // Budget alert settings state
    const budgetSettings = $derived(organizationQuery.data?.budget_alert_settings);
    let budgetEnabled = $state(false);
    let thresholds = $state<number[]>([]);
    let newThreshold = $state('');
    let budgetSaving = $state(false);
    let thresholdError = $state('');

    $effect(() => {
        budgetEnabled = budgetSettings?.enabled ?? false;
        thresholds = [...(budgetSettings?.thresholds ?? [])].sort((a, b) => a - b);
    });

    async function saveBudgetSettings() {
        toast.dismiss(toastId);
        budgetSaving = true;
        try {
            await update.mutateAsync({
                budget_alert_settings: {
                    enabled: budgetEnabled,
                    thresholds: [...thresholds].sort((a, b) => a - b)
                }
            });
            toastId = toast.success('Budget alert settings saved.');
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`Error saving budget settings: ${message}`);
        } finally {
            budgetSaving = false;
        }
    }

    function addThreshold() {
        thresholdError = '';
        const val = parseInt(newThreshold, 10);
        if (isNaN(val) || val < 1 || val > 100) {
            thresholdError = 'Enter a percentage between 1 and 100.';
            return;
        }
        if (thresholds.includes(val)) {
            thresholdError = 'That threshold is already added.';
            return;
        }
        thresholds = [...thresholds, val].sort((a, b) => a - b);
        newThreshold = '';
    }

    function removeThreshold(val: number) {
        thresholds = thresholds.filter((t) => t !== val);
    }

    function handleThresholdKeydown(e: KeyboardEvent) {
        if (e.key === 'Enter') {
            e.preventDefault();
            addThreshold();
        }
    }

    // TODO: Add Skeleton
</script>

<div class="space-y-8">
    <Muted>General organization settings</Muted>

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
                    <Field.Label for={field.name}>Organization name</Field.Label>
                    <Input
                        id={field.name}
                        name={field.name}
                        type="text"
                        placeholder="Enter organization name"
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

    <section class="space-y-4" aria-labelledby="budget-alert-heading">
        <div class="space-y-1">
            <h2 id="budget-alert-heading" class="text-sm font-medium">Budget alerts</h2>
            <Muted class="text-xs">Send email alerts when monthly event usage crosses percentage thresholds.</Muted>
        </div>

        <div class="bg-card space-y-4 rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div>
                    <div class="text-sm font-medium">Enable budget alerts</div>
                    <Muted class="text-xs">Receive email notifications when usage thresholds are crossed.</Muted>
                </div>
                <Switch
                    id="budget-enabled"
                    checked={budgetEnabled}
                    onCheckedChange={(checked) => (budgetEnabled = checked)}
                    aria-label="Enable budget alerts"
                />
            </div>

            {#if budgetEnabled}
                <div class="space-y-3">
                    <div class="text-sm font-medium">Alert thresholds</div>
                    <div class="flex flex-wrap gap-2" aria-label="Current thresholds">
                        {#each thresholds as threshold (threshold)}
                            <Badge variant="secondary" class="gap-1 pr-1">
                                {threshold}%
                                <button
                                    type="button"
                                    class="ml-1 rounded-full p-0.5 hover:bg-white/20"
                                    onclick={() => removeThreshold(threshold)}
                                    aria-label="Remove {threshold}% threshold"
                                >
                                    <X class="size-3" />
                                </button>
                            </Badge>
                        {:else}
                            <Muted class="text-xs italic">No thresholds set. Add at least one below.</Muted>
                        {/each}
                    </div>

                    <div class="flex gap-2">
                        <div class="w-28">
                            <Field.Field>
                                <Field.Label for="new-threshold" class="sr-only">New threshold percentage</Field.Label>
                                <Input
                                    id="new-threshold"
                                    type="number"
                                    min="1"
                                    max="100"
                                    placeholder="e.g. 80"
                                    bind:value={newThreshold}
                                    onkeydown={handleThresholdKeydown}
                                    aria-invalid={thresholdError ? 'true' : undefined}
                                    aria-describedby={thresholdError ? 'threshold-error' : undefined}
                                />
                                {#if thresholdError}
                                    <p id="threshold-error" class="text-destructive text-xs">{thresholdError}</p>
                                {/if}
                            </Field.Field>
                        </div>
                        <Button type="button" variant="secondary" size="sm" class="mt-0 self-start" onclick={addThreshold} aria-label="Add threshold">
                            <Plus class="mr-1 size-4" />
                            Add
                        </Button>
                    </div>
                </div>
            {/if}

            <div class="flex justify-end border-t pt-3">
                <Button type="button" onclick={saveBudgetSettings} disabled={budgetSaving || update.isPending} size="sm">
                    {#if budgetSaving || update.isPending}
                        <Spinner class="mr-2 size-4" />
                    {/if}
                    Save budget settings
                </Button>
            </div>
        </div>
    </section>

    <div class="flex w-full items-center justify-between">
        <Button variant="secondary" href={resolve('/(app)/stacks')}>
            <Stacks class="mr-2 size-4" /> Go To Stacks
        </Button>

        <DropdownMenu.Root>
            <DropdownMenu.Trigger class={buttonVariants({ variant: 'destructive' })}>
                <X class="mr-2 size-4" />
                <span>Delete</span>
            </DropdownMenu.Trigger>
            <DropdownMenu.Content align="end" class="w-56">
                <DropdownMenu.Group>
                    <DropdownMenu.GroupHeading>Actions</DropdownMenu.GroupHeading>
                    <DropdownMenu.Separator />
                    <DropdownMenu.Item onclick={() => (showRemoveDialog = true)} disabled={removeOrganization.isPending}>
                        {#if removeOrganization.isPending}
                            <Spinner />
                            <span>Deleting Organization...</span>
                        {:else}
                            <X class="mr-2 size-4" />
                            <span>Delete Organization</span>
                        {/if}
                    </DropdownMenu.Item>
                </DropdownMenu.Group>
            </DropdownMenu.Content>
        </DropdownMenu.Root>
    </div>
</div>

{#if organizationQuery.isSuccess}
    <RemoveOrganizationDialog bind:open={showRemoveDialog} name={organizationQuery.data.name} {remove} />
{/if}

