import type { Meta, StoryObj } from '@storybook/sveltekit';

import DateRangePicker from './date-range-picker.svelte';
import { quickRanges } from './quick-ranges';

const meta = {
    args: {
        quickRanges
    },
    argTypes: {},
    component: DateRangePicker,
    parameters: {
        layout: 'centered'
    },
    tags: ['autodocs'],
    title: 'Components/Shared/DateRangePicker'
} satisfies Meta<typeof DateRangePicker>;

export default meta;

type Story = StoryObj<typeof meta>;

export const DefaultQuickRanges: Story = {
    args: {
        value: 'last 24 hours'
    }
};

export const CustomQuickRanges: Story = {
    args: {
        quickRanges,
        value: 'previous week'
    },
    name: 'Custom Range'
};

export const WithSelectedRange: Story = {
    args: {
        value: '[now-1d TO now]'
    }
};

export const ShowingCustomForm: Story = {
    args: {
        value: '[now-1d TO now]'
    },
    parameters: {
        docs: {
            description: {
                story: 'This story demonstrates the calendar time picker with a selected quick range. Switch to the Custom tab to edit a custom range.'
            }
        }
    }
};

export const AutoSelectCustomTab: Story = {
    args: {
        // A value unlikely to match a predefined quick range forces the custom tab active
        value: '2025-01-01T00:00:00 TO 2025-01-02T00:00:00'
    },
    name: 'Auto Selects Custom Tab (non quick value)',
    parameters: {
        docs: {
            description: {
                story: 'When the current value does not match any quick range option, the Custom tab is automatically selected.'
            }
        }
    }
};

export const AutoSelectQuickTab: Story = {
    args: {
        value: '[now-1d TO now]'
    },
    name: 'Auto Selects Quick Tab (matching value)',
    parameters: {
        docs: {
            description: {
                story: 'When the value matches a quick range option exactly, the Quick Range tab is selected and the item highlighted.'
            }
        }
    }
};

export const CommandSearchFiltering: Story = {
    args: {
        value: '[now-5m TO now]'
    },
    name: 'Command Palette Search',
    parameters: {
        docs: {
            description: {
                story: 'Demonstrates the command palette based quick range filtering UX.'
            }
        }
    }
};
