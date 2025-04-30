<script lang="ts">
    import type { StackFrameInfo } from '$features/events/models/event-data';

    import StackTraceFrameFileName from './stack-trace-frame-file-name.svelte';
    import StackTraceFrameGenericArguments from './stack-trace-frame-generic-arguments.svelte';
    import StackTraceFrameMethodName from './stack-trace-frame-method-name.svelte';
    import StackTraceFrameNamespace from './stack-trace-frame-namespace.svelte';
    import StackTraceFrameOffset from './stack-trace-frame-offset.svelte';
    import StackTraceFrameParameters from './stack-trace-frame-parameters.svelte';

    interface Props {
        frame: StackFrameInfo;
    }

    let { frame }: Props = $props();
</script>

{#if frame}<div class="bg-inherit pl-[10px]">
        at <StackTraceFrameNamespace {frame} /><StackTraceFrameMethodName {frame} /><StackTraceFrameGenericArguments {frame} />(<StackTraceFrameParameters
            {frame}
        />)<StackTraceFrameOffset {frame} /><StackTraceFrameFileName {frame} />
    </div>{:else}&lt;null&gt;{/if}
