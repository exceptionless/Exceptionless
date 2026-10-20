<script lang="ts">
    import Filter from '@lucide/svelte/icons/filter';

    import type { StackStatus } from '#features/stacks/models.ts';

    import { Button, type ButtonProps } from '#comp/ui/button/index.ts';

    import { StatusFilter } from './models.svelte';

    type Props = Omit<ButtonProps, 'value'> & {
        changed: (filter: StatusFilter) => void;
        value: StackStatus[];
    };
    let { changed, children, class: className, value, ...props }: Props = $props();
</script>

<Button
    variant="ghost"
    size={children ? 'xs' : 'icon-xs'}
    onclick={() => changed(new StatusFilter(value))}
    title={`Filter status:${value}`}
    class={['cursor-pointer', children ? '' : 'opacity-50 hover:opacity-100 focus-visible:opacity-100', className]}
    {...props}
>
    {#if children}
        {@render children()}
    {:else}
        <Filter class="text-muted-foreground" />
    {/if}
</Button>
