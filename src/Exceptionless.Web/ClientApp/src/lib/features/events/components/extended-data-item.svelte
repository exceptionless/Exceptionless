<script lang="ts">
    import ObjectDump from '$comp/object-dump.svelte';
    import { Code, CodeBlock, H4 } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { isJSONString, isObject, isString, isXmlString } from '$features/shared/typing';
    import { UseClipboard } from '$lib/hooks/use-clipboard.svelte';
    import ArrowDown from '@lucide/svelte/icons/arrow-down';
    import ArrowUp from '@lucide/svelte/icons/arrow-up';
    import Copy from '@lucide/svelte/icons/copy';
    import MoreVertical from '@lucide/svelte/icons/more-vertical';
    import ToggleLeft from '@lucide/svelte/icons/toggle-left';
    import { toast } from 'svelte-sonner';

    interface Props {
        canPromote?: boolean;
        class?: string;
        data: unknown;
        demote?: (title: string) => Promise<void>;
        excludedKeys?: string[];
        isPromoted?: boolean;
        promote?: (title: string) => Promise<void>;
        title: string;
    }

    let {
        canPromote = true,
        class: className,
        data,
        demote = async () => {},
        excludedKeys = [],
        isPromoted = false,
        promote = async () => {},
        title
    }: Props = $props();

    function transformData(data: unknown): unknown {
        if (isJSONString(data)) {
            try {
                return JSON.parse(data);
            } catch {
                return data;
            }
        }

        return data;
    }

    function getFilteredData(data: unknown, exclusions: string[]): unknown {
        if (Array.isArray(data)) {
            return data.map((item) => getFilteredData(item, exclusions));
        }

        if (!isObject(data)) {
            return data;
        }

        return Object.entries(data)
            .filter(([key]) => !exclusions.includes(key))
            .sort(([keyA], [keyB]) => keyA.localeCompare(keyB))
            .reduce((acc: Record<string, unknown>, [key, value]) => {
                acc[key] = value;
                return acc;
            }, {});
    }

    function isEmpty(data: unknown): boolean {
        if (data === undefined || data === null) {
            return true;
        }

        if (Array.isArray(data)) {
            return data.length === 0;
        }

        if (isObject(data)) {
            return Object.keys(data).length === 0;
        }

        if (isString(data)) {
            return data.trim().length === 0;
        }

        return false;
    }

    function onToggleView() {
        showRaw = !showRaw;
    }

    let showRaw = $state(false);
    const transformedData = $derived(transformData(data));
    const filteredData = $derived(getFilteredData(transformedData, excludedKeys));
    const hasData = $derived(!isEmpty(filteredData));
    const showJSONCodeEditor = $derived(isJSONString(data) || Array.isArray(transformedData) || isObject(transformedData));
    const showXmlCodeEditor = $derived(isXmlString(filteredData));
    const canToggle = $derived(!showXmlCodeEditor);

    const clipboardData = $derived(isString(data) ? data : JSON.stringify(data, null, 2));
    const code = $derived(isString(filteredData) ? filteredData : JSON.stringify(filteredData, null, 2));

    const clipboard = new UseClipboard();
    async function copyToClipboard() {
        await clipboard.copy(clipboardData);
        if (clipboard.copied) {
            toast.success('Copy to clipboard succeeded');
        } else {
            toast.error('Copy to clipboard failed');
        }
    }
</script>

{#if hasData}
    <div class={['flex flex-col space-y-2', className]}>
        <div class="flex items-center justify-between">
            <H4>{title}</H4>
            <DropdownMenu.Root>
                <DropdownMenu.Trigger>
                    {#snippet child({ props })}
                        <Button {...props} variant="ghost" size="icon" title="Options">
                            <MoreVertical class="size-4" />
                        </Button>
                    {/snippet}
                </DropdownMenu.Trigger>
                <DropdownMenu.Content align="end">
                    <DropdownMenu.Group>
                        <DropdownMenu.GroupHeading>Actions</DropdownMenu.GroupHeading>
                        <DropdownMenu.Separator />
                        {#if canToggle}
                            <DropdownMenu.Item onclick={onToggleView} title="Toggle between raw and structured view">
                                <ToggleLeft class="mr-2 size-4" />
                                Toggle View
                            </DropdownMenu.Item>
                        {/if}
                        <DropdownMenu.Item onclick={copyToClipboard} title="Copy to clipboard">
                            <Copy class="mr-2 size-4" />
                            Copy to Clipboard
                        </DropdownMenu.Item>
                        {#if canPromote}
                            {#if !isPromoted}
                                <DropdownMenu.Item onclick={async () => await promote(title)} title="Promote to Tab">
                                    <ArrowUp class="mr-2 size-4" />
                                    Promote to Tab
                                </DropdownMenu.Item>
                            {:else}
                                <DropdownMenu.Item onclick={async () => await demote(title)} title="Demote Tab">
                                    <ArrowDown class="mr-2 size-4" />
                                    Demote Tab
                                </DropdownMenu.Item>
                            {/if}
                        {/if}
                    </DropdownMenu.Group>
                </DropdownMenu.Content>
            </DropdownMenu.Root>
        </div>

        <div class="grow overflow-auto text-xs">
            {#if showRaw || !canToggle}
                {#if showJSONCodeEditor}
                    <CodeBlock {code} language="json" />
                {:else if showXmlCodeEditor}
                    <CodeBlock {code} language="xml" />
                {:else}
                    <pre class="bg-muted rounded p-2 break-words whitespace-pre-wrap"><Code class="px-0"><div class="bg-inherit">{clipboardData}</div></Code
                        ></pre>
                {/if}
            {:else}
                <ObjectDump value={filteredData} />
            {/if}
        </div>
    </div>
{/if}
