<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import DateTime from '$comp/formatters/date-time.svelte';
    import { A, H4 } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { Separator } from '$comp/ui/separator';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Table from '$comp/ui/table';
    import { env } from '$env/dynamic/public';
    import { getInvoicesQuery, getOrganizationQuery } from '$features/organizations/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import GlobalUser from '$features/users/components/global-user.svelte';
    import CreditCard from '@lucide/svelte/icons/credit-card';
    import File from '@lucide/svelte/icons/file';
    import MoreHorizontal from '@lucide/svelte/icons/more-horizontal';

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

    const canChangePlan = $derived(organizationQuery.isSuccess && !!env.PUBLIC_STRIPE_PUBLISHABLE_KEY);

    function handleChangePlan() {
        // Navigate to plan change page or open modal
        // This is a placeholder for future implementation
        console.log('Change plan clicked');
    }

    function handleOpenInvoice(invoiceId: string) {
        // Open invoice in new window
        window.open(`/next/payment/${invoiceId}`, '_blank');
    }

    function handleViewStripeInvoice(invoiceId: string) {
        // Open Stripe invoice in new window
        window.open(`https://manage.stripe.com/invoices/in_${invoiceId}`, '_blank');
    }
</script>

<div class="space-y-6">
    <div>
        <H4>Billing</H4>
    </div>
    <Separator />

    {#if organizationQuery.isLoading}
        <div class="space-y-4">
            <Skeleton class="h-12 w-3/4" />
            <Skeleton class="h-[200px] w-full" />
        </div>
    {:else if organizationQuery.error}
        <ErrorMessage message="Unable to load organization data." />
    {:else}
        <div class="space-y-6">
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
                                <Table.Head class="w-[100px]">Actions</Table.Head>
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
                                                    <Button variant="outline" size="sm">
                                                        <MoreHorizontal class="size-4" />
                                                        <span class="sr-only">Actions</span>
                                                    </Button>
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
