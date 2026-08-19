<script lang="ts">
	import { browser } from "$app/environment";
	import { Tooltip as TooltipPrimitive } from "bits-ui";
	import { onDestroy } from "svelte";
	import { hasOpenInteractiveOverlay, INTERACTIVE_OVERLAY_OPENED_EVENT } from "../overlay-events.js";

	let {
		open = $bindable(false),
		suppressWhenOverlayOpen = false,
		...restProps
	}: TooltipPrimitive.RootProps & { suppressWhenOverlayOpen?: boolean } = $props();

	function setOpen(nextOpen: boolean): void {
		open = nextOpen && !(suppressWhenOverlayOpen && hasOpenInteractiveOverlay());
	}

	// CUSTOM: Do not let a previously opened tooltip obstruct an interactive overlay opened above it.
	if (browser) {
		const handleInteractiveOverlayOpened = () => (open = false);
		document.addEventListener(INTERACTIVE_OVERLAY_OPENED_EVENT, handleInteractiveOverlayOpened);
		onDestroy(() => document.removeEventListener(INTERACTIVE_OVERLAY_OPENED_EVENT, handleInteractiveOverlayOpened));
	}
</script>

<TooltipPrimitive.Root bind:open={() => open, setOpen} {...restProps} />
