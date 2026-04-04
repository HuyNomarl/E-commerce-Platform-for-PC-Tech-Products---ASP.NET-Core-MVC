$(function () {
    if ($("div.alert.notification").length) {
        setTimeout(() => {
            $("div.alert.notification").fadeOut();
        }, 2000);
    }
});

function readURL(input) {
    if (input.files && input.files[0]) {
        let reader = new FileReader();

        reader.onload = function (e) {
            $("img#imgpreview").attr("src", e.target.result).width(200).height(200);
        };

        reader.readAsDataURL(input.files[0]);
    }
}

(function (window, document) {
    if (window.EshopAdminSelectFilter) {
        return;
    }

    const instances = new WeakMap();
    const STYLE_ELEMENT_ID = "eshop-admin-select-filter-style";
    let pageObserver = null;

    function ensureStyles() {
        if (document.getElementById(STYLE_ELEMENT_ID)) {
            return;
        }

        const style = document.createElement("style");
        style.id = STYLE_ELEMENT_ID;
        style.textContent = `
            .admin-select-filter {
                display: flex;
                flex-direction: column;
                gap: 8px;
                width: 100%;
            }

            .admin-select-filter-input {
                height: 38px;
                min-height: 38px;
                padding: 8px 12px;
                border-radius: 10px;
                font-size: 13px;
            }

            .admin-select-filter.is-disabled {
                opacity: 0.75;
            }

            .admin-select-filter-empty {
                display: none;
                padding: 0 2px;
                font-size: 12px;
                color: #b45309;
            }

            .admin-select-filter.has-no-results .admin-select-filter-empty {
                display: block;
            }
        `;

        document.head.appendChild(style);
    }

    function matchesProductItemName(select) {
        const name = select.getAttribute("name") || "";
        return /^Items\[\d+\]\.ProductId$/.test(name);
    }

    function isInventoryTarget(select) {
        if (!(select instanceof HTMLSelectElement)) {
            return false;
        }

        if (select.matches(".js-filterable-select")) {
            return true;
        }

        if (select.closest(".inventory-receive-page")) {
            return select.id === "PublisherId" || select.classList.contains("js-product-select");
        }

        if (select.closest(".inventory-transfer-page")) {
            return matchesProductItemName(select) && select.closest("#transfer-items") !== null;
        }

        if (select.closest(".inventory-adjust-page")) {
            return (select.name || "") === "ProductId";
        }

        return false;
    }

    function getPlaceholder(select) {
        if (select.dataset.searchPlaceholder) {
            return select.dataset.searchPlaceholder;
        }

        if (select.closest(".inventory-receive-page")) {
            if (select.id === "PublisherId") {
                return "Tìm brand theo tên...";
            }

            if (select.classList.contains("js-product-select")) {
                return "Tìm sản phẩm trong brand đã chọn...";
            }
        }

        if (select.closest(".inventory-transfer-page") && matchesProductItemName(select)) {
            return "Tìm sản phẩm cần chuyển...";
        }

        if (select.closest(".inventory-adjust-page") && (select.name || "") === "ProductId") {
            return "Tìm sản phẩm cần điều chỉnh...";
        }

        return "Tìm nhanh...";
    }

    function normalizeValue(value) {
        const text = (value ?? "").toString();

        if (typeof text.normalize !== "function") {
            return text.toLowerCase();
        }

        return text
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .toLowerCase();
    }

    function serializeOption(option, index) {
        return {
            value: option.value,
            text: option.text,
            disabled: option.disabled,
            hidden: option.hidden,
            isPlaceholder: index === 0 && option.value === "",
            attributes: Array.from(option.attributes).map(attribute => ({
                name: attribute.name,
                value: attribute.value
            }))
        };
    }

    function createOptionElement(definition) {
        const option = document.createElement("option");

        definition.attributes.forEach(attribute => {
            if (attribute.name === "selected") {
                return;
            }

            option.setAttribute(attribute.name, attribute.value);
        });

        option.textContent = definition.text;
        option.disabled = definition.disabled;
        option.hidden = definition.hidden;

        return option;
    }

    class SelectFilter {
        constructor(select) {
            this.select = select;
            this.wrapper = null;
            this.searchInput = null;
            this.emptyState = null;
            this.optionCache = [];
            this.attributeObserver = null;
            this.optionsObserver = null;

            this.handleSearchInput = this.handleSearchInput.bind(this);
            this.handleSelectChange = this.handleSelectChange.bind(this);

            this.init();
        }

        init() {
            if (this.select.dataset.selectFilterEnhanced === "true") {
                return;
            }

            ensureStyles();

            const wrapper = document.createElement("div");
            wrapper.className = "admin-select-filter";

            const searchInput = document.createElement("input");
            searchInput.type = "search";
            searchInput.className = "form-control field-control admin-select-filter-input";
            searchInput.placeholder = getPlaceholder(this.select);
            searchInput.autocomplete = "off";
            searchInput.spellcheck = false;
            searchInput.setAttribute("aria-label", getPlaceholder(this.select));

            const emptyState = document.createElement("div");
            emptyState.className = "admin-select-filter-empty";
            emptyState.textContent = this.select.dataset.searchEmptyText || "Không tìm thấy lựa chọn phù hợp.";

            this.select.parentNode.insertBefore(wrapper, this.select);
            wrapper.appendChild(searchInput);
            wrapper.appendChild(this.select);
            wrapper.appendChild(emptyState);

            this.wrapper = wrapper;
            this.searchInput = searchInput;
            this.emptyState = emptyState;

            this.select.dataset.selectFilterEnhanced = "true";

            this.captureOptions();
            this.render("");
            this.syncState();

            this.searchInput.addEventListener("input", this.handleSearchInput);
            this.select.addEventListener("change", this.handleSelectChange);

            this.attributeObserver = new MutationObserver(() => this.syncState());
            this.attributeObserver.observe(this.select, {
                attributes: true,
                attributeFilter: ["class", "disabled"]
            });

            this.optionsObserver = new MutationObserver(() => {
                this.captureOptions();
                this.render(this.searchInput.value);
                this.syncState();
            });

            this.observeOptionChanges();
        }

        observeOptionChanges() {
            if (!this.optionsObserver) {
                return;
            }

            this.optionsObserver.observe(this.select, {
                childList: true,
                subtree: true
            });
        }

        captureOptions() {
            this.optionCache = Array.from(this.select.options).map((option, index) => serializeOption(option, index));
        }

        render(query) {
            const normalizedQuery = normalizeValue(query);
            const currentValue = (this.select.value ?? "").toString();
            let hasSearchMatch = normalizedQuery === "";

            const filteredOptions = this.optionCache.filter(option => {
                const isCurrent = option.value.toString() === currentValue;
                const matches = normalizedQuery === "" || normalizeValue(option.text).includes(normalizedQuery);

                if (!option.isPlaceholder && matches) {
                    hasSearchMatch = true;
                }

                return option.isPlaceholder || matches || isCurrent;
            });

            if (this.optionsObserver) {
                this.optionsObserver.disconnect();
            }

            this.select.innerHTML = "";

            filteredOptions.forEach(option => {
                this.select.appendChild(createOptionElement(option));
            });

            if (currentValue && filteredOptions.some(option => option.value.toString() === currentValue)) {
                this.select.value = currentValue;
            } else if (filteredOptions.some(option => option.isPlaceholder)) {
                this.select.value = "";
            }

            this.observeOptionChanges();

            this.wrapper.classList.toggle("has-no-results", normalizedQuery !== "" && !hasSearchMatch);
        }

        syncState() {
            const hasError = this.select.classList.contains("input-validation-error");
            this.searchInput.classList.toggle("input-validation-error", hasError);
            this.searchInput.disabled = this.select.disabled;
            this.wrapper.classList.toggle("is-disabled", this.select.disabled);
        }

        handleSearchInput() {
            this.render(this.searchInput.value);
        }

        handleSelectChange() {
            if (this.searchInput.value) {
                this.searchInput.value = "";
                this.render("");
            }

            this.syncState();
        }

        refresh(options) {
            const resetSearch = !options || options.resetSearch !== false;

            if (resetSearch) {
                this.searchInput.value = "";
            }

            this.captureOptions();
            this.render(this.searchInput.value);
            this.syncState();
        }

        reset() {
            this.searchInput.value = "";
            this.render("");
            this.syncState();
        }
    }

    function enhance(root) {
        const scope = root && typeof root.querySelectorAll === "function" ? root : document;

        scope.querySelectorAll("select").forEach(select => {
            if (!isInventoryTarget(select)) {
                return;
            }

            if (!instances.has(select)) {
                instances.set(select, new SelectFilter(select));
                return;
            }

            instances.get(select).refresh({ resetSearch: false });
        });
    }

    function refresh(select, options) {
        if (!select || !isInventoryTarget(select)) {
            return;
        }

        if (!instances.has(select)) {
            instances.set(select, new SelectFilter(select));
            return;
        }

        instances.get(select).refresh(options);
    }

    function reset(select) {
        if (!select || !instances.has(select)) {
            return;
        }

        instances.get(select).reset();
    }

    window.EshopAdminSelectFilter = {
        enhance,
        refresh,
        reset
    };

    function boot() {
        ensureStyles();
        enhance(document);

        if (pageObserver || !document.body) {
            return;
        }

        pageObserver = new MutationObserver(mutations => {
            const shouldRefresh = mutations.some(mutation =>
                Array.from(mutation.addedNodes).some(node =>
                    node instanceof Element &&
                    (node.matches("select") || node.querySelector("select"))
                ));

            if (shouldRefresh) {
                enhance(document);
            }
        });

        pageObserver.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }
})(window, document);
