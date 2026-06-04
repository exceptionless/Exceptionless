<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import { Muted } from '$comp/typography';
    import * as Avatar from '$comp/ui/avatar';
    import { Button, buttonVariants } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
    import {
        deleteOrganization,
        deleteOrganizationIcon,
        getOrganizationQuery,
        patchOrganization,
        uploadOrganizationIcon
    } from '$features/organizations/api.svelte';
    import RemoveOrganizationDialog from '$features/organizations/components/dialogs/remove-organization-dialog.svelte';
    import { type NewOrganizationFormData, NewOrganizationSchema } from '$features/organizations/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { getInitials } from '$shared/strings';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import Projects from '@lucide/svelte/icons/folder-open';
    import Stacks from '@lucide/svelte/icons/layers';
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
    const uploadIcon = uploadOrganizationIcon({
        route: {
            get id() {
                return organizationId;
            }
        }
    });
    const removeIcon = deleteOrganizationIcon({
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
                    toastId = toast.success('Successfully updated Organization');
                    return null;
                } catch (error: unknown) {
                    toastId = toast.error('Error saving organization. Please try again.');
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return { form: 'An unexpected error occurred.' };
                }
            }
        }
    }));

    const debouncedFormSubmit = debounce(1000, () => form.handleSubmit());
    const isIconSaving = $derived(uploadIcon.isPending || removeIcon.isPending);

    async function handleIconUpload(file: File) {
        toast.dismiss(toastId);
        try {
            await uploadIcon.mutateAsync(file);
            toastId = toast.success('Successfully updated organization icon.');
        } catch (error: unknown) {
            toastId = toast.error(getProblemMessage(error, 'Error saving organization icon. Please try again.'));
        }
    }

    async function handleRemoveIcon() {
        toast.dismiss(toastId);
        try {
            await removeIcon.mutateAsync();
            toastId = toast.success('Successfully removed organization icon.');
        } catch (error: unknown) {
            toastId = toast.error(getProblemMessage(error, 'Error removing organization icon. Please try again.'));
        }
    }

    function getProblemMessage(error: unknown, fallback: string) {
        if (!(error instanceof ProblemDetails)) {
            return fallback;
        }

        return error.errors.file?.[0] ?? error.title ?? fallback;
    }

    // TODO: Add Skeleton
</script>

<div class="space-y-6">
    <Muted>General organization settings</Muted>

    <div class="flex flex-col gap-4 sm:flex-row sm:items-center">
        <Avatar.Root class="h-20 w-20 rounded-lg" title="Organization Icon">
            {#if organizationQuery.data?.icon_url}
                <Avatar.Image alt={`${organizationQuery.data.name} icon`} src={organizationQuery.data.icon_url} />
            {/if}
            <Avatar.Fallback class="rounded-lg">{getInitials(organizationQuery.data?.name ?? '?')}</Avatar.Fallback>
        </Avatar.Root>
        <div class="space-y-3">
            <Muted>Upload a custom icon or remove it to use the organization initials.</Muted>
            <div class="flex flex-col gap-2 sm:flex-row">
                <Input
                    aria-label="Upload organization icon"
                    accept="image/png,image/jpeg,image/gif,image/webp"
                    disabled={isIconSaving}
                    type="file"
                    onchange={(e) => {
                        const file = e.currentTarget.files?.[0];
                        if (file) {
                            void handleIconUpload(file);
                            e.currentTarget.value = '';
                        }
                    }}
                />
                {#if organizationQuery.data?.icon_url}
                    <Button variant="outline" onclick={handleRemoveIcon} disabled={isIconSaving}>
                        {#if removeIcon.isPending}
                            <Spinner class="mr-2 size-4" />
                        {:else}
                            <X class="mr-2 size-4" />
                        {/if}
                        Remove
                    </Button>
                {/if}
            </div>
        </div>
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

    <div class="flex w-full flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div class="flex flex-wrap gap-3">
            <Button variant="secondary" href={resolve('/(app)/stack')}>
                <Stacks class="mr-2 size-4" /> Go To Stacks
            </Button>
            <Button variant="secondary" href={resolve('/(app)/organization/[organizationId]/projects', { organizationId })}>
                <Projects class="mr-2 size-4" /> Go To Projects
            </Button>
        </div>

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
