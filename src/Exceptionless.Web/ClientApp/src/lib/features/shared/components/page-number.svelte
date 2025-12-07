<script lang="ts">
    import Number from '$comp/formatters/number.svelte';

    interface Props {
        class?: string;
        currentPage: number;
        size?: 'base' | 'sm' | 'xs';
        totalPages: number;
    }

    let { class: className = '', currentPage, size = 'sm', totalPages }: Props = $props();

    const displayPage = $derived(Math.max(1, currentPage));
    const displayTotal = $derived(Math.max(1, totalPages));

    function getSizeClass(size: 'base' | 'sm' | 'xs'): string {
        switch (size) {
            case 'base':
                return 'text-base';
            case 'xs':
                return 'text-xs';
            default:
                return 'text-sm';
        }
    }
</script>

<div class={['min-w-0 font-medium', getSizeClass(size), className]}>
    <div class="hidden items-center justify-center sm:flex">
        <span class="truncate">Page <Number value={displayPage} /> of <Number value={displayTotal} /></span>
    </div>

    <div class="flex items-center justify-center sm:hidden">
        <span aria-hidden="true" class="truncate"><Number value={displayPage} /> / <Number value={displayTotal} /></span>
    </div>

    <span class="sr-only">Page <Number value={displayPage} /> of <Number value={displayTotal} /></span>
</div>
