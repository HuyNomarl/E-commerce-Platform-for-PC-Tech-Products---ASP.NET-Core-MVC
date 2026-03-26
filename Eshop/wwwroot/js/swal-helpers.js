(function (window, document) {
    function getTitle(icon, title) {
        if (title) {
            return title;
        }

        switch (icon) {
            case "success":
                return "Thành công";
            case "error":
                return "Có lỗi xảy ra";
            case "warning":
                return "Cảnh báo";
            default:
                return "Thông báo";
        }
    }

    window.showAppAlert = function (message, icon, title) {
        const normalizedIcon = icon || "info";
        const normalizedTitle = getTitle(normalizedIcon, title);

        if (!window.Swal) {
            window.alert(message || normalizedTitle);
            return Promise.resolve({ isConfirmed: true });
        }

        return window.Swal.fire({
            icon: normalizedIcon,
            title: normalizedTitle,
            text: message || "",
            confirmButtonText: "Đóng"
        });
    };

    window.showAppConfirm = function (options) {
        const config = options || {};
        const title = config.title || "Bạn có chắc chắn?";
        const text = config.text || "";

        if (!window.Swal) {
            return Promise.resolve({
                isConfirmed: window.confirm(text ? title + "\n" + text : title)
            });
        }

        return window.Swal.fire({
            title: title,
            text: text,
            icon: config.icon || "warning",
            showCancelButton: true,
            confirmButtonText: config.confirmButtonText || "Xác nhận",
            cancelButtonText: config.cancelButtonText || "Hủy",
            confirmButtonColor: config.confirmButtonColor || "#d33",
            cancelButtonColor: config.cancelButtonColor || "#6b7280"
        });
    };

    window.showAppPrompt = function (options) {
        const config = options || {};

        if (!window.Swal) {
            const value = window.prompt(config.title || "Nhập thông tin", config.inputValue || "");
            return Promise.resolve({
                isConfirmed: value !== null,
                value: value
            });
        }

        return window.Swal.fire({
            title: config.title || "Nhập thông tin",
            text: config.text || "",
            input: "text",
            inputValue: config.inputValue || "",
            inputPlaceholder: config.inputPlaceholder || "",
            showCancelButton: true,
            confirmButtonText: config.confirmButtonText || "Xác nhận",
            cancelButtonText: config.cancelButtonText || "Hủy",
            inputValidator: function (value) {
                if (config.required && !value) {
                    return config.requiredMessage || "Vui lòng nhập giá trị.";
                }

                return null;
            }
        });
    };

    async function showPendingNotifications() {
        const container = document.getElementById("app-notifications");
        if (!container) {
            return;
        }

        let notifications = [];

        try {
            notifications = JSON.parse(container.textContent || "[]");
        } catch (error) {
            console.error("Cannot parse notifications", error);
        }

        for (const item of notifications) {
            await window.showAppAlert(item.message, item.type, item.title);
        }
    }

    document.addEventListener("DOMContentLoaded", function () {
        void showPendingNotifications();
    });

    document.addEventListener("click", async function (event) {
        const trigger = event.target.closest("a[data-swal-confirm-link]");
        if (!trigger) {
            return;
        }

        event.preventDefault();

        const result = await window.showAppConfirm({
            title: trigger.dataset.swalTitle || "Bạn có chắc chắn?",
            text: trigger.dataset.swalText || "",
            icon: trigger.dataset.swalIcon || "warning",
            confirmButtonText: trigger.dataset.swalConfirmText || "Đồng ý",
            cancelButtonText: trigger.dataset.swalCancelText || "Hủy"
        });

        if (result.isConfirmed && trigger.href) {
            window.location.href = trigger.href;
        }
    });

    document.addEventListener("submit", async function (event) {
        const form = event.target.closest("form[data-swal-confirm]");
        if (!form || form.dataset.swalConfirmed === "1") {
            return;
        }

        event.preventDefault();

        const result = await window.showAppConfirm({
            title: form.dataset.swalTitle || "Bạn có chắc chắn?",
            text: form.dataset.swalText || "",
            icon: form.dataset.swalIcon || "warning",
            confirmButtonText: form.dataset.swalConfirmText || "Đồng ý",
            cancelButtonText: form.dataset.swalCancelText || "Hủy"
        });

        if (result.isConfirmed) {
            form.dataset.swalConfirmed = "1";
            form.submit();
        }
    });
})(window, document);
