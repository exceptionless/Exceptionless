<script lang="ts">
    import DataTableFooter from './data-table-footer.svelte';
    import DataTablePager from './data-table-pager.svelte';
    import DataTableRoot from './data-table.svelte';

    interface Props {
        onPageIndexChange: (pageIndex: number) => void;
        onPageSizeChange: (pageSize: number) => void;
        variant?: 'floating' | 'simple';
    }

    let { onPageIndexChange, onPageSizeChange, variant = 'simple' }: Props = $props();

    let limit = $state(10);
    let pageIndex = $state(0);

    const table = {
        getPageCount: () => 3,
        options: {
            state: {
                pagination: {
                    get pageIndex() {
                        return pageIndex;
                    }
                }
            }
        },
        setPageIndex: (value: number) => {
            pageIndex = value;
            onPageIndexChange(value);
        },
        setPageSize: (value: number) => {
            limit = value;
            onPageSizeChange(value);
        },
        store: {
            state: {
                pagination: {
                    get pageIndex() {
                        return pageIndex;
                    }
                }
            }
        }
    } as never;
</script>

<div data-testid="scroll-container" style="height: 200px; overflow-y: auto;">
    <DataTableRoot>
        <DataTableFooter {table} {variant}>
            <button type="button">Actions</button>
            <DataTablePager bind:value={limit} {table} {variant} />
        </DataTableFooter>
        <div data-slot="data-table-body">
            <table>
                <tbody>
                    <tr class="hidden">
                        <td>No data</td>
                    </tr>
                    <tr>
                        <td>First row</td>
                    </tr>
                </tbody>
            </table>
        </div>
    </DataTableRoot>
</div>
