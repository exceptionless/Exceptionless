<script lang="ts">
	import { browser } from "$app/environment";
	import { DropdownMenu as DropdownMenuPrimitive } from "bits-ui";
	import { onDestroy } from "svelte";
	import { INTERACTIVE_OVERLAY_OPENED_EVENT, updateOpenInteractiveOverlay } from "../overlay-events.js";

	let { open = $bindable(false), ...restProps }: DropdownMenuPrimitive.RootProps = $props();
	const overlayId = Symbol("dropdown-menu");
	let wasOpen = false;

	// CUSTOM: Interactive menus take precedence over transient hover content.
	$effect(() => {
		if (!browser || open === wasOpen) {
			return;
		}

		updateOpenInteractiveOverlay(overlayId, open);
		if (open) {
			document.dispatchEvent(new Event(INTERACTIVE_OVERLAY_OPENED_EVENT));
		}

		wasOpen = open;
	});

	onDestroy(() => updateOpenInteractiveOverlay(overlayId, false));
</script>

<DropdownMenuPrimitive.Root bind:open {...restProps} />
