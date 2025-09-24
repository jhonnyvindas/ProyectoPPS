window.tilopayInterop = (function () {
    let _inited = false;
    let _busy = false;
    let _dotnetRef = null;
    let _lastInitResult = null;

    function require(cond, msg) { if (!cond) throw new Error(msg); }

    function ensureSdk() {
        require(!!window.Tilopay, "Tilopay SDK no cargó (window.Tilopay undefined)");
    }

    function ensureDom() {
        const root = document.querySelector(".payFormTilopay");
        require(root, "No existe .payFormTilopay en el DOM");
    }

    function ensureResponseContainer() {
        let c = document.getElementById("responseTilopay");
        if (!c) {
            c = document.createElement("div");
            c.id = "responseTilopay";
            c.style.display = "none";
            document.body.appendChild(c);
            console.log("[tilopayInterop] #responseTilopay creado");
        }
        return c;
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

    function pickPayfac(methods) {
        if (!Array.isArray(methods) || methods.length === 0) return null;

        const s = methods.find(m => typeof m === "string" && m.includes(":payfac:"));
        if (s) return s;

        const o = methods.find(m =>
            m && typeof m === "object" &&
            (
                (typeof m.id === "string" && m.id.includes(":payfac:")) ||
                (typeof m.type === "string" && m.type.toLowerCase() === "card")
            )
        );
        if (o) return o.id || null;

        return null;
    }

    async function ensureInit(token, options, dotnetRef) {
        ensureSdk();
        require(!!token, "Token vacío para Init");
        ensureDom();
        ensureResponseContainer();

        if (dotnetRef) _dotnetRef = dotnetRef;

        if (_inited) {
            if (options && typeof window.Tilopay?.updateOptions === "function") {
                await window.Tilopay.updateOptions(options);
            }

            try {
                const methods =
                    (await window.Tilopay?.getMethods?.()) ??
                    window.Tilopay?.methods ??
                    _lastInitResult?.methods ??
                    [];
                const payfac = pickPayfac(methods);
                if (_dotnetRef && payfac) {
                    await _dotnetRef.invokeMethodAsync('OnDefaultMethod', payfac);
                } else {
                    console.warn("[tilopayInterop] No hay método :payfac: disponible para esta moneda (update).");
                }
            } catch { }

            return true;
        }

        const cfg = Object.assign({}, options || {}, { token });

        console.log("[tilopayInterop] Init cfg:", cfg);
        require(typeof window.Tilopay.Init === "function", "Tilopay.Init no existe en el SDK");

        const initResult = await window.Tilopay.Init(cfg);
        _lastInitResult = initResult;              
        _inited = true;
        console.log("[tilopayInterop] SDK inicializado ✔", initResult);


        try {
            const methods =
                initResult?.methods ??
                (await window.Tilopay?.getMethods?.()) ??
                window.Tilopay?.methods ??
                [];
            const payfac = pickPayfac(methods);
            if (_dotnetRef && payfac) {
                await _dotnetRef.invokeMethodAsync('OnDefaultMethod', payfac);
            } else {
                console.warn("[tilopayInterop] No hay método :payfac: disponible para esta moneda (init).");
            }
        } catch { }

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

    async function getCardType() {
        try {
            ensureSdk();
            if (typeof window.Tilopay.getCardType !== "function")
                return "";
            const r = await window.Tilopay.getCardType();
            return (r?.message || r || "").toString().toLowerCase();
        } catch {
            return ""; 
        }
    }

    function watchCardBrand(dotnetRef) {
        const ref = dotnetRef || _dotnetRef;

        let currentInput = null;
        let bound = false;

        const bind = () => {
            const el = document.getElementById("tlpy_cc_number")
                || document.querySelector('input[name="cc_number"]')
                || document.querySelector('input[name="tlpy_cc_number"]');

            if (!el) return;

            if (currentInput === el && bound) return; 

            if (currentInput && currentInput._tlpyBrandHandler) {
                currentInput.removeEventListener("input", currentInput._tlpyBrandHandler);
                currentInput.removeEventListener("change", currentInput._tlpyBrandHandler);
                currentInput.removeEventListener("blur", currentInput._tlpyBrandHandler);
            }

            const handler = async () => {
                try {
                    ensureSdk();
                    if (typeof window.Tilopay.getCardType !== "function") return;
                    const r = await window.Tilopay.getCardType();
                    const brand = (r && (r.message || r.cardType || r.type || r.result || r) || "")
                        .toString().toLowerCase();

                    if (ref) await ref.invokeMethodAsync("OnCardBrandChanged", brand || "");
                } catch {  }
            };

            el._tlpyBrandHandler = handler;
            el.addEventListener("input", handler);
            el.addEventListener("change", handler);
            el.addEventListener("blur", handler);

            currentInput = el;
            bound = true;

            handler();
        };

        bind();
        if (!watchCardBrand._interval) {
            watchCardBrand._interval = setInterval(bind, 500);
        }
    }

    // ------------------ PAY ------------------
    async function prepareAndPay() {
        ensureSdk();
        require(_inited, "SDK no inicializado; llama a ensureInit primero.");
        ensureDom();
        ensureResponseContainer();

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

    async function getPayfacMethod() {
        try {
            const methods =
                (await window.Tilopay?.getMethods?.()) ??
                window.Tilopay?.methods ??
                _lastInitResult?.methods ??
                [];
            return pickPayfac(methods);
        } catch {
            return _lastInitResult?.methods ? pickPayfac(_lastInitResult.methods) : null;
        }
    }

    return {
        ensureInit,
        prepareAndPay,
        hideMethodAndCards,
        setDefaultMethod,
        getCardType,
        watchCardBrand,
        getPayfacMethod
    };
})();
