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

    if (!toggleBtn || !chatBox || !closeBtn || !sendBtn || !input || !messagesBox || !title) {
        return;
    }

    let adminId = null;
    let started = false;
    let isLoaded = false;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect()
        .build();

    function showChat() {
        chatBox.classList.remove("is-hidden");
        chatBox.setAttribute("aria-hidden", "false");
    }

    function hideChat() {
        chatBox.classList.add("is-hidden");
        chatBox.setAttribute("aria-hidden", "true");
    }

    function scrollToBottom() {
        messagesBox.scrollTop = messagesBox.scrollHeight;
    }

    function clearMessages() {
        messagesBox.innerHTML = "";
    }

    function appendMessage(message, isMe) {
        const emptyState = messagesBox.querySelector(".support-chat-empty");
        if (emptyState) emptyState.remove();

        const item = document.createElement("div");
        item.className = `support-chat-item ${isMe ? "me" : "other"}`;

        const bubble = document.createElement("div");
        bubble.className = "support-chat-bubble";

        const content = document.createElement("div");
        content.textContent = message.content || "";

        const time = document.createElement("small");
        time.className = "support-chat-time";
        time.textContent = message.createdAt || "";

        bubble.appendChild(content);
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

    async function loadAdmin() {
        const response = await fetch("/Chat/GetAdminInfo");
        const data = await response.json();

        if (!data.success) {
            throw new Error(data.message || "Không lấy được thông tin admin.");
        }

        adminId = data.adminId;
        title.textContent = `Chat với ${data.adminName || "Admin"}`;
    }

    async function loadHistory() {
        if (!adminId) return;

        const response = await fetch(`/Chat/GetSupportMessages?adminId=${encodeURIComponent(adminId)}`);
        const data = await response.json();

        clearMessages();

        if (!data || data.length === 0) {
            messagesBox.innerHTML = `<div class="support-chat-empty">Chưa có tin nhắn nào.</div>`;
            return;
        }

        data.forEach(function (msg) {
            const isMe = msg.senderId !== adminId;
            appendMessage({
                content: msg.content,
                createdAt: msg.createdAt
            }, isMe);
        });
    }

    async function openChatFirstTime() {
        await loadAdmin();
        await ensureStarted();
        await loadHistory();
        isLoaded = true;
    }

    async function sendMessage() {
        const content = input.value.trim();
        if (!content) return;

        if (!adminId) {
            Swal.fire("Thông báo", "Chưa tìm thấy admin để chat.", "warning");
            return;
        }

        try {
            await ensureStarted();
            await connection.invoke("SendMessage", adminId, content);
            input.value = "";
            input.focus();
        } catch (error) {
            console.error(error);
            Swal.fire("Lỗi", "Gửi tin nhắn thất bại.", "error");
        }
    }

    connection.on("ReceiveSupportMessage", function (message) {
        if (!adminId) return;

        const related = message.senderId === adminId || message.receiverId === adminId;
        if (!related) return;

        const isMe = message.senderId !== adminId;
        appendMessage(message, isMe);
    });

    toggleBtn.addEventListener("click", async function () {
        const isHidden = chatBox.classList.contains("is-hidden");

        if (!isHidden) {
            hideChat();
            return;
        }

        showChat();

        try {
            if (!isLoaded) {
                await openChatFirstTime();
            }
        } catch (error) {
            console.error(error);
            hideChat();
            Swal.fire("Lỗi", error.message || "Không mở được hộp chat.", "error");
        }
    });

    closeBtn.addEventListener("click", function () {
        hideChat();
    });

    sendBtn.addEventListener("click", function () {
        sendMessage();
    });

    input.addEventListener("keypress", function (e) {
        if (e.key === "Enter") {
            e.preventDefault();
            sendMessage();
        }
    });
}