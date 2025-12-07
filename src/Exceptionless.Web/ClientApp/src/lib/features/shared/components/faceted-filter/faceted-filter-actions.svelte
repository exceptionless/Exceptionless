<script lang="ts">
    import { Button } from '$comp/ui/button';
    import Separator from '$comp/ui/separator/separator.svelte';

    interface Props {
        clear: () => void;
        remove: () => void;
        showClear: boolean;
    }

    let { clear, remove, showClear }: Props = $props();
</script>

<div class="flex flex-col">
    <Separator />
    {#if showClear}
        <Button class="justify-center text-center" variant="ghost" onclick={clear}>Clear filter</Button>
    {/if}
    <Button class="justify-center text-center" variant="ghost" onclick={remove}>Remove filter</Button>
</div>
