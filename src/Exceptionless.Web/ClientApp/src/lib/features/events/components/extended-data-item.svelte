<script lang="ts">
    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import ObjectDump from '$comp/object-dump.svelte';
    import { Code, H4 } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import ArrowDown from '@lucide/svelte/icons/arrow-down';
    import ArrowUp from '@lucide/svelte/icons/arrow-up';

    interface Props {
        canPromote?: boolean;
        data: unknown;
        demote?: (title: string) => Promise<void>;
        excludedKeys?: string[];
        isPromoted?: boolean;
        promote?: (title: string) => Promise<void>;
        title: string;
    }

    let { canPromote = true, data, demote = async () => {}, excludedKeys = [], isPromoted = false, promote = async () => {}, title }: Props = $props();

    function getData(data: unknown, exclusions: string[]): unknown {
        if (typeof data !== 'object' || !(data instanceof Object)) {
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

    function hasFilteredData(data: unknown): boolean {
        if (data === undefined || data === null) {
            return false;
        }

        if (Array.isArray(data)) {
            return data.length > 0;
        }

        if (Object.prototype.toString.call(data) === '[object Object]') {
            return Object.keys(data).length > 0;
        }

        return true;
    }

    function onToggleView(e: Event) {
        e.preventDefault();
        showRaw = !showRaw;
    }

    let showRaw = $state(false);
    let filteredData = getData(data, excludedKeys);
    let hasData = hasFilteredData(filteredData);
    let json = data ? JSON.stringify(data, null, 2) : null;
</script>

{#if hasData}
    <div class="flex justify-between">
        <H4 class="mb-2">{title}</H4>
        <div class="flex justify-end gap-x-1">
            <Button onclick={onToggleView} variant="outline">Toggle View</Button>

            <CopyToClipboardButton value={json}></CopyToClipboardButton>

            {#if canPromote}
                {#if !isPromoted}
                    <Button onclick={async () => await promote(title)} size="icon" title="Promote to Tab"
                        ><ArrowUp /><span class="sr-only">Promote to Tab</span></Button
                    >
                {:else}
                    <Button onclick={async () => await demote(title)} size="icon" title="Demote Tab"
                        ><ArrowDown /><span class="sr-only">Demote Tab</span></Button
                    >
                {/if}
            {/if}
        </div>
    </div>

    <div class="grow overflow-auto text-xs">
        {#if showRaw}
            <pre class="bg-muted rounded p-2 break-words whitespace-pre-wrap"><Code class="px-0"><div class="bg-inherit">{json}</div></Code></pre>
        {:else}
            <ObjectDump value={filteredData} />
        {/if}
    </div>
{/if}
