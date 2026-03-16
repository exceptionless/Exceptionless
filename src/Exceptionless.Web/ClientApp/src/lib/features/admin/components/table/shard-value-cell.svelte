<script lang="ts">
    import Number from '$comp/formatters/number.svelte';

    interface Props {
        metricId: string;
        value: number;
    }

    let { metricId, value }: Props = $props();
</script>

<span class={metricId === 'unassigned' && value > 0 ? 'font-semibold text-amber-500' : ''}>
    <Number {value} />
</span>
