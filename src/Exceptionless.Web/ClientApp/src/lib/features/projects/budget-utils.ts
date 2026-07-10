import { type ProjectIngestLimit, ProjectIngestLimitType } from './models';

export type ProjectBudgetType = 'fixed' | 'none' | 'percent';

export function createProjectIngestLimit(type: ProjectBudgetType, value: string): null | ProjectIngestLimit {
    if (type === 'none') {
        return null;
    }

    const numericValue = Number(value.trim());
    if (!Number.isFinite(numericValue)) {
        return null;
    }

    return type === 'fixed'
        ? { fixed_limit: numericValue, percent_of_organization_limit: null, type: ProjectIngestLimitType.Fixed }
        : { fixed_limit: null, percent_of_organization_limit: numericValue, type: ProjectIngestLimitType.PercentOfOrganizationLimit };
}

export function getEffectiveProjectLimit(organizationLimit: number, ingestLimit: null | ProjectIngestLimit): null | number {
    if (!ingestLimit) {
        return null;
    }

    if (ingestLimit.type === ProjectIngestLimitType.Fixed) {
        if (!Number.isInteger(ingestLimit.fixed_limit) || (ingestLimit.fixed_limit ?? 0) <= 0) {
            return null;
        }

        return organizationLimit < 0 ? ingestLimit.fixed_limit! : Math.min(ingestLimit.fixed_limit!, organizationLimit);
    }

    const percentage = ingestLimit.percent_of_organization_limit;
    if (organizationLimit < 0 || percentage == null || !Number.isFinite(percentage) || percentage <= 0 || percentage > 100) {
        return null;
    }

    return Math.min(organizationLimit, Math.ceil((organizationLimit * percentage) / 100));
}
