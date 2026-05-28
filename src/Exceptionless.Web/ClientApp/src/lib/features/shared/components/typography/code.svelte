<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import { cn } from '$lib/utils';

    type Props = HTMLAttributes<Element>;

    let { children, class: className, ...props }: Props = $props();
</script>

<code class={cn('bg-muted relative rounded px-[0.6rem] py-2 font-mono text-sm', className)} {...props}
    >{#if children}{@render children()}{/if}</code
>
