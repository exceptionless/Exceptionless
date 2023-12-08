<script lang="ts">
	import P from './typography/P.svelte';

	export let message: string[] | string | undefined;

	$: text = Array.isArray(message) ? message.join(', ') : message;
</script>

{#if text}
	<P class="text-[0.8rem] font-medium text-destructive">{text}</P>
{/if}
