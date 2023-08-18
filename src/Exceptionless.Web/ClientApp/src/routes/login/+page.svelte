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
					<span class="label-text text-base">Email</span>
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
					<span class="label-text text-base">Password</span>
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
			<!-- <button
				class="flex w-full items-center justify-center space-x-4 rounded-md border p-2 focus:ring-2 focus:ring-gray-400 focus:ring-offset-1"
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					viewBox="0 0 32 32"
					class="h-5 w-5 fill-current"
				>
					<path
						d="M16 0.396c-8.839 0-16 7.167-16 16 0 7.073 4.584 13.068 10.937 15.183 0.803 0.151 1.093-0.344 1.093-0.772 0-0.38-0.009-1.385-0.015-2.719-4.453 0.964-5.391-2.151-5.391-2.151-0.729-1.844-1.781-2.339-1.781-2.339-1.448-0.989 0.115-0.968 0.115-0.968 1.604 0.109 2.448 1.645 2.448 1.645 1.427 2.448 3.744 1.74 4.661 1.328 0.14-1.031 0.557-1.74 1.011-2.135-3.552-0.401-7.287-1.776-7.287-7.907 0-1.751 0.62-3.177 1.645-4.297-0.177-0.401-0.719-2.031 0.141-4.235 0 0 1.339-0.427 4.4 1.641 1.281-0.355 2.641-0.532 4-0.541 1.36 0.009 2.719 0.187 4 0.541 3.043-2.068 4.381-1.641 4.381-1.641 0.859 2.204 0.317 3.833 0.161 4.235 1.015 1.12 1.635 2.547 1.635 4.297 0 6.145-3.74 7.5-7.296 7.891 0.556 0.479 1.077 1.464 1.077 2.959 0 2.14-0.020 3.864-0.020 4.385 0 0.416 0.28 0.916 1.104 0.755 6.4-2.093 10.979-8.093 10.979-15.156 0-8.833-7.161-16-16-16z"
					>
					</path>
				</svg>
				<p>Login with GitHub</p>
			</button> -->
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
