(function () {
  'use strict';

  angular.module('exceptionless.billing')
    .controller('ChangePlanDialog', function ($uibModalInstance, adminService, analyticsService, Common, $ExceptionlessClient, $intercom, INTERCOM_APPID, notificationService, organizationService, stripe, STRIPE_PUBLISHABLE_KEY, userService, translateService, $window, data) {
      var vm = this;
      function cancel() {
        analyticsService.lead(getAnalyticsData());
        $ExceptionlessClient.createFeatureUsage(vm._source + '.cancel')
          .setProperty('CurrentPlan', vm.currentPlan)
          .setProperty('CouponId', vm.coupon)
          .setProperty('IsNewCard', isNewCard())
          .setProperty('OrganizationId', vm.currentOrganization.id)
          .submit();

        $uibModalInstance.dismiss('cancel');
      }

      function createStripeToken() {
        function onSuccess(response) {
          analyticsService.addPaymentInfo();
          return response;
        }

        var expiration = Common.parseExpiry(vm.card.expiry);
        var payload = {
          number: vm.card.number,
          cvc: vm.card.cvc,
          exp_month: expiration.month,
          exp_year: expiration.year,
          name: vm.card.name
        };

        return stripe.card.createToken(payload).then(onSuccess);
      }

      function getAnalyticsData() {
        return { content_name: vm.currentPlan.name, content_ids: [vm.currentPlan.id], content_type: 'product', currency: 'USD', value: vm.currentPlan.price };
      }

      function save(isValid) {
        function onCreateTokenSuccess(response) {
          return changePlan(false, { stripeToken: response.id, last4: response.card.last4, couponId: vm.coupon }).then(onSuccess, onFailure);
        }

        function onSuccess(response) {
          if(!response.data.success) {
            analyticsService.lead(getAnalyticsData());
            vm.paymentMessage = translateService.T('An error occurred while changing plans.') + ' ' + translateService.T('Message:') + ' ' + response.data.message;
            $ExceptionlessClient.createException(new Error(response.data.message))
              .markAsCritical()
              .setSource(vm._source + '.save.error')
              .setProperty('CurrentPlan', vm.currentPlan)
              .setProperty('CouponId', vm.coupon)
              .setProperty('IsNewCard', isNewCard())
              .setProperty('OrganizationId', vm.currentOrganization.id)
              .submit();

            return;
          }

          analyticsService.purchase(getAnalyticsData());
          $ExceptionlessClient.createFeatureUsage(vm._source + '.save')
            .markAsCritical()
            .setMessage(response.data.message)
            .setProperty('CurrentPlan', vm.currentPlan)
            .setProperty('CouponId', vm.coupon)
            .setProperty('IsNewCard', isNewCard())
            .setProperty('OrganizationId', vm.currentOrganization.id)
            .submit();

          $uibModalInstance.close(vm.currentPlan);
          notificationService.success(translateService.T('Thanks! Your billing plan has been successfully changed.'));
        }

        function onFailure(response) {
          if (response.error && response.error.message) {
            vm.paymentMessage = response.error.message;
          } else {
            vm.paymentMessage = translateService.T('An error occurred while changing plans.');
          }

          analyticsService.lead(getAnalyticsData());
          $ExceptionlessClient.createException(new Error(vm.paymentMessage))
            .markAsCritical()
            .setSource(vm._source + '.save.error')
            .setProperty('CurrentPlan', vm.currentPlan)
            .setProperty('CouponId', vm.coupon)
            .setProperty('IsNewCard', isNewCard())
            .setProperty('OrganizationId', vm.currentOrganization.id)
            .submit();
        }

        if (!isValid || !vm.currentPlan) {
          return;
        }

        vm.paymentMessage = null;
        if (vm.currentOrganization.plan_id === vm._freePlanId && vm.currentPlan.id === vm._freePlanId) {
          cancel();
          return;
        }

        if (hasAdminRole() || vm.currentPlan.id === vm._freePlanId) {
          return changePlan(hasAdminRole()).then(onSuccess, onFailure);
        }

        if (vm.currentPlan.price > 0 && isNewCard()) {
          try {
            return createStripeToken().then(onCreateTokenSuccess, onFailure);
          } catch (error) {
            vm.paymentMessage = translateService.T('An error occurred while changing plans.');
            $ExceptionlessClient.createException(error)
              .markAsCritical()
              .setSource(vm._source + '.save.error')
              .setProperty('CurrentPlan', vm.currentPlan)
              .setProperty('CouponId', vm.coupon)
              .setProperty('IsNewCard', isNewCard())
              .setProperty('OrganizationId', vm.currentOrganization.id)
              .submit();
            return null;
          }
        }

        return changePlan(false, { couponId: vm.coupon }).then(onSuccess, onFailure);
      }

      function changeOrganization() {
        vm.card.mode = hasExistingCard() ? 'existing' : 'new';
        return getPlans();
      }

      function changePlan(isAdmin, options) {
        if (isAdmin) {
          return adminService.changePlan({ organizationId: vm.currentOrganization.id, planId: vm.currentPlan.id });
        } else {
          return organizationService.changePlan(vm.currentOrganization.id, angular.extend({}, { planId: vm.currentPlan.id }, options));
        }
      }

      function getOrganizations() {
        function getSelectedOrganization() {
          function onSuccess(response) {
            vm.organizations.push(response.data.plain());
            return vm.organizations;
          }

          if (!data || vm.organizations.filter(function(o) { return o.id === data; })[0])
            return;

          return organizationService.getById(data, false).then(onSuccess);
        }

        function getAllOrganizations() {
          function onSuccess(response) {
            angular.forEach(response.data.plain(), function(value, key) {
              vm.organizations.push(value);
            });

            return vm.organizations;
          }

          return  organizationService.getAll({}, false).then(onSuccess);
        }

        function onSuccess() {
          vm.currentOrganization = vm.organizations.filter(function(o) { return o.id === (vm.currentOrganization.id || data); })[0];
          if (!vm.currentOrganization) {
            vm.currentOrganization = vm.organizations.length > 0 ? vm.organizations[0] : {};
          }

          vm.card.mode = hasExistingCard() ? 'existing' : 'new';
        }

        function onFailure(response) {
          notificationService.error(translateService.T('An error occurred while loading your organizations.') + ' ' + vm._contactSupport);
          $ExceptionlessClient.createFeatureUsage(vm._source + '.getOrganizations.error')
            .markAsCritical()
            .setMessage(response && response.data && response.data.message)
            .submit();

          cancel();
        }

        vm.organizations = [];
        return getAllOrganizations().then(getSelectedOrganization).then(onSuccess, onFailure);
      }

      function getPlans() {
        function onSuccess(response) {
          vm.plans = response.data.plain();

          // Upsell to the next plan.
          var currentPlan = vm.plans.filter(function(p) { return p.id === vm.currentOrganization.plan_id; })[0] || vm.plans[0];
          var currentPlanIndex = vm.plans.indexOf(currentPlan);
          vm.currentPlan = vm.plans.length > currentPlanIndex + 1 ? vm.plans[currentPlanIndex + 1] : currentPlan;

          return vm.plans;
        }

        function onFailure(response) {
          notificationService.error(translateService.T('An error occurred while loading available billing plans.') + ' ' + vm._contactSupport);
          $ExceptionlessClient.createFeatureUsage(vm._source + '.getPlans.error')
            .markAsCritical()
            .setMessage(response && response.data && response.data.message)
            .submit();

          cancel();
        }

        return organizationService.getPlans(vm.currentOrganization.id).then(onSuccess, onFailure);
      }

      function getUser() {
        function onSuccess(response) {
          vm.user = response.data.plain();

          if (!vm.card.name) {
            vm.card.name = vm.user.full_name;
          }

          return vm.user;
        }

        function onFailure(response) {
          notificationService.error(translateService.T('An error occurred while loading your user account.') + ' ' + vm._contactSupport);
          $ExceptionlessClient.createFeatureUsage(vm._source + '.getUser.error')
            .markAsCritical()
            .setMessage(response && response.data && response.data.message)
            .submit();

          cancel();
        }

        return userService.getCurrentUser().then(onSuccess, onFailure);
      }

      function hasAdminRole() {
        return userService.hasAdminRole(vm.user);
      }

      function hasExistingCard() {
        return !!vm.currentOrganization.card_last4;
      }

      function isBillingEnabled() {
        return !!STRIPE_PUBLISHABLE_KEY;
      }

      function isCancellingPlan() {
        return vm.currentPlan && vm.currentPlan.id === vm._freePlanId && vm.currentOrganization.plan_id !== vm._freePlanId;
      }

      function isNewCard() {
        return vm.card && vm.card.mode === 'new';
      }

      function isPaidPlan() {
        return vm.currentPlan && vm.currentPlan.price !== 0;
      }

      function showIntercom() {
        $ExceptionlessClient.submitFeatureUsage(vm._source + '.showIntercom');
        if (INTERCOM_APPID) {
          $intercom.showNewMessage();
        } else {
          $window.open('https://exceptionless.com', '_blank');
        }
      }

      this.$onInit = function $onInit() {
        vm._source = 'exceptionless.billing.ChangePlanDialog';
        vm._contactSupport = translateService.T('Please contact support for more information.');
        vm._freePlanId = 'EX_FREE';
        vm.cancel = cancel;
        vm.card = {};
        vm.changeOrganization = changeOrganization;
        vm.coupon = null;
        vm.currentOrganization = {};
        vm.currentPlan = {};
        vm.getPlans = getPlans;
        vm.hasAdminRole = hasAdminRole;
        vm.hasExistingCard = hasExistingCard;
        vm.isBillingEnabled = isBillingEnabled;
        vm.isCancellingPlan = isCancellingPlan;
        vm.isNewCard = isNewCard;
        vm.isPaidPlan = isPaidPlan;
        vm.organizations = [];
        vm.paymentMessage = !isBillingEnabled() ? translateService.T('Billing is currently disabled.') : null;
        vm.plans = [];
        vm.save = save;
        vm.showIntercom = showIntercom;
        vm.stripe = {};

        $ExceptionlessClient.submitFeatureUsage(vm._source);
        getOrganizations().then(getPlans).then(getUser);
      };
    });
}());
