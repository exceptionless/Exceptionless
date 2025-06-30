<script lang="ts">
    import * as Command from '$comp/ui/command';

    interface Props {
        clear: () => void;
        close: () => void;
        remove: () => void;
        showClear: boolean;
    }

    let { clear, close, remove, showClear }: Props = $props();
</script>

<Command.Root>
    <Command.List>
        <Command.Separator />
        {#if showClear}
            <Command.Item class="justify-center text-center" onSelect={clear}>Clear filter</Command.Item>
        {/if}
        <Command.Item class="justify-center text-center" onSelect={remove}>Remove filter</Command.Item>
        <Command.Item class="justify-center text-center" onSelect={close}>Close</Command.Item>
    </Command.List>
</Command.Root>
