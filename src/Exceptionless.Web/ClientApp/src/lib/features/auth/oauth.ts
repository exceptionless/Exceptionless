export const mcpReadScope = 'mcp:read';
export const offlineAccessScope = 'offline_access';

export function formatOAuthScope(scope: string): string {
    switch (scope) {
        case 'events:read':
            return 'Events Read';
        case mcpReadScope:
            return 'MCP';
        case offlineAccessScope:
            return 'Offline Access';
        case 'projects:read':
            return 'Projects Read';
        case 'stacks:read':
            return 'Stacks Read';
        case 'stacks:write':
            return 'Stacks Write';
        default:
            return scope;
    }
}
