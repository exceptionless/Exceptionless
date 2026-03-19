<script lang="ts">
    import Number from '$comp/formatters/number.svelte';

    interface Props {
        value: number;
    }

    let { value }: Props = $props();
</script>

<span class="font-mono text-xs {value > 0 ? 'font-semibold text-amber-500' : ''}">
    {#if value > 0}<Number {value} />{:else}—{/if}
</span>
