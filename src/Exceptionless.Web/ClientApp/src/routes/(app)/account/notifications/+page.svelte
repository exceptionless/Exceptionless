<script lang="ts">
	import ErrorMessage from '$comp/ErrorMessage.svelte';
	import { Separator } from '$comp/ui/separator';
	import { Button } from '$comp/ui/button';
	import Loading from '$comp/Loading.svelte';

	import H3 from '$comp/typography/H3.svelte';
	import H4 from '$comp/typography/H4.svelte';
	import Muted from '$comp/typography/Muted.svelte';
	import { User } from '$lib/models/api.generated';
	import { ProblemDetails, globalLoading as loading } from '$lib/api/FetchClient';
	import Switch from '$comp/primitives/Switch.svelte';

	const data = new User();
	data.email_notifications_enabled = true;

	let problem = new ProblemDetails();

	async function onSave() {
		if ($loading) {
			return;
		}

		// let response = await save(data);
		// if (response.ok) {
		//     // TODO
		// } else {
		// 	problem = response.problem;
		// }
	}
</script>

<div class="space-y-6">
	<div>
		<H3>Notifications</H3>
		<Muted>Configure how you receive notifications.</Muted>
	</div>
	<Separator />

	<form on:submit|preventDefault={onSave} class="space-y-2">
		<ErrorMessage message={problem.errors.general}></ErrorMessage>

		<H4 class="mb-4">Email Notifications</H4>
		<div class="flex flex-row items-center justify-between rounded-lg border p-4">
			<div class="space-y-0.5">
				<H4>Communication emails</H4>
				<Muted>Receive emails about your account activity.</Muted>
			</div>
			<Switch id="email_notifications_enabled" bind:checked={data.email_notifications_enabled}
			></Switch>
		</div>

		<div class="pt-2">
			<Button type="submit">
				{#if $loading}
					<Loading class="mr-2" variant="secondary"></Loading> Saving...
				{:else}
					Save
				{/if}
			</Button>
		</div>
	</form>
</div>
