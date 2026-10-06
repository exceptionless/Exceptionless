<script lang="ts">
    import type { ErrorInfo } from '$features/events/models/event-data';

    import StackTraceFrame from './stack-trace-frame.svelte';

    interface Props {
        error: ErrorInfo;
    }

    let { error }: Props = $props();
</script>

{#if error.stack_trace}<div class="flex flex-col bg-inherit pl-[10px]" data-slot="stack-trace-frames">
        {#each error.stack_trace as frame, index (index)}<StackTraceFrame {frame} />{/each}
    </div>{/if}
