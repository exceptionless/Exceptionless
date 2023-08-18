<script lang="ts">
	import logo from '$lib/assets/exceptionless-350.png';
	import IconMicrosoft from '~icons/mdi/microsoft';
	import IconGoogle from '~icons/mdi/google';
	import IconFacebook from '~icons/mdi/facebook';
	import IconGitHub from '~icons/mdi/github';
</script>

<svelte:head>
	<title>Log in</title>
</svelte:head>

<div class="relative flex h-screen flex-col justify-center overflow-hidden">
	<div class="m-auto w-full rounded-md bg-white p-6 shadow-md lg:max-w-lg">
		<img class="mx-auto w-auto" src={logo} alt="Exceptionless" />
		<h2 class="mt-10 text-center text-2xl font-bold leading-9 tracking-tight">
			Log in to your account
		</h2>
		<form class="space-y-4">
			<div>
				<label for="email" class="label">
					<span class="label-text">Email</span>
				</label>
				<input
					id="email"
					type="email"
					placeholder="Email Address"
					class="input input-bordered input-primary w-full"
					required
				/>
			</div>
			<div>
				<label for="password" class="label">
					<span class="label-text">Password</span>
					<div class="text-sm">
						<a href="/forgot-password" class="link link-secondary">Forgot password?</a>
					</div>
				</label>
				<input
					id="password"
					type="password"
					placeholder="Enter Password"
					class="input input-bordered input-primary w-full"
					required
				/>
			</div>
			<div>
				<button class="btn btn-primary btn-block">Login</button>
			</div>
		</form>

		<div class="my-4 flex w-full items-center">
			<hr class="w-full" />
			<p class="px-3">OR</p>
			<hr class="w-full" />
		</div>
		<div class="auto-cols-2 grid grid-flow-col grid-rows-2 gap-4">
			<button class="btn" aria-label="Login with Microsoft">
				<IconMicrosoft /> Microsoft
			</button>
			<button class="btn" aria-label="Login with Google">
				<IconGoogle /> Google
			</button>
			<button class="btn" aria-label="Login with Facebook">
				<IconFacebook /> Facebook
			</button>
			<button class="btn" aria-label="Login with GitHub">
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
			<a href="https://exceptionless.com/terms" target="_blank" class="link"
				>Terms of Service</a
			>.
		</p>
	</div>
</div>
