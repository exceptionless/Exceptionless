<script lang="ts">
    import { A } from '$comp/typography';

    import IconFacebook from '~icons/mdi/facebook';
    import IconGitHub from '~icons/mdi/github';
    import IconTwitter from '~icons/mdi/twitter';
    import IconWeb from '~icons/mdi/web';

    const currentYear = new Date().getFullYear();
</script>

<div class="mx-4 my-6 text-sm text-muted-foreground md:flex md:items-center md:justify-between">
    <ul class="mb-6 flex flex-wrap items-center md:mb-0">
        <li>
            &copy; {currentYear}
            <A href="https://exceptionless.com" variant="navigation" target="_blank" class="mr-4 md:mr-6">Exceptionless</A>
        </li>
        <li>
            <A href="https://exceptionless.com/news/" variant="navigation" class="mr-4 md:mr-6">News</A>
        </li>
        <li>
            <A href="https://exceptionless.com/terms/" variant="navigation" class="mr-4 md:mr-6">Terms of Use</A>
        </li>
        <li>
            <A href="https://exceptionless.com/privacy/" variant="navigation" class="mr-4 md:mr-6">Privacy Policy</A>
        </li>
        <li>
            <A href="https://exceptionless.com" variant="navigation" class="mr-4 md:mr-6">Contact</A>
        </li>
        <li>
            <A href="https://github.com/exceptionless/Exceptionless/releases" variant="navigation">9.0-TODO</A>
        </li>
    </ul>
    <div class="flex space-x-6 sm:justify-center">
        <A href="https://www.facebook.com/exceptionless/" target="_blank" variant="navigation" aria-label="Exceptionless Facebook Website">
            <IconFacebook class="h-5 w-5" />
        </A>
        <A href="https://x.com/Exceptionless" target="_blank" variant="navigation" aria-label="Exceptionless Twitter Website">
            <IconTwitter class="h-5 w-5" />
        </A>
        <A href="https://github.com/exceptionless" target="_blank" variant="navigation" aria-label="Exceptionless GitHub Website">
            <IconGitHub class="h-5 w-5" />
        </A>
        <A href="https://exceptionless.com/" target="_blank" variant="navigation" aria-label="Exceptionless Website">
            <IconWeb class="h-5 w-5" />
        </A>
    </div>
</div>
