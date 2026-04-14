document.addEventListener("DOMContentLoaded", function () {
    initCategoryMenu();
    initSupportChat();
});

function initCategoryMenu() {
    const toggleBtn = document.getElementById("ttCategoryToggle");
    const dropdown = document.getElementById("ttCategoryDropdown");

    if (!toggleBtn || !dropdown) return;

    toggleBtn.addEventListener("click", function (e) {
        e.stopPropagation();
        dropdown.classList.toggle("show");
    });

    document.addEventListener("click", function (e) {
        const clickedOutside = !dropdown.contains(e.target) && !toggleBtn.contains(e.target);
        if (clickedOutside) {
            dropdown.classList.remove("show");
        }
    });
}

function initSupportChat() {
    const toggleBtn = document.getElementById("supportChatToggle");
    const chatBox = document.getElementById("supportChatBox");
    const closeBtn = document.getElementById("supportChatClose");
    const sendBtn = document.getElementById("supportChatSend");
    const input = document.getElementById("supportChatInput");
    const messagesBox = document.getElementById("supportChatMessages");
    const title = document.getElementById("supportChatTitle");
    const attachmentInput = document.getElementById("supportChatAttachment");
    const selectionBox = document.getElementById("supportChatSelection");
    const productPicker = document.getElementById("supportChatProductPicker");
    const pickProductBtn = document.getElementById("supportChatPickProduct");
    const productSearchInput = document.getElementById("supportChatProductSearch");
    const productSearchBtn = document.getElementById("supportChatProductSearchBtn");
    const productResults = document.getElementById("supportChatProductResults");

    if (!toggleBtn || !chatBox || !closeBtn || !sendBtn || !input || !messagesBox || !title) {
        return;
    }

    let supportUserId = null;
    let started = false;
    let isLoaded = false;
    let sending = false;
    let selectedAttachment = null;
    let selectedProduct = null;
    const renderedMessageIds = new Set();

    const antiForgeryTokenInput = chatBox.querySelector('input[name="__RequestVerificationToken"]');
    const antiForgeryToken = antiForgeryTokenInput ? antiForgeryTokenInput.value : "";

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/chat")
        .withAutomaticReconnect()
        .build();

    function escapeHtml(value) {
        return (value || "").replace(/[&<>"']/g, function (char) {
            return ({
                "&": "&amp;",
                "<": "&lt;",
                ">": "&gt;",
                "\"": "&quot;",
                "'": "&#39;"
            })[char];
        });
    }

    function formatPrice(value) {
        return new Intl.NumberFormat("vi-VN").format(value || 0) + " d";
    }

    function showChat() {
        chatBox.classList.remove("is-hidden");
        chatBox.setAttribute("aria-hidden", "false");
        document.body.classList.add("support-chat-open");
        window.dispatchEvent(new CustomEvent("eshop:support-chat-opened"));
    }

    function hideChat() {
        chatBox.classList.add("is-hidden");
        chatBox.setAttribute("aria-hidden", "true");
        document.body.classList.remove("support-chat-open");
        window.dispatchEvent(new CustomEvent("eshop:support-chat-closed"));
    }

    function scrollToBottom() {
        messagesBox.scrollTop = messagesBox.scrollHeight;
    }

    function renderEmptyState(text) {
        messagesBox.innerHTML = '<div class="support-chat-empty">' + escapeHtml(text) + "</div>";
    }

    function clearMessages() {
        renderedMessageIds.clear();
        messagesBox.innerHTML = "";
    }

    function clearAttachment() {
        selectedAttachment = null;
        if (attachmentInput) {
            attachmentInput.value = "";
        }
    }

    function clearProduct() {
        selectedProduct = null;
    }

    function renderSelection() {
        if (!selectionBox) {
            return;
        }

        if (!selectedAttachment && !selectedProduct) {
            selectionBox.style.display = "none";
            selectionBox.innerHTML = "";
            return;
        }

        selectionBox.style.display = "block";

        if (selectedAttachment) {
            const mediaLabel = (selectedAttachment.type || "").startsWith("video/") ? "Video" : "Anh";
            selectionBox.innerHTML = `
                <button type="button" class="remove-selection" data-clear="attachment">&times;</button>
                <strong>${mediaLabel}</strong>
                <div>${escapeHtml(selectedAttachment.name)}</div>
            `;
            return;
        }

        selectionBox.innerHTML = `
            <button type="button" class="remove-selection" data-clear="product">&times;</button>
            <strong>San pham da chon</strong>
            <div>${escapeHtml(selectedProduct.name || "")}</div>
        `;
    }

    function toggleProductPicker(forceOpen) {
        if (!productPicker) {
            return;
        }

        const shouldOpen = typeof forceOpen === "boolean"
            ? forceOpen
            : productPicker.style.display === "none";

        productPicker.style.display = shouldOpen ? "block" : "none";
        if (!shouldOpen && productResults) {
            productResults.innerHTML = "";
        }
    }

    function createMessageContent(message) {
        const wrapper = document.createElement("div");
        wrapper.className = "support-chat-rich";

        if (message.text) {
            const text = document.createElement("div");
            text.textContent = message.text;
            wrapper.appendChild(text);
        }

        if (message.attachmentUrl) {
            const mediaWrap = document.createElement("div");
            mediaWrap.className = "support-chat-media";

            if ((message.messageType || "").toLowerCase() === "video") {
                const video = document.createElement("video");
                video.controls = true;
                video.preload = "metadata";
                video.src = message.attachmentUrl;
                mediaWrap.appendChild(video);
            } else {
                const img = document.createElement("img");
                img.src = message.attachmentUrl;
                img.alt = message.attachmentName || "chat-media";
                mediaWrap.appendChild(img);
            }

            wrapper.appendChild(mediaWrap);
        }

        if (message.product) {
            const link = document.createElement("a");
            link.className = "support-chat-product-card";
            link.href = message.product.url || "#";
            link.target = "_blank";
            link.rel = "noopener noreferrer";

            const img = document.createElement("img");
            img.src = message.product.imageUrl || "/images/gallery1.jpg";
            img.alt = message.product.name || "san-pham";

            const body = document.createElement("div");
            body.className = "support-chat-product-card-body";

            const name = document.createElement("strong");
            name.textContent = message.product.name || "San pham";

            const price = document.createElement("span");
            price.textContent = formatPrice(message.product.price);

            body.appendChild(name);
            body.appendChild(price);
            link.appendChild(img);
            link.appendChild(body);
            wrapper.appendChild(link);
        }

        return wrapper;
    }

    function appendMessage(message) {
        const messageId = message && message.id ? String(message.id) : null;
        if (messageId && renderedMessageIds.has(messageId)) {
            return;
        }

        if (messageId) {
            renderedMessageIds.add(messageId);
        }

        const emptyState = messagesBox.querySelector(".support-chat-empty");
        if (emptyState) emptyState.remove();

        const isMe = !message.isFromSupport;
        const item = document.createElement("div");
        item.className = "support-chat-item " + (isMe ? "me" : "other");

        const bubble = document.createElement("div");
        bubble.className = "support-chat-bubble";

        if (!isMe) {
            const sender = document.createElement("span");
            sender.className = "support-chat-sender";
            sender.textContent = message.senderName || "Admin";
            bubble.appendChild(sender);
        }

        bubble.appendChild(createMessageContent(message));

        const time = document.createElement("small");
        time.className = "support-chat-time";
        time.textContent = message.createdAt || "";
        bubble.appendChild(time);

        item.appendChild(bubble);
        messagesBox.appendChild(item);
        scrollToBottom();
    }

    async function ensureStarted() {
        if (started) return;
        await connection.start();
        started = true;
    }

    async function loadSupportInfo() {
        const response = await fetch("/Chat/GetAdminInfo");
        const data = await response.json();

        if (!data.success) {
            throw new Error(data.message || "Khong lay duoc thong tin ho tro.");
        }

        supportUserId = data.adminId || null;
        title.textContent = data.title || ("Chat voi " + (data.adminName || "Admin"));
    }

    async function loadHistory() {
        const response = await fetch("/Chat/GetSupportMessages");
        const data = await response.json();

        clearMessages();

        if (!data || !data.length) {
            renderEmptyState("Chưa có tin nhắn nào.");
            return;
        }

        data.forEach(appendMessage);
    }

    async function searchProducts() {
        if (!productResults) {
            return;
        }

        const keyword = (productSearchInput && productSearchInput.value || "").trim();
        const response = await fetch("/Chat/SearchProducts?term=" + encodeURIComponent(keyword));
        const data = await response.json();

        productResults.innerHTML = "";

        if (!data || !data.length) {
            productResults.innerHTML = '<div class="support-chat-empty">Không tìm thấy sản phẩm phù hợp.</div>';
            return;
        }

        data.forEach(function (product) {
            const item = document.createElement("button");
            item.type = "button";
            item.className = "support-chat-product-item";
            item.innerHTML = `
                <img src="${escapeHtml(product.imageUrl || "/images/gallery1.jpg")}" alt="${escapeHtml(product.name || "san-pham")}" />
                <div>
                    <strong>${escapeHtml(product.name || "San pham")}</strong>
                    <span>${escapeHtml(formatPrice(product.price))}</span>
                </div>
            `;

            item.addEventListener("click", function () {
                clearAttachment();
                selectedProduct = product;
                renderSelection();
                toggleProductPicker(false);
            });

            productResults.appendChild(item);
        });
    }

    async function openChat() {
        showChat();
        await loadSupportInfo();
        await ensureStarted();
        await loadHistory();
        isLoaded = true;
    }

    async function sendMessage() {
        if (sending) {
            return;
        }

        const content = input.value.trim();
        if (!content && !selectedAttachment && !selectedProduct) {
            return;
        }

        sending = true;
        sendBtn.disabled = true;

        try {
            await ensureStarted();

            const formData = new FormData();
            formData.append("__RequestVerificationToken", antiForgeryToken);
            if (supportUserId) {
                formData.append("receiverId", supportUserId);
            }
            if (content) {
                formData.append("content", content);
            }
            if (selectedAttachment) {
                formData.append("attachment", selectedAttachment);
            }
            if (selectedProduct && selectedProduct.id) {
                formData.append("productId", selectedProduct.id);
            }

            const response = await fetch("/Chat/SendSupportMessage", {
                method: "POST",
                body: formData
            });

            const payload = await response.json();
            if (!response.ok) {
                throw new Error(payload && payload.message ? payload.message : "Gui tin nhan that bai.");
            }

            appendMessage(payload);
            input.value = "";
            clearAttachment();
            clearProduct();
            renderSelection();
            input.focus();
        } catch (error) {
            console.error(error);
            Swal.fire("Loi", error.message || "Gui tin nhan that bai.", "error");
        } finally {
            sending = false;
            sendBtn.disabled = false;
        }
    }

    connection.on("ReceiveSupportMessage", function (message) {
        appendMessage(message);
    });

    connection.onreconnected(async function () {
        try {
            await loadHistory();
        } catch (error) {
            console.error(error);
        }
    });

    toggleBtn.addEventListener("click", async function () {
        const isHidden = chatBox.classList.contains("is-hidden");

        if (!isHidden) {
            hideChat();
            return;
        }

        try {
            if (!isLoaded) {
                await openChat();
            } else {
                showChat();
                await loadHistory();
            }
        } catch (error) {
            console.error(error);
            hideChat();
            Swal.fire("Loi", error.message || "Khong mo duoc hop chat.", "error");
        }
    });

    closeBtn.addEventListener("click", function () {
        hideChat();
    });

    sendBtn.addEventListener("click", function () {
        sendMessage();
    });

    input.addEventListener("keydown", function (event) {
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            sendMessage();
        }
    });

    if (attachmentInput) {
        attachmentInput.addEventListener("change", function () {
            selectedAttachment = this.files && this.files[0] ? this.files[0] : null;
            if (selectedAttachment) {
                clearProduct();
                toggleProductPicker(false);
            }
            renderSelection();
        });
    }

    if (pickProductBtn) {
        pickProductBtn.addEventListener("click", function () {
            if (selectedAttachment) {
                clearAttachment();
                renderSelection();
            }
            toggleProductPicker();
        });
    }

    if (productSearchBtn) {
        productSearchBtn.addEventListener("click", function () {
            searchProducts();
        });
    }

    if (productSearchInput) {
        productSearchInput.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                searchProducts();
            }
        });
    }

    if (selectionBox) {
        selectionBox.addEventListener("click", function (event) {
            const button = event.target.closest("[data-clear]");
            if (!button) {
                return;
            }

            if (button.getAttribute("data-clear") === "attachment") {
                clearAttachment();
            } else {
                clearProduct();
            }

            renderSelection();
        });
    }
}
