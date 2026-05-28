(function () {
    "use strict";

    angular
        .module("exceptionless.websocket", ["app.config", "exceptionless", "exceptionless.auth"])
        .factory("websocketService", function ($ExceptionlessClient, $rootScope, $timeout, authService, BASE_URL) {
            var ResilientWebSocket = (function () {
                function ResilientWebSocket(url, protocols) {
                    if (protocols === void 0) {
                        protocols = [];
                    }
                    this.reconnectInterval = 1000;
                    this.timeoutInterval = 2000;
                    this.forcedClose = false;
                    this.timedOut = false;
                    this.hasConnectedOnce = false;
                    this.protocols = [];
                    this.onopen = function (event) {};
                    this.onclose = function (event) {};
                    this.onconnecting = function () {};
                    this.onmessage = function (event) {};
                    this.onerror = function (event) {};
                    this.ontransportfallback = function (event) {
                        return false;
                    };
                    this.url = url;
                    this.protocols = protocols;
                    this.readyState = WebSocket.CONNECTING;
                    this.connect(false);
                }

                ResilientWebSocket.prototype.connect = function (reconnectAttempt) {
                    var _this = this;
                    this.ws = new WebSocket(this.url, this.protocols);
                    this.onconnecting();
                    var localWs = this.ws;
                    var timeout = setTimeout(function () {
                        _this.timedOut = true;
                        localWs.close();
                        _this.timedOut = false;
                    }, this.timeoutInterval);
                    this.ws.onopen = function (event) {
                        clearTimeout(timeout);
                        _this.readyState = WebSocket.OPEN;
                        _this.hasConnectedOnce = true;
                        reconnectAttempt = false;
                        _this.onopen(event);
                    };
                    this.ws.onclose = function (event) {
                        clearTimeout(timeout);
                        _this.ws = null;
                        if (_this.forcedClose) {
                            _this.readyState = WebSocket.CLOSED;
                            _this.onclose(event);
                        } else if (!_this.hasConnectedOnce && _this.ontransportfallback(event) === true) {
                            _this.readyState = WebSocket.CLOSED;
                        } else {
                            _this.readyState = WebSocket.CONNECTING;
                            _this.onconnecting();
                            if (!reconnectAttempt && !_this.timedOut) {
                                _this.onclose(event);
                            }
                            setTimeout(function () {
                                _this.connect(true);
                            }, _this.reconnectInterval);
                        }
                    };
                    this.ws.onmessage = function (event) {
                        _this.onmessage(event);
                    };
                    this.ws.onerror = function (event) {
                        _this.onerror(event);
                    };
                };
                ResilientWebSocket.prototype.send = function (data) {
                    if (this.ws) {
                        return this.ws.send(data);
                    }
                    throw new Error("INVALID_STATE_ERR : Pausing to reconnect websocket");
                };
                ResilientWebSocket.prototype.close = function () {
                    if (this.ws) {
                        this.forcedClose = true;
                        this.ws.close();
                        return true;
                    }
                    return false;
                };
                ResilientWebSocket.prototype.refresh = function () {
                    if (this.ws) {
                        this.ws.close();
                        return true;
                    }

                    return false;
                };

                return ResilientWebSocket;
            })();

            var source = "exceptionless.websocket.websocketService";
            var _connection;
            var _websocketTimeout;

            function start() {
                startDelayed(1);
            }

            function startDelayed(delay) {
                function startImpl() {
                    if (supportsWebSocket() && startWebSocket()) {
                        return;
                    }

                    if (!startSse()) {
                        $ExceptionlessClient.submitLog("No supported push transport is available.", "warn", source);
                    }
                }

                if (_connection || _websocketTimeout) {
                    stop();
                }

                _websocketTimeout = $timeout(startImpl, delay || 1000);
            }

            function startWebSocket() {
                // Keep WebSocket as the preferred Angular transport during rollout so existing
                // release notification refresh behavior stays unchanged until SSE fully replaces it.
                try {
                    _connection = new ResilientWebSocket(getWebSocketPushUrl());
                } catch (error) {
                    _connection = null;
                    return false;
                }

                _connection.ontransportfallback = function () {
                    return startSse();
                };
                _connection.onmessage = function (ev) {
                    handleMessage(ev.data);
                };

                return true;
            }

            function startSse() {
                if (typeof EventSource === "undefined") {
                    return false;
                }

                _connection = new EventSource(getSsePushUrl());
                _connection.onmessage = function (ev) {
                    handleMessage(ev.data);
                };

                return true;
            }

            function handleMessage(payload) {
                var data = payload ? JSON.parse(payload) : null;
                if (!data || !data.type) {
                    return;
                }

                if (data.message && data.message.change_type >= 0) {
                    data.message.added = data.message.change_type === 0;
                    data.message.updated = data.message.change_type === 1;
                    data.message.deleted = data.message.change_type === 2;
                }

                $rootScope.$emit(data.type, data.message);

                // This event is fired when a user is added or removed from an organization.
                if (data.type === "UserMembershipChanged" && data.message && data.message.organization_id) {
                    $rootScope.$emit("OrganizationChanged", data.message);
                    $rootScope.$emit("ProjectChanged", data.message);
                }
            }

            function stop() {
                if (_websocketTimeout) {
                    $timeout.cancel(_websocketTimeout);
                    _websocketTimeout = null;
                }

                if (_connection) {
                    var connection = _connection;
                    _connection = null;

                    if (connection.close) {
                        connection.close();
                    }
                }
            }

            function supportsWebSocket() {
                return typeof WebSocket !== "undefined";
            }

            function getWebSocketPushUrl() {
                var pushUrl = getSsePushUrl();
                var protoMatch = /^(https?):\/\//;
                if (BASE_URL.startsWith("https:")) {
                    return pushUrl.replace(protoMatch, "wss://");
                }

                return pushUrl.replace(protoMatch, "ws://");
            }

            function getSsePushUrl() {
                return BASE_URL + "/api/v2/push?access_token=" + authService.getToken();
            }

            var service = {
                start: start,
                startDelayed: startDelayed,
                stop: stop,
            };

            return service;
        });
})();
