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
    import { deleteOrganization, getOrganizationQuery, patchOrganization } from '$features/organizations/api.svelte';
    import RemoveOrganizationDialog from '$features/organizations/components/dialogs/remove-organization-dialog.svelte';
    import { NewOrganization } from '$features/organizations/models';
    import { structuredCloneState } from '$features/shared/utils/state.svelte';
    import { applyServerSideErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import Issues from '@lucide/svelte/icons/bug';
    import X from '@lucide/svelte/icons/x';
    import { toast } from 'svelte-sonner';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';
    import { debounce } from 'throttle-debounce';

    let toastId = $state<number | string>();
    let previousOrganizationRef = $state<NewOrganization>();

    const organizationId = page.params.organizationId || '';
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

            await goto('/next/organization/list');
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while trying to delete the organization: ${message}`);
        }
    }

    const form = superForm(defaults(structuredCloneState(organizationQuery.data) ?? new NewOrganization(), classvalidatorClient(NewOrganization)), {
        dataType: 'json',
        async onUpdate({ form, result }) {
            if (!form.valid) {
                return;
            }

            try {
                await update.mutateAsync(form.data);

                toast.dismiss(toastId);
                toastId = toast.success('Successfully updated Organization name');
            } catch (error: unknown) {
                if (error instanceof ProblemDetails) {
                    applyServerSideErrors(form, error);
                    result.status = error.status ?? 500;
                } else {
                    result.status = 500;
                }

                toastId = toast.error(form.message ?? 'Error saving organization name. Please try again.');
            }
        },
        SPA: true,
        validators: classvalidatorClient(NewOrganization)
    });

    const { enhance, form: formData, message, submit, submitting, tainted } = form;
    const debouncedFormSubmit = debounce(1000, submit);

    $effect(() => {
        if (!organizationQuery.isSuccess) {
            return;
        }

        if (!$submitting && !$tainted && organizationQuery.data !== previousOrganizationRef) {
            const clonedData = structuredCloneState(organizationQuery.data);
            form.reset({ data: clonedData, keepMessage: true });
            previousOrganizationRef = organizationQuery.data;
        }
    });

    // TODO: Add Skeleton
</script>

<div class="space-y-6">
    <div>
        <H3>Manage Organization</H3>
        <Muted>Manage your organization name.</Muted>
    </div>
    <Separator />

    <form method="POST" use:enhance>
        <ErrorMessage message={$message}></ErrorMessage>
        <Form.Field {form} name="name">
            <Form.Control>
                {#snippet children({ props })}
                    <Form.Label>Organization name</Form.Label>
                    <Input {...props} bind:value={$formData.name} type="text" placeholder="Enter organization name" required oninput={debouncedFormSubmit} />
                {/snippet}
            </Form.Control>
            <Form.Description />
            <Form.FieldErrors />
        </Form.Field>
    </form>

    <div class="flex w-full items-center justify-between">
        <div class="flex gap-2">
            <Button variant="secondary" href="/next/issues">
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
                        <DropdownMenu.Item onclick={() => (showRemoveDialog = true)} disabled={removeOrganization.isPending}>
                            {#if removeOrganization.isPending}
                                <Loading class="mr-2" variant="secondary"></Loading>
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
</div>

{#if organizationQuery.isSuccess}
    <RemoveOrganizationDialog bind:open={showRemoveDialog} name={organizationQuery.data.name} {remove} />
{/if}
