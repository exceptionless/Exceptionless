<script lang="ts">
    import { resolve } from '$app/paths';
    import ErrorMessage from '$comp/error-message.svelte';
    import DateTime from '$comp/formatters/date-time.svelte';
    import { A, H4, Large, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
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
    import { organization } from '$features/organizations/context.svelte';
    import GlobalUser from '$features/users/components/global-user.svelte';
    import CreditCard from '@lucide/svelte/icons/credit-card';
    import File from '@lucide/svelte/icons/file';
    import MoreHorizontal from '@lucide/svelte/icons/more-horizontal';
    import { queryParamsState } from 'kit-query-params';
    import { toast } from 'svelte-sonner';
    import { debounce } from 'throttle-debounce';

    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organization.current;
            }
        }
    });

    const invoicesQuery = getInvoicesQuery({
        route: {
            get organizationId() {
                return organization.current!;
            }
        }
    });

    const updateOrganizationData = postOrganizationData({
        route: {
            get id() {
                return organization.current;
            }
        }
    });

    const removeOrganizationData = deleteOrganizationData({
        route: {
            get id() {
                return organization.current;
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
    let currentToastId = $state<number | string>();
    let billingName = $state('');
    let billingAddress = $state('');
    let billingVatNumber = $state('');
    let billingVatId = $state('');

    const billingNameIsDirty = $derived(billingName !== billingInformation.name);
    const billingAddressIsDirty = $derived(billingAddress !== billingInformation.address);
    const billingVatNumberIsDirty = $derived(billingVatNumber !== billingInformation.vatNumber);
    const billingVatIdIsDirty = $derived(billingVatId !== billingInformation.vatId);

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

    async function updateOrRemoveOrganizationBillingInformation(key: string, value: string, label: string) {
        toast.dismiss(currentToastId);

        try {
            const normalizedValue = normalizeOrganizationBillingInformationValue(value);
            if (normalizedValue) {
                await updateOrganizationData.mutateAsync({ key, value: normalizedValue });
            } else {
                await removeOrganizationData.mutateAsync({ key });
            }

            currentToastId = toast.success(`Successfully updated ${label}.`);
        } catch {
            currentToastId = toast.error(`Error updating ${label}. Please try again.`);
        }
    }

    async function saveBillingName() {
        if (!billingNameIsDirty) {
            return;
        }

        await updateOrRemoveOrganizationBillingInformation(organizationBillingInformationDataKeys.name, billingName, 'billing name');
    }

    async function saveBillingAddress() {
        if (!billingAddressIsDirty) {
            return;
        }

        await updateOrRemoveOrganizationBillingInformation(organizationBillingInformationDataKeys.address, billingAddress, 'billing address');
    }

    async function saveBillingVatNumber() {
        if (!billingVatNumberIsDirty) {
            return;
        }

        await updateOrRemoveOrganizationBillingInformation(organizationBillingInformationDataKeys.vatNumber, billingVatNumber, 'VAT number');
    }

    async function saveBillingVatId() {
        if (!billingVatIdIsDirty) {
            return;
        }

        await updateOrRemoveOrganizationBillingInformation(organizationBillingInformationDataKeys.vatId, billingVatId, 'VAT ID');
    }

    const debouncedSaveBillingName = debounce(500, saveBillingName);
    const debouncedSaveBillingAddress = debounce(500, saveBillingAddress);
    const debouncedSaveBillingVatNumber = debounce(500, saveBillingVatNumber);
    const debouncedSaveBillingVatId = debounce(500, saveBillingVatId);

    $effect(() => {
        if (organizationQuery.dataUpdatedAt) {
            billingName = billingInformation.name;
            billingAddress = billingInformation.address;
            billingVatNumber = billingInformation.vatNumber;
            billingVatId = billingInformation.vatId;
        }
    });
</script>

<div class="space-y-6">
    {#if organizationQuery.isLoading}
        <div class="space-y-4">
            <Skeleton class="h-12 w-3/4" />
            <Skeleton class="h-50 w-full" />
        </div>
    {:else if organizationQuery.error}
        <ErrorMessage message="Unable to load organization data." />
    {:else}
        <div class="space-y-6">
            <section class="space-y-4">
                <div class="space-y-2">
                    <H4>Billing information</H4>
                    <Muted>Add the details that should appear on billing documents for this organization.</Muted>
                </div>

                <div class="grid gap-4 md:grid-cols-2">
                    <div class="space-y-2">
                        <Large>Organization name</Large>
                        <Input type="text" placeholder="Example: Acme, Inc." bind:value={billingName} oninput={debouncedSaveBillingName} />
                    </div>

                    <div class="space-y-2">
                        <Large>VAT ID</Large>
                        <Input type="text" placeholder="Example: DE123456789" bind:value={billingVatId} oninput={debouncedSaveBillingVatId} />
                    </div>

                    <div class="space-y-2 md:col-span-2">
                        <Large>Organization address</Large>
                        <Textarea
                            rows={4}
                            placeholder="Example: 123 Main Street&#10;Anytown, ST 12345&#10;United States"
                            bind:value={billingAddress}
                            oninput={debouncedSaveBillingAddress}
                        />
                    </div>

                    <div class="space-y-2">
                        <Large>VAT Number</Large>
                        <Input type="text" placeholder="Example: 123456789" bind:value={billingVatNumber} oninput={debouncedSaveBillingVatNumber} />
                    </div>
                </div>
            </section>

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
                <div class="space-y-2">
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
