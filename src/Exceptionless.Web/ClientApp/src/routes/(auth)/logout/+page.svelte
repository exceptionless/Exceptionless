<script lang="ts">
	import { FetchClient, ProblemDetails } from '$api/FetchClient';
	import { goto } from '$app/navigation';
	import { isAuthenticated, logout } from '$lib/api/Auth';

	$: if (!$isAuthenticated) {
		goto('/login', { replaceState: true });
	}

	const api = new FetchClient();
	const loading = api.loading;
	let problem = new ProblemDetails();

	async function onLogout() {
		if ($loading) {
			return;
		}

		const response = await api.get('auth/logout');
		if (response.ok) {
			await logout();
			await goto('/login');
		} else {
			problem = problem.setErrorMessage(
				'An error occurred while logging out, please try again.'
			);
		}
	}
</script>

<svelte:head>
	<title>Log out</title>
</svelte:head>

<h2 class="mt-5 text-center text-2xl font-bold leading-9 tracking-tight">Log out?</h2>
<form on:submit|preventDefault={onLogout}>
	{#if problem.errors.general}<p class="text-error">{problem.errors.general}</p>{/if}
	<div class="my-4">
		<button type="submit" class="btn btn-primary btn-block">
			{#if $loading}
				<span class="loading loading-spinner"></span> Logging out...
			{:else}
				Logout
			{/if}
		</button>
	</div>
</form>
