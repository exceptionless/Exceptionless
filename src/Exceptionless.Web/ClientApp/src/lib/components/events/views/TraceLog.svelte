<script lang="ts">
	export let logs: string[] | undefined;
</script>

{#if logs}
	<ul>
		{#each logs || [] as log}
			<li>{log}</li>
		{/each}
	</ul>
{/if}
