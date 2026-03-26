document.addEventListener("DOMContentLoaded", function () {
    const canvas = document.getElementById("revenueTrendChart");
    if (!canvas || typeof Chart === "undefined") return;

    const labels = window.dashboardRevenueLabels || [];
    const revenues = window.dashboardRevenueData || [];
    const orders = window.dashboardOrderData || [];

    new Chart(canvas, {
        type: "bar",
        data: {
            labels: labels,
            datasets: [
                {
                    type: "bar",
                    label: "Doanh thu",
                    data: revenues,
                    yAxisID: "y",
                    backgroundColor: "rgba(37, 99, 235, 0.82)",
                    borderRadius: 8,
                    borderSkipped: false,
                    maxBarThickness: 30
                },
                {
                    type: "line",
                    label: "Số đơn",
                    data: orders,
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
                                return " Doanh thu: " + Number(context.raw).toLocaleString("vi-VN") + " đ";
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
                            return Number(value).toLocaleString("vi-VN") + " đ";
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
});