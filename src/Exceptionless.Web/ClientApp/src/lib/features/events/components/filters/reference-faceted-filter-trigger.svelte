<script lang="ts">
    import { Button, type ButtonProps } from '$comp/ui/button';
    import Filter from '@lucide/svelte/icons/filter';

    import { ReferenceFilter } from './models.svelte';

    type Props = ButtonProps & {
        changed: (filter: ReferenceFilter) => void;
        value?: string;
    };
    let { changed, children, class: className, value, ...props }: Props = $props();

    const title = `Search reference:${value}`;
</script>

<Button
    variant="ghost"
    size={children ? 'xs' : 'icon-xs'}
    onclick={() => changed(new ReferenceFilter(value))}
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
