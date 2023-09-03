<script lang="ts">
	import EmailInput from '$comp/form/EmailInput.svelte';
	import PasswordInput from '$comp/form/PasswordInput.svelte';

	import IconFacebook from '~icons/mdi/facebook';
	import IconGitHub from '~icons/mdi/github';
	import IconGoogle from '~icons/mdi/google';
	import IconMicrosoft from '~icons/mdi/microsoft';

	import { goto } from '$app/navigation';
	import { page } from '$app/stores';
	import { login, liveLogin, facebookLogin, googleLogin, githubLogin } from '$api/auth';
	import { FetchClient, ProblemDetails, globalLoading } from '$lib/api/FetchClient';
	import { Login, type TokenResult } from '$lib/models/api';

	const client = new FetchClient();
	const data = new Login();
	let loading = client.loading;
	data.invite_token = $page.url.searchParams.get('token');

	let problem = new ProblemDetails();
	const redirectUrl = $page.url.searchParams.get('redirect') ?? '/';

	async function onLogin() {
		if ($loading) return;

		let response = await login(data.email, data.password);

		if (response.ok) {
			await goto(redirectUrl);
		} else {
			problem = response.problem;
		}
	}
</script>

<svelte:head>
	<title>Log in</title>
</svelte:head>

<h2 class="mt-5 text-center text-2xl font-bold leading-9 tracking-tight">Log in to your account</h2>
<form on:submit|preventDefault={onLogin}>
	{#if problem.errors.general}<p class="text-error">{problem.errors.general}</p>{/if}
	<EmailInput name="email" bind:value={data.email} autocomplete="email" required {problem}
	></EmailInput>
	<PasswordInput
		name="password"
		bind:value={data.password}
		autocomplete="current-password"
		minlength={6}
		maxlength={100}
		required
		{problem}
		placeholder="Enter password"
	>
		<span slot="label" class="label-text-alt text-sm">
			<a href="/forgot-password" class="link link-secondary" tabindex="-1">Forgot password?</a
			>
		</span>
	</PasswordInput>
	<div class="my-4">
		<button type="submit" class="btn btn-primary btn-block">
			{#if $globalLoading}
				<span class="loading loading-spinner"></span> Logging in...
			{:else}
				Login
			{/if}
		</button>
	</div>
</form>

<div class="my-4 flex w-full items-center">
	<hr class="w-full" />
	<p class="px-3">OR</p>
	<hr class="w-full" />
</div>
<div class="auto-cols-2 grid grid-flow-col grid-rows-2 gap-4">
	<button class="btn" aria-label="Login with Microsoft" on:click={() => liveLogin(redirectUrl)}>
		<IconMicrosoft /> Microsoft
	</button>
	<button class="btn" aria-label="Login with Google" on:click={() => googleLogin(redirectUrl)}>
		<IconGoogle /> Google
	</button>
	<button
		class="btn"
		aria-label="Login with Facebook"
		on:click={() => facebookLogin(redirectUrl)}
	>
		<IconFacebook /> Facebook
	</button>
	<button class="btn" aria-label="Login with GitHub" on:click={() => githubLogin(redirectUrl)}>
		<IconGitHub /> GitHub
	</button>
</div>

<p class="mt-5 text-center text-sm">
	Not a member?
	<a href="/signup" class="link link-primary">Start a free trial</a>
</p>

<p class="mt-5 text-center text-sm">
	By signing up, you agree to our <a
		href="https://exceptionless.com/privacy"
		target="_blank"
		class="link">Privacy Policy</a
	>
	and
	<a href="https://exceptionless.com/terms" target="_blank" class="link">Terms of Service</a>.
</p>
