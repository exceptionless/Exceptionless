<script lang="ts">
    import type { ErrorInfo } from '$features/events/models/event-data';

    import { Code } from '$comp/typography';
    import { getErrors, getStackFrame } from '$features/events/persistent-event';

    import StackTraceHeader from './stack-trace-header.svelte';

    interface Props {
        error: ErrorInfo;
    }

    let { error }: Props = $props();

    const errors = getErrors(error);
</script>

<pre class="whitespace-pre-wrap break-words rounded bg-muted p-2"><Code class="px-0"
        ><StackTraceHeader {errors}></StackTraceHeader>{#each errors.reverse() as error, index}{#if error.stack_trace}<div
                    class="bg-inherit pl-[10px]">{#each error.stack_trace as frame}{getStackFrame(frame)}<br />{/each}{#if index < errors.length - 1}<div
                            class="bg-inherit text-muted-foreground">--- End of inner exception stack trace ---</div>{/if}</div>{/if}{/each}</Code
    ></pre>
