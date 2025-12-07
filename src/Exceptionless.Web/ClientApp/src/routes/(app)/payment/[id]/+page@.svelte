<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import DateTime from '$comp/formatters/date-time.svelte';
    import Logo from '$comp/logo.svelte';
    import { A, H4 } from '$comp/typography';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Table from '$comp/ui/table';
    import { accessToken } from '$features/auth/index.svelte';
    import { getInvoiceQuery } from '$features/organizations/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import Currency from '$features/shared/components/formatters/currency.svelte';
    import File from '@lucide/svelte/icons/file';

    const invoiceId = $derived(page.params.id || '');

    const invoiceQuery = getInvoiceQuery({
        route: {
            get id() {
                return invoiceId;
            }
        }
    });

    $effect(() => {
        if (!accessToken.current || !organization.current || invoiceQuery.isError) {
            goto(resolve('/(app)'));
        }

        if (invoiceQuery.isSuccess && invoiceQuery.data?.organization_id !== organization.current) {
            goto(resolve('/(app)'));
        }
    });
</script>

<svelte:head>
    <title>Payment - Exceptionless</title>
</svelte:head>

{#if invoiceQuery.isLoading}
    <div class="space-y-6">
        <div class="grid grid-cols-1 gap-6 md:grid-cols-2">
            <div class="space-y-4">
                <Skeleton class="h-20 w-32" />
                <Skeleton class="h-24 w-full" />
            </div>
            <div>
                <Skeleton class="h-48 w-full" />
            </div>
        </div>
        <Skeleton class="h-64 w-full" />
    </div>
{:else if invoiceQuery.error}
    <ErrorMessage message="Unable to load invoice. The invoice may not exist or you may not have permission to view it." />
{:else if invoiceQuery.data}
    <div class="container mx-auto max-w-4xl space-y-8 py-8">
        <div class="grid grid-cols-1 gap-8 md:grid-cols-2">
            <div class="space-y-4">
                <div class="flex flex-col items-start space-y-4">
                    <Logo class="mx-0 h-16" />
                    <address class="text-muted-foreground text-sm not-italic">
                        <strong class="text-foreground">Exceptionless</strong><br />
                        5250 Hwy 78, Suite 750-324<br />
                        Sachse, TX 75048<br />
                    </address>
                </div>
            </div>

            <div class="bg-card rounded-lg border">
                <Table.Root>
                    <Table.Body>
                        <Table.Row>
                            <Table.Head class="font-semibold">Organization</Table.Head>
                            <Table.Cell>{invoiceQuery.data.organization_name}</Table.Cell>
                        </Table.Row>
                        <Table.Row>
                            <Table.Head class="font-semibold">Invoice #</Table.Head>
                            <Table.Cell>{invoiceQuery.data.id}</Table.Cell>
                        </Table.Row>
                        <Table.Row>
                            <Table.Head class="font-semibold">Invoice Date</Table.Head>
                            <Table.Cell><DateTime value={invoiceQuery.data.date} /></Table.Cell>
                        </Table.Row>
                        <Table.Row>
                            <Table.Head class="font-semibold">Status</Table.Head>
                            <Table.Cell>
                                <span class={invoiceQuery.data.paid ? 'font-semibold' : 'text-destructive font-semibold'}>
                                    {invoiceQuery.data.paid ? 'Paid' : 'Unpaid'}
                                </span>
                            </Table.Cell>
                        </Table.Row>
                    </Table.Body>
                </Table.Root>
            </div>
        </div>

        <div class="bg-card rounded-lg border">
            <div class="bg-muted/50 border-b p-4">
                <div class="flex items-center gap-2">
                    <File class="size-4" />
                    <H4 class="mb-0">{invoiceQuery.data.paid ? 'Receipt' : 'Invoice'}</H4>
                </div>
            </div>

            <Table.Root>
                <Table.Header>
                    <Table.Row>
                        <Table.Head>Description</Table.Head>
                        <Table.Head>Date</Table.Head>
                        <Table.Head class="text-right">Amount</Table.Head>
                    </Table.Row>
                </Table.Header>
                <Table.Body>
                    {#each invoiceQuery.data.items as item, index (index)}
                        <Table.Row>
                            <Table.Cell>{item.description}</Table.Cell>
                            <Table.Cell>{item.date || ''}</Table.Cell>
                            <Table.Cell class="text-right font-bold">
                                <Currency value={item.amount} />
                            </Table.Cell>
                        </Table.Row>
                    {/each}
                    <Table.Row class="border-t-2">
                        <Table.Cell></Table.Cell>
                        <Table.Cell class="font-semibold">Total</Table.Cell>
                        <Table.Cell class="text-right font-bold">
                            <Currency value={invoiceQuery.data.total} />
                        </Table.Cell>
                    </Table.Row>
                </Table.Body>
            </Table.Root>
        </div>

        <div class="text-muted-foreground grid grid-cols-1 gap-6 text-sm md:grid-cols-2">
            <div>
                <strong class="text-foreground">Email:</strong>
                <A href="mailto:sales@exceptionless.com">sales@exceptionless.com</A>
            </div>
            <div>
                <strong class="text-foreground">Website:</strong>
                <A href="https://exceptionless.com" target="_blank">https://exceptionless.com</A>
            </div>
        </div>
    </div>
{/if}
