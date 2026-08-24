<script lang="ts">
    import DataTableFooter from './data-table-footer.svelte';
    import DataTablePager from './data-table-pager.svelte';
    import DataTableRoot from './data-table.svelte';

    interface Props {
        onPageIndexChange: (pageIndex: number) => void;
        onPageSizeChange: (pageSize: number) => void;
    }

    let { onPageIndexChange, onPageSizeChange }: Props = $props();

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

<DataTableRoot>
    <DataTableFooter {table}>
        <button type="button">Bulk Actions</button>
        <DataTablePager bind:value={limit} {table} />
    </DataTableFooter>
</DataTableRoot>
