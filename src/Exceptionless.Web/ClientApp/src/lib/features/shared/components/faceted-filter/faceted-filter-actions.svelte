<script lang="ts">
    import * as Command from '$comp/ui/command';

    interface Props {
        apply: () => void;
        clear: () => void;
        close: () => void;
        remove: () => void;
        showApply: boolean;
        showClear: boolean;
    }

    let { apply, clear, close, remove, showApply, showClear }: Props = $props();
</script>

<Command.Root>
    <Command.List>
        <Command.Separator />
        {#if showApply}
            <Command.Item class="text-primary justify-center text-center font-bold" onSelect={apply}>Apply filter</Command.Item>
            <Command.Separator />
        {/if}
        {#if showClear}
            <Command.Item class="justify-center text-center" onSelect={clear}>Clear filter</Command.Item>
        {/if}
        <Command.Item class="justify-center text-center" onSelect={remove}>Remove filter</Command.Item>
        <Command.Item class="justify-center text-center" onSelect={close}>Close</Command.Item>
    </Command.List>
</Command.Root>
