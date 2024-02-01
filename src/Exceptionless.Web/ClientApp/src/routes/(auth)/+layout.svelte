<script lang="ts">
    import IconExceptionlessLogo from '~icons/exceptionless/logo';
    import IconExceptionlessLogoDark from '~icons/exceptionless/logo-dark';
</script>

<div class="flex h-screen">
	<div class="w-full p-6 m-auto rounded-md shadow-md lg:max-w-lg">
        <IconExceptionlessLogo class="mx-auto dark:hidden" />
        <IconExceptionlessLogoDark class="hidden mx-auto dark:block" />

		<slot />
	</div>
</div>
