<script lang="ts">
    import { H4, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Collapsible from '$comp/ui/collapsible';
    import * as Tooltip from '$comp/ui/tooltip';
    import { UseClipboard } from '$lib/hooks/use-clipboard.svelte';
    import Check from '@lucide/svelte/icons/check';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import CircleAlert from '@lucide/svelte/icons/circle-alert';
    import Clipboard from '@lucide/svelte/icons/clipboard';
    import Wrench from '@lucide/svelte/icons/wrench';

    import type { AssistantToolActivity } from '../models';

    import { assistantToolErrorMessage, formatAssistantToolJson } from '../assistant-tool-result';

    interface Props {
        tool: AssistantToolActivity;
    }

    let { tool }: Props = $props();
    let open = $state(false);

    const argumentsClipboard = new UseClipboard({
        delay: 1500
    });
    const resultClipboard = new UseClipboard({
        delay: 1500
    });

    const toolLabels: Record<string, string> = {
        add_stack_reference_link: 'Added stack reference link',
        get_event: 'Retrieved event details',
        get_stack: 'Retrieved stack details',
        list_projects: 'Listed projects',
        remove_stack_reference_link: 'Removed stack reference link',
        search_stacks: 'Searched error stacks',
        set_stack_critical: 'Updated stack critical setting',
        snooze_stack: 'Snoozed stack',
        update_stack_status: 'Updated stack status'
    };
    const toolFailureLabels: Record<string, string> = {
        add_stack_reference_link: 'Couldn’t add stack reference link',
        get_event: 'Couldn’t retrieve event details',
        get_stack: 'Couldn’t retrieve stack details',
        list_projects: 'Couldn’t list projects',
        remove_stack_reference_link: 'Couldn’t remove stack reference link',
        search_stacks: 'Couldn’t search error stacks',
        set_stack_critical: 'Couldn’t update stack critical setting',
        snooze_stack: 'Couldn’t snooze stack',
        update_stack_status: 'Couldn’t update stack status'
    };
    const toolCancelledLabels: Record<string, string> = {
        add_stack_reference_link: 'Add stack reference link cancelled',
        get_event: 'Event details request cancelled',
        get_stack: 'Stack details request cancelled',
        list_projects: 'List projects request cancelled',
        remove_stack_reference_link: 'Remove stack reference link cancelled',
        search_stacks: 'Search error stacks request cancelled',
        set_stack_critical: 'Update stack critical setting cancelled',
        snooze_stack: 'Snooze stack request cancelled',
        update_stack_status: 'Update stack status request cancelled'
    };

    let label = $derived(
        tool.status === 'failed'
            ? (toolFailureLabels[tool.name] ?? 'Tool call failed')
            : tool.status === 'cancelled'
              ? (toolCancelledLabels[tool.name] ?? 'Tool call cancelled')
              : (toolLabels[tool.name] ?? humanize(tool.name))
    );
    let errorMessage = $derived(tool.status === 'failed' ? assistantToolErrorMessage(tool.result) : undefined);
    let formattedArguments = $derived(formatAssistantToolJson(tool.arguments));
    let formattedResult = $derived(formatAssistantToolJson(tool.result));

    function humanize(value: string): string {
        const text = value.replaceAll('_', ' ');
        return text.charAt(0).toUpperCase() + text.slice(1);
    }
</script>

<Tooltip.Provider delayDuration={300}>
    <Collapsible.Root bind:open class="group/tool">
        <div
            class={[
                'bg-muted/30 mb-2 overflow-hidden rounded-lg border text-xs',
                (tool.status === 'failed' || tool.status === 'cancelled') && 'border-destructive/40 bg-destructive/5'
            ]}
        >
            <Collapsible.Trigger>
                {#snippet child({ props })}
                    <Button class="h-auto w-full justify-start rounded-none border-0 px-3 py-2 text-left" variant="ghost" {...props}>
                        {#if tool.status === 'running'}
                            <span class="bg-primary size-2 shrink-0 animate-pulse rounded-full" aria-hidden="true"></span>
                        {:else if tool.status === 'failed' || tool.status === 'cancelled'}
                            <CircleAlert aria-hidden="true" class="text-destructive" data-icon="inline-start" />
                        {:else}
                            <Check aria-hidden="true" class="text-primary" data-icon="inline-start" />
                        {/if}
                        <Wrench aria-hidden="true" data-icon="inline-start" />
                        <span class="min-w-0 flex-1 truncate">{label}</span>
                        <ChevronDown aria-hidden="true" class="transition-transform group-data-[state=open]/tool:rotate-180" data-icon="inline-end" />
                    </Button>
                {/snippet}
            </Collapsible.Trigger>
            {#if errorMessage}
                <P class="text-destructive mt-0! border-t px-3 py-2 text-xs leading-relaxed">{errorMessage}</P>
            {/if}
            <Collapsible.Content>
                <div class="border-t px-3 py-3">
                    <div class="flex flex-col gap-3">
                        <section aria-label="Tool request">
                            <div class="mb-1.5 flex items-center justify-between gap-2">
                                <H4 class="text-muted-foreground text-sm font-medium">Request</H4>
                                <Tooltip.Root>
                                    <Tooltip.Trigger>
                                        {#snippet child({ props })}
                                            <Button
                                                {...props}
                                                aria-label="Copy tool request"
                                                onclick={() => void argumentsClipboard.copy(tool.arguments)}
                                                size="icon-xs"
                                                variant="ghost"
                                            >
                                                {#if argumentsClipboard.copied}<Check aria-hidden="true" />{:else}<Clipboard aria-hidden="true" />{/if}
                                            </Button>
                                        {/snippet}
                                    </Tooltip.Trigger>
                                    <Tooltip.Content>{argumentsClipboard.copied ? 'Copied' : 'Copy request'}</Tooltip.Content>
                                </Tooltip.Root>
                            </div>
                            <pre
                                class="bg-background max-h-48 overflow-auto rounded-md border p-2 font-mono text-[11px] leading-relaxed wrap-anywhere whitespace-pre-wrap">{formattedArguments}</pre>
                        </section>
                        {#if tool.result}
                            <section aria-label="Tool response">
                                <div class="mb-1.5 flex items-center justify-between gap-2">
                                    <H4 class="text-muted-foreground text-sm font-medium">Response</H4>
                                    <Tooltip.Root>
                                        <Tooltip.Trigger>
                                            {#snippet child({ props })}
                                                <Button
                                                    {...props}
                                                    aria-label="Copy tool response"
                                                    onclick={() => void resultClipboard.copy(tool.result ?? '')}
                                                    size="icon-xs"
                                                    variant="ghost"
                                                >
                                                    {#if resultClipboard.copied}<Check aria-hidden="true" />{:else}<Clipboard aria-hidden="true" />{/if}
                                                </Button>
                                            {/snippet}
                                        </Tooltip.Trigger>
                                        <Tooltip.Content>{resultClipboard.copied ? 'Copied' : 'Copy response'}</Tooltip.Content>
                                    </Tooltip.Root>
                                </div>
                                <pre
                                    class="bg-background max-h-64 overflow-auto rounded-md border p-2 font-mono text-[11px] leading-relaxed wrap-anywhere whitespace-pre-wrap">{formattedResult}</pre>
                            </section>
                        {/if}
                    </div>
                </div>
            </Collapsible.Content>
        </div>
    </Collapsible.Root>
</Tooltip.Provider>
