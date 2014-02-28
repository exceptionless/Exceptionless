/// <reference path="../exceptionless.ts" />

module exceptionless.account {
    export class BillingPlanModal {
        private billingInfo: BillingInfoModel;

        constructor() {
             $('#plan-modal').on('show', () => {
                 if (!this.billingInfo) {
                     this.billingInfo = new BillingInfoModel();
                     ko.applyBindings(this.billingInfo, document.getElementById('billing-body'));
                 }

                 var organizationId = $('#plan-modal').data('organizationId');
                 $('#plan-modal').removeData('organizationId');
                 if (!StringUtil.isNullOrEmpty(organizationId))
                    this.billingInfo.selectedOrganization = ko.utils.arrayFirst(App.organizations(), (o: models.Organization) => o.id === organizationId);
             });

            $('#cardNumber').payment('formatCardNumber');

            // TODO: Add definition for this event.
            (<any>$('#cardNumber')).on('payment.cardType', (event, cardType) => {
                $('#billing-body .payment .number').removeClass("visa mastercard amex discover dinersclub jcb").addClass(cardType);
            });
            $('#cardExpiry').payment('formatCardExpiry');
            $('#cardCVC').payment('formatCardCVC');

            $('#change-plan-form').submit((event) => {
                $("#change-plan-button").prop('disabled', true);

                $('input').removeClass('invalid');
                $('.validation').removeClass('passed failed');

                var organizationId = this.billingInfo.selectedOrganization.id;
                var planId = this.billingInfo.selectedPlan.id;
                var cardType = $.payment.cardType($('#cardNumber').val());
                var cardExpiry = $('#cardExpiry').payment('cardExpiryVal');
                var cardNumber = $('#cardNumber').val();
                var cardName = $('#cardName').val();
                var cardCvc = $('#cardCVC').val();

                if (this.billingInfo.selectedOrganization.planId === Constants.FREE_PLAN_ID && planId === Constants.FREE_PLAN_ID) {
                    $("#change-plan-button").prop('disabled', false);
                    $('#plan-modal').modal('hide');
                    return false;
                }

                if (this.billingInfo.hasAdminRole || this.billingInfo.selectedPlan.id === Constants.FREE_PLAN_ID) {
                    this.changePlan(this.billingInfo.hasAdminRole, organizationId, planId);
                    return false;
                }

                if (this.billingInfo.cardMode == 'new') {
                    $('#cardName').toggleClass('invalid', cardName.length == 0);
                    $('#cardNumber').toggleClass('invalid', !$.payment.validateCardNumber(cardNumber));
                    $('#cardExpiry').toggleClass('invalid', !$.payment.validateCardExpiry(cardExpiry));
                    $('#cardCVC').toggleClass('invalid', !$.payment.validateCardCVC(cardCvc, cardType));
                }

                if ($('input.invalid').length) {
                    $('.validation').addClass('failed');
                    $("#change-plan-button").prop('disabled', false);
                    return false;
                } else {
                    $('.validation').addClass('passed');
                }
                
                if (this.billingInfo.cardMode == 'new') {
                    Stripe.createToken({
                        number: cardNumber,
                        cvc: cardCvc,
                        exp_month: cardExpiry.month,
                        exp_year: cardExpiry.year,
                        name: cardName
                    }, (status: number, response: StripeTokenResponse) => {
                        if (response.error) {
                            $(".payment-message").text(response.error.message);
                            $("#change-plan-button").prop('disabled', false);
                        } else {
                            this.changePlan(false, organizationId, planId, response.id, response.card.last4);
                        }
                    });
                } else {
                    this.changePlan(false, organizationId, planId);
                }

                return false;
            });

            $("#change-plan-button").click(() => {
                $('#change-plan-form').submit();
            });
        }

        private changePlan(hasAdminRole: boolean, organizationId: string, planId: string, stripeToken?: string, last4?: string) {
            $.ajax({
                type: 'POST',
                url: hasAdminRole ? '/admin/changeplan' : '/organization/changeplan',
                data: { organizationId: organizationId, planId: planId, stripeToken: stripeToken, last4: last4 },
                success: (data) => {
                    if (!data.Success) {
                        App.showErrorNotification('Error changing your plan. <br /> Message: ' + data.Message);
                        return;
                    }

                    App.showSuccessNotification('<strong>Thanks!</strong> Your billing plan has been successfully changed.');
                    $('#plan-modal .payment input').val('');
                    $('#plan-modal').modal('hide');
                },
                error: (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                    App.showErrorNotification('Error changing your plan.');
                },
                complete: (req, status) => {
                    $("#change-plan-button").prop('disabled', false);
                },
                dataType: "json"
            });
        }
    }

    export class BillingInfoModel {
        stripeToken= '';
        cardMode = 'new';

        selectedOrganization = new models.Organization('', 'Loading...', 0, 0, 0, 0);
        selectedPlan = new account.BillingPlan(Constants.FREE_PLAN_ID, 'Free', 'Free', 0, false, 1, 2500, 5, 1, 7, false);

        constructor() {
            ko.track(this);

            ko.getObservable(this, 'selectedOrganization').subscribe((org) => {
                this.cardMode = !org.cardLast4 ? 'new' : 'existing';

                var currentPlan: account.BillingPlan = ko.utils.arrayFirst(App.plans(), (plan: account.BillingPlan) => plan.id === this.selectedOrganizationPlan.id);
                var upsell: account.BillingPlan = ko.utils.arrayFirst(App.plans(), (plan: account.BillingPlan) => plan.price > this.selectedOrganizationPlan.price);
                this.selectedPlan = upsell ? upsell : currentPlan ? currentPlan : this.plans[1];
            });

            App.selectedOrganization.subscribe((o) => this.selectedOrganization = <any>o);
        }

        public get hasAdminRole(): boolean {
            return App.user().hasAdminRole();
        }

        public get organizations(): models.Organization[] {
            return App.organizations();
        }

        public get plans(): account.BillingPlan[]{
            var currentPlan: account.BillingPlan = ko.utils.arrayFirst(App.plans(), (plan: account.BillingPlan) => plan.id === this.selectedOrganizationPlan.id);

            return ko.utils.arrayFilter(App.plans(), (p: account.BillingPlan) => !p.isHidden || currentPlan.id === p.id || App.user().hasAdminRole());
        }
        
        public get selectedOrganizationPlan(): account.BillingPlan {
            return ko.utils.arrayFirst(App.organizations(), (o: models.Organization) => o.id === this.selectedOrganization.id).selectedPlan;
        }
    }

    export class BillingPlan {
        id: string;
        name: string;
        description: string;
        price: number;
        hasPremiumFeatures: boolean;
        maxProjects: number;
        maxErrors: number;
        maxPerStack: number;
        maxUsers: number;
        statRetention: number;
        isHidden: boolean;

        constructor(id: string, name: string, description: string, price: number, hasPremiumFeatures: boolean, maxProjects: number, maxErrors: number, maxPerStack: number, maxUsers: number, statRetention: number, isHidden: boolean) {
            this.id = id;
            this.name = name;
            this.description = description;
            this.price = price;
            this.hasPremiumFeatures = hasPremiumFeatures;
            this.maxProjects = maxProjects;
            this.maxErrors = maxErrors;
            this.maxPerStack = maxPerStack;
            this.maxUsers = maxUsers;
            this.statRetention = statRetention;
            this.isHidden = isHidden;

            ko.track(this);
        }
    }
}