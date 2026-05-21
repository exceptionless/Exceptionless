<script lang="ts">
    import { Muted } from '$comp/typography';
    import * as Dialog from '$comp/ui/dialog';
    import * as Kbd from '$comp/ui/kbd';
    import { appKeyboardShortcuts, formatKeyboardShortcut, type ShortcutKey } from '$features/shared/keyboard-shortcuts';

    type ShortcutRow = {
        action: string;
        shortcuts: readonly (readonly ShortcutKey[])[];
    };

    type ShortcutSection = {
        rows: readonly ShortcutRow[];
        title: string;
    };

    type Props = {
        open: boolean;
    };

    let { open = $bindable() }: Props = $props();

    const shortcutSections: ShortcutSection[] = [
        {
            rows: [
                { action: 'Go to Issues', shortcuts: [appKeyboardShortcuts.issues.keys] },
                { action: 'Go to All Events', shortcuts: [appKeyboardShortcuts.allEvents.keys] },
                { action: 'Show Keyboard Shortcuts', shortcuts: [appKeyboardShortcuts.keyboardShortcuts.keys] }
            ],
            title: 'Navigation'
        },
        {
            rows: [
                { action: 'Open Organization Switcher', shortcuts: [appKeyboardShortcuts.switchOrganization.keys] },
                { action: 'Open User Menu', shortcuts: [appKeyboardShortcuts.userMenu.keys] }
            ],
            title: 'Menus'
        },
        {
            rows: [
                { action: 'Open Command Palette', shortcuts: [appKeyboardShortcuts.commandPalette.keys] },
                { action: 'Open Selected Command', shortcuts: [['Enter']] },
                { action: 'Close Command Palette', shortcuts: [['Esc']] }
            ],
            title: 'Command Palette'
        },
        {
            rows: [
                { action: 'Open Selected Row', shortcuts: [['Enter']] },
                { action: 'Toggle Selected Row', shortcuts: [['Space']] },
                { action: 'Close Menus and Popovers', shortcuts: [['Esc']] }
            ],
            title: 'Lists and Tables'
        }
    ];

    function shortcutLabel(keys: readonly ShortcutKey[]): string {
        return formatKeyboardShortcut(keys);
    }
</script>

<Dialog.Root bind:open>
    <Dialog.Content
        preventScroll={false}
        overlayClass="bg-black/20 supports-backdrop-filter:backdrop-blur-none"
        class="max-h-[min(42rem,calc(100vh-2rem))] gap-0 overflow-hidden p-0 sm:max-w-3xl"
    >
        <Dialog.Header class="border-b px-4 py-3 sm:px-5">
            <Dialog.Title class="text-base">Keyboard Shortcuts</Dialog.Title>
            <Dialog.Description>
                <Muted class="text-xs">Quick actions available from the app shell.</Muted>
            </Dialog.Description>
        </Dialog.Header>

        <div class="grid gap-4 overflow-y-auto p-4 md:grid-cols-2 md:p-5">
            {#each shortcutSections as section (section.title)}
                <section aria-labelledby={`shortcut-section-${section.title.toLowerCase().replaceAll(' ', '-')}`} class="overflow-hidden rounded-md border">
                    <h2
                        id={`shortcut-section-${section.title.toLowerCase().replaceAll(' ', '-')}`}
                        class="bg-muted/60 border-b px-3 py-2 text-sm font-semibold"
                    >
                        {section.title}
                    </h2>
                    <div class="divide-y">
                        {#each section.rows as row (row.action)}
                            <div class="grid min-h-10 grid-cols-[minmax(0,1fr)_auto] items-center gap-3 px-3 py-2.5 text-sm">
                                <span class="min-w-0 leading-5">{row.action}</span>
                                <div class="flex items-center gap-1.5">
                                    {#each row.shortcuts as shortcut, index (shortcut.join('+'))}
                                        {#if index > 0}
                                            <span class="text-muted-foreground text-xs">or</span>
                                        {/if}
                                        <Kbd.Group>
                                            <Kbd.Root>{shortcutLabel(shortcut)}</Kbd.Root>
                                        </Kbd.Group>
                                    {/each}
                                </div>
                            </div>
                        {/each}
                    </div>
                </section>
            {/each}
        </div>
    </Dialog.Content>
</Dialog.Root>
