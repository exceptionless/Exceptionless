<script lang="ts">
	import type { HTMLAnchorAttributes } from 'svelte/elements';
	import { isLargeScreen, isSidebarOpen } from '$lib/stores/sidebar';
	import { page } from '$app/stores';
	import { derived } from 'svelte/store';

	export let href: HTMLAnchorAttributes['href'];
	export let title: string;

	const active = derived(page, ($page) => $page.url.pathname == href);
</script>

<a
	{href}
	{title}
	class="group flex items-center rounded-lg p-2 text-base font-normal hover:bg-accent hover:text-accent-foreground"
	class:bg-accent={$active}
	class:text-accent-foreground={$active}
>
	<slot
		name="icon"
		iconClass="w-6 h-6 transition duration-75 text-muted-foreground group-hover:text-foreground {$active
			? 'text-foreground'
			: ''}"
	/>
	<span class="ml-3 {!$isSidebarOpen && $isLargeScreen ? 'lg:absolute lg:hidden' : ''}"
		>{title}</span
	>
</a>
