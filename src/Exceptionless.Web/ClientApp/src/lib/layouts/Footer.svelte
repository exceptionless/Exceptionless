<script lang="ts">
	import * as Card from '$comp/ui/card';

	import IconFacebook from '~icons/mdi/facebook';
	import IconGitHub from '~icons/mdi/github';
	import IconTwitter from '~icons/mdi/twitter';
	import IconWeb from '~icons/mdi/web';
</script>

<Card.Root class="mx-4 my-6">
	<Card.Content class="pt-6 mx-4 md:flex md:items-center md:justify-between">
		<ul class="flex flex-wrap items-center mb-6 space-y-1 md:mb-0">
			<li>
				<a
					href="https://exceptionless.com/news/"
					class="mr-4 text-sm font-normal hover:underline md:mr-6">News</a
				>
			</li>
			<li>
				<a
					href="https://exceptionless.com/terms/"
					class="mr-4 text-sm font-normal hover:underline md:mr-6">Terms of Use</a
				>
			</li>
			<li>
				<a
					href="https://exceptionless.com/privacy/"
					class="mr-4 text-sm font-normal hover:underline md:mr-6">Privacy Policy</a
				>
			</li>
			<li>
				<a
					href="https://exceptionless.com"
					class="mr-4 text-sm font-normal hover:underline md:mr-6">Contact</a
				>
			</li>
			<li>
				<a
					href="https://github.com/exceptionless/Exceptionless/releases"
					class="text-sm font-normal hover:underline">9.0-TODO</a
				>
			</li>
		</ul>
		<div class="flex space-x-6 sm:justify-center">
			<a
				href="https://www.facebook.com/exceptionless/"
				target="_blank"
				class="text-muted-foreground hover:text-foreground"
				aria-label="Exceptionless Facebook Website"
			>
				<IconFacebook class="w-5 h-5" />
			</a>
			<a
				href="https://x.com/Exceptionless"
				target="_blank"
				class="text-muted-foreground hover:text-foreground"
				aria-label="Exceptionless Twitter Website"
			>
				<IconTwitter class="w-5 h-5" />
			</a>
			<a
				href="https://github.com/exceptionless"
				target="_blank"
				class="text-muted-foreground hover:text-foreground"
				aria-label="Exceptionless GitHub Website"
			>
				<IconGitHub class="w-5 h-5" />
			</a>
			<a
				href="https://exceptionless.com/"
				class="text-muted-foreground hover:text-foreground"
				target="_blank"
				aria-label="Exceptionless Website"
			>
				<IconWeb class="w-5 h-5" />
			</a>
		</div>
	</Card.Content>
</Card.Root>
<p class="my-10 text-sm text-center text-gray-500">
	&copy; 2023 <a href="https://exceptionless.com" class="hover:underline" target="_blank"
		>Exceptionless</a
	>. All rights reserved.
</p>
