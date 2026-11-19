import type { ProductTourState } from '#features/users/models.ts';

import { getProductTourRecordedAt } from '#features/product-tours/eligibility.ts';

export type ChecklistItemId = 'account' | 'ai-tools' | 'ask-exie' | 'first-event' | 'invite-team' | 'notifications' | 'project' | 'verify-email' | 'versions';

/** Items the user completes by visiting them. Their state is stored with the product tours. */
export type ChecklistVisitItemId = Extract<ChecklistItemId, 'ai-tools' | 'ask-exie' | 'invite-team' | 'notifications' | 'versions'>;

export const CHECKLIST_VISIT_KEYS = {
    'ai-tools': 'checklist-ai-tools',
    'ask-exie': 'checklist-ask-exie',
    'invite-team': 'checklist-invite-team',
    notifications: 'checklist-notifications',
    versions: 'checklist-versions'
} as const satisfies Record<ChecklistVisitItemId, string>;

export const CHECKLIST_DISMISSED_KEY = 'checklist-dismissed';

export type ChecklistStateKey = (typeof CHECKLIST_VISIT_KEYS)[ChecklistVisitItemId] | typeof CHECKLIST_DISMISSED_KEY;

/** New organizations get the checklist for this long unless it is finished or dismissed. */
export const CHECKLIST_ORGANIZATION_AGE_DAYS = 30;

export interface ChecklistInput {
    emailVerified: boolean;
    hasEvents: boolean;
    hasProject: boolean;
    /** Exie is listed only where the organization can use it (or can once the email is verified). */
    includeAskExie?: boolean;
    productTours?: ProductTourState;
}

export interface ChecklistItem {
    complete: boolean;
    id: ChecklistItemId;
    /** Visit items complete when opened; data items follow the account. */
    kind: 'data' | 'visit';
}

const ITEM_ORDER: readonly ChecklistItemId[] = [
    'account',
    'project',
    'first-event',
    'verify-email',
    'ask-exie',
    'notifications',
    'ai-tools',
    'invite-team',
    'versions'
];

export interface ChecklistVisibilityInput {
    now?: Date;
    organizationCreatedUtc?: null | string;
    productTours?: ProductTourState;
}

export function getChecklistItems({ emailVerified, hasEvents, hasProject, includeAskExie = false, productTours }: ChecklistInput): ChecklistItem[] {
    return ITEM_ORDER.filter((id) => includeAskExie || id !== 'ask-exie').map((id): ChecklistItem => {
        switch (id) {
            case 'account':
                return { complete: true, id, kind: 'data' };
            case 'first-event':
                return { complete: hasEvents, id, kind: 'data' };
            case 'project':
                return { complete: hasProject, id, kind: 'data' };
            case 'verify-email':
                return { complete: emailVerified, id, kind: 'data' };
            default:
                return { complete: isChecklistKeyRecorded(productTours, CHECKLIST_VISIT_KEYS[id]), id, kind: 'visit' };
        }
    });
}

export function isChecklistComplete(items: readonly ChecklistItem[]): boolean {
    return items.every((item) => item.complete);
}

export function isChecklistKeyRecorded(productTours: ProductTourState | undefined, key: ChecklistStateKey): boolean {
    return !!getProductTourRecordedAt(productTours, key.replaceAll('-', '_'));
}

/** The checklist is for new organizations and stays hidden once dismissed. */
export function shouldShowChecklist({ now = new Date(), organizationCreatedUtc, productTours }: ChecklistVisibilityInput): boolean {
    if (isChecklistKeyRecorded(productTours, CHECKLIST_DISMISSED_KEY) || !organizationCreatedUtc) {
        return false;
    }

    const createdAt = Date.parse(organizationCreatedUtc);
    if (Number.isNaN(createdAt)) {
        return false;
    }

    return now.getTime() - createdAt < CHECKLIST_ORGANIZATION_AGE_DAYS * 24 * 60 * 60 * 1000;
}
