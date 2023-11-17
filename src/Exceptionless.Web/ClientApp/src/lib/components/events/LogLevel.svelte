<script lang="ts">
	import { Badge } from 'flowbite-svelte';
	import type { LogLevel } from '$lib/models/client-data';

	export let level: LogLevel | undefined;

	let color:
		| 'red'
		| 'yellow'
		| 'green'
		| 'indigo'
		| 'purple'
		| 'pink'
		| 'blue'
		| 'dark'
		| 'primary'
		| 'none'
		| undefined = 'none';
	if (level === 'trace' || level === 'debug') color = 'blue';
	else if (level === 'info') color = 'green';
	else if (level === 'warn') color = 'yellow';
	else if (level === 'error') color = 'red';
</script>

{#if level}
	<Badge {color}>
		{level}
	</Badge>
{/if}
