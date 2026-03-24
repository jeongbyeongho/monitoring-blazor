window.monitorCharts = (function () {
  let cpuGauge;
  let memoryGauge;
  let diskGauge;
  let networkChart;
  let resourceChart;
  const palette = {
    text: "#d6e8fb",
    subtleText: "#9eb9d3",
    grid: "rgba(147, 178, 210, 0.22)",
    gaugeRemainder: "rgba(106, 136, 168, 0.28)"
  };

  function gaugeConfig(ctx, color) {
    return new Chart(ctx, {
      type: "doughnut",
      data: {
        labels: ["value", "remain"],
        datasets: [{ data: [0, 100], backgroundColor: [color, palette.gaugeRemainder], borderWidth: 0 }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        cutout: "72%",
        plugins: { legend: { display: false }, tooltip: { enabled: false } }
      }
    });
  }

  function updateGauge(chart, value) {
    if (!chart) return;
    const v = Math.max(0, Math.min(100, Number(value) || 0));
    chart.data.datasets[0].data = [v, 100 - v];
    chart.update("none");
  }

  return {
    init: function () {
      const cpuCanvas = document.getElementById("cpuGauge");
      const memCanvas = document.getElementById("memoryGauge");
      const diskCanvas = document.getElementById("diskGauge");
      const networkCanvas = document.getElementById("networkChart");
      const resourceCanvas = document.getElementById("resourceChart");

      if (!cpuCanvas || !memCanvas || !diskCanvas || !networkCanvas || !resourceCanvas) {
        return false;
      }

      if (cpuGauge) cpuGauge.destroy();
      if (memoryGauge) memoryGauge.destroy();
      if (diskGauge) diskGauge.destroy();
      if (networkChart) networkChart.destroy();
      if (resourceChart) resourceChart.destroy();

      cpuGauge = gaugeConfig(cpuCanvas.getContext("2d"), "rgba(255, 99, 132, 1)");
      memoryGauge = gaugeConfig(memCanvas.getContext("2d"), "rgba(54, 162, 235, 1)");
      diskGauge = gaugeConfig(diskCanvas.getContext("2d"), "rgba(40, 167, 69, 1)");

      networkChart = new Chart(networkCanvas.getContext("2d"), {
        type: "line",
        data: {
          labels: [],
          datasets: [
            { label: "TX (Mbps)", data: [], borderColor: "rgb(247, 117, 138)", fill: false, tension: 0.25, pointRadius: 0 },
            { label: "RX (Mbps)", data: [], borderColor: "rgb(92, 176, 255)", fill: false, tension: 0.25, pointRadius: 0 }
          ]
        },
        options: {
          responsive: true,
          animation: false,
          plugins: {
            legend: { labels: { color: palette.text } },
            tooltip: {
              titleColor: palette.text,
              bodyColor: palette.text,
              backgroundColor: "rgba(14, 25, 43, 0.92)",
              borderColor: "rgba(145, 178, 212, 0.35)",
              borderWidth: 1
            }
          },
          scales: {
            x: {
              ticks: { color: palette.subtleText, maxTicksLimit: 8 },
              grid: { color: palette.grid }
            },
            y: {
              beginAtZero: true,
              ticks: { color: palette.subtleText },
              grid: { color: palette.grid }
            }
          }
        }
      });

      resourceChart = new Chart(resourceCanvas.getContext("2d"), {
        type: "line",
        data: {
          labels: [],
          datasets: [
            { label: "CPU (%)", data: [], borderColor: "rgba(255, 121, 145, 1)", backgroundColor: "rgba(255, 121, 145, 0.18)", fill: true, tension: 0.3, pointRadius: 0 },
            { label: "Memory (%)", data: [], borderColor: "rgba(97, 185, 255, 1)", backgroundColor: "rgba(97, 185, 255, 0.18)", fill: true, tension: 0.3, pointRadius: 0 }
          ]
        },
        options: {
          responsive: true,
          animation: false,
          plugins: {
            legend: { labels: { color: palette.text } },
            tooltip: {
              titleColor: palette.text,
              bodyColor: palette.text,
              backgroundColor: "rgba(14, 25, 43, 0.92)",
              borderColor: "rgba(145, 178, 212, 0.35)",
              borderWidth: 1
            }
          },
          scales: {
            x: {
              ticks: { color: palette.subtleText, maxTicksLimit: 8 },
              grid: { color: palette.grid }
            },
            y: {
              beginAtZero: true,
              max: 100,
              ticks: { color: palette.subtleText },
              grid: { color: palette.grid }
            }
          }
        }
      });

      return true;
    },

    update: function (payload) {
      if (!payload) return;

      updateGauge(cpuGauge, payload.cpu);
      updateGauge(memoryGauge, payload.memory);
      updateGauge(diskGauge, payload.disk);

      if (networkChart) {
        networkChart.data.labels = payload.labels || [];
        networkChart.data.datasets[0].data = payload.tx || [];
        networkChart.data.datasets[1].data = payload.rx || [];
        networkChart.update("none");
      }

      if (resourceChart) {
        resourceChart.data.labels = payload.labels || [];
        resourceChart.data.datasets[0].data = payload.cpuSeries || [];
        resourceChart.data.datasets[1].data = payload.memorySeries || [];
        resourceChart.update("none");
      }
    }
  };
})();
