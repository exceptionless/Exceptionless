import type { Meta, StoryObj } from '@storybook/sveltekit';

import CustomRangeForm from './custom-range-form.svelte';
import type { CustomDateRange } from "$features/shared/models";

const meta = {
    args: {
        apply: (range: CustomDateRange) => {
            console.log('Applied range:', range);
            alert(`Applied range: ${range.start} TO ${range.end}`);
        }
    },
    argTypes: {
        range: { control: 'object' }
    },
    component: CustomRangeForm,
    tags: ['autodocs'],
    title: 'Components/Shared/DateRangePicker/CustomRangeForm'
} satisfies Meta<typeof CustomRangeForm>;

export default meta;

type Story = StoryObj<typeof meta>;

export const DefaultValues: Story = {};

export const WithRelativeRange: Story = {
    args: {
        range: {
            end: 'now',
            start: 'now-12h'
        }
    }
};

export const WithAbsoluteRange: Story = {
    args: {
        range: {
            end: '2025-01-02T00:00:00',
            start: '2025-01-01T00:00:00'
        }
    }
};

export const WithEmptyRange: Story = {
    args: {
        range: null
    }
};

export const InvalidDateFormatError: Story = {
    args: {
        range: {
            end: 'now',
            start: 'invalid-date-format'
        }
    },
    name: 'Validation Error - Invalid Date Format'
};

export const InvalidMonthError: Story = {
    args: {
        range: {
            end: 'now',
            start: '2025-13-01T00:00:00' // Invalid month (13)
        }
    },
    name: 'Validation Error - Invalid Month'
};

export const InvalidDayError: Story = {
    args: {
        range: {
            end: 'now',
            start: '2025-01-32T00:00:00' // Invalid day (32)
        }
    },
    name: 'Validation Error - Invalid Day'
};

export const InvalidHourError: Story = {
    args: {
        range: {
            end: 'now',
            start: '2025-01-01T25:00:00' // Invalid hour (25)
        }
    },
    name: 'Validation Error - Invalid Hour'
};

export const InvalidMinuteError: Story = {
    args: {
        range: {
            end: 'now',
            start: '2025-01-01T00:60:00' // Invalid minute (60)
        }
    },
    name: 'Validation Error - Invalid Minute'
};

export const InvalidTimeExpressionError: Story = {
    args: {
        range: {
            end: 'now',
            start: 'now-invalid'
        }
    },
    name: 'Validation Error - Invalid Time Expression'
};

export const InvalidNumberError: Story = {
    args: {
        range: {
            end: 'now',
            start: 'now-abc' // Invalid number in offset
        }
    },
    name: 'Validation Error - Invalid Number'
};

export const UnknownTimeUnitError: Story = {
    args: {
        range: {
            end: 'now',
            start: 'now-5z' // Unknown time unit 'z'
        }
    },
    name: 'Validation Error - Unknown Time Unit'
};

export const InvalidRangeFormatError: Story = {
    args: {
        range: {
            end: 'now',
            start: 'now-1h TO now-2h TO now' // Invalid range format
        }
    },
    name: 'Validation Error - Invalid Range Format'
};

export const InvalidDateError: Story = {
    args: {
        range: {
            end: 'now',
            start: '2025-02-30T00:00:00' // February 30th doesn't exist
        }
    },
    name: 'Validation Error - Invalid Date (Feb 30th)'
};
