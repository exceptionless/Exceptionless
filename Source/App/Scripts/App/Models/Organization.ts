/// <reference path="../exceptionless.ts" />

module exceptionless.models {
    export class Organization {
        id: string;
        name: string;
        projectCount: number;
        stackCount: number;
        errorCount: number;
        totalErrorCount: number;
        lastErrorDate: Date;
        subscribeDate: Date;

        billingChangeDate: Date;
        billingChangedByUserId: string;
        billingStatus = enumerations.BillingStatus.Active;
        billingPrice: number;

        planId: string;
        cardLast4: string;
        stripeCustomerId: string;

        isSuspended: boolean;
        suspensionCode: string;
        suspensionDate: Date;
        suspendedByUserId: string;
        suspensionNotes: string;

        constructor(id: string, name: string, projectCount: number, stackCount: number, errorCount: number, totalErrorCount: number, lastErrorDate?: Date, subscribeDate?: Date, billingChangeDate?: Date, billingChangedByUserId?: string, billingStatus?: number, billingPrice?:number, planId?: string, cardLast4?: string, stripeCustomerId?: string, isSuspended?: boolean, suspensionCode?: string, suspensionDate?: Date, suspendedByUserId?: string, suspensionNotes?: string) {
            this.id = id;
            this.name = name;
            this.projectCount = projectCount;
            this.stackCount = stackCount;
            this.errorCount = errorCount;
            this.totalErrorCount = totalErrorCount;

            this.lastErrorDate = lastErrorDate;

            if (subscribeDate)
                this.subscribeDate = subscribeDate;
            
            if (billingStatus)
                this.billingStatus = <enumerations.BillingStatus>billingStatus;

            if (billingChangeDate)
                this.billingChangeDate = billingChangeDate;

            this.billingChangedByUserId = !StringUtil.isNullOrEmpty(billingChangedByUserId) ? billingChangedByUserId : '';
            this.billingPrice = billingPrice;
            this.planId = planId;
            this.cardLast4 = cardLast4;
            this.stripeCustomerId = stripeCustomerId;

            this.isSuspended = isSuspended == true;
            this.suspensionCode = !StringUtil.isNullOrEmpty(suspensionCode) ? suspensionCode : '';
            this.suspendedByUserId = !StringUtil.isNullOrEmpty(suspendedByUserId) ? suspendedByUserId : '';
            this.suspensionNotes = !StringUtil.isNullOrEmpty(suspensionNotes) ? suspensionNotes : '';

            if (suspensionDate)
                this.suspensionDate = suspensionDate;
            
            ko.track(this);
        }

        public get createdOn(): Date {
            return new Date(parseInt(this.id.substring(0, 8), 16) * 1000);
        }

        public get projects(): models.ProjectInfo[] {
            return ko.utils.arrayFilter(App.projects(), (project: models.ProjectInfo) => {
                return project.organizationId === this.id;
            });
        }

        public get selectedPlan(): account.BillingPlan {
            var plan = ko.utils.arrayFirst(App.plans(), (plan: account.BillingPlan) => plan.id === this.planId);
            return plan ? plan : new account.BillingPlan(Constants.FREE_PLAN_ID, 'Free', 'Free', 0, false, 1, 2500, 5, 1, 7, false);
        }

        public get billingStatusAbbr(): string {
            if (this.isSuspended)
                return '[S]';

            return '[' + enumerations.BillingStatus[this.billingStatus].toUpperCase()[0] + ']';
        }

        public get billingSummary(): string {
            var summary = '<h5>Billing (' + enumerations.BillingStatus[this.billingStatus].toString() + ')</h5>';
            summary += '<p>Subscribed on: <strong>' + DateUtil.format(moment(this.subscribeDate)) + '</strong></p>';

            if (this.billingChangeDate)
                summary += '<p>Last changed: <strong>' + DateUtil.format(moment(this.billingChangeDate)) + '</strong></p>';

            if (!StringUtil.isNullOrEmpty(this.billingChangedByUserId))
                summary += '<p>Changed by: <strong>' + this.billingChangedByUserId + '</strong></p>';

            if (!StringUtil.isNullOrEmpty(this.cardLast4))
                summary += '<p>Card Last 4: <strong>' + this.cardLast4 + '</strong></p>';

            if (this.isSuspended) {
                summary +=  '<h5>Suspension information</h5>';
                summary += '<p>Suspended on: <strong>' + DateUtil.format(moment(this.suspensionDate)) + '</strong></p>';
                summary += '<p>Suspended by: <strong>' + this.suspendedByUserId + '</strong></p>';
                summary += '<p>Reason: <strong>' + this.suspensionCode + '</strong></p>';

                if (!StringUtil.isNullOrEmpty(this.suspensionNotes))
                    summary += '<p>Notes: <strong>' + this.suspensionNotes + '</strong></p>';
            }

            return summary;
        }

        public get retentionSummary(): string {
            var summary = '<p>Projects: <strong>' + numeral(this.projectCount).format('0,0[.]0') + '</strong></p>';
            summary += '<p>Stacks: <strong>' + numeral(this.stackCount).format('0, 0[.]0') + '</strong></p>';
            summary += '<p>Errors: <strong>' + numeral(this.errorCount).format('0, 0[.]0') + '</strong></p>';
            summary += '<p>Total Errors: <strong>' + numeral(this.totalErrorCount).format('0, 0[.]0') + '</strong></p>';
            return summary;
        }

        public get activitySummary(): string {
            var activity = '<p>Created on: <strong>' + DateUtil.format(moment(this.createdOn)) + '</strong></p>';
            
            if (this.subscribeDate)
                activity += '<p>Subscribed on: <strong>' + DateUtil.format(moment(this.subscribeDate));
            
            var lastActivity = this.lastErrorDate && <any>this.lastErrorDate !== "0001-01-01T00:00:00Z" ? DateUtil.format(moment(this.lastErrorDate)) : 'Never';
            activity += '</strong></p><p>Last Error: <strong> ' + lastActivity + '</strong></p>';
            
            return activity;
        }
    }
}