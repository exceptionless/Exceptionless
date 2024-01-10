import Root from './data-table.svelte';
import Body from './data-table-body.svelte';
import Toolbar from './data-table-toolbar.svelte';
import FacetedFilter from './data-table-faceted-filter.svelte';
import PageSize from './data-table-page-size.svelte';
import Pagination from './data-table-pagination.svelte';

import StatusCell from './data-table-status-cell.svelte';

export {
	Root,
	Body,
	Toolbar,
	FacetedFilter,
	PageSize,
	Pagination,
	StatusCell,
	//
	Root as DataTable,
	Body as DataTableBody,
	Toolbar as DataTableToolbar,
	FacetedFilter as DataTableFacetedFilter,
	PageSize as DataTablePageSize,
	Pagination as DataTablePagination,
	StatusCell as DataTableStatusCell
};
