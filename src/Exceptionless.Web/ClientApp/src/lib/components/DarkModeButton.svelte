<script lang="ts">
	import IconMoonWaningCrescent from '~icons/mdi/moon-waning-crescent';
	import IconWhiteBalanceSunny from '~icons/mdi/white-balance-sunny';
	import { Button } from '$comp/ui/button';

	import { toggleMode } from 'mode-watcher';
</script>

<Button on:click={toggleMode} variant="outline" size="icon" title="Toggle dark mode">
	<IconWhiteBalanceSunny
		class="h-[1.2rem] w-[1.2rem] rotate-0 scale-100 transition-all dark:-rotate-90 dark:scale-0"
	/>
	<IconMoonWaningCrescent
		class="absolute h-[1.2rem] w-[1.2rem] rotate-90 scale-0 transition-all dark:rotate-0 dark:scale-100"
	/>
	<span class="sr-only">Toggle theme</span>
</Button>
