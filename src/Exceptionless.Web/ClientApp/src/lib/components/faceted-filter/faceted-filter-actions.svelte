<script lang="ts">
    import * as Command from '$comp/ui/command';

    export let showApply: boolean;
    export let onApply: () => void;
    export let showClear: boolean;
    export let onClear: () => void;
    export let onRemove: () => void;
</script>

<Command.Root>
    <Command.List>
        <Command.Separator />
        {#if showApply}
            <Command.Item class="justify-center text-center font-bold text-primary" onSelect={onApply}>Apply filter</Command.Item>
            <Command.Separator />
        {/if}
        {#if showClear}
            <Command.Item class="justify-center text-center" onSelect={onClear}>Clear filter</Command.Item>
        {/if}
        <Command.Item class="justify-center text-center" onSelect={onRemove}>Remove filter</Command.Item>
    </Command.List>
</Command.Root>
