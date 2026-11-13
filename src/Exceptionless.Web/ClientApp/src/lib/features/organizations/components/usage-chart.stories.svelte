<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';
    import { expect, userEvent, waitFor, within } from 'storybook/test';

    import { createUsageChartData } from './usage-chart.fixtures.ts';
    import UsageChart from './usage-chart.svelte';

    async function checkConstantLimit({ canvasElement }: { canvasElement: HTMLElement }) {
        // Arrange
        const canvas = within(canvasElement);
        const limitButton = await canvas.findByRole('button', {
            name: 'Limit'
        });
        const seriesCount = canvasElement.querySelectorAll('.lc-area-path').length;

        // Act
        await userEvent.click(limitButton);

        // Assert
        await waitFor(() => expect(canvasElement.querySelectorAll('.lc-area-path')).toHaveLength(1));

        // Act
        await userEvent.click(limitButton);

        // Assert
        await waitFor(() => {
            expect(canvasElement.querySelectorAll('.lc-area-path')).toHaveLength(seriesCount);
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
