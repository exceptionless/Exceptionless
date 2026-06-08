<script lang="ts">
    import type { StackFrameInfo } from '$features/events/models/event-data';

    interface Props {
        frame: StackFrameInfo;
    }

    let { frame }: Props = $props();
</script>

{#if frame.declaring_namespace || frame.declaring_type}<span class="text-purple-700 dark:text-purple-300"
        >{#if frame.declaring_namespace}{frame.declaring_namespace}.{/if}{#if frame.declaring_type}{frame.declaring_type.replace('+', '')}.{/if}</span
    >{/if}
