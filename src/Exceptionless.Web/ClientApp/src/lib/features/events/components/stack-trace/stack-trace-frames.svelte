<script lang="ts">
    import type { ErrorInfo } from '$features/events/models/event-data';

    import StackTraceFrame from './stack-trace-frame.svelte';

    interface Props {
        error: ErrorInfo;
    }

    let { error }: Props = $props();
</script>

{#if error.stack_trace}<div class="bg-inherit pl-[10px]">
        {#each error.stack_trace as frame}<StackTraceFrame {frame} />{/each}
    </div>{/if}
