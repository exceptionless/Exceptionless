<script lang="ts">
	export let date: Date | string | undefined;
</script>

{#if date}
	{new Date(date).toLocaleString()}
{/if}
