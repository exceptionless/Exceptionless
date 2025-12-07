<script lang="ts">
    import { Button, type ButtonProps } from '$comp/ui/button';
    import Filter from '@lucide/svelte/icons/filter';

    import { NumberFilter } from './models.svelte';

    type Props = Omit<ButtonProps, 'value'> & {
        changed: (filter: NumberFilter) => void;
        term: string;
        value?: number;
    };
    let { changed, children, class: className, term, value, ...props }: Props = $props();
</script>

<Button
    variant="ghost"
    size={children ? 'xs' : 'icon-xs'}
    onclick={() => changed(new NumberFilter(term, value))}
    title={`Search ${term}:${value}`}
    class={['cursor-pointer', children ? '' : 'opacity-50 hover:opacity-100 focus-visible:opacity-100', className]}
    {...props}
>
    {#if children}
        {@render children()}
    {:else}
        <Filter class="text-muted-foreground" />
    {/if}
</Button>
