import Content from './pagination-content.svelte';
import Ellipsis from './pagination-ellipsis.svelte';
import FirstButton from './pagination-first-button.svelte';
import Item from './pagination-item.svelte';
import Link from './pagination-link.svelte';
import Next from './pagination-next.svelte';
import NextButton from './pagination-next-button.svelte';
import PrevButton from './pagination-prev-button.svelte';
import Previous from './pagination-previous.svelte';
import Root from './pagination.svelte';

export {
	Root,
	Content,
	FirstButton,
	Item,
	Link,
	PrevButton, // old
	NextButton, // old
	Ellipsis,
	Previous,
	Next,
	//
	Root as Pagination,
	Content as PaginationContent,
    FirstButton as PaginationFirstButton,
	Item as PaginationItem,
	Link as PaginationLink,
	PrevButton as PaginationPrevButton, // old
	NextButton as PaginationNextButton, // old
	Ellipsis as PaginationEllipsis,
	Previous as PaginationPrevious,
	Next as PaginationNext,
};
