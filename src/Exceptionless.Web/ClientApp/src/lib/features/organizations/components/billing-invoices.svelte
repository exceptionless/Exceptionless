<script lang="ts">
    import type { Snippet } from 'svelte';

    import ErrorMessage from '$comp/error-message.svelte';
    import DateTime from '$comp/formatters/date-time.svelte';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Table from '$comp/ui/table';
    import File from '@lucide/svelte/icons/file';
    import MoreHorizontal from '@lucide/svelte/icons/more-horizontal';

    import type { InvoiceGridModel } from '../models';

    interface Props {
        hasError?: boolean;
        invoices?: InvoiceGridModel[];
        isLoading?: boolean;
        onopeninvoice?: (invoiceId: string) => void;
        stripeInvoiceAction?: Snippet<[InvoiceGridModel]>;
    }

    let { hasError = false, invoices = [], isLoading = false, onopeninvoice = () => {}, stripeInvoiceAction }: Props = $props();
</script>

{#if isLoading}
    <div class="flex flex-col gap-2" role="status" aria-label="Loading invoices">
        <Skeleton class="h-8 w-full" />
        <Skeleton class="h-8 w-full" />
        <Skeleton class="h-8 w-full" />
    </div>
{:else if hasError}
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
                {#if invoices.length > 0}
                    {#each invoices as invoice (invoice.id)}
                        <Table.Row>
                            <Table.Cell class="hover:bg-muted/50 cursor-pointer" onclick={() => onopeninvoice(invoice.id)}>
                                {invoice.id}
                            </Table.Cell>
                            <Table.Cell class="hover:bg-muted/50 cursor-pointer" onclick={() => onopeninvoice(invoice.id)}>
                                <DateTime value={invoice.date} />
                            </Table.Cell>
                            <Table.Cell class="hover:bg-muted/50 cursor-pointer" onclick={() => onopeninvoice(invoice.id)}>
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
                                        <DropdownMenu.Item onclick={() => onopeninvoice(invoice.id)}>
                                            <File class="mr-2 size-4" />
                                            View Payment
                                        </DropdownMenu.Item>
                                        {@render stripeInvoiceAction?.(invoice)}
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
