<script lang="ts">
    import type { ParameterInfo, StackFrameInfo } from '$features/events/models/event-data';

    interface Props {
        frame: StackFrameInfo;
    }

    let { frame }: Props = $props();

    // Keep frame formatting inline: nested components and conditional blocks each create a Svelte DOM anchor for every stack frame.
    function getGenericArguments(genericArguments: string[] | undefined): string {
        return genericArguments?.join(', ') ?? '';
    }

    function getGenericArgumentsDelimiter(genericArguments: string[] | undefined, delimiter: '<' | '>'): string {
        return genericArguments?.length ? delimiter : '';
    }

    function getNamespace(frame: StackFrameInfo): string {
        const namespace = frame.declaring_namespace ? `${frame.declaring_namespace}.` : '';
        const type = frame.declaring_type ? `${frame.declaring_type.replace('+', '')}.` : '';

        return `${namespace}${type}`;
    }

    function getOffset(frame: StackFrameInfo): unknown {
        return frame.data?.ILOffset || frame.data?.NativeOffset;
    }

    function getParameterName(parameter: ParameterInfo): string {
        return parameter.name ? `\u00a0${parameter.name}` : '';
    }

    function getParameterType(parameter: ParameterInfo): string {
        const namespace = parameter.type_namespace ? `${parameter.type_namespace}.` : '';
        const type = parameter.type?.replace('+', '') ?? '';

        return `${namespace}${type}`;
    }
</script>

{#if frame}<div class="bg-inherit pl-[10px]" data-slot="stack-trace-frame">
        <span class="whitespace-nowrap" data-slot="stack-trace-frame-content"
            >at <span class="text-purple-700 dark:text-purple-300">{getNamespace(frame)}</span><span class="text-green-800 dark:text-green-300"
            >{frame.name || '<anonymous>'}</span
        >{getGenericArgumentsDelimiter(frame.generic_arguments, '<')}<span class="text-purple-700 dark:text-purple-300"
            >{getGenericArguments(frame.generic_arguments)}</span
        >{getGenericArgumentsDelimiter(frame.generic_arguments, '>')}({#each frame.parameters ?? [] as parameter, index (index)}<span
                >{index > 0 ? ',\u00a0' : ''}<span class="text-purple-700 dark:text-purple-300">{getParameterType(parameter)}</span
                >{getGenericArgumentsDelimiter(parameter.generic_arguments, '<')}<span class="text-purple-700 dark:text-purple-300"
                    >{getGenericArguments(parameter.generic_arguments)}</span
                >{getGenericArgumentsDelimiter(parameter.generic_arguments, '>')}{getParameterName(parameter)}</span
            >{/each}){getOffset(frame) ? '\u00a0at offset ' : ''}<span class="text-blue-700 dark:text-blue-400">{getOffset(frame) ?? ''}</span>{frame.file_name
            ? '\u00a0in '
            : ''}<span class="text-blue-700 dark:text-blue-400"
            >{frame.file_name ?? ''}{frame.file_name && frame.line_number ? `:line ${frame.line_number}` : ''}</span
        >{frame.file_name && frame.column ? ':col ' : ''}<span class="text-blue-700 dark:text-blue-400"
            >{frame.file_name && frame.column ? frame.column : ''}</span
        >
    </div>{:else}&lt;null&gt;{/if}
