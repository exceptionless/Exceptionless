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

    const errors = getErrors(error);
</script>

<pre class="bg-muted rounded p-2 break-words whitespace-pre-wrap"><Code class="px-0"
        >{#each errors.reverse() as error, index}<StackTraceHeader {error} /><StackTraceFrames {error} />{#if index < errors.length - 1}<br />{/if}{/each}</Code
    ></pre>
