import type { AssistantToolActivity } from './models';

interface AssistantToolLabels {
    complete: string;
    failed: string;
    running: string;
}

const toolLabels: Record<string, AssistantToolLabels> = {
    add_stack_reference_link: { complete: 'Added a reference link', failed: 'Couldn’t add the reference link', running: 'Adding a reference link' },
    count_events: { complete: 'Counted events', failed: 'Couldn’t count events', running: 'Counting events' },
    get_client_setup_instructions: {
        complete: 'Read client setup instructions',
        failed: 'Couldn’t read setup instructions',
        running: 'Reading setup instructions'
    },
    get_event: { complete: 'Read event details', failed: 'Couldn’t read event details', running: 'Reading event details' },
    get_filter_fields: { complete: 'Checked available filters', failed: 'Couldn’t check filters', running: 'Checking available filters' },
    get_project: { complete: 'Read project details', failed: 'Couldn’t read project details', running: 'Reading project details' },
    get_project_setup: { complete: 'Read project setup', failed: 'Couldn’t read project setup', running: 'Reading project setup' },
    get_stack: { complete: 'Read stack details', failed: 'Couldn’t read stack details', running: 'Reading stack details' },
    get_stack_events: { complete: 'Listed stack events', failed: 'Couldn’t list stack events', running: 'Listing stack events' },
    list_organizations: { complete: 'Listed organizations', failed: 'Couldn’t list organizations', running: 'Listing organizations' },
    list_projects: { complete: 'Listed projects', failed: 'Couldn’t list projects', running: 'Listing projects' },
    remove_stack_reference_link: { complete: 'Removed a reference link', failed: 'Couldn’t remove the reference link', running: 'Removing a reference link' },
    resolve_project: { complete: 'Found the project', failed: 'Couldn’t find the project', running: 'Finding the project' },
    search_events: { complete: 'Searched events', failed: 'Couldn’t search events', running: 'Searching events' },
    search_stacks: { complete: 'Searched error stacks', failed: 'Couldn’t search error stacks', running: 'Searching error stacks' },
    set_stack_critical: { complete: 'Updated the critical setting', failed: 'Couldn’t update the critical setting', running: 'Updating the critical setting' },
    snooze_stack: { complete: 'Snoozed the stack', failed: 'Couldn’t snooze the stack', running: 'Snoozing the stack' },
    update_stack_status: { complete: 'Updated the stack status', failed: 'Couldn’t update the stack status', running: 'Updating the stack status' }
};

/** Tools that change data. Their results are always shown so a change is never silent. */
const mutationTools = new Set(['add_stack_reference_link', 'remove_stack_reference_link', 'set_stack_critical', 'snooze_stack', 'update_stack_status']);

/** Tools that drive UI rather than research, so they never appear in the trace. */
const hiddenTools = new Set(['suggest_followups']);

/**
 * A short qualifier that tells similar steps apart, such as the project and time range a search
 * covered or the title of the stack it read.
 */
export function getAssistantToolDetail(tool: AssistantToolActivity, projectNames: ReadonlyMap<string, string>): string | undefined {
    if (isMutationTool(tool)) {
        const unchanged = parseRecord(parseRecord(tool.result)?.data)?.changed === false;
        return [unchanged ? 'No change needed' : undefined, getAssistantToolStackTitle(tool)].filter(Boolean).join(' · ') || undefined;
    }

    const args = parseRecord(tool.arguments);
    const details: string[] = [];
    const projectId = typeof args?.projectId === 'string' ? args.projectId : undefined;
    const projectName = (projectId ? projectNames.get(projectId) : undefined) ?? (typeof args?.projectName === 'string' ? args.projectName : undefined);
    if (projectName) {
        details.push(projectName);
    }

    if (typeof args?.filter === 'string' && args.filter.trim()) {
        details.push(args.filter.trim());
    }

    if (typeof args?.time === 'string' && args.time.trim()) {
        details.push(describeTimeRange(args.time.trim()));
    }

    if (typeof args?.groupBy === 'string' && args.groupBy.trim()) {
        details.push(`by ${args.groupBy.trim()}`);
    }

    if (details.length === 0 && tool.status === 'complete' && (tool.name === 'get_stack' || tool.name === 'get_event')) {
        const data = parseRecord(parseRecord(tool.result)?.data);
        const title = getAssistantToolStackTitle(tool) ?? (typeof data?.message === 'string' ? data.message.trim() : undefined);
        if (title) {
            details.push(title);
        }
    }

    return details.length > 0 ? details.join(' · ') : undefined;
}

export function getAssistantToolLabel(tool: AssistantToolActivity, isStreaming: boolean): string {
    const labels = toolLabels[tool.name];
    const name = humanizeToolName(tool.name);

    if (tool.status === 'running') {
        return isStreaming ? `${labels?.running ?? `Running ${name}`}…` : (labels?.failed ?? `Couldn’t finish ${name}`);
    }

    if (tool.status === 'failed') {
        return labels?.failed ?? `Couldn’t finish ${name}`;
    }

    if (tool.status === 'cancelled') {
        return `Stopped: ${(labels?.running ?? name).toLowerCase()}`;
    }

    return isMutationTool(tool) ? describeMutation(tool) : (labels?.complete ?? `Ran ${name}`);
}

/** Project names from list_projects results, so later steps can name the project they searched. */
export function getAssistantToolProjectNames(tools: AssistantToolActivity[]): Map<string, string> {
    const names = new Map<string, string>();
    for (const tool of tools) {
        if (tool.name !== 'list_projects' && tool.name !== 'get_project' && tool.name !== 'resolve_project') {
            continue;
        }

        const data = parseRecord(tool.result)?.data;
        const items = Array.isArray((data as { items?: unknown })?.items) ? (data as { items: unknown[] }).items : [data];
        for (const item of items) {
            const record = parseRecord(item);
            if (typeof record?.id === 'string' && typeof record.name === 'string') {
                names.set(record.id, record.name);
            }
        }
    }

    return names;
}

/** The stack a mutation changed, so the change can link to it. */
export function getAssistantToolStackId(tool: AssistantToolActivity): string | undefined {
    const args = parseRecord(tool.arguments);
    const stackId = args?.stackId ?? args?.stack_id;
    return typeof stackId === 'string' && /^[a-f0-9]{24}$/i.test(stackId) ? stackId : undefined;
}

export function getAssistantToolStackTitle(tool: AssistantToolActivity): string | undefined {
    const data = parseRecord(parseRecord(tool.result)?.data);
    const resource = parseRecord(data?.stack) ?? data;
    const title = resource?.title ?? resource?.name;
    return typeof title === 'string' && title.trim() ? title.trim() : undefined;
}

export function isHiddenAssistantTool(tool: AssistantToolActivity): boolean {
    return hiddenTools.has(tool.name);
}

export function isMutationTool(tool: AssistantToolActivity): boolean {
    return mutationTools.has(tool.name);
}

function describeMutation(tool: AssistantToolActivity): string {
    const args = parseRecord(tool.arguments);

    switch (tool.name) {
        case 'add_stack_reference_link':
            return 'Added a reference link';
        case 'remove_stack_reference_link':
            return 'Removed a reference link';
        case 'set_stack_critical':
            return args?.critical === false ? 'Stopped marking future events critical' : 'Marked future events critical';
        case 'snooze_stack':
            return typeof args?.duration === 'string' && args.duration ? `Snoozed for ${args.duration}` : 'Snoozed the stack';
        case 'update_stack_status': {
            const status = typeof args?.status === 'string' ? args.status.toLowerCase() : undefined;
            const version = typeof args?.fixedInVersion === 'string' && args.fixedInVersion ? ` in ${args.fixedInVersion}` : '';
            return status ? `Marked ${status}${version}` : 'Updated the stack status';
        }
        default:
            return toolLabels[tool.name]?.complete ?? `Ran ${humanizeToolName(tool.name)}`;
    }
}

function humanizeToolName(value: string): string {
    return value.replaceAll('_', ' ');
}

function parseRecord(value: unknown): Record<string, unknown> | undefined {
    let parsed = value;
    if (typeof value === 'string') {
        try {
            parsed = JSON.parse(value) as unknown;
        } catch {
            return undefined;
        }
    }

    return typeof parsed === 'object' && parsed !== null && !Array.isArray(parsed) ? (parsed as Record<string, unknown>) : undefined;
}

const calendarPeriods: Record<string, string> = {
    '[now-1d/d TO now-1d/d]': 'yesterday',
    '[now-1M/M TO now-1M/M]': 'last month',
    '[now-1w/w TO now-1w/w]': 'last week',
    '[now/d TO now/d]': 'today',
    '[now/M TO now/M]': 'this month',
    '[now/w TO now/w]': 'this week'
};
const timeUnits: Record<string, string> = { d: 'days', h: 'hours', m: 'minutes', M: 'months', w: 'weeks', y: 'years' };

/** Names a date math time range the way a person would, such as "last 24 hours" or "today", and shows anything else as written. */
export function describeTimeRange(time: string): string {
    const normalized = time.replace(/\s+/g, ' ');
    const calendarPeriod = calendarPeriods[normalized];
    if (calendarPeriod) {
        return calendarPeriod;
    }

    const rolling = /^\[now-(\d+)([mhdwMy]) TO now\]$/.exec(normalized);
    if (rolling) {
        const [, amount, unit] = rolling;
        return `last ${amount} ${timeUnits[unit!]}`;
    }

    return normalized;
}
