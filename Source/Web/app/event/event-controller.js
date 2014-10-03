/*global UAParser:false */
(function () {
    'use strict';

    angular.module('app.event')
        .controller('Event', ['$state', '$stateParams', 'errorService', 'eventService', 'notificationService', function ($state, $stateParams, errorService, eventService, notificationService) {
            var eventId = $stateParams.id;
            var vm = this;

            function createTabs() {
                var tabs = [{ title: 'Overview', template_key: 'overview' }];
                if (isError()) {
                    if (vm.event.data.error) {
                        tabs.push({ title: 'Exception', template_key: 'error' });
                    } else if (vm.event.data.simple_error) {
                        tabs.push({ title: 'Exception', template_key: 'simple-error' });
                    }
                }

                if (hasRequestInfo()) {
                    tabs.push({ title: 'Request', template_key: 'request' });
                }

                if (hasEnvironmentInfo()) {
                    tabs.push({ title: 'Environment', template_key: 'environment' });
                }

                vm.tabs = tabs;
            }

            function get() {
                return eventService.getById(eventId)
                    .then(function (response) {
                        vm.event = response.data;
                    }, function() {
                        $state.go('app.dashboard');
                        notificationService.error('The stack "' + $stateParams.id + '" could not be found.');
                    });
            }

            function getUserAgent() {
                var parser = new UAParser();
                parser.setUA(vm.event.data.request.user_agent);
                return parser.getResult();
            }

            function getBrowser() {
                var browser = getUserAgent().browser;
                return browser.name + ' ' + browser.version;
            }

            function getBrowserOS() {
                var os = getUserAgent().os;
                return os.name + ' ' + os.version;
            }

            function getDevice() {
                var device = getUserAgent().device;
                return device.model;
            }

            function getErrorType(){
                if (vm.event.data.error) {
                    return errorService.getTargetMethodType(vm.event.data.error);
                }

                if (vm.event.data.simple_error) {
                    return vm.event.data.simple_error.type;
                }

                return 'Unknown';
            }

            function getMessage() {
                if (vm.event.data.error) {
                    return errorService.getTargetException(vm.event.data.error).message;
                }

                return vm.event.message;
            }

            function getRequestUrl() {
                var request = vm.event.data.request;
                var path = request.is_secure ? 'https://' : 'http://';
                path += request.host;

                if (request.port !== 80 && request.port !== 443) {
                    path += ':' + request.port;
                }

                if (request.path.indexOf('/') !== 0) {
                    path += '/';
                }

                if (Object.keys(request.query_string).length > 0) {
                    path += '?';
                    for (var key in request.query_string){
                        path += key + '=' + request.query_string[key];
                    }
                }

                return path;
            }

            function hasCookies() {
                return Object.keys(vm.event.data.request.cookies).length > 0;
            }

            function hasDevice() {
                return hasUserAgent() && getDevice();
            }

            function hasEnvironmentInfo() {
                return vm.event.data && vm.event.data.environment;
            }

            function hasIdentity() {
                return vm.event.data && vm.event.data.user && vm.event.data.user.identity;
            }

            function hasReferrer(){
                return vm.event.data && vm.event.data.request && vm.event.data.request.referrer;
            }

            function hasRequestInfo() {
                return vm.event.data && vm.event.data.request;
            }

            function hasUserAgent() {
                return vm.event.data && vm.event.data.request && vm.event.data.request.user_agent;
            }

            function hasUserEmail() {
                return vm.event.data && vm.event.data.user_description && vm.event.data.user_description.email_address;
            }

            function hasUserDescription() {
                return vm.event.data && vm.event.data.user_description && vm.event.data.user_description.description;
            }

            function hasTags() {
                return vm.event.tags && vm.event.tags.length > 0;
            }

            function isError() {
                return vm.event.type === 'error';
            }

            vm.event = {};
            vm.getBrowser = getBrowser;
            vm.getBrowserOS = getBrowserOS;
            vm.getDevice = getDevice;
            vm.getErrorType = getErrorType;
            vm.getMessage = getMessage;
            vm.getRequestUrl = getRequestUrl;
            vm.hasCookies = hasCookies;
            vm.hasDevice = hasDevice;
            vm.hasIdentity = hasIdentity;
            vm.hasReferrer = hasReferrer;
            vm.hasRequestInfo = hasRequestInfo;
            vm.hasTags = hasTags;
            vm.hasUserAgent = hasUserAgent;
            vm.hasUserDescription = hasUserDescription;
            vm.hasUserEmail = hasUserEmail;
            vm.isError = isError;
            vm.tabs = [];

            get().then(createTabs);
        }]);
}());
