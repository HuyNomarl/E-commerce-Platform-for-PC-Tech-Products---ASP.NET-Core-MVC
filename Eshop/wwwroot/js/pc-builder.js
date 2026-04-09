
const userIsAuthenticated = @(User.Identity?.IsAuthenticated == true ? "true" : "false");
const SLOT_CONFIGS = [
    { type: "CPU", label: "1. CPU", buttonText: "Chọn CPU", multi: false },
    { type: "Mainboard", label: "2. MAINBOARD", buttonText: "Chọn Mainboard", multi: false },
    { type: "RAM", label: "3. RAM", buttonText: "Chọn RAM", multi: true },
    { type: "GPU", label: "4. CARD ĐỒ HỌA", buttonText: "Chọn Card Đồ Họa", multi: false },
    { type: "SSD", label: "5. Ổ CỨNG", buttonText: "Chọn Ổ Cứng", multi: true },
    { type: "PSU", label: "6. NGUỒN (PSU)", buttonText: "Chọn Nguồn (PSU)", multi: false },
    { type: "Cooler", label: "7. TẢN NHIỆT", buttonText: "Chọn Tản Nhiệt", multi: false },
    { type: "Case", label: "8. VỎ CASE", buttonText: "Chọn Vỏ Case", multi: false },
    { type: "Monitor", label: "9. MÀN HÌNH", buttonText: "Chọn Màn Hình", multi: true }
];

const buildState = {
    CPU: null,
    Mainboard: null,
    RAM: [],
    GPU: null,
    SSD: [],
    PSU: null,
    Cooler: null,
    Case: null,
    Monitor: []
};

let pickerType = null;
let pickerAllItems = [];
let pickerFilteredItems = [];
let compatibilityState = { totalPrice: 0, estimatedPower: 0, messages: [] };
let shareSearchResults = [];
let selectedShareReceiverId = null;
let sharedBuildItems = [];
let chatResponseSeed = 0;

const chatConversation = [];
const chatSuggestionStore = new Map();

const pickerFilterState = {
    keyword: "",
    sort: "default",
    inStockOnly: false,
    compatibleOnly: true,
    publisherIds: [],
    specs: {}
};

const builderTableBody = document.getElementById("builderTableBody");
const pickerOverlay = document.getElementById("pickerOverlay");
const pickerTitle = document.getElementById("pickerTitle");
const pickerList = document.getElementById("pickerList");
const pickerKeyword = document.getElementById("pickerKeyword");
const topTotalPrice = document.getElementById("topTotalPrice");
const bottomTotalPrice = document.getElementById("bottomTotalPrice");
const builderMessages = document.getElementById("builderMessages");
const pickerFilters = document.getElementById("pickerFilters");
const pickerSort = document.getElementById("pickerSort");
const pickerInStockOnly = document.getElementById("pickerInStockOnly");
const pickerCompatibleOnly = document.getElementById("pickerCompatibleOnly");
const buildChatMessages = document.getElementById("buildChatMessages");
const buildChatInput = document.getElementById("buildChatInput");
const sendBuildChatBtn = document.getElementById("sendBuildChatBtn");
const toggleBuildChatbox = document.getElementById("toggleBuildChatbox");
const buildChatbox = document.getElementById("buildChatbox");
const saveDraftBtn = document.getElementById("saveDraftBtn");
const exportExcelBtn = document.getElementById("exportExcelBtn");
const importExcelBtn = document.getElementById("importExcelBtn");
const importExcelInput = document.getElementById("importExcelInput");
const printBuildBtn = document.getElementById("printBuildBtn");
const shareBuildBtn = document.getElementById("shareBuildBtn");
const shareReceiverKeyword = document.getElementById("shareReceiverKeyword");
const shareReceiverResults = document.getElementById("shareReceiverResults");
const shareNoteInput = document.getElementById("shareNoteInput");
const btnSearchShareReceiver = document.getElementById("btnSearchShareReceiver");
const btnConfirmShareBuild = document.getElementById("btnConfirmShareBuild");
const sharedBuildList = document.getElementById("sharedBuildList");

function initPcBuilderPage() {
    document.getElementById("btnClosePicker")?.addEventListener("click", closePicker);
    document.getElementById("btnCancelPicker")?.addEventListener("click", closePicker);
    document.getElementById("btnSearchPicker")?.addEventListener("click", applyPickerFilters);
    document.getElementById("btnConfirmPicker")?.addEventListener("click", confirmPickerSelection);
    document.getElementById("btnResetBuild")?.addEventListener("click", resetBuild);
    document.getElementById("saveBuildBtn")?.addEventListener("click", addBuildToCart);
    document.getElementById("btnClearPickerFilters")?.addEventListener("click", clearPickerFilters);
    saveDraftBtn?.addEventListener("click", saveBuild);
    exportExcelBtn?.addEventListener("click", exportBuildExcel);
    importExcelBtn?.addEventListener("click", () => importExcelInput?.click());
    importExcelInput?.addEventListener("change", onImportExcelFileSelected);
    printBuildBtn?.addEventListener("click", () => window.print());
    shareBuildBtn?.addEventListener("click", shareCurrentBuild);
    btnSearchShareReceiver?.addEventListener("click", searchShareReceivers);
    btnConfirmShareBuild?.addEventListener("click", shareCurrentBuild);

    pickerSort?.addEventListener("change", () => {
        pickerFilterState.sort = pickerSort.value;
        applyPickerFilters();
    });

    pickerInStockOnly?.addEventListener("change", () => {
        pickerFilterState.inStockOnly = pickerInStockOnly.checked;
        applyPickerFilters();
    });

    pickerCompatibleOnly?.addEventListener("change", () => {
        pickerFilterState.compatibleOnly = pickerCompatibleOnly.checked;
        applyPickerFilters();
    });

    pickerOverlay?.addEventListener("click", (e) => {
        if (e.target === pickerOverlay) closePicker();
    });

    sendBuildChatBtn?.addEventListener("click", () => {
        askBuildAssistant(buildChatInput.value);
        buildChatInput.value = "";
    });

    buildChatInput?.addEventListener("keydown", (e) => {
        if (e.key === "Enter") {
            e.preventDefault();
            askBuildAssistant(buildChatInput.value);
            buildChatInput.value = "";
        }
    });

    toggleBuildChatbox?.addEventListener("click", () => {
        buildChatbox.classList.toggle("collapsed");
        toggleBuildChatbox.textContent = buildChatbox.classList.contains("collapsed") ? "+" : "−";
    });

    shareReceiverKeyword?.addEventListener("keydown", (e) => {
        if (e.key === "Enter") {
            e.preventDefault();
            searchShareReceivers();
        }
    });

    renderBuildTable();
    renderMessages();
    renderShareReceiverResults();
    renderSharedBuildList();

    if (userIsAuthenticated) {
        loadSharedBuilds();
    }
}

function getFilterConfig(type) {
    switch (type) {
        case "CPU":
            return [{ code: "cpu_socket", title: "Socket CPU" }];
        case "Mainboard":
            return [
                { code: "mb_socket", title: "Socket Mainboard" },
                { code: "mb_chipset", title: "Chipset" },
                { code: "mb_form_factor", title: "Kích thước Mainboard" },
                { code: "mb_ram_type", title: "Loại RAM hỗ trợ" }
            ];
        case "RAM":
            return [
                { code: "ram_capacity_gb", title: "Dung lượng RAM" },
                { code: "ram_type", title: "Loại RAM" },
                { code: "ram_bus_mhz", title: "Bus RAM" }
            ];
        case "SSD":
            return [
                { code: "ssd_storage_type", title: "Loại ổ" },
                { code: "ssd_capacity_gb", title: "Dung lượng SSD" },
                { code: "ssd_interface", title: "Chuẩn giao tiếp" }
            ];
        case "GPU":
            return [
                { code: "gpu_chip", title: "GPU" },
                { code: "gpu_vram_gb", title: "VRAM" }
            ];
        case "PSU":
            return [
                { code: "psu_watt", title: "Công suất PSU" },
                { code: "psu_efficiency", title: "Chứng nhận" },
                { code: "psu_standard", title: "Chuẩn nguồn" }
            ];
        case "Case":
            return [{ code: "case_psu_standard", title: "Chuẩn PSU hỗ trợ" }];
        case "Cooler":
            return [{ code: "cooler_height_mm", title: "Chiều cao tản nhiệt" }];
        case "Monitor":
            return [
                { code: "monitor_size_inch", title: "Kích thước màn hình" },
                { code: "monitor_resolution", title: "Độ phân giải" },
                { code: "monitor_refresh_rate_hz", title: "Tần số quét" },
                { code: "monitor_panel_type", title: "Tấm nền" }
            ];
        default:
            return [];
    }
}

function renderBuildTable() {
    builderTableBody.innerHTML = SLOT_CONFIGS.map(slot => {
        const selected = buildState[slot.type];
        const hasItems = slot.multi ? selected.length > 0 : !!selected;

        return `
                    <tr>
                        <td class="slot-col">${slot.label}</td>
                        <td class="content-col">
                            ${hasItems
                ? renderSelectedContent(slot.type, slot.multi)
                : `
                                    <div class="empty-slot-wrap">
                                        <button type="button" class="btn-choose-component" onclick="openPicker('${slot.type}')">
                                            ＋ ${slot.buttonText}
                                        </button>
                                    </div>
                                  `
            }
                        </td>
                    </tr>
                `;
    }).join("");

    renderTotals();
}

function renderSelectedContent(type, multi) {
    const items = multi ? buildState[type] : [buildState[type]];

    return `
                <div class="selected-group">
                    ${items.map(item => renderSelectedItem(type, multi, item)).join("")}
                    <div>
                        <button type="button" class="btn-choose-component" onclick="openPicker('${type}')">
                            ＋ ${getButtonText(type)}
                        </button>
                    </div>
                </div>
            `;
}

function renderSelectedItem(type, multi, item) {
    const imageUrl = item.image || "/images/no-image.png";
    const stockClass = Number(item.stock || 0) > 0 ? "selected-stock" : "selected-out";
    const stockText = Number(item.stock || 0) > 0 ? "Còn hàng" : "Hết hàng";

    return `
                <div class="selected-item">
                    <img class="selected-thumb" src="${imageUrl}" alt="${escapeHtml(item.name)}" />
                    <div class="selected-info">
                        <div class="selected-name">${escapeHtml(item.name)}</div>
                        <div class="selected-sub">${renderSummaryLine(item.summarySpecs)}</div>
                        <div>- Kho hàng: <span class="${stockClass}">${stockText}</span></div>
                    </div>

                    <div class="selected-price-side">
                        <div class="unit-price">${formatPrice(item.price)}</div>

                        ${multi ? `
                            <div class="qty-box">
                                <button type="button" class="qty-btn" onclick="changeQty('${type}', ${item.id}, -1)">-</button>
                                <input class="qty-input" value="${item.quantity || 1}" readonly />
                                <button type="button" class="qty-btn" onclick="changeQty('${type}', ${item.id}, 1)">+</button>
                            </div>
                        ` : `
                            <div class="qty-box">
                                <input class="qty-input" value="1" readonly />
                            </div>
                        `}

                        <div class="line-total">${formatPrice((item.price || 0) * (item.quantity || 1))}</div>

                        <div class="item-actions">
                            <button type="button" class="mini-action-btn" onclick="openPicker('${type}')">Đổi</button>
                            <button type="button" class="mini-action-btn mini-remove-btn" onclick="removeSelectedItem('${type}', ${item.id})">Xóa</button>
                        </div>
                    </div>
                </div>
            `;
}

function renderSummaryLine(summarySpecs) {
    if (!summarySpecs || summarySpecs.length === 0) return "";
    return summarySpecs.map(x => `- ${escapeHtml(x)}`).join("<br />");
}

function getButtonText(type) {
    const slot = SLOT_CONFIGS.find(x => x.type === type);
    return slot ? slot.buttonText : "Chọn linh kiện";
}

async function openPicker(type) {
    pickerType = type;
    pickerTitle.textContent = `CHỌN LINH KIỆN - ${type.toUpperCase()}`;
    resetPickerFilterState();
    pickerOverlay.classList.add("show");
    await loadPickerItems();
}

function closePicker() {
    pickerOverlay.classList.remove("show");
    pickerType = null;
    pickerAllItems = [];
    pickerFilteredItems = [];
    pickerList.innerHTML = "";
    pickerFilters.innerHTML = "";
}

function resetPickerFilterState() {
    pickerFilterState.keyword = "";
    pickerFilterState.sort = "default";
    pickerFilterState.inStockOnly = false;
    pickerFilterState.compatibleOnly = true;
    pickerFilterState.publisherIds = [];
    pickerFilterState.specs = {};

    if (pickerKeyword) pickerKeyword.value = "";
    if (pickerSort) pickerSort.value = "default";
    if (pickerInStockOnly) pickerInStockOnly.checked = false;
    if (pickerCompatibleOnly) pickerCompatibleOnly.checked = true;
}

async function loadPickerItems() {
    if (!pickerType) return;

    pickerList.innerHTML = `<div class="empty-picker">Đang tải dữ liệu...</div>`;

    try {
        const url = `/api/pc-builder/products?componentType=${encodeURIComponent(pickerType)}&page=1&pageSize=1000`;
        const res = await fetch(url);

        const text = await res.text();
        if (!res.ok) {
            console.error("PRODUCTS ERROR:", res.status, text);
            throw new Error(`Không tải được danh sách linh kiện. HTTP ${res.status}`);
        }

        const data = text ? JSON.parse(text) : {};
        pickerAllItems = data.items || [];
        renderPickerFilters();
        applyPickerFilters();
    } catch (error) {
        pickerAllItems = [];
        pickerFilteredItems = [];
        pickerFilters.innerHTML = "";
        pickerList.innerHTML = `<div class="empty-picker">${escapeHtml(error.message || "Có lỗi khi tải linh kiện.")}</div>`;
        console.error(error);
    }
}

function renderPickerFilters() {
    if (!pickerType) {
        pickerFilters.innerHTML = "";
        return;
    }

    const filterConfig = getFilterConfig(pickerType);
    const publisherMap = new Map();

    pickerAllItems.forEach(item => {
        const key = item.publisherId;
        if (!key) return;

        if (!publisherMap.has(key)) {
            publisherMap.set(key, {
                id: item.publisherId,
                name: item.publisherName || "Khác",
                count: 0
            });
        }

        publisherMap.get(key).count++;
    });

    const publishers = [...publisherMap.values()].sort((a, b) => a.name.localeCompare(b.name));

    const specGroups = filterConfig.map(group => {
        const valueMap = new Map();

        pickerAllItems.forEach(item => {
            const specs = item.filterSpecs || [];
            specs.filter(s => s.code === group.code).forEach(spec => {
                if (!valueMap.has(spec.value)) {
                    valueMap.set(spec.value, { value: spec.value, count: 0 });
                }
                valueMap.get(spec.value).count++;
            });
        });

        return {
            code: group.code,
            title: group.title,
            options: [...valueMap.values()].sort((a, b) => naturalCompare(a.value, b.value))
        };
    }).filter(x => x.options.length > 0);

    let html = "";

    if (publishers.length > 0) {
        html += `
                    <div class="filter-group">
                        <div class="filter-group-title">Hãng sản xuất</div>
                        <div class="filter-options-grid">
                            ${publishers.map(p => `
                                <label class="filter-option-label">
                                    <input
                                        type="checkbox"
                                        class="publisher-filter-check"
                                        value="${p.id}"
                                        ${pickerFilterState.publisherIds.includes(String(p.id)) ? "checked" : ""}
                                    />
                                    <span>${escapeHtml(p.name)} <span class="filter-option-count">(${p.count})</span></span>
                                </label>
                            `).join("")}
                        </div>
                    </div>
                `;
    }

    html += specGroups.map(group => `
                <div class="filter-group">
                    <div class="filter-group-title">${escapeHtml(group.title)}</div>
                    <div class="filter-options-grid">
                        ${group.options.map(opt => `
                            <label class="filter-option-label">
                                <input
                                    type="checkbox"
                                    class="spec-filter-check"
                                    data-code="${group.code}"
                                    value="${escapeHtml(opt.value)}"
                                    ${((pickerFilterState.specs[group.code] || []).includes(opt.value)) ? "checked" : ""}
                                />
                                <span>${escapeHtml(opt.value)} <span class="filter-option-count">(${opt.count})</span></span>
                            </label>
                        `).join("")}
                    </div>
                </div>
            `).join("");

    pickerFilters.innerHTML = html;

    document.querySelectorAll(".publisher-filter-check").forEach(input => {
        input.addEventListener("change", () => {
            pickerFilterState.publisherIds = [...document.querySelectorAll(".publisher-filter-check:checked")].map(x => x.value);
            applyPickerFilters();
        });
    });

    document.querySelectorAll(".spec-filter-check").forEach(input => {
        input.addEventListener("change", () => {
            const all = [...document.querySelectorAll(".spec-filter-check")];
            const next = {};

            all.forEach(x => {
                if (!x.checked) return;
                const code = x.dataset.code;
                if (!next[code]) next[code] = [];
                next[code].push(x.value);
            });

            pickerFilterState.specs = next;
            applyPickerFilters();
        });
    });
}

function applyPickerFilters() {
    pickerFilterState.keyword = pickerKeyword.value.trim().toLowerCase();
    let items = [...pickerAllItems];

    if (pickerFilterState.keyword) {
        items = items.filter(item => {
            const name = (item.name || "").toLowerCase();
            const publisher = (item.publisherName || "").toLowerCase();
            const specs = (item.filterSpecs || []).map(x => (x.value || "").toLowerCase()).join(" ");
            return name.includes(pickerFilterState.keyword) ||
                publisher.includes(pickerFilterState.keyword) ||
                specs.includes(pickerFilterState.keyword);
        });
    }

    if (pickerFilterState.inStockOnly) {
        items = items.filter(item => Number(item.stock || 0) > 0);
    }

    if (pickerFilterState.compatibleOnly) {
        items = items.filter(item => isCompatibleWithCurrentBuild(item, pickerType));
    }

    if (pickerFilterState.publisherIds.length > 0) {
        items = items.filter(item => pickerFilterState.publisherIds.includes(String(item.publisherId)));
    }

    Object.keys(pickerFilterState.specs).forEach(code => {
        const selectedValues = pickerFilterState.specs[code] || [];
        if (selectedValues.length === 0) return;

        items = items.filter(item => {
            const specs = item.filterSpecs || [];
            return specs.some(s => s.code === code && selectedValues.includes(s.value));
        });
    });

    if (pickerFilterState.sort === "price_asc") {
        items.sort((a, b) => Number(a.price || 0) - Number(b.price || 0));
    } else if (pickerFilterState.sort === "price_desc") {
        items.sort((a, b) => Number(b.price || 0) - Number(a.price || 0));
    } else if (pickerFilterState.sort === "name_asc") {
        items.sort((a, b) => (a.name || "").localeCompare(b.name || ""));
    }

    pickerFilteredItems = items;
    renderPickerList();
}

function renderPickerList() {
    if (pickerFilteredItems.length === 0) {
        pickerList.innerHTML = `<div class="empty-picker">Không có linh kiện phù hợp với bộ lọc hiện tại.</div>`;
        return;
    }

    const multi = isMultiSlot(pickerType);

    pickerList.innerHTML = pickerFilteredItems.map(item => {
        const checked = isCheckedInPicker(item.id, multi, pickerType) ? "checked" : "";
        const inputType = multi ? "checkbox" : "radio";
        const imageUrl = item.image || "/images/no-image.png";
        const stockText = Number(item.stock || 0) > 0 ? "Còn hàng" : "Hết hàng";
        const stockClass = Number(item.stock || 0) > 0 ? "selected-stock" : "selected-out";

        return `
                    <label class="picker-item">
                        <div class="picker-check-wrap">
                            <input class="picker-check" type="${inputType}" name="pickerSelection" value="${item.id}" ${checked} />
                        </div>

                        <img class="picker-img" src="${imageUrl}" alt="${escapeHtml(item.name)}" />

                        <div>
                            <div class="picker-name">${escapeHtml(item.name)}</div>
                            <div class="picker-meta">
                                ${item.publisherName ? `Hãng: ${escapeHtml(item.publisherName)}<br />` : ""}
                                Kho hàng: <span class="${stockClass}">${stockText}</span><br />
                                ${renderSummaryLine(item.summarySpecs)}
                            </div>
                        </div>

                        <div class="picker-price">${formatPrice(item.price)}</div>
                    </label>
                `;
    }).join("");
}

function clearPickerFilters() {
    resetPickerFilterState();
    renderPickerFilters();
    applyPickerFilters();
}

function isCheckedInPicker(itemId, multi, type) {
    if (multi) return buildState[type].some(x => x.id === itemId);
    return buildState[type] && buildState[type].id === itemId;
}

function isMultiSlot(type) {
    const slot = SLOT_CONFIGS.find(x => x.type === type);
    return slot ? slot.multi : false;
}

function getSpecValues(item, code) {
    return (item.filterSpecs || [])
        .filter(x => x.code === code)
        .map(x => String(x.value || "").trim())
        .filter(Boolean);
}

function getFirstSpecValue(item, code) {
    const values = getSpecValues(item, code);
    return values.length > 0 ? values[0] : null;
}

function getFirstSpecNumber(item, code) {
    const raw = getFirstSpecValue(item, code);
    if (!raw) return null;
    const match = String(raw).replace(",", ".").match(/-?\d+(\.\d+)?/);
    return match ? Number(match[0]) : null;
}

function equalText(a, b) {
    return String(a || "").trim().toLowerCase() === String(b || "").trim().toLowerCase();
}

function includesText(list, value) {
    return list.some(x => equalText(x, value));
}

function isCompatibleWithCurrentBuild(item, targetType) {
    const cpu = buildState.CPU;
    const mainboard = buildState.Mainboard;
    const gpu = buildState.GPU;
    const psu = buildState.PSU;
    const pcCase = buildState.Case;
    const cooler = buildState.Cooler;

    if (targetType === "CPU" && mainboard) {
        const cpuSocket = getFirstSpecValue(item, "cpu_socket");
        const mbSocket = getFirstSpecValue(mainboard, "mb_socket");
        if (cpuSocket && mbSocket && !equalText(cpuSocket, mbSocket)) return false;
    }

    if (targetType === "Mainboard") {
        if (cpu) {
            const cpuSocket = getFirstSpecValue(cpu, "cpu_socket");
            const mbSocket = getFirstSpecValue(item, "mb_socket");
            if (cpuSocket && mbSocket && !equalText(cpuSocket, mbSocket)) return false;
        }

        if (pcCase) {
            const mbSize = getFirstSpecValue(item, "mb_form_factor");
            const supportedSizes = getSpecValues(pcCase, "case_supported_mb_sizes");
            if (mbSize && supportedSizes.length > 0 && !includesText(supportedSizes, mbSize)) return false;
        }
    }

    if (targetType === "RAM" && mainboard) {
        const ramType = getFirstSpecValue(item, "ram_type");
        const mbRamType = getFirstSpecValue(mainboard, "mb_ram_type");
        if (ramType && mbRamType && !equalText(ramType, mbRamType)) return false;
    }

    if (targetType === "GPU" && pcCase) {
        const gpuLength = getFirstSpecNumber(item, "gpu_length_mm");
        const caseMaxGpu = getFirstSpecNumber(pcCase, "case_max_gpu_length_mm");
        if (gpuLength != null && caseMaxGpu != null && gpuLength > caseMaxGpu) return false;
    }

    if (targetType === "PSU") {
        if (gpu) {
            const recommendPsu = getFirstSpecNumber(gpu, "gpu_recommended_psu_w");
            const psuWatt = getFirstSpecNumber(item, "psu_watt");
            if (recommendPsu != null && psuWatt != null && psuWatt < recommendPsu) return false;
        }

        if (pcCase) {
            const psuStandard = getFirstSpecValue(item, "psu_standard");
            const casePsuStandard = getFirstSpecValue(pcCase, "case_psu_standard");
            if (psuStandard && casePsuStandard && !equalText(psuStandard, casePsuStandard)) return false;
        }
    }

    if (targetType === "Case") {
        if (mainboard) {
            const mbSize = getFirstSpecValue(mainboard, "mb_form_factor");
            const supportedSizes = getSpecValues(item, "case_supported_mb_sizes");
            if (mbSize && supportedSizes.length > 0 && !includesText(supportedSizes, mbSize)) return false;
        }

        if (gpu) {
            const gpuLength = getFirstSpecNumber(gpu, "gpu_length_mm");
            const caseMaxGpu = getFirstSpecNumber(item, "case_max_gpu_length_mm");
            if (gpuLength != null && caseMaxGpu != null && gpuLength > caseMaxGpu) return false;
        }

        if (cooler) {
            const coolerHeight = getFirstSpecNumber(cooler, "cooler_height_mm");
            const caseMaxCooler = getFirstSpecNumber(item, "case_max_cooler_height_mm");
            if (coolerHeight != null && caseMaxCooler != null && coolerHeight > caseMaxCooler) return false;
        }

        if (psu) {
            const psuStandard = getFirstSpecValue(psu, "psu_standard");
            const casePsuStandard = getFirstSpecValue(item, "case_psu_standard");
            if (psuStandard && casePsuStandard && !equalText(psuStandard, casePsuStandard)) return false;
        }
    }

    if (targetType === "Cooler" && pcCase) {
        const coolerHeight = getFirstSpecNumber(item, "cooler_height_mm");
        const caseMaxCooler = getFirstSpecNumber(pcCase, "case_max_cooler_height_mm");
        if (coolerHeight != null && caseMaxCooler != null && coolerHeight > caseMaxCooler) return false;
    }

    return true;
}

async function confirmPickerSelection() {
    if (!pickerType) return;

    const multi = isMultiSlot(pickerType);
    const checkedInputs = [...document.querySelectorAll('input[name="pickerSelection"]:checked')];
    const checkedIds = checkedInputs.map(x => Number(x.value));
    const selectedProducts = pickerAllItems.filter(x => checkedIds.includes(x.id));

    if (multi) {
        buildState[pickerType] = selectedProducts.map(item => {
            const old = buildState[pickerType].find(x => x.id === item.id);
            return { ...item, quantity: old ? old.quantity : 1 };
        });
    } else {
        buildState[pickerType] = selectedProducts.length > 0 ? { ...selectedProducts[0], quantity: 1 } : null;
    }

    closePicker();
    renderBuildTable();
    await checkCompatibility();
}

function changeQty(type, productId, delta) {
    if (!isMultiSlot(type)) return;

    const target = buildState[type].find(x => x.id === productId);
    if (!target) return;

    target.quantity = (target.quantity || 1) + delta;

    if (target.quantity <= 0) {
        buildState[type] = buildState[type].filter(x => x.id !== productId);
    }

    renderBuildTable();
    checkCompatibility();
}

function removeSelectedItem(type, productId) {
    if (isMultiSlot(type)) {
        buildState[type] = buildState[type].filter(x => x.id !== productId);
    } else {
        buildState[type] = null;
    }

    renderBuildTable();
    checkCompatibility();
}

function collectPayloadItems() {
    const items = [];

    SLOT_CONFIGS.forEach(slot => {
        if (slot.multi) {
            buildState[slot.type].forEach(item => {
                items.push({
                    componentType: slot.type,
                    productId: item.id,
                    quantity: item.quantity || 1
                });
            });
        } else if (buildState[slot.type]) {
            items.push({
                componentType: slot.type,
                productId: buildState[slot.type].id,
                quantity: 1
            });
        }
    });

    return items;
}

async function checkCompatibility() {
    const items = collectPayloadItems();

    if (items.length === 0) {
        compatibilityState = {
            totalPrice: 0,
            estimatedPower: 0,
            messages: [{ level: "info", message: "Chưa có cảnh báo tương thích." }]
        };
        renderTotals();
        renderMessages();
        return;
    }

    try {
        const res = await fetch("/api/pc-builder/check", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ items })
        });

        const text = await res.text();
        console.log("CHECK RESPONSE:", res.status, text);

        let data = {};
        try {
            data = text ? JSON.parse(text) : {};
        } catch {
            throw new Error(`API check trả về không phải JSON. HTTP ${res.status}`);
        }

        if (!res.ok) {
            throw new Error(data.title || data.message || `HTTP ${res.status}`);
        }

        compatibilityState = {
            totalPrice: data.totalPrice || 0,
            estimatedPower: data.estimatedPower || 0,
            messages: (data.messages && data.messages.length > 0)
                ? data.messages
                : [{ level: "info", message: "Cấu hình hiện chưa có cảnh báo tương thích." }]
        };
    } catch (error) {
        console.error("CHECK ERROR:", error);
        compatibilityState = {
            totalPrice: 0,
            estimatedPower: 0,
            messages: [{ level: "warning", message: error.message || "Không kiểm tra được tương thích lúc này." }]
        };
    }

    renderTotals();
    renderMessages();
}

function renderTotals() {
    const priceText = formatPrice(compatibilityState.totalPrice || 0);
    topTotalPrice.textContent = priceText;
    bottomTotalPrice.textContent = priceText;
}

function renderMessages() {
    builderMessages.innerHTML = compatibilityState.messages.map(m => `
                <div class="builder-message ${m.level || 'info'}">• ${escapeHtml(m.message || "")}</div>
            `).join("");
}

function resetBuild() {
    SLOT_CONFIGS.forEach(slot => {
        buildState[slot.type] = slot.multi ? [] : null;
    });

    compatibilityState = {
        totalPrice: 0,
        estimatedPower: 0,
        messages: [{ level: "info", message: "Chưa có cảnh báo tương thích." }]
    };

    renderBuildTable();
    renderMessages();
}

async function saveBuild() {
    const items = collectPayloadItems();
    if (items.length === 0) {
        await showAppAlert("Bạn chưa chọn linh kiện nào.", "warning");
        return;
    }

    const buildName = await promptBuildName(
        "Lưu cấu hình",
        "Nhập tên cấu hình bạn muốn lưu.",
        "Lưu");

    if (!buildName) return;

    const res = await fetch("/api/pc-builder/save", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ buildName, items })
    });

    const data = await res.json();

    if (!res.ok) {
        await showAppAlert(data.message || "Lưu cấu hình thất bại.", "error");
        return;
    }

    await showAppAlert(`Lưu thành công. Mã build: ${data.buildCode}`, "success");
}

async function promptBuildName(title, text, confirmButtonText) {
    const promptResult = await showAppPrompt({
        title,
        text,
        inputValue: "PC Gaming mới",
        inputPlaceholder: "Ví dụ: PC Gaming mới",
        confirmButtonText,
        cancelButtonText: "Hủy",
        required: true,
        requiredMessage: "Vui lòng nhập tên cấu hình."
    });

    if (!promptResult.isConfirmed) return null;
    return String(promptResult.value || "").trim();
}

async function parseJsonResponse(res) {
    const text = await res.text();

    if (!text) {
        return {};
    }

    try {
        return JSON.parse(text);
    } catch {
        return { message: text };
    }
}

function buildCompatibilityMessages(detail, warnings) {
    const result = [];

    (warnings || []).forEach(message => {
        result.push({ level: "warning", message });
    });

    (detail.messages || []).forEach(message => {
        result.push(message);
    });

    if (result.length === 0) {
        result.push({ level: "info", message: "Cấu hình hiện chưa có cảnh báo tương thích." });
    }

    return result;
}

function applyResolvedBuild(detail, warnings) {
    closePicker();

    SLOT_CONFIGS.forEach(slot => {
        buildState[slot.type] = slot.multi ? [] : null;
    });

    (detail.items || []).forEach(entry => {
        const type = entry.componentType || entry.product?.componentType;
        if (!type || !Object.prototype.hasOwnProperty.call(buildState, type)) {
            return;
        }

        const product = {
            ...(entry.product || {}),
            id: entry.productId,
            quantity: entry.quantity || 1
        };

        if (isMultiSlot(type)) {
            buildState[type].push(product);
        } else {
            buildState[type] = product;
        }
    });

    compatibilityState = {
        totalPrice: Number(detail.totalPrice || 0),
        estimatedPower: Number(detail.estimatedPower || 0),
        messages: buildCompatibilityMessages(detail, warnings)
    };

    renderBuildTable();
    renderMessages();
}

async function exportBuildExcel() {
    const items = collectPayloadItems();
    if (items.length === 0) {
        await showAppAlert("Bạn chưa chọn linh kiện nào.", "warning");
        return;
    }

    const buildName = await promptBuildName(
        "Xuất file Excel",
        "Nhập tên cấu hình để xuất file Excel.",
        "Xuất Excel");

    if (!buildName) return;

    const res = await fetch("/api/pc-builder/export-excel", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ buildName, items })
    });

    if (!res.ok) {
        const data = await parseJsonResponse(res);
        await showAppAlert(data.message || "Không thể xuất file Excel lúc này.", "error");
        return;
    }

    await downloadResponseAsFile(res, `${buildName}.xlsx`);
}

async function downloadResponseAsFile(res, fallbackName) {
    const blob = await res.blob();
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    const disposition = res.headers.get("Content-Disposition") || "";
    const matchedFileName = disposition.match(/filename\*?=(?:UTF-8''|\"?)([^\";]+)/i);
    const fileName = matchedFileName
        ? decodeURIComponent(matchedFileName[1].replaceAll('"', ""))
        : fallbackName;

    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
}

async function onImportExcelFileSelected(event) {
    const file = event.target.files && event.target.files[0];
    if (!file) return;

    await importBuildFromExcel(file);
    event.target.value = "";
}

async function importBuildFromExcel(file) {
    const formData = new FormData();
    formData.append("file", file);

    const res = await fetch("/api/pc-builder/import-excel", {
        method: "POST",
        body: formData
    });

    const data = await parseJsonResponse(res);

    if (!res.ok) {
        const detailedMessage = Array.isArray(data.errors) && data.errors.length > 0
            ? `${data.message || "File Excel không hợp lệ."}\n${data.errors.join("\n")}`
            : (data.message || "Không thể nhập file Excel.");

        await showAppAlert(detailedMessage, "error");
        return;
    }

    applyResolvedBuild(data, data.warnings || []);

    if ((data.warnings || []).length > 0) {
        await showAppAlert("Đã nhập file Excel, nhưng có một số dòng không khớp hoàn toàn. Chi tiết đang hiển thị trong vùng cảnh báo.", "warning");
        return;
    }

    await showAppAlert(`Đã nhập cấu hình từ file ${data.sourceFileName || file.name}.`, "success");
}

function renderShareReceiverResults() {
    if (!shareReceiverResults) return;

    if (!userIsAuthenticated) {
        shareReceiverResults.innerHTML = `<div class="utility-empty">Đăng nhập bằng tài khoản khách hàng để tìm người nhận và chia sẻ cấu hình.</div>`;
        return;
    }

    if (!shareSearchResults.length) {
        shareReceiverResults.innerHTML = `<div class="utility-empty">Chưa có kết quả tìm kiếm. Hãy nhập tên hoặc email rồi bấm "Tìm người nhận".</div>`;
        return;
    }

    shareReceiverResults.innerHTML = shareSearchResults.map(user => `
                <div class="share-result-item ${selectedShareReceiverId === user.id ? "active" : ""}">
                    <div class="share-result-top">
                        <div>
                            <div class="share-result-name">${escapeHtml(user.userName || "Khách hàng")}</div>
                            <div class="share-result-meta">${escapeHtml(user.email || "Không có email")}</div>
                        </div>
                        <button type="button" class="share-select-btn" onclick="selectShareReceiver('${escapeHtml(user.id)}')">
                            ${selectedShareReceiverId === user.id ? "Đã chọn" : "Chọn"}
                        </button>
                    </div>
                </div>
            `).join("");
}

async function searchShareReceivers() {
    if (!userIsAuthenticated) {
        await showAppAlert("Bạn cần đăng nhập bằng tài khoản khách hàng để chia sẻ cấu hình.", "warning");
        return;
    }

    const keyword = String(shareReceiverKeyword?.value || "").trim();
    const res = await fetch(`/api/pc-builder/share-users?keyword=${encodeURIComponent(keyword)}`);
    const data = await parseJsonResponse(res);

    if (!res.ok) {
        await showAppAlert(data.message || "Không tìm được người nhận lúc này.", "error");
        return;
    }

    shareSearchResults = Array.isArray(data) ? data : [];

    if (!shareSearchResults.some(x => x.id === selectedShareReceiverId)) {
        selectedShareReceiverId = shareSearchResults.length > 0 ? shareSearchResults[0].id : null;
    }

    renderShareReceiverResults();
}

function selectShareReceiver(userId) {
    selectedShareReceiverId = userId;
    renderShareReceiverResults();
}

async function shareCurrentBuild() {
    if (!userIsAuthenticated) {
        await showAppAlert("Bạn cần đăng nhập bằng tài khoản khách hàng để chia sẻ cấu hình.", "warning");
        return;
    }

    const items = collectPayloadItems();
    if (items.length === 0) {
        await showAppAlert("Bạn chưa chọn linh kiện nào.", "warning");
        return;
    }

    if (!selectedShareReceiverId) {
        await showAppAlert("Bạn chưa chọn người nhận.", "warning");
        return;
    }

    const buildName = await promptBuildName(
        "Chia sẻ cấu hình",
        "Nhập tên cấu hình trước khi gửi cho người nhận.",
        "Chia sẻ");

    if (!buildName) return;

    const res = await fetch("/api/pc-builder/share", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
            buildName,
            receiverId: selectedShareReceiverId,
            note: shareNoteInput?.value || "",
            items
        })
    });

    const data = await parseJsonResponse(res);

    if (!res.ok) {
        await showAppAlert(data.message || "Không thể chia sẻ cấu hình lúc này.", "error");
        return;
    }

    if (shareNoteInput) {
        shareNoteInput.value = "";
    }

    await showAppAlert(data.message || "Đã chia sẻ cấu hình thành công.", "success");
}

async function loadSharedBuilds() {
    if (!sharedBuildList) return;

    if (!userIsAuthenticated) {
        renderSharedBuildList();
        return;
    }

    const res = await fetch("/api/pc-builder/shared");
    const data = await parseJsonResponse(res);

    if (!res.ok) {
        sharedBuildList.innerHTML = `<div class="utility-empty">${escapeHtml(data.message || "Không tải được danh sách cấu hình được chia sẻ.")}</div>`;
        return;
    }

    sharedBuildItems = Array.isArray(data) ? data : [];
    renderSharedBuildList();
}

function renderSharedBuildList() {
    if (!sharedBuildList) return;

    if (!userIsAuthenticated) {
        sharedBuildList.innerHTML = `<div class="utility-empty">Đăng nhập bằng tài khoản khách hàng để xem các cấu hình được chia sẻ.</div>`;
        return;
    }

    const items = Array.isArray(sharedBuildItems) ? sharedBuildItems : [];
    if (!items.length) {
        sharedBuildList.innerHTML = `<div class="utility-empty">Chưa có cấu hình nào được chia sẻ.</div>`;
        return;
    }

    sharedBuildList.innerHTML = items.map(item => `
                <div class="shared-build-item">
                    <div class="shared-build-top">
                        <div>
                            <div class="shared-build-name">${escapeHtml(item.buildName || "PC Build")}</div>
                            <div class="shared-build-meta">
                                Người gửi: ${escapeHtml(item.senderName || "Không rõ")}<br />
                                Thời gian: ${escapeHtml(item.createdAtText || "")}<br />
                                Tổng giá: ${formatPrice(item.totalPrice || 0)}
                                ${item.note ? `<br />Ghi chú: ${escapeHtml(item.note)}` : ""}
                            </div>
                        </div>
                    </div>
                    <div class="shared-build-actions">
                        <button type="button" class="share-load-btn" onclick="loadSharedBuild('${escapeHtml(item.shareCode)}')">Nạp vào Builder</button>
                        <button type="button" class="share-download-btn" onclick="downloadSharedBuildExcel('${escapeHtml(item.shareCode)}')">Tải Excel</button>
                    </div>
                </div>
            `).join("");
}

async function loadSharedBuild(shareCode) {
    const res = await fetch(`/api/pc-builder/shared/${encodeURIComponent(shareCode)}`);
    const data = await parseJsonResponse(res);

    if (!res.ok) {
        await showAppAlert(data.message || "Không thể tải cấu hình được chia sẻ.", "error");
        return;
    }

    applyResolvedBuild(data, []);
    window.scrollTo({ top: 0, behavior: "smooth" });
    await showAppAlert("Đã nạp cấu hình được chia sẻ vào Build PC.", "success");
    loadSharedBuilds();
}

function downloadSharedBuildExcel(shareCode) {
    window.location.href = `/api/pc-builder/shared/${encodeURIComponent(shareCode)}/excel`;
}

function appendTextChatMessage(role, text) {
    if (!buildChatMessages) return null;

    const div = document.createElement("div");
    div.className = `chat-message ${role}`;
    div.textContent = text || "";
    buildChatMessages.appendChild(div);
    buildChatMessages.scrollTop = buildChatMessages.scrollHeight;
    return div;
}

function recordChatTurn(role, content) {
    const safeContent = String(content || "").trim();
    if (!safeContent) return;

    chatConversation.push({
        role: role === "assistant" ? "assistant" : "user",
        content: safeContent
    });

    if (chatConversation.length > 16) {
        chatConversation.splice(0, chatConversation.length - 16);
    }
}

function collectChatHistoryPayload() {
    return chatConversation.slice(-8).map(item => ({
        role: item.role,
        content: item.content
    }));
}

function renderChatText(text) {
    return escapeHtml(text || "").replaceAll("\n", "<br />");
}

function renderChatClarificationHints(hints) {
    const safeHints = Array.isArray(hints) ? hints.filter(Boolean) : [];
    if (!safeHints.length) return "";

    return `
                <div class="chat-hint-box">
                    <div class="chat-hint-title">Thông tin nên bổ sung để mình tư vấn sát hơn</div>
                    ${safeHints.map(hint => `<div class="chat-hint-item">- ${escapeHtml(hint)}</div>`).join("")}
                </div>
            `;
}

function renderChatProductPanel(messageId, suggestedProducts) {
    const items = Array.isArray(suggestedProducts) ? suggestedProducts.filter(x => x && x.product) : [];
    if (!items.length) return "";

    const cardHtml = items.map((entry, index) => {
        const product = entry.product || {};
        const imageUrl = product.image || "/images/no-image.png";
        const stockIn = Number(product.stock || 0) > 0;
        const componentType = entry.componentType || product.componentType || "Linh kiện";

        return `
            <label class="chat-product-card">
                <input
                    type="checkbox"
                    class="chat-product-check"
                    data-chat-response-id="${messageId}"
                    data-suggestion-index="${index}" />

                <img class="chat-product-thumb" src="${imageUrl}" alt="${escapeHtml(product.name || "")}" />

                <div class="chat-product-main">
                    <div class="chat-product-top">
                        <div class="chat-product-name">${escapeHtml(product.name || "Sản phẩm")}</div>
                        <div class="chat-product-price">${formatPrice(product.price || 0)}</div>
                    </div>

                    <div class="chat-product-badges">
                        <span class="chat-product-badge">${escapeHtml(componentType)}</span>
                        <span class="chat-stock-badge ${stockIn ? "in" : "out"}">${stockIn ? "Còn hàng" : "Hết hàng"}</span>
                    </div>

                    ${entry.reason ? `<div class="chat-product-reason">${escapeHtml(entry.reason)}</div>` : ""}
                </div>
            </label>
        `;
    }).join("");

    return `
        <div class="chat-product-panel" data-chat-response-id="${messageId}">
            <div class="chat-product-panel-head">
                <div class="chat-product-panel-title">Sản phẩm gợi ý từ trợ lý build PC</div>
                <div class="chat-product-panel-sub">Chỉ hiển thị thông tin quan trọng để dễ chọn và đỡ rối mắt.</div>
            </div>

            <div class="chat-product-list">
                ${cardHtml}
                </div>

            <div class="chat-product-actions">
                <button type="button" class="chat-product-add-btn" onclick="applyChatSelections(${messageId})">
                    Thêm sản phẩm đã chọn vào Build PC
                </button>
            </div>
        </div>
    `;
}

function appendBotChatResponse(response) {
    if (!buildChatMessages) return null;

    const messageId = ++chatResponseSeed;
    const suggestedProducts = Array.isArray(response?.suggestedProducts) ? response.suggestedProducts : [];
    chatSuggestionStore.set(String(messageId), suggestedProducts);

    const div = document.createElement("div");
    div.className = "chat-message bot chat-rich";
    div.innerHTML = `
        <div class="chat-bubble-text">${renderChatText(response?.reply || "Mình chưa có câu trả lời phù hợp.")}</div>
        ${renderChatClarificationHints(response?.clarificationHints)}
        ${renderChatProductPanel(messageId, suggestedProducts)}
    `;

    buildChatMessages.appendChild(div);
    buildChatMessages.scrollTop = buildChatMessages.scrollHeight;
    return div;
}

async function applyChatSelections(messageId) {
    const suggestions = chatSuggestionStore.get(String(messageId)) || [];
    const panel = buildChatMessages?.querySelector(`[data-chat-response-id="${messageId}"]`);
    if (!panel) return;

    const checkedInputs = [...panel.querySelectorAll(".chat-product-check:checked")];
    if (!checkedInputs.length) {
        await showAppAlert("Bạn chưa tick sản phẩm nào để thêm vào Build PC.", "warning");
        return;
    }

    const selectedEntries = checkedInputs
        .map(input => suggestions[Number(input.dataset.suggestionIndex)])
        .filter(Boolean);

    const invalidSingleTypes = [];
    const groupedByType = new Map();

    selectedEntries.forEach(entry => {
        const type = entry.componentType || entry.product?.componentType;
        if (!type) return;

        if (!groupedByType.has(type)) {
            groupedByType.set(type, []);
        }

        groupedByType.get(type).push(entry);
    });

    groupedByType.forEach((items, type) => {
        if (!isMultiSlot(type) && items.length > 1) {
            invalidSingleTypes.push(type);
        }
    });

    if (invalidSingleTypes.length > 0) {
        await showAppAlert(`Mỗi loại ${invalidSingleTypes.join(", ")} chỉ có thể chọn 1 sản phẩm mỗi lần.`, "warning");
        return;
    }

    let addedCount = 0;
    let replacedCount = 0;
    let skippedCount = 0;

    selectedEntries.forEach(entry => {
        const type = entry.componentType || entry.product?.componentType;
        const product = entry.product;

        if (!type || !product || !Object.prototype.hasOwnProperty.call(buildState, type)) {
            skippedCount++;
            return;
        }

        const nextProduct = {
            ...product,
            quantity: 1
        };

        if (isMultiSlot(type)) {
            const existing = buildState[type].find(x => x.id === product.id);
            if (existing) {
                skippedCount++;
                return;
            }

            buildState[type].push(nextProduct);
            addedCount++;
            return;
        }

        if (buildState[type] && buildState[type].id === product.id) {
            skippedCount++;
            return;
        }

        if (buildState[type]) {
            replacedCount++;
        }

        buildState[type] = nextProduct;
        addedCount++;
    });

    renderBuildTable();
    await checkCompatibility();

    checkedInputs.forEach(input => {
        input.checked = false;
    });

    if (addedCount === 0 && skippedCount > 0) {
        await showAppAlert("Các sản phẩm đã chọn đã có sẵn trong Build PC hoặc không thể thêm lúc này.", "warning");
        return;
    }

    const messageParts = [`Đã cập nhật ${addedCount} linh kiện vào Build PC.`];
    if (replacedCount > 0) {
        messageParts.push(`Có ${replacedCount} vị trí đơn đã được thay thế.`);
    }

    await showAppAlert(messageParts.join(" "), "success");
    window.scrollTo({ top: 0, behavior: "smooth" });
}

async function askBuildAssistant(message) {
    const text = String(message || "").trim();
    if (!text) return;

    const historyPayload = collectChatHistoryPayload();
    appendTextChatMessage("user", text);
    recordChatTurn("user", text);

    const loadingNode = appendTextChatMessage("bot", "Mình đang kiểm tra cấu hình và tìm sản phẩm phù hợp cho bạn...");

    try {
        const res = await fetch("/api/pc-builder/chat", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                message: text,
                items: collectPayloadItems(),
                history: historyPayload
            })
        });

        const data = await parseJsonResponse(res);

        if (!res.ok) {
            throw new Error(data.message || "Không thể nhận tư vấn lúc này.");
        }

        loadingNode?.remove();
        appendBotChatResponse(data);
        recordChatTurn("assistant", data.reply || "Mình chưa có câu trả lời phù hợp.");
    } catch (error) {
        loadingNode?.remove();
        appendTextChatMessage("bot", error.message || "Đang có lỗi khi tư vấn cấu hình.");
    }
}

function formatPrice(value) {
    return Number(value || 0).toLocaleString("vi-VN") + " đ";
}

function naturalCompare(a, b) {
    return String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: "base" });
}

function escapeHtml(str) {
    if (str == null) return "";
    return String(str)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

async function addBuildToCart() {
    const items = collectPayloadItems();

    if (items.length === 0) {
        await showAppAlert("Bạn chưa chọn linh kiện nào.", "warning");
        return;
    }

    const buildName = await promptBuildName(
        "Thêm cấu hình vào giỏ hàng",
        "Nhập tên cấu hình để lưu cùng giỏ hàng.",
        "Tiếp tục");

    if (!buildName) return;

    const res = await fetch("/api/pc-builder/add-to-cart", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ buildName, items })
    });

    const data = await res.json();

    if (!res.ok) {
        await showAppAlert(data.message || "Không thể thêm cấu hình vào giỏ hàng.", "error");
        return;
    }

    await showAppAlert(data.message || "Đã thêm cấu hình vào giỏ hàng.", "success");
    window.location.href = "/Cart/Index";
}

window.openPicker = openPicker;
window.changeQty = changeQty;
window.removeSelectedItem = removeSelectedItem;
window.askBuildAssistant = askBuildAssistant;
window.applyChatSelections = applyChatSelections;
window.selectShareReceiver = selectShareReceiver;
window.loadSharedBuild = loadSharedBuild;
window.downloadSharedBuildExcel = downloadSharedBuildExcel;

if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initPcBuilderPage);
} else {
    initPcBuilderPage();
}
