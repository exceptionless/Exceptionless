<script lang="ts">
    import type { StackFrameInfo } from '$features/events/models/event-data';

    interface Props {
        frame: StackFrameInfo;
    }

    let { frame }: Props = $props();
</script>

{#if frame.generic_arguments?.length}{'<'}<span class="text-purple-400">{frame.generic_arguments.join(', ')}</span>{'>'}{/if}
