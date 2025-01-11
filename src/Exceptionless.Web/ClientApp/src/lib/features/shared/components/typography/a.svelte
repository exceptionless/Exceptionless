<script lang="ts">
    import { cn } from '$lib/utils';

    import { type Props, variants } from './a';

    let { children, class: className, variant = 'default', ...props }: Props = $props();
</script>

<a class={cn(variants({ variant }), className)} {...props}>
    {#if children}
        {@render children()}
    {/if}
</a>
