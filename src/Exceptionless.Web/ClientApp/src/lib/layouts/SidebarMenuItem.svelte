<script lang="ts">
	import type { HTMLAnchorAttributes } from 'svelte/elements';
	import { isLargeScreen, isSidebarOpen } from '$lib/stores/sidebar';

	export let href: HTMLAnchorAttributes['href'];
	export let name: string;
</script>

<a
	{href}
	title={name}
	class="flex items-center p-2 text-base font-normal rounded-lg group hover:bg-accent hover:text-accent-foreground"
>
	<slot
		name="icon"
		iconClass="w-6 h-6 transition duration-75 text-muted-foreground group-hover:text-foreground"
	/>
	<span class="ml-3 {!$isSidebarOpen && $isLargeScreen ? 'lg:hidden lg:absolute' : ''}"
		>{name}</span
	>
</a>
