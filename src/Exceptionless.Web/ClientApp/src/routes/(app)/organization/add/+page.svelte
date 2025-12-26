<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import ErrorMessage from '$comp/error-message.svelte';
    import { H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
    import { postOrganization } from '$features/organizations/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { useHideOrganizationNotifications } from '$features/organizations/hooks/use-hide-organization-notifications.svelte';
    import { type NewOrganizationFormData, NewOrganizationSchema } from '$features/organizations/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';

    let toastId = $state<number | string>();
    const createOrganization = postOrganization();

    useHideOrganizationNotifications();

    const form = createForm(() => ({
        defaultValues: {
            name: ''
        } as NewOrganizationFormData,
        validators: {
            onSubmit: NewOrganizationSchema,
            onSubmitAsync: async ({ value }) => {
                toast.dismiss(toastId);
                try {
                    const { id } = await createOrganization.mutateAsync(value);
                    // Update the persisted organization state so the sidebar selects the new org
                    organization.current = id;
                    toastId = toast.success('Organization added successfully');
                    await goto(resolve('/(app)/organization/[organizationId]/manage', { organizationId: id }));
                    return null;
                } catch (error: unknown) {
                    toastId = toast.error('Error creating organization. Please try again.');
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return { form: 'An unexpected error occurred.' };
                }
            }
        }
    }));
</script>

<div class="flex flex-col gap-4">
    <div class="flex flex-col gap-1">
        <H3>Add Organization</H3>
        <Muted>Add a new organization to start tracking errors and events.</Muted>
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
        <form.Subscribe selector={(state) => state.isSubmitting}>
            {#snippet children(isSubmitting)}
                <Button type="submit" class="mt-4" disabled={isSubmitting}>
                    {#if isSubmitting}
                        <Spinner /> Adding Organization...
                    {:else}
                        Add Organization
                    {/if}
                </Button>
            {/snippet}
        </form.Subscribe>
    </form>
</div>
