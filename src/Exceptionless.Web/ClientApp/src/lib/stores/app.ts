import { readable, writable, type Writable } from 'svelte/store';
import { persisted } from 'svelte-persisted-store';

export const isSidebarOpen = persisted('sidebar-open', false);
export const isPageWithSidebar: Writable<boolean> = writable(true);
export const isCommandOpen: Writable<boolean> = writable(false);

// Helper functions
export const isSmallScreen = readable(false); //mediaQuery('(min-width: 640px)');
export const isMediumScreen = readable(false); //mediaQuery('(min-width: 768px)');
export const isLargeScreen = readable(true); // mediaQuery('(min-width: 1024px)');
