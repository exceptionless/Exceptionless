import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import BillingInvoices from './billing-invoices.svelte';

const paidInvoiceId = '671f17bb3d274d1f38a5c201';
const invoices = [
    {
        date: '2026-07-01T14:32:00Z',
        id: paidInvoiceId,
        paid: true,
        status: 'paid',
        total: 120
    },
    {
        date: '2026-06-01T14:29:00Z',
        id: '665dbff6bc16d969f98f44c2',
        paid: false,
        status: 'open',
        total: 90
    }
];

describe('BillingInvoices', () => {
    it('renders invoice statuses and opens a selected invoice', async () => {
        const onopeninvoice = vi.fn();

        render(BillingInvoices, { invoices, onopeninvoice });

        expect(screen.getByText('Paid')).toBeTruthy();
        expect(screen.getByText('Payment due')).toBeTruthy();

        await fireEvent.click(screen.getByText(paidInvoiceId));

        expect(onopeninvoice).toHaveBeenCalledWith(paidInvoiceId);

        await fireEvent.click(screen.getAllByRole('button', { name: 'Actions' })[0]!);
        await fireEvent.click(await screen.findByText('View Payment'));

        expect(onopeninvoice).toHaveBeenNthCalledWith(2, paidInvoiceId);
    });

    it('renders the empty state', () => {
        render(BillingInvoices);

        expect(screen.getByText('No invoices were found.')).toBeTruthy();
    });

    it('renders the loading state', () => {
        render(BillingInvoices, { isLoading: true });

        expect(screen.getByRole('status', { name: 'Loading invoices' })).toBeTruthy();
    });

    it('renders the error state', () => {
        render(BillingInvoices, { hasError: true });

        expect(screen.getByText('Unable to load invoice data.')).toBeTruthy();
    });
});
