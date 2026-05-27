<script lang="ts">
    import { Button } from '$comp/ui/button';
    import Separator from '$comp/ui/separator/separator.svelte';

    interface Props {
        clear: () => void;
        hidden?: boolean;
        remove: () => void;
        showClear: boolean;
        toggleHidden?: () => void;
    }

    let { clear, hidden = false, remove, showClear, toggleHidden }: Props = $props();
</script>

<div class="flex flex-col">
    <Separator />
    {#if showClear}
        <Button variant="ghost" onclick={clear}>Clear Filter</Button>
    {/if}
    {#if toggleHidden}
        <Button variant="ghost" onclick={toggleHidden}>{hidden ? 'Show Filter' : 'Hide Filter'}</Button>
    {/if}
    <Button variant="ghost" onclick={remove}>Remove Filter</Button>
</div>
