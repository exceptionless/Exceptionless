<script lang="ts">
    import type { StackFrameInfo } from '$features/events/models/event-data';

    interface Props {
        frame: StackFrameInfo;
    }

    let { frame }: Props = $props();
</script>

{#if frame.generic_arguments?.length}&lt;<span class="text-purple-700 dark:text-purple-300">{frame.generic_arguments.join(', ')}</span>&gt;{/if}
