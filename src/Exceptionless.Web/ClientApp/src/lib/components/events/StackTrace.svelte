<script lang="ts">
    import type { ErrorInfo } from '$lib/models/client-data';

    import { Code } from '$comp/typography';
    import { getErrors, getStackFrame } from '$lib/helpers/persistent-event';

    import StackTraceHeader from './StackTraceHeader.svelte';

    interface Props {
        error: ErrorInfo;
    }

    let { error }: Props = $props();

    const errors = getErrors(error);
</script>

<pre class="whitespace-pre-wrap break-words border p-2"><Code
        ><StackTraceHeader {errors}></StackTraceHeader>{#each errors.reverse() as error, index}{#if error.stack_trace}<div
                    class="pl-[10px]">{#each error.stack_trace as frame}{getStackFrame(frame)}<br
                        />{/each}{#if index < errors.length - 1}<div>--- End of inner exception stack trace ---</div>{/if}</div>{/if}{/each}</Code
    ></pre>
