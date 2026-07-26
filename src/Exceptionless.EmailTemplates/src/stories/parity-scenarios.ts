import modernEventNotice from '../../../Exceptionless.Core/Mail/Templates/event-notice.html?raw';
import modernOrganizationAdded from '../../../Exceptionless.Core/Mail/Templates/organization-added.html?raw';
import modernOrganizationInvited from '../../../Exceptionless.Core/Mail/Templates/organization-invited.html?raw';
import modernOrganizationNotice from '../../../Exceptionless.Core/Mail/Templates/organization-notice.html?raw';
import modernOrganizationPaymentFailed from '../../../Exceptionless.Core/Mail/Templates/organization-payment-failed.html?raw';
import modernProjectDailySummary from '../../../Exceptionless.Core/Mail/Templates/project-daily-summary.html?raw';
import modernUserEmailVerify from '../../../Exceptionless.Core/Mail/Templates/user-email-verify.html?raw';
import modernUserPasswordReset from '../../../Exceptionless.Core/Mail/Templates/user-password-reset.html?raw';
import legacyEventNotice from '../legacy/event-notice.html?raw';
import legacyOrganizationAdded from '../legacy/organization-added.html?raw';
import legacyOrganizationInvited from '../legacy/organization-invited.html?raw';
import legacyOrganizationNotice from '../legacy/organization-notice.html?raw';
import legacyOrganizationPaymentFailed from '../legacy/organization-payment-failed.html?raw';
import legacyProjectDailySummary from '../legacy/project-daily-summary.html?raw';
import legacyUserEmailVerify from '../legacy/user-email-verify.html?raw';
import legacyUserPasswordReset from '../legacy/user-password-reset.html?raw';
import {
    blockedDailySummaryTokens,
    dailySummaryTokens,
    emailVerifyTokens,
    eventNoticeTokens,
    fillTokens,
    monthlyOrganizationNoticeTokens,
    organizationAddedTokens,
    organizationInvitedTokens,
    organizationNoticeTokens,
    passwordResetTokens,
    paymentFailedTokens,
    reoccurredEventNoticeTokens,
    regressedEventNoticeTokens,
    unconfiguredDailySummaryTokens,
    type TokenData
} from './sample-data.js';

export type ParityScenario = {
    id: string;
    template: string;
    variant: string;
    height: number;
    modernHtml: string;
    legacyHtml: string;
};

type ScenarioDefinition = {
    id: string;
    template: string;
    variant: string;
    height: number;
    modernTemplate: string;
    legacyTemplate: string;
    tokens: TokenData;
};

const definitions: ScenarioDefinition[] = [
    {
        id: 'event-notice-new-critical',
        template: 'Event Notice',
        variant: 'New Critical Event',
        height: 700,
        modernTemplate: modernEventNotice,
        legacyTemplate: legacyEventNotice,
        tokens: eventNoticeTokens
    },
    {
        id: 'event-notice-regression',
        template: 'Event Notice',
        variant: 'Critical Regression',
        height: 700,
        modernTemplate: modernEventNotice,
        legacyTemplate: legacyEventNotice,
        tokens: regressedEventNoticeTokens
    },
    {
        id: 'event-notice-reoccurred',
        template: 'Event Notice',
        variant: 'Reoccurred Without Details',
        height: 550,
        modernTemplate: modernEventNotice,
        legacyTemplate: legacyEventNotice,
        tokens: reoccurredEventNoticeTokens
    },
    {
        id: 'organization-added',
        template: 'Organization Added',
        variant: 'Default',
        height: 700,
        modernTemplate: modernOrganizationAdded,
        legacyTemplate: legacyOrganizationAdded,
        tokens: organizationAddedTokens
    },
    {
        id: 'organization-invited',
        template: 'Organization Invited',
        variant: 'Default',
        height: 700,
        modernTemplate: modernOrganizationInvited,
        legacyTemplate: legacyOrganizationInvited,
        tokens: organizationInvitedTokens
    },
    {
        id: 'organization-notice-hourly',
        template: 'Organization Notice',
        variant: 'Hourly Throttling',
        height: 650,
        modernTemplate: modernOrganizationNotice,
        legacyTemplate: legacyOrganizationNotice,
        tokens: organizationNoticeTokens
    },
    {
        id: 'organization-notice-monthly',
        template: 'Organization Notice',
        variant: 'Monthly Plan Limit',
        height: 650,
        modernTemplate: modernOrganizationNotice,
        legacyTemplate: legacyOrganizationNotice,
        tokens: monthlyOrganizationNoticeTokens
    },
    {
        id: 'organization-payment-failed',
        template: 'Payment Failed',
        variant: 'Default',
        height: 600,
        modernTemplate: modernOrganizationPaymentFailed,
        legacyTemplate: legacyOrganizationPaymentFailed,
        tokens: paymentFailedTokens
    },
    {
        id: 'project-daily-summary-default',
        template: 'Daily Summary',
        variant: 'Default',
        height: 900,
        modernTemplate: modernProjectDailySummary,
        legacyTemplate: legacyProjectDailySummary,
        tokens: dailySummaryTokens
    },
    {
        id: 'project-daily-summary-blocked',
        template: 'Daily Summary',
        variant: 'Discarded Events and Free Plan',
        height: 1500,
        modernTemplate: modernProjectDailySummary,
        legacyTemplate: legacyProjectDailySummary,
        tokens: blockedDailySummaryTokens
    },
    {
        id: 'project-daily-summary-unconfigured',
        template: 'Daily Summary',
        variant: 'Unconfigured Project',
        height: 650,
        modernTemplate: modernProjectDailySummary,
        legacyTemplate: legacyProjectDailySummary,
        tokens: unconfiguredDailySummaryTokens
    },
    {
        id: 'user-email-verify',
        template: 'Email Verify',
        variant: 'Default',
        height: 500,
        modernTemplate: modernUserEmailVerify,
        legacyTemplate: legacyUserEmailVerify,
        tokens: emailVerifyTokens
    },
    {
        id: 'user-password-reset',
        template: 'Password Reset',
        variant: 'Default',
        height: 550,
        modernTemplate: modernUserPasswordReset,
        legacyTemplate: legacyUserPasswordReset,
        tokens: passwordResetTokens
    }
];

export const parityScenarios: ParityScenario[] = definitions.map(
    ({ id, template, variant, height, modernTemplate, legacyTemplate, tokens }) => ({
        id,
        template,
        variant,
        height,
        modernHtml: fillTokens(modernTemplate, tokens),
        legacyHtml: fillTokens(legacyTemplate, tokens)
    })
);
