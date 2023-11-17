<script lang="ts">
	import {
		Footer,
		FooterCopyright,
		FooterIcon,
		FooterLinkGroup,
		FooterLink
	} from 'flowbite-svelte';

	import IconFacebook from '~icons/mdi/facebook';
	import IconGitHub from '~icons/mdi/github';
	import IconTwitter from '~icons/mdi/twitter';
	import IconWeb from '~icons/mdi/web';
</script>

<Footer
	class="p-4 my-6 mx-4 bg-white rounded-lg shadow md:flex md:items-center md:justify-between md:p-6 xl:p-8 dark:bg-gray-800"
>
	<FooterLinkGroup ulClass="flex flex-wrap items-center mb-6 space-y-1 md:mb-0">
		<FooterLink
			href="https://exceptionless.com/news/"
			aClass="mr-4 text-sm font-normal text-gray-500 hover:underline md:mr-6 dark:text-gray-400"
			target="_blank">News</FooterLink
		>
		<FooterLink
			href="https://exceptionless.com/terms/"
			aClass="mr-4 text-sm font-normal text-gray-500 hover:underline md:mr-6 dark:text-gray-400"
			target="_blank">Terms of Use</FooterLink
		>
		<FooterLink
			href="https://exceptionless.com/privacy/"
			aClass="mr-4 text-sm font-normal text-gray-500 hover:underline md:mr-6 dark:text-gray-400"
			target="_blank">Privacy Policy</FooterLink
		>
		<FooterLink
			href="/next"
			aClass="mr-4 text-sm font-normal text-gray-500 hover:underline md:mr-6 dark:text-gray-400"
			>Contact</FooterLink
		>
		<FooterLink
			aClass="mr-4 text-sm font-normal text-gray-500 hover:underline md:mr-6 dark:text-gray-400"
			href="https://github.com/exceptionless/Exceptionless/releases"
			target="_blank">9.0-TODO</FooterLink
		>
	</FooterLinkGroup>

	<div class="flex space-x-6 sm:justify-center">
		<FooterIcon
			href="https://www.facebook.com/exceptionless/"
			class="text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white"
			target="_blank"
			ariaLabel="Exceptionless Facebook Website"
		>
			<IconFacebook class="w-5 h-5" />
		</FooterIcon>
		<FooterIcon
			href="https://x.com/Exceptionless"
			class="text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white"
			target="_blank"
			ariaLabel="Exceptionless Twitter Website"
		>
			<IconTwitter class="w-5 h-5" />
		</FooterIcon>
		<FooterIcon
			href="https://github.com/exceptionless"
			class="text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white"
			target="_blank"
			ariaLabel="Exceptionless GitHub Website"
		>
			<IconGitHub class="w-5 h-5" />
		</FooterIcon>
		<FooterIcon
			href="https://exceptionless.com/"
			class="text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white"
			target="_blank"
			ariaLabel="Exceptionless Website"
		>
			<IconWeb class="w-5 h-5" />
		</FooterIcon>
	</div>
</Footer>

<div class="my-10 text-sm text-center text-gray-500">
	<FooterCopyright href="https://exceptionless.com" by="Exceptionless" target="_blank" />
</div>
