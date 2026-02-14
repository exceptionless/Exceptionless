export interface QuickRangeOption {
    description?: string;
    label: string;
    value: string;
}

export interface QuickRangeSection {
    label: string;
    options: QuickRangeOption[];
}

export type QuickRangeTuple = readonly [string, string, string];

export const quickRanges: QuickRangeSection[] = [
    {
        label: 'Recent',
        options: [
            { label: 'Last 5 minutes', value: '[now-5m TO now]' },
            { label: 'Last 15 minutes', value: '[now-15m TO now]' },
            { label: 'Last 1 hour', value: '[now-1h TO now]' },
            { label: 'Last 3 hours', value: '[now-3h TO now]' },
            { label: 'Last 6 hours', value: '[now-6h TO now]' }
        ]
    },
    {
        label: 'Today/This week',
        options: [
            { label: 'Last 24 hours', value: '[now-1d TO now]' },
            { label: 'Today so far', value: '[now/d TO now]' },
            { label: 'Last 7 days', value: '[now-7d TO now]' },
            { label: 'This week so far', value: '[now/w TO now]' }
        ]
    },
    {
        label: 'Longer periods',
        options: [
            { label: 'Last 30 days', value: '[now-30d TO now]' },
            { label: 'Last 90 days', value: '[now-90d TO now]' },
            { label: 'This month so far', value: '[now/M TO now]' },
            { label: 'This year so far', value: '[now/y TO now]' }
        ]
    },
    {
        label: 'Complete periods',
        options: [
            { label: 'Previous day', value: '[now-1d/d TO now/d}' },
            { label: 'Previous week', value: '[now-1w/w TO now/w}' },
            { label: 'Previous month', value: '[now-1M/M TO now/M}' }
        ]
    }
];
