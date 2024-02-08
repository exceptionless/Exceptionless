import { writable, type Writable } from 'svelte/store';
import { persisted } from 'svelte-local-storage-store';
import { mediaQuery } from 'svelte-legos';

export const isSidebarOpen = persisted('sidebar-open', false);
export const isPageWithSidebar: Writable<boolean> = writable(true);
export const isCommandOpen: Writable<boolean> = writable(false);

// Helper functions
export const isSmallScreen = mediaQuery('(min-width: 640px)');
export const isMediumScreen = mediaQuery('(min-width: 768px)');
export const isLargeScreen = mediaQuery('(min-width: 1024px)');
