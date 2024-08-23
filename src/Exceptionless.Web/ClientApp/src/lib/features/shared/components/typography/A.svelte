<script lang="ts">
    import { cn } from '$lib/utils';

    import { type Props, variants } from './A';

    let { children, class: className, variant = 'default', ...props }: Props = $props();
</script>

<a class={cn(variants({ className, variant }))} {...props}>
    {#if children}
        {@render children()}
    {/if}
</a>
