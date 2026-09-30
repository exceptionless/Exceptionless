const assert = require("node:assert/strict");
const fs = require("node:fs");
const test = require("node:test");
const vm = require("node:vm");

const source = fs.readFileSync(require.resolve("../app/auth/auth.js"), "utf8");

function createRequestInterceptor(initialState) {
    let configure;
    const angular = {
        module: () => ({
            config(callback) {
                configure = callback;
            },
        }),
    };
    vm.runInNewContext(source, { angular, window: { location: { origin: "http://localhost" } } });

    const interceptors = [];
    const authProvider = {
        facebook() {},
        github() {},
        google() {},
        oauth2() {},
    };
    const stateProvider = {
        state() {
            return this;
        },
    };
    configure(
        authProvider,
        { interceptors },
        stateProvider,
        "http://localhost",
        "facebook",
        "google",
        "github",
        "microsoft"
    );

    let storedState = initialState;
    const storage = {
        get: () => storedState,
        remove: () => {
            storedState = null;
        },
    };
    const interceptor = interceptors[0]({ reject: Promise.reject.bind(Promise) }, storage);
    return { request: interceptor.request, state: () => storedState };
}

const microsoftRequest = (state) => ({
    method: "POST",
    url: "http://localhost/api/v2/auth/microsoft",
    data: { code: "code", state },
});

test("Microsoft token exchange rejects missing or mismatched callback state", async () => {
    for (const state of [undefined, "wrong-state"]) {
        const interceptor = createRequestInterceptor("expected-state");
        await assert.rejects(interceptor.request(microsoftRequest(state)), (error) => error.status === 400);
        assert.equal(interceptor.state(), "expected-state");
    }
});

test("Microsoft token exchange rejects a callback without a stored nonce", async () => {
    const interceptor = createRequestInterceptor(null);
    await assert.rejects(interceptor.request(microsoftRequest("state")), (error) => error.status === 400);
});

test("Microsoft token exchange accepts matching state once", async () => {
    const interceptor = createRequestInterceptor("expected-state");
    const request = microsoftRequest("expected-state");
    assert.equal(interceptor.request(request), request);
    assert.equal(interceptor.state(), null);
    await assert.rejects(interceptor.request(request), (error) => error.status === 400);
});

test("Other OAuth requests keep their existing behavior", () => {
    const interceptor = createRequestInterceptor("expected-state");
    const request = { method: "POST", url: "http://localhost/api/v2/auth/google", data: {} };
    assert.equal(interceptor.request(request), request);
    assert.equal(interceptor.state(), "expected-state");
});
