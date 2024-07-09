<script lang="ts">
    import ArrowDownIcon from '~icons/mdi/arrow-down';
    import ArrowUpIcon from '~icons/mdi/arrow-up';
    import CopyToClipboardButton from '$comp/CopyToClipboardButton.svelte';
    import ObjectDump from '$comp/ObjectDump.svelte';
    import { Button } from '$comp/ui/button';
    import { Code, H4 } from '$comp/typography';

    interface Props {
        title: string;
        data: unknown;
        canPromote?: boolean;
        isPromoted?: boolean;
        excludedKeys?: string[];
        promote?: (title: string) => void;
        demote?: (title: string) => void;
    }

    let { title, data, canPromote = true, isPromoted = false, excludedKeys = [], promote = () => {}, demote = () => {} }: Props = $props();

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
            <Button variant="outline" on:click={onToggleView}>Toggle View</Button>

            <CopyToClipboardButton value={json}></CopyToClipboardButton>

            {#if canPromote}
                {#if !isPromoted}
                    <Button size="icon" on:click={() => promote(title)} title="Promote to Tab"><ArrowUpIcon /></Button>
                {:else}
                    <Button size="icon" on:click={() => demote(title)} title="Demote Tab"><ArrowDownIcon /></Button>
                {/if}
            {/if}
        </div>
    </div>

    {#if showRaw}
        <pre class="overflow-auto whitespace-pre-wrap break-words p-2 text-xs"><Code>{json}</Code></pre>
    {:else}
        <ObjectDump value={filteredData} />
    {/if}
{/if}
