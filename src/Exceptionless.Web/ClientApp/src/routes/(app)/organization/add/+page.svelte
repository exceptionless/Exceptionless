<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { ProblemDetails } from '@foundatiofx/fetchclient';
    import { createForm } from '@tanstack/svelte-form';
    import { untrack } from 'svelte';
    import { toast } from 'svelte-sonner';
    import { type infer as Infer, object, string } from 'zod';

    import type { NewProject } from '#features/projects/models.ts';

    import ErrorMessage from '#comp/error-message.svelte';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Card from '#comp/ui/card/index.ts';
    import * as Field from '#comp/ui/field/index.ts';
    import { Input } from '#comp/ui/input/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import { showBillingDialogOnUpgradeProblem } from '#features/billing/index.ts';
    import ExieGuide from '#features/onboarding/components/exie-guide.svelte';
    import OnboardingStepper from '#features/onboarding/components/onboarding-stepper.svelte';
    import { getFirstName, suggestOrganizationName } from '#features/onboarding/organization-name.ts';
    import { getOrganizationsQuery, postOrganization } from '#features/organizations/api.svelte.ts';
    import { organization } from '#features/organizations/context.svelte.ts';
    import { useHideOrganizationNotifications } from '#features/organizations/hooks/use-hide-organization-notifications.svelte.ts';
    import { createProductTourActions } from '#features/product-tours/actions.svelte.ts';
    import ProductTourSpotlight from '#features/product-tours/components/product-tour-spotlight.svelte';
    import { productTourCheckpoint } from '#features/product-tours/state.svelte.ts';
    import { postProject } from '#features/projects/api.svelte.ts';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '#features/shared/validation.ts';
    import { getMeQuery } from '#features/users/api.svelte.ts';

    const SetupSchema = object({
        organization_name: string().trim().min(1, 'Name your organization'),
        project_name: string().trim().min(1, 'Give your app a name')
    });

    type SetupFormData = Infer<typeof SetupSchema>;

    let toastId = $state<number | string>();
    // Creating the organization and project are separate requests. If the project fails,
    // a retry must reuse the organization instead of creating (and being billed for) another.
    let createdOrganization = $state<{ id: string; name: string }>();

    const meQuery = getMeQuery();
    const organizationsQuery = getOrganizationsQuery({});
    const createOrganization = postOrganization();
    const createProject = postProject();
    const tourActions = createProductTourActions();
    const projectConfigureCheckpoint = $derived(productTourCheckpoint.current?.tourName === 'project-configure' ? productTourCheckpoint.current : undefined);
    const firstName = $derived(getFirstName(meQuery.data?.full_name));
    const isAdditionalOrganization = $derived((organizationsQuery.data?.data ?? []).some((item) => item.id !== createdOrganization?.id));
    const CREATE_ERROR_MESSAGE = 'Something went wrong while setting things up. Please try again.';

    useHideOrganizationNotifications();

    const form = createForm(() => ({
        defaultValues: {
            organization_name: '',
            project_name: ''
        } as SetupFormData,
        validators: {
            onSubmit: SetupSchema,
            onSubmitAsync: async ({ value }) => {
                toast.dismiss(toastId);
                try {
                    if (!createdOrganization) {
                        const newOrganization = await createOrganization.mutateAsync({
                            name: value.organization_name.trim()
                        });
                        createdOrganization = {
                            id: newOrganization.id,
                            name: newOrganization.name
                        };
                    }

                    const organizationId = createdOrganization.id;
                    organization.current = organizationId;

                    const createdProject = await createProject.mutateAsync({
                        delete_bot_data_enabled: true,
                        name: value.project_name.trim(),
                        organization_id: organizationId
                    } as NewProject);

                    const checkpoint = projectConfigureCheckpoint;
                    if (checkpoint) {
                        productTourCheckpoint.advance(checkpoint, 'choose-platform', organizationId);
                    }

                    await goto(
                        resolve('/(app)/project/[projectId]/configure', {
                            projectId: createdProject.id
                        }) + '?redirect=true'
                    );
                    return null;
                } catch (error: unknown) {
                    if (showBillingDialogOnUpgradeProblem(error, organization.current, () => form.handleSubmit())) {
                        return null;
                    }

                    if (error instanceof ProblemDetails) {
                        toastId = toast.error(error.title || CREATE_ERROR_MESSAGE);
                        return problemDetailsToFormErrors(error);
                    }

                    toastId = toast.error(CREATE_ERROR_MESSAGE);
                    return {
                        form: 'An unexpected error occurred, please try again.'
                    };
                }
            }
        }
    }));

    // Suggest an organization name once the account loads, unless the user already typed one.
    $effect(() => {
        const user = meQuery.data;
        if (!user) {
            return;
        }

        untrack(() => {
            if (form.getFieldMeta('organization_name')?.isDirty || form.getFieldValue('organization_name')) {
                return;
            }

            form.setFieldValue('organization_name', suggestOrganizationName(user.email_address, user.full_name), {
                dontUpdateMeta: true
            });
        });
    });
</script>

<div class="mx-auto flex w-full max-w-xl flex-col gap-6">
    <ExieGuide mood="hello" size={72} title={firstName ? `Hi ${firstName}, I'm Exie!` : "Hi, I'm Exie!"}>
        {#if isAdditionalOrganization}
            Starting something new? Tell me about the app first, then where it belongs.
        {:else}
            Let's get your first app connected. Start by telling me what it's called.
        {/if}
    </ExieGuide>

    <OnboardingStepper current="name-project" />

    <Card.Root>
        <Card.Header>
            <h1 class="text-foreground text-xl font-semibold tracking-tight">Set up Exceptionless</h1>
            <Card.Description>Name your app and organization. Next, I'll show you how to connect it.</Card.Description>
        </Card.Header>
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
                    <form.Field name="project_name">
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
                                <Field.Description id="project-name-description">This becomes your first project. You can add more later.</Field.Description>
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>
                    <form.Field name="organization_name">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)} data-tour="setup-organization-name">
                                <Field.Label for={field.name}>Organization name</Field.Label>
                                <Input
                                    aria-describedby="organization-name-description"
                                    aria-invalid={ariaInvalid(field)}
                                    autocomplete="organization"
                                    id={field.name}
                                    maxlength={100}
                                    name={field.name}
                                    onblur={field.handleBlur}
                                    oninput={(e) => field.handleChange(e.currentTarget.value)}
                                    readonly={!!createdOrganization}
                                    required
                                    value={createdOrganization?.name ?? field.state.value}
                                />
                                <Field.Description id="organization-name-description">
                                    {#if createdOrganization}
                                        {createdOrganization.name} is ready. I'll just add the project.
                                    {:else}
                                        Organizations hold your team and billing; projects are the apps you monitor.
                                    {/if}
                                </Field.Description>
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>
                    <form.Subscribe selector={(state) => state.isSubmitting}>
                        {#snippet children(isSubmitting)}
                            <Button class="w-full" disabled={isSubmitting} size="lg" type="submit">
                                {#if isSubmitting}
                                    <Spinner data-icon="inline-start" /> Setting things up…
                                {:else}
                                    Continue
                                {/if}
                            </Button>
                        {/snippet}
                    </form.Subscribe>
                </Field.Group>
            </form>
        </Card.Content>
    </Card.Root>
</div>

{#if projectConfigureCheckpoint}
    {#key projectConfigureCheckpoint.checkpointName}
        <ProductTourSpotlight
            checkpoint={projectConfigureCheckpoint}
            showProgress={false}
            description={projectConfigureCheckpoint.checkpointName === 'organization-name'
                ? 'Your organization holds your team and billing. Keep the suggestion or change it.'
                : 'Give your app a name. It becomes your first project.'}
            onDismiss={tourActions.dismiss}
            onPrevious={projectConfigureCheckpoint.checkpointName === 'organization-name'
                ? (checkpoint) => {
                      productTourCheckpoint.advance(checkpoint, 'project-name');
                  }
                : undefined}
            onNext={projectConfigureCheckpoint.checkpointName === 'project-name'
                ? (checkpoint) => {
                      productTourCheckpoint.advance(checkpoint, 'organization-name');
                  }
                : undefined}
            side="top"
            target={projectConfigureCheckpoint.checkpointName === 'organization-name' ? "[data-tour='setup-organization-name']" : "[data-tour='project-name']"}
            title={projectConfigureCheckpoint.checkpointName === 'organization-name' ? 'Name your organization' : 'Name your first project'}
        />
    {/key}
{/if}
