<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import * as Command from '$comp/ui/command';

    export let showApply: boolean;
    export let showClear: boolean;

    const dispatch = createEventDispatcher();
</script>

<Command.Root>
    <Command.List>
        <Command.Separator />
        {#if showApply}
            <Command.Item class="justify-center text-center font-bold text-primary" onSelect={() => dispatch('apply')}>Apply filter</Command.Item>
            <Command.Separator />
        {/if}
        {#if showClear}
            <Command.Item class="justify-center text-center" onSelect={() => dispatch('clear')}>Clear filter</Command.Item>
        {/if}
        <Command.Item class="justify-center text-center" onSelect={() => dispatch('remove')}>Remove filter</Command.Item>
        <Command.Item class="justify-center text-center" onSelect={() => dispatch('close')}>Close</Command.Item>
    </Command.List>
</Command.Root>
