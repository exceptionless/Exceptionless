<script lang="ts">
	import { cn } from '$lib/utils';

	let className: string | undefined | null = undefined;
	export { className as class };
</script>

<h3 class={cn('scroll-m-20 text-2xl font-semibold tracking-tight', className)} {...$$restProps}>
	<slot />
</h3>
