/* eslint-disable */
/**
 * Satellizer 0.15.5
 * (c) 2016 Sahat Yalkabov
 * License: MIT
 */

(function (global, factory) {
    typeof exports === "object" && typeof module !== "undefined"
        ? (module.exports = factory())
        : typeof define === "function" && define.amd
        ? define(factory)
        : (global.satellizer = factory());
})(this, function () {
    "use strict";

    var Config = (function () {
        function Config() {
            this.baseUrl = "/";
            this.loginUrl = "/auth/login";
            this.signupUrl = "/auth/signup";
            this.unlinkUrl = "/auth/unlink/";
            this.tokenName = "token";
            this.tokenPrefix = "satellizer";
            this.tokenHeader = "Authorization";
            this.tokenType = "Bearer";
            this.storageType = "localStorage";
            this.tokenRoot = null;
            this.withCredentials = false;
            this.providers = {
                facebook: {
                    name: "facebook",
                    url: "/auth/facebook",
                    authorizationEndpoint: "https://www.facebook.com/v2.5/dialog/oauth",
                    redirectUri: window.location.origin + "/",
                    requiredUrlParams: ["display", "scope"],
                    scope: ["email"],
                    scopeDelimiter: ",",
                    display: "popup",
                    oauthType: "2.0",
                    popupOptions: { width: 580, height: 400 },
                },
                google: {
                    name: "google",
                    url: "/auth/google",
                    authorizationEndpoint: "https://accounts.google.com/o/oauth2/auth",
                    redirectUri: window.location.origin,
                    requiredUrlParams: ["scope"],
                    optionalUrlParams: ["display", "state"],
                    scope: ["profile", "email"],
                    scopePrefix: "openid",
                    scopeDelimiter: " ",
                    display: "popup",
                    oauthType: "2.0",
                    popupOptions: { width: 452, height: 633 },
                    state: function () {
                        return encodeURIComponent(Math.random().toString(36).substr(2));
                    },
                },
                github: {
                    name: "github",
                    url: "/auth/github",
                    authorizationEndpoint: "https://github.com/login/oauth/authorize",
                    redirectUri: window.location.origin,
                    optionalUrlParams: ["scope"],
                    scope: ["user:email"],
                    scopeDelimiter: " ",
                    oauthType: "2.0",
                    popupOptions: { width: 1020, height: 618 },
                },
                instagram: {
                    name: "instagram",
                    url: "/auth/instagram",
                    authorizationEndpoint: "https://api.instagram.com/oauth/authorize",
                    redirectUri: window.location.origin,
                    requiredUrlParams: ["scope"],
                    scope: ["basic"],
                    scopeDelimiter: "+",
                    oauthType: "2.0",
                },
                linkedin: {
                    name: "linkedin",
                    url: "/auth/linkedin",
                    authorizationEndpoint: "https://www.linkedin.com/uas/oauth2/authorization",
                    redirectUri: window.location.origin,
                    requiredUrlParams: ["state"],
                    scope: ["r_emailaddress"],
                    scopeDelimiter: " ",
                    state: "STATE",
                    oauthType: "2.0",
                    popupOptions: { width: 527, height: 582 },
                },
                twitter: {
                    name: "twitter",
                    url: "/auth/twitter",
                    authorizationEndpoint: "https://api.twitter.com/oauth/authenticate",
                    redirectUri: window.location.origin,
                    oauthType: "1.0",
                    popupOptions: { width: 495, height: 645 },
                },
                twitch: {
                    name: "twitch",
                    url: "/auth/twitch",
                    authorizationEndpoint: "https://api.twitch.tv/kraken/oauth2/authorize",
                    redirectUri: window.location.origin,
                    requiredUrlParams: ["scope"],
                    scope: ["user_read"],
                    scopeDelimiter: " ",
                    display: "popup",
                    oauthType: "2.0",
                    popupOptions: { width: 500, height: 560 },
                },
                live: {
                    name: "live",
                    url: "/auth/live",
                    authorizationEndpoint: "https://login.live.com/oauth20_authorize.srf",
                    redirectUri: window.location.origin,
                    requiredUrlParams: ["display", "scope"],
                    scope: ["wl.emails"],
                    scopeDelimiter: " ",
                    display: "popup",
                    oauthType: "2.0",
                    popupOptions: { width: 500, height: 560 },
                },
                yahoo: {
                    name: "yahoo",
                    url: "/auth/yahoo",
                    authorizationEndpoint: "https://api.login.yahoo.com/oauth2/request_auth",
                    redirectUri: window.location.origin,
                    scope: [],
                    scopeDelimiter: ",",
                    oauthType: "2.0",
                    popupOptions: { width: 559, height: 519 },
                },
                bitbucket: {
                    name: "bitbucket",
                    url: "/auth/bitbucket",
                    authorizationEndpoint: "https://bitbucket.org/site/oauth2/authorize",
                    redirectUri: window.location.origin + "/",
                    requiredUrlParams: ["scope"],
                    scope: ["email"],
                    scopeDelimiter: " ",
                    oauthType: "2.0",
                    popupOptions: { width: 1028, height: 529 },
                },
                spotify: {
                    name: "spotify",
                    url: "/auth/spotify",
                    authorizationEndpoint: "https://accounts.spotify.com/authorize",
                    redirectUri: window.location.origin,
                    optionalUrlParams: ["state"],
                    requiredUrlParams: ["scope"],
                    scope: ["user-read-email"],
                    scopePrefix: "",
                    scopeDelimiter: ",",
                    oauthType: "2.0",
                    popupOptions: { width: 500, height: 530 },
                    state: function () {
                        return encodeURIComponent(Math.random().toString(36).substr(2));
                    },
                },
            };
            this.httpInterceptor = function () {
                return true;
            };
        }
        Object.defineProperty(Config, "getConstant", {
            get: function () {
                return new Config();
            },
            enumerable: true,
            configurable: true,
        });
        return Config;
    })();
    var AuthProvider = (function () {
        function AuthProvider(SatellizerConfig) {
            this.SatellizerConfig = SatellizerConfig;
        }
        Object.defineProperty(AuthProvider.prototype, "baseUrl", {
            get: function () {
                return this.SatellizerConfig.baseUrl;
            },
            set: function (value) {
                this.SatellizerConfig.baseUrl = value;
            },
            enumerable: true,
            configurable: true,
        });
        Object.defineProperty(AuthProvider.prototype, "loginUrl", {
            get: function () {
                return this.SatellizerConfig.loginUrl;
            },
            set: function (value) {
                this.SatellizerConfig.loginUrl = value;
            },
            enumerable: true,
            configurable: true,
        });
        Object.defineProperty(AuthProvider.prototype, "signupUrl", {
            get: function () {
                return this.SatellizerConfig.signupUrl;
            },
            set: function (value) {
                this.SatellizerConfig.signupUrl = value;
            },
            enumerable: true,
            configurable: true,
        });
        Object.defineProperty(AuthProvider.prototype, "unlinkUrl", {
            get: function () {
                return this.SatellizerConfig.unlinkUrl;
            },
            set: function (value) {
                this.SatellizerConfig.unlinkUrl = value;
            },
            enumerable: true,
            configurable: true,
        });
        Object.defineProperty(AuthProvider.prototype, "tokenRoot", {
            get: function () {
                return this.SatellizerConfig.tokenRoot;
            },
            set: function (value) {
                this.SatellizerConfig.tokenRoot = value;
            },
            enumerable: true,
            configurable: true,
        });
        Object.defineProperty(AuthProvider.prototype, "tokenName", {
            get: function () {
                return this.SatellizerConfig.tokenName;
            },
            set: function (value) {
                this.SatellizerConfig.tokenName = value;
            },
            enumerable: true,
            configurable: true,
        });
        Object.defineProperty(AuthProvider.prototype, "tokenPrefix", {
            get: function () {
                return this.SatellizerConfig.tokenPrefix;
            },
            set: function (value) {
                this.SatellizerConfig.tokenPrefix = value;
            },
            enumerable: true,
            configurable: true,
        });
        Object.defineProperty(AuthProvider.prototype, "tokenHeader", {
            get: function () {
                return this.SatellizerConfig.tokenHeader;
            },
            set: function (value) {
                this.SatellizerConfig.tokenHeader = value;
            },
            enumerable: true,
            configurable: true,
        });
        Object.defineProperty(AuthProvider.prototype, "tokenType", {
            get: function () {
                return this.SatellizerConfig.tokenType;
            },
            set: function (value) {
                this.SatellizerConfig.tokenType = value;
            },
            enumerable: true,
            configurable: true,
        });
        Object.defineProperty(AuthProvider.prototype, "withCredentials", {
            get: function () {
                return this.SatellizerConfig.withCredentials;
            },
            set: function (value) {
                this.SatellizerConfig.withCredentials = value;
            },
            enumerable: true,
            configurable: true,
        });
        Object.defineProperty(AuthProvider.prototype, "storageType", {
            get: function () {
                return this.SatellizerConfig.storageType;
            },
            set: function (value) {
                this.SatellizerConfig.storageType = value;
            },
            enumerable: true,
            configurable: true,
        });
        Object.defineProperty(AuthProvider.prototype, "httpInterceptor", {
            get: function () {
                return this.SatellizerConfig.httpInterceptor;
            },
            set: function (value) {
                if (typeof value === "function") {
                    this.SatellizerConfig.httpInterceptor = value;
                } else {
                    this.SatellizerConfig.httpInterceptor = function () {
                        return value;
                    };
                }
            },
            enumerable: true,
            configurable: true,
        });
        AuthProvider.prototype.facebook = function (options) {
            angular.extend(this.SatellizerConfig.providers.facebook, options);
        };
        AuthProvider.prototype.google = function (options) {
            angular.extend(this.SatellizerConfig.providers.google, options);
        };
        AuthProvider.prototype.github = function (options) {
            angular.extend(this.SatellizerConfig.providers.github, options);
        };
        AuthProvider.prototype.instagram = function (options) {
            angular.extend(this.SatellizerConfig.providers.instagram, options);
        };
        AuthProvider.prototype.linkedin = function (options) {
            angular.extend(this.SatellizerConfig.providers.linkedin, options);
        };
        AuthProvider.prototype.twitter = function (options) {
            angular.extend(this.SatellizerConfig.providers.twitter, options);
        };
        AuthProvider.prototype.twitch = function (options) {
            angular.extend(this.SatellizerConfig.providers.twitch, options);
        };
        AuthProvider.prototype.live = function (options) {
            angular.extend(this.SatellizerConfig.providers.live, options);
        };
        AuthProvider.prototype.yahoo = function (options) {
            angular.extend(this.SatellizerConfig.providers.yahoo, options);
        };
        AuthProvider.prototype.bitbucket = function (options) {
            angular.extend(this.SatellizerConfig.providers.bitbucket, options);
        };
        AuthProvider.prototype.spotify = function (options) {
            angular.extend(this.SatellizerConfig.providers.spotify, options);
        };
        AuthProvider.prototype.oauth1 = function (options) {
            this.SatellizerConfig.providers[options.name] = angular.extend(options, {
                oauthType: "1.0",
            });
        };
        AuthProvider.prototype.oauth2 = function (options) {
            this.SatellizerConfig.providers[options.name] = angular.extend(options, {
                oauthType: "2.0",
            });
        };
        AuthProvider.prototype.$get = function (SatellizerShared, SatellizerLocal, SatellizerOAuth) {
            return {
                login: function (user, options) {
                    return SatellizerLocal.login(user, options);
                },
                signup: function (user, options) {
                    return SatellizerLocal.signup(user, options);
                },
                logout: function () {
                    return SatellizerShared.logout();
                },
                authenticate: function (name, data) {
                    return SatellizerOAuth.authenticate(name, data);
                },
                link: function (name, data) {
                    console.log(this);
                    return SatellizerOAuth.authenticate(name, data);
                },
                unlink: function (name, options) {
                    return SatellizerOAuth.unlink(name, options);
                },
                isAuthenticated: function () {
                    return SatellizerShared.isAuthenticated();
                },
                getPayload: function () {
                    return SatellizerShared.getPayload();
                },
                getToken: function () {
                    return SatellizerShared.getToken();
                },
                setToken: function (token) {
                    return SatellizerShared.setToken({ access_token: token });
                },
                removeToken: function () {
                    return SatellizerShared.removeToken();
                },
                setStorageType: function (type) {
                    return SatellizerShared.setStorageType(type);
                },
            };
        };
        AuthProvider.$inject = ["SatellizerConfig"];
        return AuthProvider;
    })();
    AuthProvider.prototype.$get.$inject = ["SatellizerShared", "SatellizerLocal", "SatellizerOAuth"];

    function joinUrl(baseUrl, url) {
        if (/^(?:[a-z]+:)?\/\//i.test(url)) {
            return url;
        }
        var joined = [baseUrl, url].join("/");
        var normalize = function (str) {
            return str.replace(/[\/]+/g, "/").replace(/\/\?/g, "?").replace(/\/\#/g, "#").replace(/\:\//g, "://");
        };
        return normalize(joined);
    }
    function getFullUrlPath(location) {
        var isHttps = location.protocol === "https:";
        return (
            location.protocol +
            "//" +
            location.hostname +
            ":" +
            (location.port || (isHttps ? "443" : "80")) +
            (/^\//.test(location.pathname) ? location.pathname : "/" + location.pathname)
        );
    }
    function parseQueryString(str) {
        var obj = {};
        var key;
        var value;
        angular.forEach((str || "").split("&"), function (keyValue) {
            if (keyValue) {
                value = keyValue.split("=");
                key = decodeURIComponent(value[0]);
                obj[key] = angular.isDefined(value[1]) ? decodeURIComponent(value[1]) : true;
            }
        });
        return obj;
    }
    function decodeBase64(str) {
        var buffer;
        if (typeof module !== "undefined" && module.exports) {
            try {
                buffer = require("buffer").Buffer;
            } catch (err) {}
        }
        var fromCharCode = String.fromCharCode;
        var re_btou = new RegExp(
            ["[\xC0-\xDF][\x80-\xBF]", "[\xE0-\xEF][\x80-\xBF]{2}", "[\xF0-\xF7][\x80-\xBF]{3}"].join("|"),
            "g"
        );
        var cb_btou = function (cccc) {
            switch (cccc.length) {
                case 4:
                    var cp =
                        ((0x07 & cccc.charCodeAt(0)) << 18) |
                        ((0x3f & cccc.charCodeAt(1)) << 12) |
                        ((0x3f & cccc.charCodeAt(2)) << 6) |
                        (0x3f & cccc.charCodeAt(3));
                    var offset = cp - 0x10000;
                    return fromCharCode((offset >>> 10) + 0xd800) + fromCharCode((offset & 0x3ff) + 0xdc00);
                case 3:
                    return fromCharCode(
                        ((0x0f & cccc.charCodeAt(0)) << 12) |
                            ((0x3f & cccc.charCodeAt(1)) << 6) |
                            (0x3f & cccc.charCodeAt(2))
                    );
                default:
                    return fromCharCode(((0x1f & cccc.charCodeAt(0)) << 6) | (0x3f & cccc.charCodeAt(1)));
            }
        };
        var btou = function (b) {
            return b.replace(re_btou, cb_btou);
        };
        var _decode = buffer
            ? function (a) {
                  return (a.constructor === buffer.constructor ? a : new buffer(a, "base64")).toString();
              }
            : function (a) {
                  return btou(atob(a));
              };
        return _decode(
            String(str)
                .replace(/[-_]/g, function (m0) {
                    return m0 === "-" ? "+" : "/";
                })
                .replace(/[^A-Za-z0-9\+\/]/g, "")
        );
    }

    var Shared = (function () {
        function Shared($q, $window, SatellizerConfig, SatellizerStorage) {
            this.$q = $q;
            this.$window = $window;
            this.SatellizerConfig = SatellizerConfig;
            this.SatellizerStorage = SatellizerStorage;
            var _a = this.SatellizerConfig,
                tokenName = _a.tokenName,
                tokenPrefix = _a.tokenPrefix;
            this.prefixedTokenName = tokenPrefix ? [tokenPrefix, tokenName].join("_") : tokenName;
        }
        Shared.prototype.getToken = function () {
            return this.SatellizerStorage.get(this.prefixedTokenName);
        };
        Shared.prototype.getPayload = function () {
            var token = this.SatellizerStorage.get(this.prefixedTokenName);
            if (token && token.split(".").length === 3) {
                try {
                    var base64Url = token.split(".")[1];
                    var base64 = base64Url.replace("-", "+").replace("_", "/");
                    return JSON.parse(decodeBase64(base64));
                } catch (e) {}
            }
        };
        Shared.prototype.setToken = function (response) {
            var tokenRoot = this.SatellizerConfig.tokenRoot;
            var tokenName = this.SatellizerConfig.tokenName;
            var accessToken = response && response.access_token;
            var token;
            if (accessToken) {
                if (angular.isObject(accessToken) && angular.isObject(accessToken.data)) {
                    response = accessToken;
                } else if (angular.isString(accessToken)) {
                    token = accessToken;
                }
            }
            if (!token && response) {
                var tokenRootData =
                    tokenRoot &&
                    tokenRoot.split(".").reduce(function (o, x) {
                        return o[x];
                    }, response.data);
                token = tokenRootData ? tokenRootData[tokenName] : response.data && response.data[tokenName];
            }
            if (token) {
                this.SatellizerStorage.set(this.prefixedTokenName, token);
            }
        };
        Shared.prototype.removeToken = function () {
            this.SatellizerStorage.remove(this.prefixedTokenName);
        };
        Shared.prototype.isAuthenticated = function () {
            var token = this.SatellizerStorage.get(this.prefixedTokenName);
            if (token) {
                if (token.split(".").length === 3) {
                    try {
                        var base64Url = token.split(".")[1];
                        var base64 = base64Url.replace("-", "+").replace("_", "/");
                        var exp = JSON.parse(this.$window.atob(base64)).exp;
                        if (typeof exp === "number") {
                            return Math.round(new Date().getTime() / 1000) < exp;
                        }
                    } catch (e) {
                        return true; // Pass: Non-JWT token that looks like JWT
                    }
                }
                return true; // Pass: All other tokens
            }
            return false; // Fail: No token at all
        };
        Shared.prototype.logout = function () {
            this.SatellizerStorage.remove(this.prefixedTokenName);
            return this.$q.when();
        };
        Shared.prototype.setStorageType = function (type) {
            this.SatellizerConfig.storageType = type;
        };
        Shared.$inject = ["$q", "$window", "SatellizerConfig", "SatellizerStorage"];
        return Shared;
    })();

    var Local = (function () {
        function Local($http, SatellizerConfig, SatellizerShared) {
            this.$http = $http;
            this.SatellizerConfig = SatellizerConfig;
            this.SatellizerShared = SatellizerShared;
        }
        Local.prototype.login = function (user, options) {
            var _this = this;
            if (options === void 0) {
                options = {};
            }
            options.url = options.url
                ? options.url
                : joinUrl(this.SatellizerConfig.baseUrl, this.SatellizerConfig.loginUrl);
            options.data = user || options.data;
            options.method = options.method || "POST";
            options.withCredentials = options.withCredentials || this.SatellizerConfig.withCredentials;
            return this.$http(options).then(function (response) {
                _this.SatellizerShared.setToken(response);
                return response;
            });
        };
        Local.prototype.signup = function (user, options) {
            if (options === void 0) {
                options = {};
            }
            options.url = options.url
                ? options.url
                : joinUrl(this.SatellizerConfig.baseUrl, this.SatellizerConfig.signupUrl);
            options.data = user || options.data;
            options.method = options.method || "POST";
            options.withCredentials = options.withCredentials || this.SatellizerConfig.withCredentials;
            return this.$http(options);
        };
        Local.$inject = ["$http", "SatellizerConfig", "SatellizerShared"];
        return Local;
    })();

    var Popup = (function () {
        function Popup($interval, $window, $q) {
            this.$interval = $interval;
            this.$window = $window;
            this.$q = $q;
            this.popup = null;
            this.defaults = {
                redirectUri: null,
            };
        }
        Popup.prototype.stringifyOptions = function (options) {
            var parts = [];
            angular.forEach(options, function (value, key) {
                parts.push(key + "=" + value);
            });
            return parts.join(",");
        };
        Popup.prototype.open = function (url, name, popupOptions, redirectUri, dontPoll) {
            var width = popupOptions.width || 500;
            var height = popupOptions.height || 500;
            var options = this.stringifyOptions({
                width: width,
                height: height,
                top: this.$window.screenY + (this.$window.outerHeight - height) / 2.5,
                left: this.$window.screenX + (this.$window.outerWidth - width) / 2,
            });
            var popupName =
                this.$window["cordova"] || this.$window.navigator.userAgent.indexOf("CriOS") > -1 ? "_blank" : name;
            this.popup = this.$window.open(url, popupName, options);
            if (this.popup && this.popup.focus) {
                this.popup.focus();
            }
            if (dontPoll) {
                return;
            }
            if (this.$window["cordova"]) {
                return this.eventListener(redirectUri);
            } else {
                if (url === "about:blank") {
                    this.popup.location = url;
                }
                return this.polling(redirectUri);
            }
        };
        Popup.prototype.polling = function (redirectUri) {
            var _this = this;
            return this.$q(function (resolve, reject) {
                var redirectUriParser = document.createElement("a");
                redirectUriParser.href = redirectUri;
                var redirectUriPath = getFullUrlPath(redirectUriParser);
                var polling = _this.$interval(function () {
                    if (!_this.popup || _this.popup.closed || _this.popup.closed === undefined) {
                        _this.$interval.cancel(polling);
                        reject(new Error("The popup window was closed"));
                    }
                    try {
                        var popupWindowPath = getFullUrlPath(_this.popup.location);
                        if (popupWindowPath === redirectUriPath) {
                            if (_this.popup.location.search || _this.popup.location.hash) {
                                var query = parseQueryString(
                                    _this.popup.location.search.substring(1).replace(/\/$/, "")
                                );
                                var hash = parseQueryString(
                                    _this.popup.location.hash.substring(1).replace(/[\/$]/, "")
                                );
                                var params = angular.extend({}, query, hash);
                                if (params.error) {
                                    reject(new Error(params.error));
                                } else {
                                    resolve(params);
                                }
                            } else {
                                reject(
                                    new Error(
                                        "OAuth redirect has occurred but no query or hash parameters were found. " +
                                            "They were either not set during the redirect, or were removed—typically by a " +
                                            "routing library—before Satellizer could read it."
                                    )
                                );
                            }
                            _this.$interval.cancel(polling);
                            _this.popup.close();
                        }
                    } catch (error) {}
                }, 500);
            });
        };
        Popup.prototype.eventListener = function (redirectUri) {
            var _this = this;
            return this.$q(function (resolve, reject) {
                _this.popup.addEventListener("loadstart", function (event) {
                    if (event.url.indexOf(redirectUri) !== 0) {
                        return;
                    }
                    var parser = document.createElement("a");
                    parser.href = event.url;
                    if (parser.search || parser.hash) {
                        var query = parseQueryString(parser.search.substring(1).replace(/\/$/, ""));
                        var hash = parseQueryString(parser.hash.substring(1).replace(/[\/$]/, ""));
                        var params = angular.extend({}, query, hash);
                        if (params.error) {
                            reject(new Error(params.error));
                        } else {
                            resolve(params);
                        }
                        _this.popup.close();
                    }
                });
                _this.popup.addEventListener("loaderror", function () {
                    reject(new Error("Authorization failed"));
                });
                _this.popup.addEventListener("exit", function () {
                    reject(new Error("The popup window was closed"));
                });
            });
        };
        Popup.$inject = ["$interval", "$window", "$q"];
        return Popup;
    })();

    var OAuth1 = (function () {
        function OAuth1($http, $window, SatellizerConfig, SatellizerPopup) {
            this.$http = $http;
            this.$window = $window;
            this.SatellizerConfig = SatellizerConfig;
            this.SatellizerPopup = SatellizerPopup;
            this.defaults = {
                name: null,
                url: null,
                authorizationEndpoint: null,
                scope: null,
                scopePrefix: null,
                scopeDelimiter: null,
                redirectUri: null,
                requiredUrlParams: null,
                defaultUrlParams: null,
                oauthType: "1.0",
                popupOptions: { width: null, height: null },
            };
        }
        OAuth1.prototype.init = function (options, userData) {
            var _this = this;
            angular.extend(this.defaults, options);
            var name = options.name,
                popupOptions = options.popupOptions;
            var redirectUri = this.defaults.redirectUri;
            // Should open an empty popup and wait until request token is received
            if (!this.$window["cordova"]) {
                this.SatellizerPopup.open("about:blank", name, popupOptions, redirectUri, true);
            }
            return this.getRequestToken().then(function (response) {
                return _this.openPopup(options, response).then(function (popupResponse) {
                    return _this.exchangeForToken(popupResponse, userData);
                });
            });
        };
        OAuth1.prototype.openPopup = function (options, response) {
            var url = [options.authorizationEndpoint, this.buildQueryString(response.data)].join("?");
            var redirectUri = this.defaults.redirectUri;
            if (this.$window["cordova"]) {
                return this.SatellizerPopup.open(url, options.name, options.popupOptions, redirectUri);
            } else {
                this.SatellizerPopup.popup.location = url;
                return this.SatellizerPopup.polling(redirectUri);
            }
        };
        OAuth1.prototype.getRequestToken = function () {
            var url = this.SatellizerConfig.baseUrl
                ? joinUrl(this.SatellizerConfig.baseUrl, this.defaults.url)
                : this.defaults.url;
            return this.$http.post(url, this.defaults);
        };
        OAuth1.prototype.exchangeForToken = function (oauthData, userData) {
            var payload = angular.extend({}, userData, oauthData);
            var exchangeForTokenUrl = this.SatellizerConfig.baseUrl
                ? joinUrl(this.SatellizerConfig.baseUrl, this.defaults.url)
                : this.defaults.url;
            return this.$http.post(exchangeForTokenUrl, payload, {
                withCredentials: this.SatellizerConfig.withCredentials,
            });
        };
        OAuth1.prototype.buildQueryString = function (obj) {
            var str = [];
            angular.forEach(obj, function (value, key) {
                str.push(encodeURIComponent(key) + "=" + encodeURIComponent(value));
            });
            return str.join("&");
        };
        OAuth1.$inject = ["$http", "$window", "SatellizerConfig", "SatellizerPopup"];
        return OAuth1;
    })();

    var OAuth2 = (function () {
        function OAuth2($http, $window, $timeout, $q, SatellizerConfig, SatellizerPopup, SatellizerStorage) {
            this.$http = $http;
            this.$window = $window;
            this.$timeout = $timeout;
            this.$q = $q;
            this.SatellizerConfig = SatellizerConfig;
            this.SatellizerPopup = SatellizerPopup;
            this.SatellizerStorage = SatellizerStorage;
            this.defaults = {
                name: null,
                url: null,
                clientId: null,
                authorizationEndpoint: null,
                redirectUri: null,
                scope: null,
                scopePrefix: null,
                scopeDelimiter: null,
                state: null,
                requiredUrlParams: null,
                defaultUrlParams: ["response_type", "client_id", "redirect_uri"],
                responseType: "code",
                responseParams: {
                    code: "code",
                    clientId: "clientId",
                    redirectUri: "redirectUri",
                },
                oauthType: "2.0",
                popupOptions: { width: null, height: null },
            };
        }
        OAuth2.camelCase = function (name) {
            return name.replace(/([\:\-\_]+(.))/g, function (_, separator, letter, offset) {
                return offset ? letter.toUpperCase() : letter;
            });
        };
        OAuth2.prototype.init = function (options, userData) {
            var _this = this;
            return this.$q(function (resolve, reject) {
                angular.extend(_this.defaults, options);
                var stateName = _this.defaults.name + "_state";
                var _a = _this.defaults,
                    name = _a.name,
                    state = _a.state,
                    popupOptions = _a.popupOptions,
                    redirectUri = _a.redirectUri,
                    responseType = _a.responseType;
                if (typeof state === "function") {
                    _this.SatellizerStorage.set(stateName, state());
                } else if (typeof state === "string") {
                    _this.SatellizerStorage.set(stateName, state);
                }
                var url = [_this.defaults.authorizationEndpoint, _this.buildQueryString()].join("?");
                _this.SatellizerPopup.open(url, name, popupOptions, redirectUri)
                    .then(function (oauth) {
                        if (responseType === "token" || !url) {
                            return resolve(oauth);
                        }
                        if (oauth.state && oauth.state !== _this.SatellizerStorage.get(stateName)) {
                            return reject(
                                new Error(
                                    "The value returned in the state parameter does not match the state value from your original " +
                                        "authorization code request."
                                )
                            );
                        }
                        resolve(_this.exchangeForToken(oauth, userData));
                    })
                    .catch(function (error) {
                        return reject(error);
                    });
            });
        };
        OAuth2.prototype.exchangeForToken = function (oauthData, userData) {
            var _this = this;
            var payload = angular.extend({}, userData);
            angular.forEach(this.defaults.responseParams, function (value, key) {
                switch (key) {
                    case "code":
                        payload[value] = oauthData.code;
                        break;
                    case "clientId":
                        payload[value] = _this.defaults.clientId;
                        break;
                    case "redirectUri":
                        payload[value] = _this.defaults.redirectUri;
                        break;
                    default:
                        payload[value] = oauthData[key];
                }
            });
            if (oauthData.state) {
                payload.state = oauthData.state;
            }

            if (!this.defaults.url) {
                return payload;
            }

            var exchangeForTokenUrl = this.SatellizerConfig.baseUrl
                ? joinUrl(this.SatellizerConfig.baseUrl, this.defaults.url)
                : this.defaults.url;
            return this.$http.post(exchangeForTokenUrl, payload, {
                withCredentials: this.SatellizerConfig.withCredentials,
            });
        };
        OAuth2.prototype.buildQueryString = function () {
            var _this = this;
            var keyValuePairs = [];
            var urlParamsCategories = ["defaultUrlParams", "requiredUrlParams", "optionalUrlParams"];
            angular.forEach(urlParamsCategories, function (paramsCategory) {
                angular.forEach(_this.defaults[paramsCategory], function (paramName) {
                    var camelizedName = OAuth2.camelCase(paramName);
                    var paramValue = angular.isFunction(_this.defaults[paramName])
                        ? _this.defaults[paramName]()
                        : _this.defaults[camelizedName];
                    if (paramName === "redirect_uri" && !paramValue) {
                        return;
                    }
                    if (paramName === "state") {
                        var stateName = _this.defaults.name + "_state";
                        paramValue = encodeURIComponent(_this.SatellizerStorage.get(stateName));
                    }
                    if (paramName === "scope" && Array.isArray(paramValue)) {
                        paramValue = paramValue.join(_this.defaults.scopeDelimiter);
                        if (_this.defaults.scopePrefix) {
                            paramValue = [_this.defaults.scopePrefix, paramValue].join(_this.defaults.scopeDelimiter);
                        }
                    }
                    keyValuePairs.push([paramName, paramValue]);
                });
            });
            return keyValuePairs
                .map(function (pair) {
                    return pair.join("=");
                })
                .join("&");
        };
        OAuth2.$inject = [
            "$http",
            "$window",
            "$timeout",
            "$q",
            "SatellizerConfig",
            "SatellizerPopup",
            "SatellizerStorage",
        ];
        return OAuth2;
    })();

    var OAuth = (function () {
        function OAuth(
            $http,
            $window,
            $timeout,
            $q,
            SatellizerConfig,
            SatellizerPopup,
            SatellizerStorage,
            SatellizerShared,
            SatellizerOAuth1,
            SatellizerOAuth2
        ) {
            this.$http = $http;
            this.$window = $window;
            this.$timeout = $timeout;
            this.$q = $q;
            this.SatellizerConfig = SatellizerConfig;
            this.SatellizerPopup = SatellizerPopup;
            this.SatellizerStorage = SatellizerStorage;
            this.SatellizerShared = SatellizerShared;
            this.SatellizerOAuth1 = SatellizerOAuth1;
            this.SatellizerOAuth2 = SatellizerOAuth2;
        }
        OAuth.prototype.authenticate = function (name, userData) {
            var _this = this;
            return this.$q(function (resolve, reject) {
                var provider = _this.SatellizerConfig.providers[name];
                var oauth = null;
                switch (provider.oauthType) {
                    case "1.0":
                        oauth = new OAuth1(_this.$http, _this.$window, _this.SatellizerConfig, _this.SatellizerPopup);
                        break;
                    case "2.0":
                        oauth = new OAuth2(
                            _this.$http,
                            _this.$window,
                            _this.$timeout,
                            _this.$q,
                            _this.SatellizerConfig,
                            _this.SatellizerPopup,
                            _this.SatellizerStorage
                        );
                        break;
                    default:
                        return reject(new Error("Invalid OAuth Type"));
                }
                return oauth
                    .init(provider, userData)
                    .then(function (response) {
                        if (provider.url) {
                            _this.SatellizerShared.setToken(response);
                        }
                        resolve(response);
                    })
                    .catch(function (error) {
                        reject(error);
                    });
            });
        };
        OAuth.prototype.unlink = function (provider, httpOptions) {
            if (httpOptions === void 0) {
                httpOptions = {};
            }
            httpOptions.url = httpOptions.url
                ? httpOptions.url
                : joinUrl(this.SatellizerConfig.baseUrl, this.SatellizerConfig.unlinkUrl);
            httpOptions.data = { provider: provider } || httpOptions.data;
            httpOptions.method = httpOptions.method || "POST";
            httpOptions.withCredentials = httpOptions.withCredentials || this.SatellizerConfig.withCredentials;
            return this.$http(httpOptions);
        };
        OAuth.$inject = [
            "$http",
            "$window",
            "$timeout",
            "$q",
            "SatellizerConfig",
            "SatellizerPopup",
            "SatellizerStorage",
            "SatellizerShared",
            "SatellizerOAuth1",
            "SatellizerOAuth2",
        ];
        return OAuth;
    })();

    var Storage = (function () {
        function Storage($window, SatellizerConfig) {
            this.$window = $window;
            this.SatellizerConfig = SatellizerConfig;
            this.memoryStore = {};
        }
        Storage.prototype.get = function (key) {
            try {
                return this.$window[this.SatellizerConfig.storageType].getItem(key);
            } catch (e) {
                return this.memoryStore[key];
            }
        };
        Storage.prototype.set = function (key, value) {
            try {
                this.$window[this.SatellizerConfig.storageType].setItem(key, value);
            } catch (e) {
                this.memoryStore[key] = value;
            }
        };
        Storage.prototype.remove = function (key) {
            try {
                this.$window[this.SatellizerConfig.storageType].removeItem(key);
            } catch (e) {
                delete this.memoryStore[key];
            }
        };
        Storage.$inject = ["$window", "SatellizerConfig"];
        return Storage;
    })();

    var Interceptor = (function () {
        function Interceptor(SatellizerConfig, SatellizerShared, SatellizerStorage) {
            var _this = this;
            this.SatellizerConfig = SatellizerConfig;
            this.SatellizerShared = SatellizerShared;
            this.SatellizerStorage = SatellizerStorage;
            this.request = function (config) {
                if (config["skipAuthorization"]) {
                    return config;
                }
                if (_this.SatellizerShared.isAuthenticated() && _this.SatellizerConfig.httpInterceptor()) {
                    var tokenName = _this.SatellizerConfig.tokenPrefix
                        ? [_this.SatellizerConfig.tokenPrefix, _this.SatellizerConfig.tokenName].join("_")
                        : _this.SatellizerConfig.tokenName;
                    var token = _this.SatellizerStorage.get(tokenName);
                    if (_this.SatellizerConfig.tokenHeader && _this.SatellizerConfig.tokenType) {
                        token = _this.SatellizerConfig.tokenType + " " + token;
                    }
                    config.headers[_this.SatellizerConfig.tokenHeader] = token;
                }
                return config;
            };
        }
        Interceptor.Factory = function (SatellizerConfig, SatellizerShared, SatellizerStorage) {
            return new Interceptor(SatellizerConfig, SatellizerShared, SatellizerStorage);
        };
        Interceptor.$inject = ["SatellizerConfig", "SatellizerShared", "SatellizerStorage"];
        return Interceptor;
    })();
    Interceptor.Factory.$inject = ["SatellizerConfig", "SatellizerShared", "SatellizerStorage"];

    var HttpProviderConfig = (function () {
        function HttpProviderConfig($httpProvider) {
            this.$httpProvider = $httpProvider;
            $httpProvider.interceptors.push(Interceptor.Factory);
        }
        HttpProviderConfig.$inject = ["$httpProvider"];
        return HttpProviderConfig;
    })();

    angular
        .module("satellizer", [])
        .provider("$auth", [
            "SatellizerConfig",
            function (SatellizerConfig) {
                return new AuthProvider(SatellizerConfig);
            },
        ])
        .constant("SatellizerConfig", Config.getConstant)
        .service("SatellizerShared", Shared)
        .service("SatellizerLocal", Local)
        .service("SatellizerPopup", Popup)
        .service("SatellizerOAuth", OAuth)
        .service("SatellizerOAuth2", OAuth2)
        .service("SatellizerOAuth1", OAuth1)
        .service("SatellizerStorage", Storage)
        .service("SatellizerInterceptor", Interceptor)
        .config([
            "$httpProvider",
            function ($httpProvider) {
                return new HttpProviderConfig($httpProvider);
            },
        ]);
    var ng1 = "satellizer";

    return ng1;
});
//# sourceMappingURL=satellizer.js.map
