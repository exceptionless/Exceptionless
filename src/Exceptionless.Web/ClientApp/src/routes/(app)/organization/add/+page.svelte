<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import ErrorMessage from '$comp/error-message.svelte';
    import Loading from '$comp/loading.svelte';
    import { H3, Muted } from '$comp/typography';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
    import { postOrganization } from '$features/organizations/api.svelte';
    import { useHideOrganizationNotifications } from '$features/organizations/hooks/use-hide-organization-notifications.svelte';
    import { applyServerSideErrors } from '$features/shared/validation';
    import { NewOrganization } from '$generated/api';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { toast } from 'svelte-sonner';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';

    let toastId = $state<number | string>();
    const createOrganization = postOrganization();

    useHideOrganizationNotifications();

    const form = superForm(defaults(new NewOrganization(), classvalidatorClient(NewOrganization)), {
        dataType: 'json',
        id: 'post-organization',
        async onUpdate({ form, result }) {
            if (!form.valid) {
                return;
            }

            toast.dismiss(toastId);
            try {
                const { id } = await createOrganization.mutateAsync(form.data);
                toastId = toast.success('Organization added successfully');
                await goto(resolve('/(app)/organization/[organizationId]/manage', { organizationId: id }));

                // HACK: This is to prevent sveltekit from stealing focus
                result.type = 'failure';
            } catch (error: unknown) {
                if (error instanceof ProblemDetails) {
                    applyServerSideErrors(form, error);
                    result.status = error.status ?? 500;
                } else {
                    result.status = 500;
                }

                toastId = toast.error(form.message ?? 'Error creating organization. Please try again.');
            }
        },
        SPA: true,
        validators: classvalidatorClient(NewOrganization)
    });

    const { enhance, form: formData, message, submitting } = form;
</script>

<div class="flex flex-col gap-4">
    <div class="flex flex-col gap-1">
        <H3>Add Organization</H3>
        <Muted>Add a new organization to start tracking errors and events.</Muted>
    </div>
    <form method="POST" use:enhance>
        <ErrorMessage message={$message} />
        <Form.Field {form} name="name">
            <Form.Control>
                {#snippet children({ props })}
                    <Form.Label>Organization Name</Form.Label>
                    <Input {...props} bind:value={$formData.name} placeholder="Enter organization name" />
                {/snippet}
            </Form.Control>
            <Form.FieldErrors />
        </Form.Field>

        <Form.Button>
            {#if $submitting}
                <Loading class="mr-2" variant="secondary"></Loading> Adding Organization...
            {:else}
                Add Organization
            {/if}</Form.Button
        >
    </form>
</div>
