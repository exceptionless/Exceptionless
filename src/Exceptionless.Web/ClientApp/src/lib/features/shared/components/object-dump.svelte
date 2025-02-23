<script lang="ts">
    import type { Snippet } from 'svelte';

    import * as Table from '$comp/ui/table';

    import ObjectDump from './object-dump.svelte';
    import List from './typography/list.svelte';

    interface Props {
        children?: Snippet<[unknown]>;
        value: unknown;
    }

    let { value }: Props = $props();

    let type = typeof value;
    let isBoolean = type === 'boolean' || value instanceof Boolean;
    let isObject = (type === 'object' || value instanceof Object) && value !== null;
    let isNull = value === null;
    let isEmptyValue = isEmpty(value);

    function isEmpty(value: unknown) {
        if (value === undefined) {
            return true;
        }

        if (value === null) {
            return false;
        }

        if (typeof value === 'object' || value instanceof Object) {
            return Object.keys(value || {}).length === 0;
        }

        if (Array.isArray(value)) {
            return value.length === 0;
        }

        return false;
    }
</script>

{#if isEmptyValue}
    (Empty)
{:else if Array.isArray(value)}
    <List items={value}>
        {#snippet displayValue(item)}
            <ObjectDump value={item} />
        {/snippet}
    </List>
{:else if isObject}
    <Table.Root>
        <Table.Body>
            {#each Object.entries(value || {}) as [key, val] (key)}
                <Table.Row>
                    <Table.Head class="w-48 font-semibold whitespace-nowrap">{key}</Table.Head>
                    <Table.Cell><ObjectDump value={val} /></Table.Cell>
                </Table.Row>
            {/each}
        </Table.Body>
    </Table.Root>
{:else if isBoolean}
    {value ? 'True' : 'False'}
{:else if isNull}
    (Null)
{:else}
    {value}
{/if}
