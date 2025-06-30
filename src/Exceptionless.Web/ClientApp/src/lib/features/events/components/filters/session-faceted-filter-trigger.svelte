<script lang="ts">
    import type { Snippet } from 'svelte';

    import { A, type AProps } from '$comp/typography';
    import { cn } from '$lib/utils';
    import Filter from '@lucide/svelte/icons/filter';

    import { SessionFilter } from './models.svelte';

    type Props = AProps & {
        changed: (filter: SessionFilter) => void;
        children?: Snippet;
        value: string;
    };
    let { changed, children, class: className, value, ...props }: Props = $props();

    const title = `Search ref.session:${value}`;
</script>

<A class={cn('cursor-pointer', className)} onclick={() => changed(new SessionFilter(value))} {title} {...props}>
    {#if children}
        {@render children()}
    {:else}
        <Filter class="text-muted-foreground text-opacity-50 hover:text-primary size-5" />
    {/if}
</A>
