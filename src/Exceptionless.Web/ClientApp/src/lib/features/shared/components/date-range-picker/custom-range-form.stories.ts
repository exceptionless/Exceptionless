import type { CustomDateRange } from '$features/shared/models';
import type { Meta, StoryObj } from '@storybook/sveltekit';

import CustomRangeForm from './custom-range-form.svelte';

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
