<script lang="ts">
    import { Button, type ButtonProps } from '$comp/ui/button';
    import Filter from '@lucide/svelte/icons/filter';

    import { DateFilter } from './models.svelte';

    type Props = ButtonProps & {
        changed: (filter: DateFilter) => void;
        term: string;
        value?: string;
    };
    let { changed, children, class: className, term, value, ...props }: Props = $props();

    const title = `Search ${term}:${value}`;
</script>

<Button
    variant="ghost"
    size={children ? 'xs' : 'icon-xs'}
    onclick={() => changed(new DateFilter(term, value))}
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
