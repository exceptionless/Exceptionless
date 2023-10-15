<script lang="ts">
	import {
		ProblemDetails,
		globalFetchClient as api,
		globalLoading as loading
	} from '$api/FetchClient';
	import { goto } from '$app/navigation';
	import { isAuthenticated, logout } from '$api/auth';
	import Loading from '$comp/Loading.svelte';
	import ErrorMessage from '$comp/ErrorMessage.svelte';

	$: if (!$isAuthenticated) {
		goto('next/login', { replaceState: true });
	}

	let problem = new ProblemDetails();

	async function onLogout() {
		if ($loading) {
			return;
		}

		const response = await api.get('auth/logout');
		if (response.ok) {
			await logout();
			await goto('/next/login');
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
	<ErrorMessage message={problem.errors.general}></ErrorMessage>
	<div class="my-4">
		<button type="submit" class="btn btn-primary btn-block">
			{#if $loading}
				<Loading></Loading> Logging out...
			{:else}
				Logout
			{/if}
		</button>
	</div>
</form>
