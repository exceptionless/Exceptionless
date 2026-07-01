<script lang="ts">
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import DateTime from '$comp/formatters/date-time.svelte';
    import { A, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Table from '$comp/ui/table';
    import { Textarea } from '$comp/ui/textarea';
    import { env } from '$env/dynamic/public';
    import { ChangePlanDialog } from '$features/billing';
    import { deleteOrganizationData, getInvoicesQuery, getOrganizationQuery, postOrganizationData } from '$features/organizations/api.svelte';
    import {
        getOrganizationBillingInformation,
        normalizeOrganizationBillingInformationValue,
        organizationBillingInformationDataKeys
    } from '$features/organizations/billing-information';
    import { type OrganizationBillingInformationFormData, OrganizationBillingInformationSchema } from '$features/organizations/schemas';
    import { ariaInvalid, getFormErrorMessages, getProblemMessage, mapFieldErrors } from '$features/shared/validation';
    import GlobalUser from '$features/users/components/global-user.svelte';
    import CreditCard from '@lucide/svelte/icons/credit-card';
    import File from '@lucide/svelte/icons/file';
    import MoreHorizontal from '@lucide/svelte/icons/more-horizontal';
    import { createForm } from '@tanstack/svelte-form';
    import { queryParamsState } from 'kit-query-params';
    import { onDestroy } from 'svelte';
    import { toast } from 'svelte-sonner';
    import { debounce } from 'throttle-debounce';

    const billingInformationFields = [
        { key: organizationBillingInformationDataKeys.name, name: 'name' },
        { key: organizationBillingInformationDataKeys.address, name: 'address' },
        { key: organizationBillingInformationDataKeys.vatNumber, name: 'vatNumber' },
        { key: organizationBillingInformationDataKeys.vatId, name: 'vatId' }
    ] as const;

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

    const updateOrganizationData = postOrganizationData({
        route: {
            get id() {
                return organizationId;
            }
        }
    });

    const removeOrganizationData = deleteOrganizationData({
        route: {
            get id() {
                return organizationId;
            }
        }
    });

    const canChangePlan = $derived(organizationQuery.isSuccess && !!env.PUBLIC_STRIPE_PUBLISHABLE_KEY);
    const billingInformation = $derived(getOrganizationBillingInformation(organizationQuery.data));

    const params = queryParamsState({
        default: { changePlan: false },
        pushHistory: true,
        schema: { changePlan: 'boolean' }
    });

    let changePlanDialogOpen = $state(!!params.changePlan);
    let toastId = $state<number | string>();

    const form = createForm(() => ({
        defaultValues: { ...billingInformation } as OrganizationBillingInformationFormData,
        validators: {
            onSubmit: OrganizationBillingInformationSchema,
            onSubmitAsync: async ({ value }) => {
                if (!organizationId) {
                    return { form: 'Organization ID is required.' };
                }

                const currentBillingInformation = getOrganizationBillingInformation(organizationQuery.data);
                const changedFields = billingInformationFields.filter((field) => value[field.name] !== currentBillingInformation[field.name]);
                if (changedFields.length === 0) {
                    return null;
                }

                toast.dismiss(toastId);

                try {
                    await Promise.all(
                        changedFields.map((field) => {
                            const normalizedValue = normalizeOrganizationBillingInformationValue(value[field.name]);
                            if (normalizedValue) {
                                return updateOrganizationData.mutateAsync({ key: field.key, value: normalizedValue });
                            }

                            return removeOrganizationData.mutateAsync({ key: field.key });
                        })
                    );

                    toastId = toast.success('Successfully updated billing information.');
                    return null;
                } catch (error: unknown) {
                    const message = getProblemMessage(error, 'Please try again.');
                    toastId = toast.error(`Error saving billing information. ${message}`);
                    return { form: `Error saving billing information. ${message}` };
                }
            }
        }
    }));

    const debouncedFormSubmit = debounce(1000, (targetOrganizationId: string) => {
        if (targetOrganizationId === organizationId) {
            void form.handleSubmit();
        }
    });

    onDestroy(() => debouncedFormSubmit.cancel());

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
                    void form.handleSubmit();
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

            {#if invoicesQuery.isLoading}
                <div class="flex flex-col gap-2">
                    <Skeleton class="h-8 w-full" />
                    <Skeleton class="h-8 w-full" />
                    <Skeleton class="h-8 w-full" />
                </div>
            {:else if invoicesQuery.error}
                <ErrorMessage message="Unable to load invoice data." />
            {:else}
                <div class="overflow-hidden rounded-md border">
                    <Table.Root>
                        <Table.Header>
                            <Table.Row>
                                <Table.Head>Payment Number</Table.Head>
                                <Table.Head>Date</Table.Head>
                                <Table.Head>Status</Table.Head>
                                <Table.Head class="w-25">Actions</Table.Head>
                            </Table.Row>
                        </Table.Header>
                        <Table.Body>
                            {#if invoicesQuery.data?.data && invoicesQuery.data.data.length > 0}
                                {#each invoicesQuery.data.data as invoice (invoice.id)}
                                    <Table.Row>
                                        <Table.Cell class="hover:bg-muted/50 cursor-pointer" onclick={() => handleOpenInvoice(invoice.id)}>
                                            {invoice.id}
                                        </Table.Cell>
                                        <Table.Cell class="hover:bg-muted/50 cursor-pointer" onclick={() => handleOpenInvoice(invoice.id)}>
                                            <DateTime value={invoice.date} />
                                        </Table.Cell>
                                        <Table.Cell class="hover:bg-muted/50 cursor-pointer" onclick={() => handleOpenInvoice(invoice.id)}>
                                            {invoice.paid ? 'Paid' : 'Unpaid'}
                                        </Table.Cell>
                                        <Table.Cell>
                                            <DropdownMenu.Root>
                                                <DropdownMenu.Trigger>
                                                    {#snippet child({ props })}
                                                        <Button {...props} variant="outline" size="sm">
                                                            <MoreHorizontal class="size-4" />
                                                            <span class="sr-only">Actions</span>
                                                        </Button>
                                                    {/snippet}
                                                </DropdownMenu.Trigger>
                                                <DropdownMenu.Content align="end">
                                                    <DropdownMenu.Group>
                                                        <DropdownMenu.Item onclick={() => handleOpenInvoice(invoice.id)}>
                                                            <File class="mr-2 size-4" />
                                                            View Payment
                                                        </DropdownMenu.Item>
                                                        <GlobalUser>
                                                            <DropdownMenu.Item onclick={() => handleViewStripeInvoice(invoice.id)}>
                                                                <CreditCard class="mr-2 size-4" />
                                                                View Stripe Invoice
                                                            </DropdownMenu.Item>
                                                        </GlobalUser>
                                                    </DropdownMenu.Group>
                                                </DropdownMenu.Content>
                                            </DropdownMenu.Root>
                                        </Table.Cell>
                                    </Table.Row>
                                {/each}
                            {:else}
                                <Table.Row>
                                    <Table.Cell colspan={4} class="text-center">
                                        <strong>No invoices were found.</strong>
                                    </Table.Cell>
                                </Table.Row>
                            {/if}
                        </Table.Body>
                    </Table.Root>
                </div>
            {/if}
        </div>
    {/if}
</div>

{#if changePlanDialogOpen && organizationQuery.data}
    <ChangePlanDialog onclose={handleChangePlanClose} organization={organizationQuery.data} />
{/if}
