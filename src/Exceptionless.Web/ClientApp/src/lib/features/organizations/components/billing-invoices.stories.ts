import type { InvoiceGridModel } from '$features/organizations/models';
import type { Meta, StoryObj } from '@storybook/sveltekit';

import BillingInvoices from './billing-invoices.svelte';

const invoices: InvoiceGridModel[] = [
    {
        date: '2026-07-01T14:32:00Z',
        id: '671f17bb3d274d1f38a5c201',
        paid: true,
        status: 'paid',
        total: 199
    },
    {
        date: '2026-06-01T14:29:00Z',
        id: '665dbff6bc16d969f98f44c2',
        paid: true,
        status: 'paid',
        total: 199
    },
    {
        date: '2026-05-01T14:26:00Z',
        id: '6632d98891c861130ca2b4f5',
        paid: false,
        status: 'open',
        total: 199
    }
];

const meta = {
    component: BillingInvoices,
    parameters: {
        layout: 'padded'
    },
    tags: ['autodocs'],
    title: 'Features/Organizations/BillingInvoices'
} satisfies Meta<typeof BillingInvoices>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Populated: Story = {
    args: {
        invoices
    }
};

export const Empty: Story = {
    args: {
        invoices: []
    }
};

export const Loading: Story = {
    args: {
        isLoading: true
    }
};

export const Error: Story = {
    args: {
        hasError: true
    }
};
