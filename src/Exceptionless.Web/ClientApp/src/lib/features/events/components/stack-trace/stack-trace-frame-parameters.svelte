<script lang="ts">
    import type { StackFrameInfo } from '$features/events/models/event-data';

    import StackTraceFrameParameterGenericArguments from './stack-trace-frame-parameter-generic-arguments.svelte';
    import StackTraceFrameParameterName from './stack-trace-frame-parameter-name.svelte';
    import StackTraceFrameParameterNamespace from './stack-trace-frame-parameter-namespace.svelte';

    interface Props {
        frame: StackFrameInfo;
    }

    let { frame }: Props = $props();
</script>

{#if frame.parameters}{#each frame.parameters as parameter, index (index)}{#if index > 0},&nbsp;{/if}<StackTraceFrameParameterNamespace
            {parameter}
        /><StackTraceFrameParameterGenericArguments {parameter} /><StackTraceFrameParameterName {parameter} />{/each}{/if}
