export type { Invoice, InvoiceGridModel, NewOrganization, OrganizationBudgetAlertSettings, ViewOrganization } from '$generated/api';

export interface UpdateOrganization {
    name?: string;
    budget_alert_settings?: OrganizationBudgetAlertSettingsUpdate | null;
}

export interface OrganizationBudgetAlertSettingsUpdate {
    enabled: boolean;
    thresholds: number[];
}

// TODO: This should be generated from the backend enum - investigate why it wasn't included in the generated API
export enum SuspensionCode {
    Billing = 0,
    Overage = 1,
    Abuse = 2,
    Other = 100
}
