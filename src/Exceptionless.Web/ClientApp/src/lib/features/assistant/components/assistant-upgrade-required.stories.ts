import type { Meta, StoryObj } from '@storybook/sveltekit';

import AssistantUpgradeRequired from './assistant-upgrade-required.svelte';

const meta = {
    component: AssistantUpgradeRequired,
    parameters: {
        layout: 'centered'
    },
    tags: ['autodocs'],
    title: 'Features/Assistant/AccessState'
} satisfies Meta<typeof AssistantUpgradeRequired>;

export default meta;

type Story = StoryObj<typeof meta>;

export const UpgradeRequired: Story = {
    args: {
        accessState: 'upgrade-required',
        message: 'Exie is available on Medium plans and higher.',
        minimumPlanId: 'EX_MEDIUM',
        organizationId: 'organization-id'
    }
};

export const Loading: Story = {
    args: {
        accessState: 'loading'
    }
};

export const Error: Story = {
    args: {
        accessState: 'error',
        message: 'We couldn’t load this organization’s assistant access.',
        onRetry: async () => undefined
    }
};

export const Disabled: Story = {
    args: {
        accessState: 'disabled',
        message: 'Select an organization to use Exie.'
    }
};
