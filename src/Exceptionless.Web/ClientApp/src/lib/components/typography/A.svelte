<script lang="ts">
	import type { HTMLAnchorAttributes } from 'svelte/elements';
	import { cn } from '$lib/utils';
	import { tv, type VariantProps } from 'tailwind-variants';

	const variants = tv({
		base: 'underline-offset-4',
		variants: {
			variant: {
				default: 'underline hover:text-primary',
				navigation:
					'text-muted-foreground no-underline hover:underline hover:text-foreground',
				primary: 'text-primary hover:underline',
				secondary: 'text-secondary-foreground hover:underline',
				ghost: 'no-underline hover:text-foreground'
			}
		},
		defaultVariants: {
			variant: 'default'
		}
	});

	type Variant = VariantProps<typeof variants>['variant'];
	type Props = HTMLAnchorAttributes & {
		variant?: Variant;
	};

	export let variant: Props['variant'] = 'default';

	let className: HTMLAnchorAttributes['class'] = undefined;
	export { className as class };
	export let href: HTMLAnchorAttributes['href'] = undefined;
</script>

<a {href} class={cn(variants({ variant, className }))} {...$$restProps} on:click>
	<slot />
</a>
