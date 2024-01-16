<script lang="ts">
	import { Dialog as DialogPrimitive } from 'bits-ui';
</script>

<DialogPrimitive.Portal {...$$restProps}>
	<slot />
</DialogPrimitive.Portal>
