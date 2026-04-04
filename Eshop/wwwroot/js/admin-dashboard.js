document.addEventListener("DOMContentLoaded", function () {
    if (typeof Chart === "undefined") {
        return;
    }

    const numberFormatter = new Intl.NumberFormat("vi-VN");
    const compactFormatter = new Intl.NumberFormat("vi-VN", {
        notation: "compact",
        maximumFractionDigits: 1
    });
    const palette = [
        "#2563eb",
        "#0f766e",
        "#f59e0b",
        "#ef4444",
        "#7c3aed",
        "#0891b2",
        "#ea580c",
        "#4f46e5"
    ];

    function money(value) {
        return numberFormatter.format(Number(value || 0)) + " đ";
    }

    function compactNumber(value) {
        return compactFormatter.format(Number(value || 0));
    }

    function hasData(values) {
        return Array.isArray(values) && values.some(function (value) {
            return Number(value) > 0;
        });
    }

    function hexToRgba(hex, alpha) {
        const normalized = (hex || "").replace("#", "");
        if (normalized.length !== 6) {
            return hex;
        }

        const r = parseInt(normalized.substring(0, 2), 16);
        const g = parseInt(normalized.substring(2, 4), 16);
        const b = parseInt(normalized.substring(4, 6), 16);

        return "rgba(" + r + ", " + g + ", " + b + ", " + alpha + ")";
    }

    function buildPalette(count, alpha) {
        return Array.from({ length: count }, function (_, index) {
            return hexToRgba(palette[index % palette.length], alpha);
        });
    }

    function createChart(id, config) {
        const canvas = document.getElementById(id);
        if (!canvas) {
            return null;
        }

        return new Chart(canvas, config);
    }

    createChart("revenueTrendChart", {
        type: "bar",
        data: {
            labels: window.dashboardRevenueLabels || [],
            datasets: [
                {
                    type: "bar",
                    label: "Doanh thu",
                    data: window.dashboardRevenueData || [],
                    yAxisID: "y",
                    backgroundColor: "rgba(37, 99, 235, 0.82)",
                    borderRadius: 8,
                    borderSkipped: false,
                    maxBarThickness: 30
                },
                {
                    type: "line",
                    label: "Số đơn",
                    data: window.dashboardOrderData || [],
                    yAxisID: "y1",
                    borderColor: "#111827",
                    backgroundColor: "rgba(17, 24, 39, 0.08)",
                    borderWidth: 2,
                    tension: 0.35,
                    fill: false,
                    pointRadius: 2.5,
                    pointHoverRadius: 4
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                mode: "index",
                intersect: false
            },
            plugins: {
                legend: {
                    position: "top",
                    labels: {
                        boxWidth: 12,
                        usePointStyle: true,
                        padding: 16,
                        color: "#475569",
                        font: {
                            size: 11,
                            weight: "600"
                        }
                    }
                },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            if (context.dataset.label === "Doanh thu") {
                                return " Doanh thu: " + money(context.raw);
                            }

                            return " Số đơn: " + context.raw;
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: { display: false },
                    ticks: {
                        color: "#64748b",
                        font: {
                            size: 11
                        }
                    }
                },
                y: {
                    beginAtZero: true,
                    position: "left",
                    grid: {
                        color: "rgba(148, 163, 184, 0.14)"
                    },
                    ticks: {
                        color: "#64748b",
                        callback: function (value) {
                            return money(value);
                        }
                    }
                },
                y1: {
                    beginAtZero: true,
                    position: "right",
                    grid: {
                        drawOnChartArea: false
                    },
                    ticks: {
                        color: "#64748b"
                    }
                }
            }
        }
    });

    const statusLabels = window.dashboardStatusLabels || [];
    const statusCounts = window.dashboardStatusCounts || [];
    const statusAmounts = window.dashboardStatusAmounts || [];

    if (statusLabels.length > 0 && hasData(statusCounts)) {
        createChart("orderStatusChart", {
            type: "doughnut",
            data: {
                labels: statusLabels,
                datasets: [
                    {
                        data: statusCounts,
                        backgroundColor: buildPalette(statusLabels.length, 0.9),
                        borderColor: "#ffffff",
                        borderWidth: 2,
                        hoverOffset: 8
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: "64%",
                plugins: {
                    legend: {
                        position: "bottom",
                        labels: {
                            boxWidth: 10,
                            usePointStyle: true,
                            padding: 14,
                            color: "#475569",
                            font: {
                                size: 11,
                                weight: "600"
                            }
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                const index = context.dataIndex;
                                return " " + context.label + ": " + numberFormatter.format(statusCounts[index]) + " đơn - " + money(statusAmounts[index]);
                            }
                        }
                    }
                }
            }
        });
    }

    const paymentLabels = window.dashboardPaymentLabels || [];
    const paymentCounts = window.dashboardPaymentCounts || [];
    const paymentAmounts = window.dashboardPaymentAmounts || [];

    if (paymentLabels.length > 0 && hasData(paymentCounts)) {
        createChart("paymentMixChart", {
            type: "doughnut",
            data: {
                labels: paymentLabels,
                datasets: [
                    {
                        data: paymentCounts,
                        backgroundColor: buildPalette(paymentLabels.length, 0.82),
                        borderColor: "#ffffff",
                        borderWidth: 2,
                        hoverOffset: 8
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: "62%",
                plugins: {
                    legend: {
                        position: "bottom",
                        labels: {
                            boxWidth: 10,
                            usePointStyle: true,
                            padding: 14,
                            color: "#475569",
                            font: {
                                size: 11,
                                weight: "600"
                            }
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                const index = context.dataIndex;
                                return " " + context.label + ": " + numberFormatter.format(paymentCounts[index]) + " đơn - " + money(paymentAmounts[index]);
                            }
                        }
                    }
                }
            }
        });
    }

    const categoryLabels = window.dashboardCategoryLabels || [];
    const categoryRevenue = window.dashboardCategoryRevenue || [];
    const categoryQuantity = window.dashboardCategoryQuantity || [];

    if (categoryLabels.length > 0 && hasData(categoryRevenue)) {
        createChart("categoryRevenueChart", {
            type: "bar",
            data: {
                labels: categoryLabels,
                datasets: [
                    {
                        label: "Doanh thu",
                        data: categoryRevenue,
                        backgroundColor: buildPalette(categoryLabels.length, 0.86),
                        borderRadius: 10,
                        borderSkipped: false,
                        barThickness: 18
                    }
                ]
            },
            options: {
                indexAxis: "y",
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return " Doanh thu: " + money(context.raw);
                            },
                            afterLabel: function (context) {
                                const index = context.dataIndex;
                                return " Số lượng bán: " + numberFormatter.format(categoryQuantity[index]);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        beginAtZero: true,
                        grid: {
                            color: "rgba(148, 163, 184, 0.14)"
                        },
                        ticks: {
                            color: "#64748b",
                            callback: function (value) {
                                return compactNumber(value);
                            }
                        }
                    },
                    y: {
                        grid: {
                            display: false
                        },
                        ticks: {
                            color: "#334155",
                            font: {
                                size: 11,
                                weight: "600"
                            }
                        }
                    }
                }
            }
        });
    }

    const warehouseLabels = window.dashboardWarehouseLabels || [];
    const warehouseAvailable = window.dashboardWarehouseAvailable || [];
    const warehouseReserved = window.dashboardWarehouseReserved || [];

    if (warehouseLabels.length > 0 && (hasData(warehouseAvailable) || hasData(warehouseReserved))) {
        createChart("warehouseStockChart", {
            type: "bar",
            data: {
                labels: warehouseLabels,
                datasets: [
                    {
                        label: "Khả dụng",
                        data: warehouseAvailable,
                        backgroundColor: "rgba(37, 99, 235, 0.82)",
                        borderRadius: 10,
                        borderSkipped: false,
                        stack: "inventory"
                    },
                    {
                        label: "Giữ chỗ",
                        data: warehouseReserved,
                        backgroundColor: "rgba(245, 158, 11, 0.82)",
                        borderRadius: 10,
                        borderSkipped: false,
                        stack: "inventory"
                    }
                ]
            },
            options: {
                indexAxis: "y",
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: "top",
                        labels: {
                            boxWidth: 12,
                            usePointStyle: true,
                            padding: 16,
                            color: "#475569",
                            font: {
                                size: 11,
                                weight: "600"
                            }
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return " " + context.dataset.label + ": " + numberFormatter.format(context.raw);
                            },
                            footer: function (items) {
                                const index = items[0].dataIndex;
                                const total = Number(warehouseAvailable[index] || 0) + Number(warehouseReserved[index] || 0);
                                return "On-hand: " + numberFormatter.format(total);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        stacked: true,
                        beginAtZero: true,
                        grid: {
                            color: "rgba(148, 163, 184, 0.14)"
                        },
                        ticks: {
                            color: "#64748b"
                        }
                    },
                    y: {
                        stacked: true,
                        grid: {
                            display: false
                        },
                        ticks: {
                            color: "#334155",
                            font: {
                                size: 11,
                                weight: "600"
                            }
                        }
                    }
                }
            }
        });
    }
});
