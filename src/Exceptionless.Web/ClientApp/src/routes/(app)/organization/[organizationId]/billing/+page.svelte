<script lang="ts">
    import type { InvoiceGridModel } from '$features/organizations/models';

    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import { A, Muted } from '$comp/typography';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Textarea } from '$comp/ui/textarea';
    import { env } from '$env/dynamic/public';
    import { ChangePlanDialog } from '$features/billing';
    import { deleteOrganizationDataMutation, getInvoicesQuery, getOrganizationQuery, postOrganizationDataMutation } from '$features/organizations/api.svelte';
    import {
        createSerializedBillingInformationSave,
        getOrganizationBillingInformation,
        getOrganizationBillingInformationChanges,
        saveOrganizationBillingInformationChanges
    } from '$features/organizations/billing-information';
    import BillingInvoices from '$features/organizations/components/billing-invoices.svelte';
    import { type OrganizationBillingInformationFormData, OrganizationBillingInformationSchema } from '$features/organizations/schemas';
    import { ariaInvalid, getFormErrorMessages, getProblemMessage, mapFieldErrors } from '$features/shared/validation';
    import GlobalUser from '$features/users/components/global-user.svelte';
    import CreditCard from '@lucide/svelte/icons/credit-card';
    import { createForm } from '@tanstack/svelte-form';
    import { queryParamsState } from 'kit-query-params';
    import { onDestroy } from 'svelte';
    import { toast } from 'svelte-sonner';
    import { debounce } from 'throttle-debounce';

    const organizationId = $derived(page.params.organizationId || '');
    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organizationId;
            }
        }
    });

    const invoicesQuery = getInvoicesQuery({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    const updateOrganizationData = postOrganizationDataMutation();
    const removeOrganizationData = deleteOrganizationDataMutation();

    const canChangePlan = $derived(organizationQuery.isSuccess && !!env.PUBLIC_STRIPE_PUBLISHABLE_KEY);
    const billingInformation = $derived(getOrganizationBillingInformation(organizationQuery.data));

    const params = queryParamsState({
        default: { changePlan: false },
        pushHistory: true,
        schema: { changePlan: 'boolean' }
    });

    let changePlanDialogOpen = $state(!!params.changePlan);
    let initializedOrganizationId = $state<string>();
    let toastId = $state<number | string>();

    const form = createForm(() => ({
        defaultValues: { ...billingInformation } as OrganizationBillingInformationFormData,
        validators: {
            onSubmit: OrganizationBillingInformationSchema,
            onSubmitAsync: async ({ value }) => {
                const targetOrganizationId = organizationId;
                if (!targetOrganizationId) {
                    return { form: 'Organization ID is required.' };
                }

                const changes = getOrganizationBillingInformationChanges(getOrganizationBillingInformation(organizationQuery.data), value);
                if (changes.length === 0) {
                    return null;
                }

                toast.dismiss(toastId);

                try {
                    await saveOrganizationBillingInformationChanges(changes, {
                        remove: (key) => removeOrganizationData.mutateAsync({ key, organizationId: targetOrganizationId }),
                        set: (key, value) => updateOrganizationData.mutateAsync({ key, organizationId: targetOrganizationId, value })
                    });

                    if (targetOrganizationId === organizationId) {
                        toastId = toast.success('Successfully updated billing information.');
                    }

                    return null;
                } catch (error: unknown) {
                    const message = getProblemMessage(error, 'Please try again.');
                    if (targetOrganizationId === organizationId) {
                        toastId = toast.error(`Error saving billing information. ${message}`);
                    }

                    return targetOrganizationId === organizationId ? { form: `Error saving billing information. ${message}` } : null;
                }
            }
        }
    }));

    let destroyed = false;
    const submitBillingInformationForm = createSerializedBillingInformationSave(async (targetOrganizationId) => {
        if (!destroyed && targetOrganizationId === organizationId) {
            await form.handleSubmit();
        }
    });

    const debouncedFormSubmit = debounce(1000, (targetOrganizationId: string) => {
        void submitBillingInformationForm(targetOrganizationId);
    });

    $effect(() => {
        if (organizationQuery.isSuccess && initializedOrganizationId !== organizationId) {
            debouncedFormSubmit.cancel({ upcomingOnly: true });
            form.reset(getOrganizationBillingInformation(organizationQuery.data));
            initializedOrganizationId = organizationId;
        }
    });

    onDestroy(() => {
        destroyed = true;
        debouncedFormSubmit.cancel();
    });

    function handleChangePlan() {
        changePlanDialogOpen = true;
        params.changePlan = true;
    }

    function handleChangePlanClose() {
        changePlanDialogOpen = false;
        params.changePlan = false;
    }

    function handleOpenInvoice(invoiceId: string) {
        window.open(resolve('/(app)/payment/[id]', { id: invoiceId }), '_blank');
    }

    function handleViewStripeInvoice(invoiceId: string) {
        window.open(`https://manage.stripe.com/invoices/in_${encodeURIComponent(invoiceId)}`, '_blank');
    }
</script>

{#snippet stripeInvoiceAction(invoice: InvoiceGridModel)}
    <GlobalUser>
        <DropdownMenu.Item onclick={() => handleViewStripeInvoice(invoice.id)}>
            <CreditCard class="mr-2 size-4" />
            View Stripe Invoice
        </DropdownMenu.Item>
    </GlobalUser>
{/snippet}

<div class="flex flex-col gap-6">
    <Muted>Billing information and invoices</Muted>

    {#if organizationQuery.isLoading}
        <div class="flex flex-col gap-4">
            <Skeleton class="h-12 w-3/4" />
            <Skeleton class="h-50 w-full" />
        </div>
    {:else if organizationQuery.error}
        <ErrorMessage message="Unable to load organization data." />
    {:else}
        <div class="flex flex-col gap-6">
            <form
                onsubmit={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    void submitBillingInformationForm(organizationId);
                }}
            >
                <form.Subscribe selector={(state) => state.errors}>
                    {#snippet children(errors)}
                        <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                    {/snippet}
                </form.Subscribe>

                <Field.Group class="grid gap-5 md:grid-cols-2">
                    <form.Field name="name">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)}>
                                <Field.Label for={field.name}>Billing name</Field.Label>
                                <Input
                                    id={field.name}
                                    name={field.name}
                                    type="text"
                                    placeholder="Acme, Inc."
                                    value={field.state.value}
                                    onblur={field.handleBlur}
                                    oninput={(e) => {
                                        field.handleChange(e.currentTarget.value);
                                        debouncedFormSubmit(organizationId);
                                    }}
                                    aria-invalid={ariaInvalid(field)}
                                />
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>

                    <form.Field name="vatId">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)}>
                                <Field.Label for={field.name}>VAT ID</Field.Label>
                                <Input
                                    id={field.name}
                                    name={field.name}
                                    type="text"
                                    placeholder="DE123456789"
                                    value={field.state.value}
                                    onblur={field.handleBlur}
                                    oninput={(e) => {
                                        field.handleChange(e.currentTarget.value);
                                        debouncedFormSubmit(organizationId);
                                    }}
                                    aria-invalid={ariaInvalid(field)}
                                />
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>

                    <form.Field name="address">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)} class="md:col-span-2">
                                <Field.Label for={field.name}>Billing address</Field.Label>
                                <Textarea
                                    id={field.name}
                                    name={field.name}
                                    rows={4}
                                    placeholder="123 Main Street&#10;Anytown, ST 12345&#10;United States"
                                    value={field.state.value}
                                    onblur={field.handleBlur}
                                    oninput={(e) => {
                                        field.handleChange(e.currentTarget.value);
                                        debouncedFormSubmit(organizationId);
                                    }}
                                    aria-invalid={ariaInvalid(field)}
                                />
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>

                    <form.Field name="vatNumber">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)}>
                                <Field.Label for={field.name}>VAT number</Field.Label>
                                <Input
                                    id={field.name}
                                    name={field.name}
                                    type="text"
                                    placeholder="123456789"
                                    value={field.state.value}
                                    onblur={field.handleBlur}
                                    oninput={(e) => {
                                        field.handleChange(e.currentTarget.value);
                                        debouncedFormSubmit(organizationId);
                                    }}
                                    aria-invalid={ariaInvalid(field)}
                                />
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>
                </Field.Group>
            </form>

            <p>
                You are currently on the
                {#if canChangePlan}
                    <A onclick={handleChangePlan}>
                        <strong>{organizationQuery.data?.plan_name}</strong> plan
                    </A>
                {:else}
                    <strong>{organizationQuery.data?.plan_name}</strong> plan
                {/if}.
                {#if canChangePlan}
                    <A onclick={handleChangePlan}>Change your plan or billing information.</A>
                {/if}
            </p>

            <BillingInvoices
                hasError={!!invoicesQuery.error}
                invoices={invoicesQuery.data?.data ?? undefined}
                isLoading={invoicesQuery.isLoading}
                onopeninvoice={handleOpenInvoice}
                {stripeInvoiceAction}
            />
        </div>
    {/if}
</div>

{#if changePlanDialogOpen && organizationQuery.data}
    <ChangePlanDialog onclose={handleChangePlanClose} organization={organizationQuery.data} />
{/if}
