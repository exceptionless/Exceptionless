<script lang="ts">
    import * as Table from '$comp/ui/table';
    import type { Snippet } from 'svelte';
    import List from './typography/List.svelte';

    interface Props {
        value: unknown;
        children?: Snippet<[unknown]>;
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
        {#snippet children(item)}
            <svelte:self value={item} />
        {/snippet}
    </List>
{:else if isObject}
    <Table.Root>
        <Table.Body>
            {#each Object.entries(value || {}) as [key, val] (key)}
                <Table.Row>
                    <Table.Head class="w-48 whitespace-nowrap">{key}</Table.Head>
                    <Table.Cell><svelte:self value={val} /></Table.Cell>
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
