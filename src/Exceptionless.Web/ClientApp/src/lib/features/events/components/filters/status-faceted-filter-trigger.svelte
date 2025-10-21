<script lang="ts">
    import type { StackStatus } from '$features/stacks/models';

    import { Button, type ButtonProps } from '$comp/ui/button';
    import Filter from '@lucide/svelte/icons/filter';

    import { StatusFilter } from './models.svelte';

    type Props = ButtonProps & {
        changed: (filter: StatusFilter) => void;
        value: StackStatus[];
    };
    let { changed, children, class: className, value, ...props }: Props = $props();

    const title = `Search status:${value}`;
</script>

<Button
    variant="ghost"
    size={children ? 'xs' : 'icon-xs'}
    onclick={() => changed(new StatusFilter(value))}
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
