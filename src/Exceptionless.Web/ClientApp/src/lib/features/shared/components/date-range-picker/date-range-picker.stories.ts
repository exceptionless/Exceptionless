import type { Meta, StoryObj } from '@storybook/sveltekit';

import DateRangePicker from './date-range-picker.svelte';

const meta = {
    args: {},
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
        value: '[now-1d TO now]'
    },
    parameters: {
        docs: {
            description: {
                story: 'Quick ranges apply with one click. Custom range opens the From and To fields and the calendar, which apply with the Apply button.'
            }
        }
    }
};

export const AbsoluteRange: Story = {
    args: {
        value: '[2025-01-10 TO 2025-01-12]'
    },
    parameters: {
        docs: {
            description: {
                story: 'Absolute ranges open in the From and To fields with the range highlighted on the calendar. Each part of the date and time can be edited with the keyboard.'
            }
        }
    }
};

export const AbsoluteRangeWithTimes: Story = {
    args: {
        value: '[2025-01-01T09:30 TO 2025-01-02T17:00]'
    }
};

export const WholeMonth: Story = {
    args: {
        value: '[2025-01 TO 2025-01]'
    },
    parameters: {
        docs: {
            description: {
                story: 'Calendar expressions resolve to the whole month in the absolute fields. The Relative tab keeps the original expression.'
            }
        }
    }
};

export const RelativeExpression: Story = {
    args: {
        value: '[now-2h/h TO now]'
    },
    parameters: {
        docs: {
            description: {
                story: 'Custom relative expressions open in the Relative tab so they are not converted to fixed dates.'
            }
        }
    }
};

export const Empty: Story = {
    args: {
        value: ''
    }
};
