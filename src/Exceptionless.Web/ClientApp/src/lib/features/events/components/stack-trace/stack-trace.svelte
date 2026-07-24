<script lang="ts">
    import type { ErrorInfo } from '$features/events/models/event-data';

    import { Code } from '$comp/typography';
    import { getErrors } from '$features/events/persistent-event';

    import StackTraceFrames from './stack-trace-frames.svelte';
    import StackTraceHeader from './stack-trace-header.svelte';

    interface Props {
        error: ErrorInfo;
    }

    let { error }: Props = $props();

    const errors = $derived(getErrors(error));
</script>

<pre class="rounded-xl border border-border bg-muted/50 p-2 wrap-break-word whitespace-pre-wrap"><Code class="bg-transparent px-0 py-0"
        >{#each errors.reverse() as error, index (index)}<StackTraceHeader {error} /><StackTraceFrames {error} />{#if index < errors.length - 1}<br
                />{/if}{/each}</Code
    ></pre>
