<script lang="ts">
    import { resolve } from '$app/paths';
    import CircleAlert from '@lucide/svelte/icons/circle-alert';
    import CircleCheck from '@lucide/svelte/icons/circle-check';
    import CircleSlash from '@lucide/svelte/icons/circle-slash';
    import FileSearch from '@lucide/svelte/icons/file-search';
    import FileText from '@lucide/svelte/icons/file-text';
    import Hash from '@lucide/svelte/icons/hash';
    import List from '@lucide/svelte/icons/list';
    import LoaderCircle from '@lucide/svelte/icons/loader-circle';
    import PencilLine from '@lucide/svelte/icons/pencil-line';
    import Search from '@lucide/svelte/icons/search';
    import Settings2 from '@lucide/svelte/icons/settings-2';

    import * as Trace from '#comp/ui/trace/index.ts';

    import type { AssistantToolActivity } from '../models';

    import { assistantToolErrorMessage } from '../assistant-tool-result';
    import {
        getAssistantToolDetail,
        getAssistantToolLabel,
        getAssistantToolStackId,
        getAssistantToolStackTitle,
        isHiddenAssistantTool,
        isMutationTool
    } from '../assistant-tools';
    import AssistantJsonBlock from './assistant-json-block.svelte';

    interface Props {
        isStreaming?: boolean;
        /** Project names from the whole response, so a step can name the project it searched. */
        projectNames?: ReadonlyMap<string, string>;
        tools: AssistantToolActivity[];
    }

    let { isStreaming = false, projectNames = new Map(), tools }: Props = $props();
    type TraceSegment = { id: string; kind: 'change'; tool: AssistantToolActivity } | { id: string; kind: 'research'; tools: AssistantToolActivity[] };

    let visibleTools = $derived(tools.filter((tool) => !isHiddenAssistantTool(tool)));
    // Steps stay in the order they ran: consecutive lookups share a summary, and each change gets its own row.
    let segments = $derived.by(() => {
        const result: TraceSegment[] = [];
        for (const tool of visibleTools) {
            const previous = result.at(-1);
            if (isMutationTool(tool)) {
                result.push({
                    id: tool.id,
                    kind: 'change',
                    tool
                });
            } else if (previous?.kind === 'research') {
                previous.tools.push(tool);
            } else {
                result.push({
                    id: tool.id,
                    kind: 'research',
                    tools: [tool]
                });
            }
        }

        return result;
    });
    // The reader's choice for each step summary, which starts collapsed.
    let summaryOpen = $state<Record<string, boolean>>({});
    // Details render only once opened, so long JSON results are never highlighted unseen.
    let openToolIds = $state<Record<string, boolean>>({});

    function getToolIcon(tool: AssistantToolActivity) {
        if (isMutationTool(tool)) {
            return PencilLine;
        }

        if (tool.name.startsWith('search_')) {
            return Search;
        }

        if (tool.name.startsWith('list_')) {
            return List;
        }

        if (tool.name === 'count_events') {
            return Hash;
        }

        if (tool.name.includes('setup') || tool.name === 'get_filter_fields') {
            return Settings2;
        }

        return tool.name.startsWith('get_') ? FileText : FileSearch;
    }

    function getToolStatus(tool: AssistantToolActivity): Trace.TraceLineStatus | undefined {
        if (tool.status === 'running') {
            return isStreaming ? 'running' : 'error';
        }

        if (tool.status === 'failed') {
            return 'error';
        }

        return tool.status === 'cancelled' ? 'pending' : undefined;
    }
</script>

{#snippet toolIcon(tool: AssistantToolActivity)}
    {@const Icon = getToolIcon(tool)}
    <Trace.Icon>
        {#if tool.status === 'running' && isStreaming}
            <LoaderCircle class="animate-spin" />
        {:else if tool.status === 'failed' || (tool.status === 'running' && !isStreaming)}
            <CircleAlert />
        {:else if tool.status === 'cancelled'}
            <CircleSlash />
        {:else if isMutationTool(tool)}
            <CircleCheck class="text-primary" />
        {:else}
            <Icon />
        {/if}
    </Trace.Icon>
{/snippet}

{#snippet toolRow(tool: AssistantToolActivity)}
    {@const label = getAssistantToolLabel(tool, isStreaming)}
    {@const isChange = isMutationTool(tool)}
    {@const stackId = isChange ? getAssistantToolStackId(tool) : undefined}
    {@const stackTitle = isChange ? getAssistantToolStackTitle(tool) : undefined}
    {@const detail = getAssistantToolDetail(tool, projectNames)}
    {#if tool.status === 'running' || tool.status === 'cancelled'}
        <Trace.Line class={isChange ? 'text-foreground' : undefined} data-tool={tool.name} status={getToolStatus(tool)}>
            {@render toolIcon(tool)}
            <Trace.Title class="shrink-0">{label}</Trace.Title>
            {#if detail}
                <Trace.Description class="min-w-0 shrink truncate">{detail}</Trace.Description>
            {/if}
        </Trace.Line>
    {:else}
        {@const errorMessage = tool.status === 'failed' ? assistantToolErrorMessage(tool.result) : undefined}
        <Trace.Collapsible onOpenChange={(open) => (openToolIds[tool.id] = open)} open={openToolIds[tool.id] ?? false}>
            <Trace.CollapsibleTrigger aria-label={`${label}${detail ? `, ${detail}` : ''}. Show details`}>
                <Trace.Line
                    class={isChange && tool.status === 'complete' ? 'text-foreground font-medium' : undefined}
                    data-tool={tool.name}
                    status={getToolStatus(tool)}
                >
                    {@render toolIcon(tool)}
                    <Trace.Title class="shrink-0">{label}</Trace.Title>
                    {#if detail}
                        <Trace.Description class="min-w-0 shrink truncate">{detail}</Trace.Description>
                    {/if}
                </Trace.Line>
            </Trace.CollapsibleTrigger>
            <Trace.CollapsibleContent class="flex flex-col gap-2">
                {#if openToolIds[tool.id]}
                    {#if errorMessage}
                        <p class="text-destructive">{errorMessage}</p>
                    {/if}
                    {#if stackId}
                        <a
                            class="text-foreground w-fit font-medium underline underline-offset-2"
                            href={resolve('/(app)/stack/[stackId=objectid]', {
                                stackId: encodeURIComponent(stackId)
                            })}
                        >
                            Open {stackTitle ?? 'the stack'}
                        </a>
                    {/if}
                    <AssistantJsonBlock title="Request" value={tool.arguments} />
                    {#if tool.result}
                        <AssistantJsonBlock title="Response" value={tool.result} />
                    {/if}
                {/if}
            </Trace.CollapsibleContent>
        </Trace.Collapsible>
    {/if}
{/snippet}

{#snippet researchSummary(id: string, researchTools: AssistantToolActivity[])}
    {@const isResearching = isStreaming && researchTools.some((tool) => tool.status === 'running')}
    {@const failedResearchCount = researchTools.filter((tool) => tool.status === 'failed').length}
    {@const currentTool = isResearching ? researchTools.findLast((tool) => tool.status === 'running') : undefined}
    <!-- The summary stays collapsed until the reader opens it, so its height never changes while Exie works and the chat does not jump. -->
    <Trace.Collapsible onOpenChange={(open) => (summaryOpen[id] = open)} open={summaryOpen[id] ?? false}>
        <Trace.CollapsibleTrigger>
            <Trace.Line status={isResearching ? 'running' : failedResearchCount > 0 ? 'error' : undefined}>
                <Trace.Icon>
                    {#if isResearching}
                        <LoaderCircle class="animate-spin" />
                    {:else if failedResearchCount > 0}
                        <CircleAlert />
                    {:else}
                        <FileSearch />
                    {/if}
                </Trace.Icon>
                <Trace.Title>
                    {#if isResearching}
                        Researching…
                    {:else}
                        Researched in {researchTools.length} steps
                    {/if}
                </Trace.Title>
                {#if currentTool}
                    <Trace.Description class="min-w-0 shrink truncate">{getAssistantToolLabel(currentTool, isStreaming)}</Trace.Description>
                {:else if !isResearching && failedResearchCount > 0}
                    <Trace.Description>{failedResearchCount} failed</Trace.Description>
                {/if}
            </Trace.Line>
        </Trace.CollapsibleTrigger>
        <Trace.CollapsibleContent>
            <Trace.Root>
                {#each researchTools as tool (tool.id)}
                    {@render toolRow(tool)}
                {/each}
            </Trace.Root>
        </Trace.CollapsibleContent>
    </Trace.Collapsible>
{/snippet}

{#if visibleTools.length > 0}
    <Trace.Root aria-label="Exie’s steps" class="text-sm">
        {#each segments as segment (segment.id)}
            {#if segment.kind === 'change'}
                {@render toolRow(segment.tool)}
            {:else if segment.tools.length === 1}
                {@render toolRow(segment.tools[0]!)}
            {:else}
                {@render researchSummary(segment.id, segment.tools)}
            {/if}
        {/each}
    </Trace.Root>
{/if}
