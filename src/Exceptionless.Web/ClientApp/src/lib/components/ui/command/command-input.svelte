<script lang="ts">
	import { Command as CommandPrimitive } from 'cmdk-sv';
	import { MagnifyingGlass } from 'radix-icons-svelte';
	import { cn } from '$lib/utils';

	let className: string | undefined | null = undefined;
	export { className as class };
	export let value: string = '';
</script>

<div class="flex items-center px-3 border-b" data-cmdk-input-wrapper="">
	<MagnifyingGlass class="w-4 h-4 mr-2 opacity-50 shrink-0" />
	<CommandPrimitive.Input
		class={cn(
			'flex h-10 w-full rounded-md bg-transparent py-3 text-sm outline-none placeholder:text-muted-foreground disabled:cursor-not-allowed disabled:opacity-50',
			className
		)}
		{...$$restProps}
		bind:value
	/>
</div>
