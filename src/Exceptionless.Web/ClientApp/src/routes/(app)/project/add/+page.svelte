<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { ProblemDetails } from '@foundatiofx/fetchclient';
    import { createForm } from '@tanstack/svelte-form';
    import { untrack } from 'svelte';
    import { toast } from 'svelte-sonner';

    import type { NewProject } from '#features/projects/models.ts';

    import ErrorMessage from '#comp/error-message.svelte';
    import { H3 } from '#comp/typography/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Card from '#comp/ui/card/index.ts';
    import * as Field from '#comp/ui/field/index.ts';
    import { Input } from '#comp/ui/input/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import { showBillingDialogOnUpgradeProblem } from '#features/billing/index.ts';
    import ExieGuide from '#features/onboarding/components/exie-guide.svelte';
    import OnboardingStepper from '#features/onboarding/components/onboarding-stepper.svelte';
    import { organization } from '#features/organizations/context.svelte.ts';
    import { createProductTourActions } from '#features/product-tours/actions.svelte.ts';
    import ProductTourSpotlight from '#features/product-tours/components/product-tour-spotlight.svelte';
    import { productTourCheckpoint } from '#features/product-tours/state.svelte.ts';
    import { postProject } from '#features/projects/api.svelte.ts';
    import { AddProjectSchema, type NewProjectFormData } from '#features/projects/schemas.ts';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '#features/shared/validation.ts';

    let toastId = $state<number | string>();
    const createProject = postProject();
    const tourActions = createProductTourActions();
    const projectConfigureCheckpoint = $derived(productTourCheckpoint.current?.tourName === 'project-configure' ? productTourCheckpoint.current : undefined);

    const form = createForm(() => ({
        defaultValues: {
            delete_bot_data_enabled: true,
            name: '',
            organization_id: organization.current ?? ''
        } as NewProjectFormData,
        validators: {
            onSubmit: AddProjectSchema,
            onSubmitAsync: async ({ value }) => {
                toast.dismiss(toastId);
                try {
                    const { id } = await createProject.mutateAsync({
                        ...value,
                        name: value.name.trim(),
                        organization_id: organization.current ?? value.organization_id
                    } as NewProject);
                    const checkpoint = projectConfigureCheckpoint;
                    if (checkpoint) {
                        productTourCheckpoint.advance(checkpoint, 'choose-platform');
                    }

                    await goto(
                        resolve('/(app)/project/[projectId]/configure', {
                            projectId: id
                        }) + '?redirect=true'
                    );
                    return null;
                } catch (error: unknown) {
                    if (showBillingDialogOnUpgradeProblem(error, organization.current, () => form.handleSubmit())) {
                        return null;
                    }

                    if (error instanceof ProblemDetails) {
                        toastId = toast.error(error.title || 'Error creating project. Please try again.');
                        return problemDetailsToFormErrors(error);
                    }

                    toastId = toast.error('Error creating project. Please try again.');
                    return {
                        form: 'An unexpected error occurred, please try again.'
                    };
                }
            }
        }
    }));

    $effect(() => {
        const organizationId = organization.current ?? '';
        untrack(() => form.setFieldValue('organization_id', organizationId));
    });
</script>

<div class="mx-auto flex w-full max-w-xl flex-col gap-6">
    <H3>Add Project</H3>

    <ExieGuide mood="hello" size={64}>Another app? Love it. Tell me what it's called and I'll show you how to connect it.</ExieGuide>

    <OnboardingStepper current="name-project" />

    <Card.Root>
        <Card.Content>
            <form
                data-tour="project-setup-form"
                novalidate
                onsubmit={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    form.handleSubmit();
                }}
            >
                <Field.Group>
                    <form.Subscribe selector={(state) => state.errors}>
                        {#snippet children(errors)}
                            <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                        {/snippet}
                    </form.Subscribe>
                    <form.Field name="name">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)} data-tour="project-name">
                                <Field.Label for={field.name}>What's your app called?</Field.Label>
                                <Input
                                    aria-describedby="project-name-description"
                                    aria-invalid={ariaInvalid(field)}
                                    autocomplete="off"
                                    autofocus
                                    id={field.name}
                                    maxlength={100}
                                    name={field.name}
                                    onblur={field.handleBlur}
                                    oninput={(e) => field.handleChange(e.currentTarget.value)}
                                    placeholder="e.g. Storefront API"
                                    required
                                    value={field.state.value}
                                />
                                <Field.Description id="project-name-description"
                                    >Projects are the apps you monitor. Each gets its own API key.</Field.Description
                                >
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>
                    <form.Subscribe selector={(state) => state.isSubmitting}>
                        {#snippet children(isSubmitting)}
                            <Button class="w-full sm:w-auto sm:self-start" disabled={isSubmitting} size="lg" type="submit">
                                {#if isSubmitting}
                                    <Spinner data-icon="inline-start" /> Adding project…
                                {:else}
                                    Continue to Client Setup
                                {/if}
                            </Button>
                        {/snippet}
                    </form.Subscribe>
                </Field.Group>
            </form>
        </Card.Content>
    </Card.Root>
</div>

{#if projectConfigureCheckpoint?.checkpointName === 'project-name'}
    <ProductTourSpotlight
        checkpoint={projectConfigureCheckpoint}
        showProgress={false}
        onDismiss={tourActions.dismiss}
        side="top"
        target="[data-tour='project-setup-form']"
        title="Name your project"
    >
        {#snippet description()}
            Give your app a name, then select <strong>Continue to Client Setup</strong> to connect it.
        {/snippet}
    </ProductTourSpotlight>
{/if}
