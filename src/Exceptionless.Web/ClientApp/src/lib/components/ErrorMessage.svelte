<script lang="ts">
	export let message: string[] | string | undefined;

	$: text = Array.isArray(message) ? message.join(', ') : message;
</script>

{#if text}
	<p class="text-[0.8rem] font-medium text-destructive">{text}</p>
{/if}
