<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';
    import { expect, waitFor } from 'storybook/test';

    import { createUsageChartData } from './usage-chart.fixtures.ts';
    import UsageChart from './usage-chart.svelte';

    async function checkConstantLimit({ canvasElement }: { canvasElement: HTMLElement }) {
        // A constant allowance must stay horizontal as all usage counters vary.
        await waitFor(() => {
            const limit = canvasElement.querySelector<SVGPathElement>('path.usage-limit-line:not(.lc-area-path)');
            expect(limit).not.toBeNull();
            expect(limit!.getTotalLength()).toBeGreaterThan(100);
            expect(limit!.getBBox().height).toBeLessThan(0.1);
            const chart = canvasElement.querySelector('[data-slot="chart"]')!.getBoundingClientRect();
            const line = limit!.getBoundingClientRect();
            expect(line.top).toBeGreaterThanOrEqual(chart.top - 1);
            expect(line.bottom).toBeLessThanOrEqual(chart.bottom + 1);
        });
    }

    const { Story } = defineMeta({
        component: UsageChart,
        parameters: {
            layout: 'padded'
        },
        tags: ['autodocs'],
        title: 'Components/Organizations/UsageChart'
    });
</script>

<Story
    name="Organization"
    args={{
        data: createUsageChartData()
    }}
    play={checkConstantLimit}
/>
<Story
    name="Project"
    args={{
        data: createUsageChartData(912),
        showOrganizationTotal: true
    }}
    play={checkConstantLimit}
/>
<Story
    name="Empty"
    args={{
        data: []
    }}
/>
<Story
    name="Changing Limits"
    args={{
        data: createUsageChartData().map((point, index) => ({
            ...point,
            limit: index < 4 ? 2_000_000 : 4_000_000
        }))
    }}
/>
