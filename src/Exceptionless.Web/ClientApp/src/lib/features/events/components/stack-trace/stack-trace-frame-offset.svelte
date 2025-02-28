<script lang="ts">
    import type { StackFrameInfo } from '$features/events/models/event-data';

    interface Props {
        frame: StackFrameInfo;
    }

    let { frame }: Props = $props();
</script>

{#if frame.data?.ILOffset || frame.data?.NativeOffset}&nbsp;at offset <span class="text-blue-400">{frame.data.ILOffset || frame.data.NativeOffset}</span>{/if}
