<script lang="ts">
    import NumberFormatter from '$comp/formatters/number.svelte';

    interface Props {
        value: number;
    }

    let { value }: Props = $props();
</script>

{#if value}
    <NumberFormatter {value} /> days
{:else}
    -
{/if}
