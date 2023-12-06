<script lang="ts">
	import type { HTMLAnchorAttributes } from 'svelte/elements';
	import { cn } from '$lib/utils';

	let className: HTMLAnchorAttributes['class'] = undefined;
	export { className as class };
	export let href: HTMLAnchorAttributes['href'] = undefined;
</script>

<a
    {href}
	class={cn('underline underline-offset-4 hover:text-primary', className)}
	{...$$restProps}
>
	<slot />
</a>
