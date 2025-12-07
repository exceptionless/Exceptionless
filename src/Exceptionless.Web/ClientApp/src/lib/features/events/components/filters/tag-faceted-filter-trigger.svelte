<script lang="ts">
    import { Button, type ButtonProps } from '$comp/ui/button';
    import Filter from '@lucide/svelte/icons/filter';

    import { TagFilter } from './models.svelte';

    type Props = Omit<ButtonProps, 'value'> & {
        changed: (filter: TagFilter) => void;
        value: string[];
    };
    let { changed, children, class: className, value = [], ...props }: Props = $props();
</script>

<Button
    variant="ghost"
    size={children ? 'xs' : 'icon-xs'}
    onclick={() => changed(new TagFilter(value))}
    title={`Filter by tag: ${value.join(', ')}`}
    class={['cursor-pointer', children ? '' : 'opacity-50 hover:opacity-100 focus-visible:opacity-100', className]}
    {...props}
>
    {#if children}
        {@render children()}
    {:else}
        <Filter class="text-muted-foreground" />
    {/if}
</Button>
