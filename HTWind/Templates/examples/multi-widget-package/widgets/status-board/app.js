const statusSnapshots = [
  {
    services: '12 OK',
    warnings: '2',
    deployments: '5 queued'
  },
  {
    services: '11 OK',
    warnings: '3',
    deployments: '2 running'
  },
  {
    services: '12 OK',
    warnings: '1',
    deployments: '7 queued'
  }
];

const metricElements = {
  services: document.querySelector('[data-metric="services"]'),
  warnings: document.querySelector('[data-metric="warnings"]'),
  deployments: document.querySelector('[data-metric="deployments"]')
};

let snapshotIndex = 0;

function renderSnapshot() {
  const snapshot = statusSnapshots[snapshotIndex];
  Object.entries(metricElements).forEach(([key, element]) => {
    if (element) {
      element.textContent = snapshot[key];
    }
  });

  snapshotIndex = (snapshotIndex + 1) % statusSnapshots.length;
}

renderSnapshot();
window.setInterval(renderSnapshot, 4000);
