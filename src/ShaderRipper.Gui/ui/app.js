/* ShaderRipper GUI — front end.
   Two jobs: keep a Material You palette on screen, and drive one export over the
   JSON bridge.  Nothing here knows about DXBC; it sends options and renders the
   log lines the library hands back. */

// ------------------------------------------------------------------ bridge

function send(payload) {
  const text = JSON.stringify(payload);
  if (window.external && window.external.sendMessage) window.external.sendMessage(text);
  else if (window.chrome && window.chrome.webview) window.chrome.webview.postMessage(text);
  else console.warn('no host bridge', payload);
}

function onMessage(cb) {
  const wrap = (raw) => {
    if (typeof raw === 'string') {
      try { cb(JSON.parse(raw)); } catch { /* not ours */ }
    } else if (raw) {
      cb(raw);
    }
  };
  if (window.external && window.external.receiveMessage) window.external.receiveMessage(wrap);
  else if (window.chrome && window.chrome.webview) window.chrome.webview.addEventListener('message', (e) => wrap(e.data));
}

// ------------------------------------------------------------------ palette

/* One palette: the accent is blue, and only the light/dark mode is a choice. */
const HUE = 264;

const OKLCH = CSS.supports && CSS.supports('color', 'oklch(50% 0.1 200)');

function hueOf(delta) {
  const h = (HUE + delta) % 360;
  return h < 0 ? h + 360 : h;
}

/** One tonal step.  `l` is the M3 tone in percent, `c` the chroma. */
function tone(l, c, delta = 0) {
  return OKLCH
    ? `oklch(${l}% ${c} ${hueOf(delta)})`
    : `hsl(${hueOf(delta)} ${Math.round(c * 420)}% ${Math.round(l)}%)`;
}

const ERROR = { l: 52, d: 78 };

function applyTheme() {
  const dark = state.resolved === 'dark';
  const root = document.documentElement;
  const set = (k, v) => root.style.setProperty(k, v);

  set('--primary', tone(dark ? 82 : 48, dark ? .12 : .15));
  set('--on-primary', tone(dark ? 26 : 99, dark ? .08 : .02));
  set('--primary-container', tone(dark ? 40 : 90, dark ? .12 : .07));
  set('--on-primary-container', tone(dark ? 90 : 28, dark ? .05 : .09));
  set('--secondary-container', tone(dark ? 30 : 92, dark ? .04 : .045, 20));
  set('--on-secondary-container', tone(dark ? 90 : 30, dark ? .03 : .05, 20));
  set('--tertiary', tone(dark ? 78 : 52, dark ? .09 : .11, 60));

  set('--surface', tone(dark ? 19 : 98.5, .012));
  set('--surface-container-lowest', tone(dark ? 15 : 99.5, .008));
  set('--surface-container-low', tone(dark ? 21 : 96.5, .010));
  set('--surface-container', tone(dark ? 23.5 : 94.5, .012));
  set('--surface-container-high', tone(dark ? 26 : 92.5, .014));
  set('--surface-container-highest', tone(dark ? 29 : 90.5, .016));
  set('--on-surface', tone(dark ? 93 : 21, .010));
  set('--on-surface-variant', tone(dark ? 78 : 45, .018));
  set('--outline', tone(dark ? 55 : 72, .016));
  set('--outline-variant', tone(dark ? 35 : 86, .012));

  // Status colours keep their own hue: an error written in the seed colour stops
  // reading as an error.
  const e = dark ? ERROR.d : ERROR.l;
  set('--error', `oklch(${e}% ${dark ? .14 : .18} 25)`);
  set('--on-error', `oklch(${dark ? 20 : 99}% .03 25)`);
  set('--error-container', `oklch(${dark ? 32 : 92}% ${dark ? .10 : .06} 25)`);
  set('--on-error-container', `oklch(${dark ? 92 : 28}% .05 25)`);

  root.style.setProperty('color-scheme', dark ? 'dark' : 'light');
  themeBtn.title = `配色模式：${state.theme === 'system' ? '跟随系统' : state.theme === 'light' ? '浅色' : '深色'}`;
}

// -------------------------------------------------------------------- state

const state = {
  dataDir: '',
  outDir: '',
  only: '',
  shaderlab: true,
  programs: true,
  origAsm: false,
  overwrite: false,
  dedup: true,
  builtin: false,
  jobs: 8,
  theme: 'system',
  resolved: 'light',
  running: false,
};

const KEY = 'shaderripper.gui.v2';

function loadState() {
  try {
    const saved = JSON.parse(localStorage.getItem(KEY) || '{}');
    for (const k of Object.keys(state)) {
      if (k in saved && saved[k] !== undefined) state[k] = saved[k];
    }
  } catch { }
}

function saveState() {
  try {
    localStorage.setItem(KEY, JSON.stringify({
      dataDir: state.dataDir, outDir: state.outDir, only: state.only,
      shaderlab: state.shaderlab, programs: state.programs, origAsm: state.origAsm,
      overwrite: state.overwrite, dedup: state.dedup, builtin: state.builtin,
      jobs: state.jobs, theme: state.theme,
    }));
  } catch { }
}

// -------------------------------------------------------------------- nodes

const $ = (id) => document.getElementById(id);
const dataDir = $('dataDir'), outDir = $('outDir'), only = $('only');
const overwrite = $('overwrite'), dedup = $('dedup'), builtin = $('builtin');
const jobs = $('jobs'), jobsOut = $('jobsOut');
const logEl = $('log'), statsEl = $('stats'), stateEl = $('state');
const bar = $('bar'), progress = $('progress'), counts = $('counts'), percent = $('percent');
const runBtn = $('run'), runLabel = $('runLabel'), revealBtn = $('reveal');
const themeBtn = $('themeBtn');

/** True once the current output directory holds something this session wrote. */
let hasOutput = false;

/** True once the export has started writing shaders (drives the state line). */
let exporting = false;

// ------------------------------------------------------------------- wiring

function syncInputs() {
  dataDir.value = state.dataDir;
  outDir.value = state.outDir;
  only.value = state.only;
  overwrite.checked = state.overwrite;
  dedup.checked = state.dedup;
  builtin.checked = state.builtin;
  jobs.value = String(state.jobs);
  jobsOut.textContent = String(state.jobs);
  for (const c of document.querySelectorAll('.chip')) {
    c.setAttribute('aria-pressed', String(!!state[c.dataset.k]));
    c.disabled = state.running;
  }
  revealBtn.disabled = !state.outDir;
}

function cycleTheme() {
  state.theme = state.theme === 'system' ? 'light' : state.theme === 'light' ? 'dark' : 'system';
  resolveTheme();
  saveState();
}

const media = window.matchMedia('(prefers-color-scheme: dark)');

function resolveTheme() {
  state.resolved = state.theme === 'system' ? (media.matches ? 'dark' : 'light') : state.theme;
  applyTheme();
}

// --------------------------------------------------------------------- log

const LOG_MAX = 4000;
let stickToBottom = true;

logEl.addEventListener('scroll', () => {
  stickToBottom = logEl.scrollHeight - logEl.scrollTop - logEl.clientHeight < 40;
});

function clearLog() {
  logEl.innerHTML = '';
}

function appendLog(text, kind) {
  const empty = logEl.querySelector('.log-empty');
  if (empty) empty.remove();

  const div = document.createElement('div');
  div.className = 'line ' + (kind || '');
  div.textContent = text;
  logEl.appendChild(div);

  while (logEl.childElementCount > LOG_MAX) logEl.removeChild(logEl.firstElementChild);
  if (stickToBottom) logEl.scrollTop = logEl.scrollHeight;
}

// ---------------------------------------------------------------- progress

function setProgress(done, total) {
  if (!total) {
    progress.classList.add('indet');
    bar.style.width = '';
    counts.textContent = done ? `已处理 ${done}` : '准备中…';
    percent.textContent = '';
    return;
  }
  progress.classList.remove('indet');
  const pct = Math.min(100, Math.round((done / total) * 100));
  bar.style.width = pct + '%';
  counts.textContent = `${done} / ${total}`;
  percent.textContent = pct + '%';
}

function setState(text) { stateEl.textContent = text; }

/** Idle: no bar, no motion -- an indeterminate bar at rest reads as "working". */
function resetProgress() {
  progress.classList.remove('indet');
  bar.style.width = '0%';
  counts.textContent = '—';
  percent.textContent = '';
}

function stat(label, value, cls) {
  const d = document.createElement('div');
  d.className = 'stat' + (cls ? ' ' + cls : '');
  const b = document.createElement('b');
  b.textContent = String(value);
  const s = document.createElement('span');
  s.textContent = label;
  d.append(b, s);
  return d;
}

function showStats(s) {
  statsEl.innerHTML = '';
  statsEl.append(
    stat('文件', s.files),
    stat('shader', s.shaders),
    stat('已写出', s.emitted, 'good'),
    stat('程序', s.programs),
  );
  if (s.disassembled) statsEl.append(stat('反汇编', s.disassembled));
  if (s.failed) statsEl.append(stat('失败', s.failed, 'bad'));
  if (s.skipped) statsEl.append(stat('跳过', s.skipped));
}

function setRunning(on) {
  state.running = on;
  runBtn.classList.toggle('filled', !on);
  runBtn.classList.toggle('danger', on);
  runLabel.textContent = on ? '停止' : '开始导出';
  runBtn.querySelector('svg').innerHTML = on
    ? '<path d="M7 7h10v10H7z"/>'
    : '<path d="M8 5v14l11-7L8 5Z"/>';
  for (const c of document.querySelectorAll('.chip')) c.disabled = on;
  if (!on) {
    progress.classList.remove('indet');
  }
}

// -------------------------------------------------------------------- start

function collect() {
  return {
    dataDir: state.dataDir.trim(),
    outDir: state.outDir.trim(),
    only: state.only.trim(),
    shaderlab: state.shaderlab,
    programs: state.programs,
    origAsm: state.origAsm,
    overwrite: state.overwrite,
    dedup: state.dedup,
    builtin: state.builtin,
    jobs: Number(state.jobs) || 1,
  };
}

function startRun() {
  if (state.running) { send({ cmd: 'cancel' }); setState('正在停止…'); return; }

  if (!state.dataDir.trim()) {
    setState('先选择游戏数据目录');
    dataDir.focus();
    return;
  }
  if (!state.outDir.trim()) {
    setState('先选择输出目录');
    outDir.focus();
    return;
  }
  if (!state.shaderlab && !state.programs && !state.origAsm) {
    setState('至少选择一种产物');
    return;
  }

  clearLog();
  statsEl.innerHTML = '';
  hasOutput = false;
  exporting = false;
  setProgress(0, 0);
  send({ cmd: 'start', ...collect() });
}

// -------------------------------------------------------------------- events

onMessage((msg) => {
  switch (msg.ev) {
    case 'hello':
      if (!localStorage.getItem(KEY)) state.jobs = Math.max(1, Math.min(16, msg.jobs || 8));
      jobs.value = String(state.jobs);
      jobsOut.textContent = String(state.jobs);
      break;

    case 'autostart':
      if (msg.dataDir) { state.dataDir = msg.dataDir; dataDir.value = msg.dataDir; }
      if (msg.outDir) { state.outDir = msg.outDir; outDir.value = msg.outDir; }
      else if (state.dataDir && !state.outDir) {
        state.outDir = state.dataDir.replace(/[\/]+$/, '') + '_Ripped';
        outDir.value = state.outDir;
      }
      revealBtn.disabled = !state.outDir;
      saveState();
      if (msg.start) startRun();
      break;

    case 'picked':
      if (msg.kind === 'in') {
        state.dataDir = msg.path;
        dataDir.value = msg.path;
        // The default output is the CLI's own convention; filling it here saves a
        // second dialog for the common case.
        if (!state.outDir.trim()) {
          state.outDir = msg.path.replace(/[\\/]+$/, '') + '_Ripped';
          outDir.value = state.outDir;
        }
      } else {
        state.outDir = msg.path;
        outDir.value = msg.path;
      }
      revealBtn.disabled = !state.outDir;
      saveState();
      break;

    case 'started':
      setRunning(true);
      hasOutput = false;
      exporting = false;
      setState('正在读取资源…');
      setProgress(0, 0);
      appendLog('开始导出 → ' + msg.out, 'summary');
      break;

    case 'log':
      appendLog(msg.text, msg.kind);
      if (msg.kind === 'info') {
        if (msg.total) setProgress(msg.done, msg.total);
        // The reading phase has no visible end (dedup and resource-map loading happen
        // there), so the state line only changes on the first shader written.
        if (!exporting) { exporting = true; setState('正在写出 shader…'); }
      }
      if (msg.kind === 'file') setState('读取 ' + msg.text);
      break;

    case 'done': {
      setRunning(false);
      showStats(msg);
      const written = msg.emitted || 0;

      // A run that found nothing is almost always the wrong folder, and reporting
      // it as "完成" is the same false success the command line had.
      if (!msg.cancelled && !msg.shaders) {
        setState('没有找到任何 shader：确认路径指向 *_Data 目录，或只含 AssetBundle 的目录');
        progress.classList.remove('indet');
        bar.style.width = '0%';
        counts.textContent = '未找到 shader';
        percent.textContent = '';
        appendLog('未找到任何 shader —— 这个目录里没有可读的 shader 资源。', 'error');
        break;
      }
      progress.classList.remove('indet');
      bar.style.width = msg.cancelled ? '0%' : '100%';
      counts.textContent = `已写出 ${written} 个 shader`;
      percent.textContent = msg.cancelled ? '已取消' : msg.failed ? '有失败' : '完成';
      setState(msg.cancelled
        ? `已取消，已写出的 ${written} 个文件保留`
        : `完成：${written} 个 shader / ${msg.programs} 个程序`);
      hasOutput = !!msg.out;
      revealBtn.disabled = !msg.out;
      for (const n of msg.notes || []) appendLog(n, 'note');
      saveState();
      break;
    }

    case 'failed':
      setRunning(false);
      setProgress(0, 0);
      setState('失败：' + msg.text);
      appendLog(msg.text, 'error');
      break;
  }
});

// --------------------------------------------------------------------- boot

function bind() {
  dataDir.addEventListener('input', () => { state.dataDir = dataDir.value; saveState(); });
  outDir.addEventListener('input', () => { state.outDir = outDir.value; revealBtn.disabled = !outDir.value; saveState(); });
  only.addEventListener('input', () => { state.only = only.value; saveState(); });
  overwrite.addEventListener('change', () => { state.overwrite = overwrite.checked; saveState(); });
  dedup.addEventListener('change', () => { state.dedup = dedup.checked; saveState(); });
  builtin.addEventListener('change', () => { state.builtin = builtin.checked; saveState(); });
  jobs.addEventListener('input', () => {
    state.jobs = Number(jobs.value);
    jobsOut.textContent = jobs.value;
    saveState();
  });

  for (const c of document.querySelectorAll('.chip')) {
    c.addEventListener('click', () => {
      const k = c.dataset.k;
      state[k] = !state[k];
      c.setAttribute('aria-pressed', String(state[k]));
      saveState();
    });
  }

  $('pickData').addEventListener('click', () => send({ cmd: 'pick', kind: 'in', path: state.dataDir }));
  $('pickOut').addEventListener('click', () => send({ cmd: 'pick', kind: 'out', path: state.outDir }));
  runBtn.addEventListener('click', startRun);
  revealBtn.addEventListener('click', () => send({ cmd: 'reveal', path: state.outDir }));
  $('saveLog').addEventListener('click', () => send({ cmd: 'saveLog' }));
  $('clearLog').addEventListener('click', clearLog);
  themeBtn.addEventListener('click', cycleTheme);

  // Ctrl+Enter runs, Escape stops: the two things a keyboard user reaches for.
  window.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); startRun(); }
    else if (e.key === 'Escape' && state.running) { e.preventDefault(); send({ cmd: 'cancel' }); setState('正在停止…'); }
  });

  // M3 state layer: a ripple from the pointer, removed when it finishes.
  for (const el of document.querySelectorAll('.btn, .chip')) {
    el.addEventListener('pointerdown', (e) => {
      const r = el.getBoundingClientRect();
      const d = Math.max(r.width, r.height);
      const s = document.createElement('span');
      s.className = 'ripple';
      s.style.width = s.style.height = d + 'px';
      s.style.left = (e.clientX - r.left - d / 2) + 'px';
      s.style.top = (e.clientY - r.top - d / 2) + 'px';
      s.addEventListener('animationend', () => s.remove());
      el.appendChild(s);
    });
  }
}

loadState();
bind();
resolveTheme();
syncInputs();
setRunning(false);
resetProgress();
media.addEventListener('change', () => { if (state.theme === 'system') resolveTheme(); });
send({ cmd: 'hello' });
