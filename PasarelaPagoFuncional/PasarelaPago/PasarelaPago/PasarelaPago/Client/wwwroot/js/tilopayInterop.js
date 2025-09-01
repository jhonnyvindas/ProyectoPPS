// wwwroot/js/tilopayInterop.js
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

    // --- NUEVO: el SDK inyecta HTML en #responseTilopay; si no existe lo creamos ---
    function ensureResponseContainer() {
        let c = document.getElementById("responseTilopay");
        if (!c) {
            c = document.createElement("div");
            c.id = "responseTilopay";
            c.style.display = "none";
            // lo ponemos al final del body; sirve igual dentro del wrapper
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

    // ------------------ INIT UNA SOLA VEZ ------------------
    async function ensureInit(token, options, dotnetRef) {
        ensureSdk();
        require(!!token, "Token vacío para Init");
        ensureDom();
        ensureResponseContainer();

        // **IMPORTANTE**: conserva o actualiza la referencia .NET aunque ya esté inicializado
        if (dotnetRef) _dotnetRef = dotnetRef;

        if (_inited) {
            if (options && typeof window.Tilopay?.updateOptions === "function") {
                await window.Tilopay.updateOptions(options);
            }
            return true;
        }

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

    // ------------------ Marca de tarjeta con el SDK ------------------
    async function getCardType() {
        try {
            ensureSdk();
            if (typeof window.Tilopay.getCardType !== "function")
                return "";
            const r = await window.Tilopay.getCardType();
            return (r?.message || r || "").toString().toLowerCase();
        } catch {
            return ""; // sin console.error
        }
    }


    function watchCardBrand(dotnetRef) {
        const ref = dotnetRef || _dotnetRef;

        const attach = () => {
            const input = document.getElementById("tlpy_cc_number");
            if (!input) { setTimeout(attach, 150); return; }

            // evita duplicados
            if (input._tlpyBrandHandler) {
                input.removeEventListener("input", input._tlpyBrandHandler);
                input.removeEventListener("change", input._tlpyBrandHandler);
                input.removeEventListener("blur", input._tlpyBrandHandler);
            }

            const handler = async () => {
                try {
                    const brand = await getCardType();
                    if (ref) await ref.invokeMethodAsync("OnCardBrandChanged", brand || "");
                } catch { /* no-op */ }
            };

            input._tlpyBrandHandler = handler;
            input.addEventListener("input", handler);
            input.addEventListener("change", handler);
            input.addEventListener("blur", handler);

            // primera detección inmediata
            handler();
        };

        attach();
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
            //await maybeCancel();

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

    return {
        ensureInit,
        prepareAndPay,
        hideMethodAndCards,
        setDefaultMethod,
        getCardType,
        watchCardBrand
    };
})();
