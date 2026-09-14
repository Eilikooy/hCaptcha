// ES module loaded by the HCaptcha component via IJSRuntime "import".
// Requires hCaptcha's api.js (https://js.hcaptcha.com/1/api.js?render=explicit) to be referenced by the host page.

const pollIntervalMs = 50;

function hcaptchaLoaded() {
    return typeof hcaptcha !== 'undefined' && typeof hcaptcha.render === 'function';
}

function waitForHCaptcha(timeoutMs) {
    return new Promise(resolve => {
        if (hcaptchaLoaded()) {
            resolve(true);
            return;
        }

        const deadline = Date.now() + timeoutMs;
        const timer = setInterval(() => {
            if (hcaptchaLoaded()) {
                clearInterval(timer);
                resolve(true);
            } else if (Date.now() >= deadline) {
                clearInterval(timer);
                resolve(false);
            }
        }, pollIntervalMs);
    });
}

// Renders the widget into the element with the given id.
// Returns the hCaptcha widget id, or null if api.js did not become available within timeoutMs.
export async function render(dotNetInstance, elementId, siteKey, theme, size, timeoutMs) {
    if (!await waitForHCaptcha(timeoutMs)) {
        return null;
    }

    return hcaptcha.render(elementId, {
        sitekey: siteKey,
        theme: theme,
        size: size,
        callback: token => dotNetInstance.invokeMethodAsync('HCaptchaOnSuccess', token),
        'error-callback': errorCode => dotNetInstance.invokeMethodAsync('HCaptchaOnError', errorCode == null ? null : String(errorCode)),
        'expired-callback': () => dotNetInstance.invokeMethodAsync('HCaptchaOnExpired')
    });
}

export function reset(widgetId) {
    if (hcaptchaLoaded()) {
        hcaptcha.reset(widgetId);
    }
}

export function remove(widgetId) {
    if (hcaptchaLoaded()) {
        hcaptcha.remove(widgetId);
    }
}
