<script lang="ts">
    import type { StackFrameInfo } from '$features/events/models/event-data';

    interface Props {
        frame: StackFrameInfo;
    }

    let { frame }: Props = $props();
</script>

{#if frame.file_name}&nbsp;in
    <span class="text-blue-400"
        >{frame.file_name}{#if frame.line_number}:line {frame.line_number}{/if}</span
    >{#if frame.column}:col <span class="text-blue-400">{frame.column}</span>{/if}{/if}
