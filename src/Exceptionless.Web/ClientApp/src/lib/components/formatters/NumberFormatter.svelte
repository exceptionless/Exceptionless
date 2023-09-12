<script lang="ts">
	export let value: number | null = null;
</script>

{#if value !== null && !isNaN(value) && isFinite(value)}
	{Intl.NumberFormat().format(value)}
{/if}
