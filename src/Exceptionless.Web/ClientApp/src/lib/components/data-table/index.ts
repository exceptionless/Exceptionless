import Root from './data-table.svelte';
import Body from './data-table-body.svelte';
import Toolbar from './data-table-toolbar.svelte';
import FacetedFilter from './data-table-faceted-filter.svelte';
import Pagination from './data-table-pagination.svelte';

import PriorityCell from './data-table-priority-cell.svelte';
import StatusCell from './data-table-status-cell.svelte';
import TitleCell from './data-table-title-cell.svelte';

export {
	Root,
	Body,
	Toolbar,
	FacetedFilter,
	Pagination,
	PriorityCell,
	StatusCell,
	TitleCell,
	//
	Root as DataTable,
	Body as DataTableBody,
	Toolbar as DataTableToolbar,
	FacetedFilter as DataTableFacetedFilter,
	Pagination as DataTablePagination,
	PriorityCell as DataTablePriorityCell,
	StatusCell as DataTableStatusCell,
	TitleCell as DataTableTitleCell
};
