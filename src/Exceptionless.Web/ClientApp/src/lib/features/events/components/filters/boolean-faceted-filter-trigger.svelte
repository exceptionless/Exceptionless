<script lang="ts">
    import { Button, type ButtonProps } from '$comp/ui/button';
    import Filter from '@lucide/svelte/icons/filter';

    import { BooleanFilter } from './models.svelte';

    type Props = ButtonProps & {
        changed: (filter: BooleanFilter) => void;
        term: string;
        value?: boolean;
    };
    let { changed, children, class: className, term, value, ...props }: Props = $props();

    const title = `Search ${term}:${value}`;
</script>

<Button
    variant="ghost"
    size={children ? 'xs' : 'icon-xs'}
    onclick={() => changed(new BooleanFilter(term, value))}
    {title}
    class={[children ? '' : 'opacity-50 hover:opacity-100 focus-visible:opacity-100', className]}
    {...props}
>
    {#if children}
        {@render children()}
    {:else}
        <Filter class="text-muted-foreground" />
    {/if}
</Button>
