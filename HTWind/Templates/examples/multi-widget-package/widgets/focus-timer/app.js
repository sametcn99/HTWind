const timerValue = document.getElementById('timerValue');
const progressValue = document.getElementById('progressValue');
const sessionLabel = document.getElementById('sessionLabel');

const sessions = [
  { remaining: '17:00', progress: '68%', label: 'Current sprint: Deep work block' },
  { remaining: '12:30', progress: '50%', label: 'Current sprint: Documentation review' },
  { remaining: '08:45', progress: '35%', label: 'Current sprint: Final polish' }
];

let sessionIndex = 0;

function renderSession() {
  const session = sessions[sessionIndex];

  if (timerValue) {
    timerValue.textContent = session.remaining;
  }

  if (progressValue) {
    progressValue.style.width = session.progress;
  }

  if (sessionLabel) {
    sessionLabel.textContent = session.label;
  }

  sessionIndex = (sessionIndex + 1) % sessions.length;
}

renderSession();
window.setInterval(renderSession, 5000);
