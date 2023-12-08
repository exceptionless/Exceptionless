<script lang="ts">
	import A from '$comp/typography/A.svelte';
	import Muted from '$comp/typography/Muted.svelte';
	import * as Card from '$comp/ui/card';

	import IconFacebook from '~icons/mdi/facebook';
	import IconGitHub from '~icons/mdi/github';
	import IconTwitter from '~icons/mdi/twitter';
	import IconWeb from '~icons/mdi/web';

	const currentYear = new Date().getFullYear();
</script>

<Card.Root class="mx-4 my-6">
	<Card.Content class="pt-6 mx-4 md:flex md:items-center md:justify-between">
		<Muted>
			<ul class="flex flex-wrap items-center mb-6 md:mb-0">
				<li>
					<A
						href="https://exceptionless.com/news/"
						class="mr-4 no-underline md:mr-6 hover:underline hover:text-foreground"
						>News</A
					>
				</li>
				<li>
					<A
						href="https://exceptionless.com/terms/"
						class="mr-4 no-underline md:mr-6 hover:underline hover:text-foreground"
						>Terms of Use</A
					>
				</li>
				<li>
					<A
						href="https://exceptionless.com/privacy/"
						class="mr-4 no-underline md:mr-6 hover:underline hover:text-foreground"
						>Privacy Policy</A
					>
				</li>
				<li>
					<A
						href="https://exceptionless.com"
						class="mr-4 no-underline md:mr-6 hover:underline hover:text-foreground"
						>Contact</A
					>
				</li>
				<li>
					<A
						href="https://github.com/exceptionless/Exceptionless/releases"
						class="no-underline hover:underline hover:text-foreground">9.0-TODO</A
					>
				</li>
			</ul>
		</Muted>
		<Muted class="flex space-x-6 sm:justify-center">
			<A
				href="https://www.facebook.com/exceptionless/"
				target="_blank"
				class="hover:text-foreground"
				aria-label="Exceptionless Facebook Website"
			>
				<IconFacebook class="w-5 h-5" />
			</A>
			<A
				href="https://x.com/Exceptionless"
				target="_blank"
				class="hover:text-foreground"
				aria-label="Exceptionless Twitter Website"
			>
				<IconTwitter class="w-5 h-5" />
			</A>
			<A
				href="https://github.com/exceptionless"
				target="_blank"
				class="hover:text-foreground"
				aria-label="Exceptionless GitHub Website"
			>
				<IconGitHub class="w-5 h-5" />
			</A>
			<A
				href="https://exceptionless.com/"
				class="hover:text-foreground"
				target="_blank"
				aria-label="Exceptionless Website"
			>
				<IconWeb class="w-5 h-5" />
			</A>
		</Muted>
	</Card.Content>
</Card.Root>
<Muted class="my-10 text-center">
	&copy; {currentYear}
	<A href="https://exceptionless.com" class="no-underline" target="_blank">Exceptionless</A>. All
	rights reserved.
</Muted>
