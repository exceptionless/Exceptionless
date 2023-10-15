<script lang="ts">
	import type { LogLevel } from '$lib/models/client-data';

	export let level: LogLevel | undefined;
	const isLevelSuccess = level === 'trace' || level === 'debug';
	const isLevelInfo = level === 'info';
	const isLevelWarning = level === 'warn';
	const isLevelError = level === 'error';
</script>

{#if level}
	<span
		class="badge {isLevelSuccess && 'badge-success'} {isLevelInfo &&
			'badge-info'} {isLevelWarning && 'badge-warning'} {isLevelError && 'badge-error'}"
	>
		{level}
	</span>
{/if}
