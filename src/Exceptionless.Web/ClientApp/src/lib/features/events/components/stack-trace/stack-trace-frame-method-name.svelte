<script lang="ts">
    import type { StackFrameInfo } from '$features/events/models/event-data';

    interface Props {
        frame: StackFrameInfo;
    }

    let { frame }: Props = $props();
</script>

<span class="text-green-300">{frame.name || '<anonymous>'}</span>
