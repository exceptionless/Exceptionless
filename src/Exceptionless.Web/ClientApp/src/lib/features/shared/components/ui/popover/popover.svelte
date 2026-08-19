<script lang="ts">
	import { browser } from "$app/environment";
	import { Popover as PopoverPrimitive } from "bits-ui";
	import { onDestroy } from "svelte";
	import { INTERACTIVE_OVERLAY_OPENED_EVENT, updateOpenInteractiveOverlay } from "../overlay-events.js";

	let { open = $bindable(false), ...restProps }: PopoverPrimitive.RootProps = $props();
	const overlayId = Symbol("popover");
	let wasOpen = false;

	// CUSTOM: Interactive popovers take precedence over transient hover content.
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

<PopoverPrimitive.Root bind:open {...restProps} />
