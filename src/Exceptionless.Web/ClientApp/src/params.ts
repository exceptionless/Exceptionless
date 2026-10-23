import { defineParams } from '@sveltejs/kit/params';

const stackActions = ['discarded', 'ignored', 'mark-fixed'] as const;

export const params = defineParams({
    objectid: (param) => (/^[a-fA-F0-9]{24}$/.test(param) ? param : undefined),
    // Saved view slugs share the URL position with object IDs, so a 24-character hex value is never a slug.
    savedview: (param) => (/^(?![a-fA-F0-9]{24}$)[a-z0-9]+(?:-[a-z0-9]+)*$/.test(param) ? param : undefined),
    stackaction: (param) => stackActions.find((action) => action === param)
});
