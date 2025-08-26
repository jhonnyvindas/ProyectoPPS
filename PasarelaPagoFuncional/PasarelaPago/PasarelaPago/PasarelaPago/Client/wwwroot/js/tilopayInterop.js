
window.tilopayInterop = (function () {
    let _inited = false;
    let _busy = false;
    let _dotnetRef = null;

    function require(cond, msg) { if (!cond) throw new Error(msg); }

    function ensureSdk() {
        require(!!window.Tilopay, "Tilopay SDK no cargó (window.Tilopay undefined)");
    }

    function ensureDom() {
        const root = document.querySelector(".payFormTilopay");
        require(root, "No existe .payFormTilopay en el DOM");
    }

    async function maybeCancel() {
        try {
            if (typeof window.Tilopay?.cancel === "function") {
                await window.Tilopay.cancel();
                console.log("[tilopayInterop] cancel() OK");
            }
        } catch (e) {
            console.warn("[tilopayInterop] cancel() falló/ausente:", e);
        }
        try {
            document.querySelectorAll(".tilopay-modal,.tlpy-modal,.tilopay-overlay,iframe[src*='tilopay']")
                .forEach(n => n.remove());
        } catch { }
    }


    async function ensureInit(token, options, dotnetRef) {
        ensureSdk();
        require(!!token, "Token vacío para Init");
        ensureDom();

        if (_inited) {
            if (options && typeof window.Tilopay?.updateOptions === "function") {
                await window.Tilopay.updateOptions(options);
            }
            return true;
        }

        _dotnetRef = dotnetRef || null;
        const cfg = Object.assign({}, options || {}, { token });

        console.log("[tilopayInterop] Init cfg:", cfg);
        require(typeof window.Tilopay.Init === "function", "Tilopay.Init no existe en el SDK");

        const initResult = await window.Tilopay.Init(cfg);
        _inited = true;
        console.log("[tilopayInterop] SDK inicializado ✔", initResult);

        return true;
    }

    function setDefaultMethod(value) {
        const el = document.getElementById("tlpy_payment_method");
        if (!el) return;

        const methods = (window.Tilopay && (window.Tilopay.methods || window.Tilopay.getMethods?.())) || [];
        let targetId = value;
        if (!targetId || targetId === "card") {
            const m = methods.find(x => x.type === "card");
            if (m) targetId = m.id;
        }
        if (targetId) el.value = targetId;
    }

    function hideMethodAndCards() {
        const method = document.getElementById("tlpy_payment_method");
        const cards = document.getElementById("cards");
        if (method && method.closest(".row")) method.closest(".row").style.display = "none";
        if (cards && cards.closest(".row")) cards.closest(".row").style.display = "none";
    }

    async function prepareAndPay() {
        ensureSdk();
        require(_inited, "SDK no inicializado; llama a ensureInit primero.");
        ensureDom();

        if (_busy) {
            console.warn("[tilopayInterop] ya hay un pago en proceso; ignorando.");
            return { message: "Pago ya en proceso" };
        }
        _busy = true;


        try {
            console.log("[tilopayInterop] startPayment()");
            const result = await window.Tilopay.startPayment();

            console.log("[tilopayInterop] resultado:", result);

            if (_dotnetRef) {
                const status = (result && (result.status || result.result)) || "success";
                await _dotnetRef.invokeMethodAsync("OnPaymentEvent", { status, payload: result });
            }
            return result;
        } finally {
            _busy = false;
        }
    }

    async function getCardTypeOnce() {

        try {
            if (window.Tilopay && typeof window.Tilopay.getCardType === "function") {
                const res = await window.Tilopay.getCardType();
                const val = (typeof res === "string") ? res : (res?.message || res?.type || "");
                if (val) return String(val).toLowerCase();
            }
        } catch (e) {
            console.warn("[tilopayInterop] getCardTypeOnce SDK error:", e);
        }


        const el = document.querySelector("#tlpy_cc_number");
        const raw = (el?.value || "").replace(/\s+/g, "");
        if (!raw) return "";


        if (/^4\d{6,}$/.test(raw)) return "visa";
        if (/^(5[1-5]\d{4,}|2(2[2-9]\d{3}|2[3-9]\d{4}|[3-6]\d{5}|7[01]\d{4}|720\d{3}))\d*$/.test(raw)) return "mastercard";
        if (/^3[47]\d{5,}$/.test(raw)) return "amex";

        return "";
    }

    function watchCardType(selector, dotnetRef, methodName) {
        require(!!selector, "Selector vacío");
        const el = document.querySelector(selector);
        require(!!el, `No se encontró ${selector}`);

        let t = null;
        const trigger = () => {
            clearTimeout(t);
            t = setTimeout(async () => {
                const brand = await getCardTypeOnce();
                if (dotnetRef && methodName) {
                    try { await dotnetRef.invokeMethodAsync(methodName, brand); } catch { }
                }
            }, 100);
        };

        el.addEventListener("input", trigger);
        el.addEventListener("change", trigger);
        el.addEventListener("paste", trigger);
        el.addEventListener("keyup", trigger);


        trigger();

    }
    return {
        ensureInit,
        prepareAndPay,
        hideMethodAndCards,
        setDefaultMethod,
        getCardTypeOnce,
        watchCardType

    };
})();
