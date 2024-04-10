<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import A from '$comp/typography/A.svelte';
    import { cn } from '$lib/utils';
    import { OrganizationFilter } from './filters';

    export let value: string;

    let className: string | undefined | null = undefined;
    export { className as class };

    const title = `Search organization:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new OrganizationFilter(value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title} class={cn('mr-2', className)}>
    <slot><IconFilter class="text-muted-foreground hover:text-primary" /></slot>
</A>
