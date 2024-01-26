<script lang="ts">
	import IconCellphone from '~icons/mdi/cellphone';
	import IconLaptop from '~icons/mdi/laptop';

	import { Separator } from '$comp/ui/separator';
	import { Button } from '$comp/ui/button';

	import H3 from '$comp/typography/H3.svelte';
	import Muted from '$comp/typography/Muted.svelte';
	import P from '$comp/typography/P.svelte';
	import DateTime from '$comp/formatters/DateTime.svelte';
</script>

<div class="space-y-6">
	<div>
		<H3>Web sessions</H3>
		<Muted
			>This is a list of devices that have logged into your account. Revoke any sessions that
			you do not recognize.</Muted
		>
	</div>
	<Separator />

	<ul class="divide-y divide-border">
		<li class="pb-4">
			<div class="flex items-center space-x-4">
				<IconLaptop class="w-6 h-6" />
				<div class="flex-1 min-w-0">
					<P class="font-semibold">Wisconsin</P>
					<Muted>Signed in on <DateTime value={new Date()}></DateTime></Muted>
				</div>
				<div class="inline-flex items-center">
					<Button aria-label="Revoke user session" variant="outline">Revoke</Button>
				</div>
			</div>
		</li>
		<li class="py-4">
			<div class="flex items-center space-x-4">
				<IconCellphone class="w-6 h-6" />
				<div class="flex-1 min-w-0">
					<P class="font-semibold">Texas</P>
					<Muted>Signed in on <DateTime value={new Date()}></DateTime></Muted>
				</div>
				<div class="inline-flex items-center">
					<Button aria-label="Revoke user session" variant="outline">Revoke</Button>
				</div>
			</div>
		</li>
	</ul>
</div>
