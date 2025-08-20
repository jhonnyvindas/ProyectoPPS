
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

    // ------------------ INIT UNA SOLA VEZ ------------------
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

        // Enlaza callbacks .NET
        //try {
        //    window.Tilopay.response200() = function (payload) {
        //        console.log("[Tilopay][200]", payload);
        //        if (_dotnetRef) _dotnetRef.invokeMethodAsync("OnPaymentEvent", { status: "success", payload });
        //    };
        //    window.Tilopay.response400() = function (payload) {
        //        console.warn("[Tilopay][400]", payload);
        //        if (_dotnetRef) _dotnetRef.invokeMethodAsync("OnPaymentEvent", { status: "error", payload });
        //    };
        //} catch (e) {
        //    console.warn("[tilopayInterop] no fue posible envolver response200/400:", e);
        //}

        //setDefaultMethod("card");
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

    // ------------------ PAY ------------------
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
            //await maybeCancel();

            //// order único
            //if (!updateOptionsObj || !updateOptionsObj.orderNumber) {
            //    updateOptionsObj = Object.assign({}, updateOptionsObj || {}, {
            //        orderNumber: (crypto?.randomUUID?.() || Date.now().toString())
            //    });
            //}

            //if (typeof window.Tilopay.updateOptions === "function") {
            //    const result_update = await window.Tilopay.updateOptions(updateOptionsObj);
            //    console.log("[updateOptions] resultado:", result_update);
            //}

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
        setDefaultMethod
    };
})();
