(() => {
    const FP_COOKIE = "sc_fp";
    const SID_COOKIE = "sc_sid";
    const FP_MAX_DAYS = 30;
    const SID_MAX_DAYS = 7;

    if (typeof document === "undefined") {
        return;
    }

    function getCookie(name) {
        return document.cookie
            .split(";")
            .map(c => c.trim())
            .find(c => c.startsWith(`${name}=`))
            ?.split("=")[1];
    }

    function setCookie(name, value, days) {
        const expires = new Date(Date.now() + days * 24 * 60 * 60 * 1000).toUTCString();
        const secure = window.location.protocol === "https:" ? "; Secure" : "";
        document.cookie = `${name}=${value}; Path=/; SameSite=Lax; Expires=${expires}${secure}`;
    }

    async function sha256Hex(input) {
        const encoder = new TextEncoder();
        const data = encoder.encode(input);
        const hash = await crypto.subtle.digest("SHA-256", data);
        const bytes = Array.from(new Uint8Array(hash));
        return bytes.map(b => b.toString(16).padStart(2, "0")).join("");
    }

    function randomId() {
        return (crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(16).slice(2)}`).replace(/-/g, "");
    }

    async function ensureSessionId() {
        if (getCookie(SID_COOKIE)) {
            return;
        }
        setCookie(SID_COOKIE, `sc-${randomId()}`, SID_MAX_DAYS);
    }

    async function ensureFingerprint() {
        if (getCookie(FP_COOKIE)) {
            return;
        }

        const fpParts = [
            navigator.userAgent || "",
            navigator.language || "",
            navigator.platform || "",
            screen.width + "x" + screen.height,
            screen.colorDepth || "",
            Intl.DateTimeFormat().resolvedOptions().timeZone || "",
            new Date().getTimezoneOffset()
        ];

        let hash;
        try {
            hash = await sha256Hex(fpParts.join("|"));
        } catch (err) {
            console.warn("Fingerprint hash failed; using random id", err);
            hash = randomId();
        }

        setCookie(FP_COOKIE, hash, FP_MAX_DAYS);
    }

    ensureSessionId();
    ensureFingerprint();
})();
