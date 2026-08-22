<script lang="ts">
    import type { ErrorInfo } from '$features/events/models/event-data';

    import { Code } from '$comp/typography';
    import { getErrors } from '$features/events/persistent-event';

    import SourceMapStatus from './source-map-status.svelte';
    import StackTraceFrames from './stack-trace-frames.svelte';
    import StackTraceHeader from './stack-trace-header.svelte';

    interface Props {
        error: ErrorInfo;
        projectId: string;
    }

    let { error, projectId }: Props = $props();

    const errors = $derived(getErrors(error));
</script>

<div class="space-y-2">
    <SourceMapStatus {error} {projectId} />
    <pre class="bg-muted/50 border-border rounded-xl border p-2 wrap-break-word whitespace-pre-wrap"><Code class="bg-transparent px-0 py-0"
            >{#each errors.reverse() as error, index (index)}<StackTraceHeader {error} /><StackTraceFrames {error} />{#if index < errors.length - 1}<br
                    />{/if}{/each}</Code
        ></pre>
</div>
