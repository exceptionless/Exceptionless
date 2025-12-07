<script lang="ts">
    import type { SimpleErrorInfo } from '$features/events/models/event-data';

    import { Code } from '$comp/typography';
    import { getErrors } from '$features/events/persistent-event';

    import SimpleStackTraceFrames from './simple-stack-trace-frames.svelte';
    import SimpleStackTraceHeader from './simple-stack-trace-header.svelte';

    interface Props {
        error: SimpleErrorInfo;
    }

    let { error }: Props = $props();

    const errors = $derived(getErrors(error));
</script>

<pre class="bg-muted rounded p-2 wrap-break-word whitespace-pre-wrap"><Code class="px-0"
        >{#each errors.reverse() as error, index (index)}<SimpleStackTraceHeader {error} /><SimpleStackTraceFrames {error} />{#if index < errors.length - 1}<br
                />{/if}{/each}</Code
    ></pre>
