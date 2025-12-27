<script lang="ts">
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
    import { deleteOrganization, getOrganizationQuery, patchOrganization } from '$features/organizations/api.svelte';
    import RemoveOrganizationDialog from '$features/organizations/components/dialogs/remove-organization-dialog.svelte';
    import { type NewOrganizationFormData, NewOrganizationSchema } from '$features/organizations/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import Issues from '@lucide/svelte/icons/bug';
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
                    await update.mutateAsync(value);
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

    // TODO: Add Skeleton
</script>

<div class="space-y-6">
    <div>
        <H3>General</H3>
        <Muted>Manage your organization name.</Muted>
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

    <div class="flex w-full items-center justify-between">
        <Button variant="secondary" href={resolve('/(app)/issues')}>
            <Issues class="mr-2 size-4" /> Go To Issues
        </Button>

        <DropdownMenu.Root>
            <DropdownMenu.Trigger class={buttonVariants({ variant: 'destructive' })}>
                <X class="size-4" />
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
