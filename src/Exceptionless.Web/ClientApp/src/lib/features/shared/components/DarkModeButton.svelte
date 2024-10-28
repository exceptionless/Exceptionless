<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as ContextMenu from '$comp/ui/context-menu';
    import IconMoonWaningCrescent from '~icons/mdi/moon-waning-crescent';
    import IconWhiteBalanceSunny from '~icons/mdi/white-balance-sunny';
    import { setMode, toggleMode, userPrefersMode } from 'mode-watcher';

    function onUserThemePreferenceChange(mode?: string) {
        setMode(mode as 'dark' | 'light' | 'system');
    }
</script>

<ContextMenu.Root>
    <ContextMenu.Trigger>
        <Button onclick={toggleMode} size="icon" title="Toggle dark mode" variant="outline">
            <IconWhiteBalanceSunny class="h-[1.2rem] w-[1.2rem] rotate-0 scale-100 transition-all dark:-rotate-90 dark:scale-0" />
            <IconMoonWaningCrescent class="absolute h-[1.2rem] w-[1.2rem] rotate-90 scale-0 transition-all dark:rotate-0 dark:scale-100" />
            <span class="sr-only">Toggle theme</span>
        </Button>
    </ContextMenu.Trigger>
    <ContextMenu.Content>
        <ContextMenu.RadioGroup onValueChange={onUserThemePreferenceChange} value={$userPrefersMode}>
            <ContextMenu.RadioItem value="light">Light</ContextMenu.RadioItem>
            <ContextMenu.RadioItem value="dark">Dark</ContextMenu.RadioItem>
            <ContextMenu.RadioItem value="system">System</ContextMenu.RadioItem>
        </ContextMenu.RadioGroup>
    </ContextMenu.Content>
</ContextMenu.Root>
