<script lang="ts">
	import { cn } from '$lib/utils';

	let className: string | undefined | null = undefined;
	export { className as class };
</script>

<h2
	class={cn(
		'scroll-m-20 border-b pb-2 text-3xl font-semibold tracking-tight transition-colors first:mt-0',
		className
	)}
	{...$$restProps}
>
	<slot />
</h2>
