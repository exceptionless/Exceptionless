<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import A from '$comp/typography/A.svelte';
    import { cn } from '$lib/utils';
    import { ReferenceFilter } from './filters';

    export let value: string;

    let className: string | undefined | null = undefined;
    export { className as class };

    const title = `Search reference:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new ReferenceFilter(value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title} class={cn('mr-2', className)}>
    <slot><IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" /></slot>
</A>
