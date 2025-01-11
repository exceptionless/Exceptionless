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
            <A class="mr-4 md:mr-6" href="https://exceptionless.com" target="_blank" variant="navigation">Exceptionless</A>
        </li>
        <li>
            <A class="mr-4 md:mr-6" href="https://exceptionless.com/news/" variant="navigation">News</A>
        </li>
        <li>
            <A class="mr-4 md:mr-6" href="https://exceptionless.com/terms/" variant="navigation">Terms of Use</A>
        </li>
        <li>
            <A class="mr-4 md:mr-6" href="https://exceptionless.com/privacy/" variant="navigation">Privacy Policy</A>
        </li>
        <li>
            <A class="mr-4 md:mr-6" href="https://exceptionless.com" variant="navigation">Contact</A>
        </li>
        <li>
            <A href="https://github.com/exceptionless/Exceptionless/releases" variant="navigation">9.0-TODO</A>
        </li>
    </ul>
    <div class="flex space-x-6 sm:justify-center">
        <A aria-label="Exceptionless Facebook Website" href="https://www.facebook.com/exceptionless/" target="_blank" variant="navigation">
            <IconFacebook class="size-5" />
        </A>
        <A aria-label="Exceptionless Twitter Website" href="https://x.com/Exceptionless" target="_blank" variant="navigation">
            <IconTwitter class="size-5" />
        </A>
        <A aria-label="Exceptionless GitHub Website" href="https://github.com/exceptionless" target="_blank" variant="navigation">
            <IconGitHub class="size-5" />
        </A>
        <A aria-label="Exceptionless Website" href="https://exceptionless.com/" target="_blank" variant="navigation">
            <IconWeb class="size-5" />
        </A>
    </div>
</div>
