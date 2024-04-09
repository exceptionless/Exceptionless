<script lang="ts">
    import IconSearch from '~icons/mdi/search';
    import A from '$comp/typography/A.svelte';
    import { cn } from '$lib/utils';
    import type { StackStatus } from '$lib/models/api.generated';
    import { StatusFilter } from './filters';

    export let value: StackStatus[];

    let className: string | undefined | null = undefined;
    export { className as class };

    const title = `Search status:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new StatusFilter(value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title} class={cn('ml-2', className)}>
    <slot><IconSearch /></slot>
</A>
