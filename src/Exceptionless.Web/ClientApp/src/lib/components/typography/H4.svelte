<script lang="ts">
	import { cn } from '$lib/utils';

	let className: string | undefined | null = undefined;
	export { className as class };
</script>

<h4 class={cn('scroll-m-20 text-xl font-semibold tracking-tight', className)} {...$$restProps}>
	<slot />
</h4>
