/**
 * main.js — WasmShare (v2)
 *
 * Sender  → selects file → shows QR → receiver scans → WebRTC P2P transfer
 * Receiver → opens ?room=<id>&role=receiver → receives chunks → download
 *
 * Each chunk is XOR'd via WebAssembly before send and after receive.
 */

// ─── Constants ────────────────────────────────────────────────────────────────
const CHUNK_SIZE = 64 * 1024; // 64 KB chunks
const XOR_KEY    = 0xA7;      // fixed key (hidden from UI for simplicity)
const WS_URL     = `ws://${location.host}`;
const WASM_PATH  = "wasm/compress.wasm";

// ─── Wasm ─────────────────────────────────────────────────────────────────────
let wasmExports = null;
let wasmMemory  = null;

async function loadWasm() {
  let instance;
  try {
    const result = await WebAssembly.instantiateStreaming(fetch(WASM_PATH));
    instance = result.instance;
  } catch {
    const bytes = await fetch(WASM_PATH).then((r) => r.arrayBuffer());
    instance = (await WebAssembly.instantiate(bytes)).instance;
  }
  wasmExports = instance.exports;
  wasmMemory  = wasmExports.mem;
}

function wasmXor(data, key) {
  if (!wasmExports) return data;
  const mem = new Uint8Array(wasmMemory.buffer, 0, data.byteLength);
  mem.set(data);
  wasmExports.xor_buffer(0, data.byteLength, key & 0xff);
  return mem.slice();
}

// ─── UI refs ─────────────────────────────────────────────────────────────────
const ui = {
  logoBtn:         document.getElementById("logoBtn"),
  qrModal:         document.getElementById("qrModal"),
  localQrContainer: document.getElementById("localQrContainer"),
  localQrUrl:      document.getElementById("localQrUrl"),
  closeQrModal:    document.getElementById("closeQrModal"),
  senderUI:        document.getElementById("senderUI"),
  dropZone:        document.getElementById("dropZone"),
  fileInput:       document.getElementById("fileInput"),
  filePill:        document.getElementById("filePill"),
  pillIcon:        document.getElementById("pillIcon"),
  pillName:        document.getElementById("pillName"),
  pillSize:        document.getElementById("pillSize"),
  btnRemoveFile:   document.getElementById("btnRemoveFile"),
  btnShare:        document.getElementById("btnShare"),
  qrSection:       document.getElementById("qrSection"),
  qrContainer:     document.getElementById("qrContainer"),
  qrUrl:           document.getElementById("qrUrl"),
  btnCopyLink:     document.getElementById("btnCopyLink"),
  btnNewFile:      document.getElementById("btnNewFile"),
  // steps
  step1: document.getElementById("step1"),
  step2: document.getElementById("step2"),
  step3: document.getElementById("step3"),
  step4: document.getElementById("step4"),
  line1: document.getElementById("line1"),
  line2: document.getElementById("line2"),
  line3: document.getElementById("line3"),
  // status
  statusMsg:      document.getElementById("statusMsg"),
  progressWrap:   document.getElementById("progressWrap"),
  progressBar:    document.getElementById("progressBar"),
  progressPct:    document.getElementById("progressPct"),
  progressLabel:  document.getElementById("progressLabel"),
  progressSpeed:  document.getElementById("progressSpeed"),
  // receiver
  receiverWaiting: document.getElementById("receiverWaiting"),
  waitingTitle:    document.getElementById("waitingTitle"),
  waitingSub:      document.getElementById("waitingSub"),
  // download
  downloadSection: document.getElementById("downloadSection"),
  downloadLink:    document.getElementById("downloadLink"),
  dlIcon:          document.getElementById("dlIcon"),
  dlFilename:      document.getElementById("dlFilename"),
  dlSize:          document.getElementById("dlSize"),
  // toast
  toast:           document.getElementById("toast"),
};

// ─── Helpers ─────────────────────────────────────────────────────────────────
function fmtSize(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 ** 2) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 ** 3) return `${(bytes / 1024 ** 2).toFixed(2)} MB`;
  return `${(bytes / 1024 ** 3).toFixed(2)} GB`;
}

function fileIcon(name) {
  const ext = name.split(".").pop().toLowerCase();
  const map = { pdf: "📕", zip: "🗜️", rar: "🗜️", mp4: "🎬", mkv: "🎬",
    mp3: "🎵", wav: "🎵", jpg: "🖼️", jpeg: "🖼️", png: "🖼️", gif: "🖼️",
    doc: "📝", docx: "📝", xls: "📊", xlsx: "📊", ppt: "📊", pptx: "📊",
    js: "💻", ts: "💻", py: "💻", html: "💻", css: "💻", json: "💻" };
  return map[ext] || "📄";
}

function setStatus(msg, pulsing = false) {
  ui.statusMsg.innerHTML = pulsing
    ? `<div class="dot"></div><span>${msg}</span>`
    : `<span>${msg}</span>`;
}

function showProgress(label) {
  ui.progressWrap.style.display = "flex";
  ui.progressLabel.textContent = label;
  ui.progressBar.style.width = "0%";
  ui.progressPct.textContent = "0%";
  ui.progressSpeed.textContent = "";
}

function updateProgress(pct, speed = "") {
  ui.progressBar.style.width = `${pct}%`;
  ui.progressPct.textContent = `${pct}%`;
  if (speed) ui.progressSpeed.textContent = speed;
}

function setStep(n) {
  // n = 1..4
  [ui.step1, ui.step2, ui.step3, ui.step4].forEach((s, i) => {
    s.classList.remove("active", "done");
    if (i + 1 < n) s.classList.add("done");
    if (i + 1 === n) s.classList.add("active");
  });
  [ui.line1, ui.line2, ui.line3].forEach((l, i) => {
    l.classList.toggle("done", i + 1 < n);
  });
}

let toastTimer = null;
function showToast(msg) {
  ui.toast.textContent = msg;
  ui.toast.classList.add("show");
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => ui.toast.classList.remove("show"), 2500);
}

// ─── URL params ───────────────────────────────────────────────────────────────
const params   = new URLSearchParams(location.search);
const urlRoom  = params.get("room");
const isSender = params.get("role") !== "receiver";

// ─── Signaling ────────────────────────────────────────────────────────────────
function createSignaling(room) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(WS_URL);
    ws.onopen  = () => { ws.send(JSON.stringify({ type: "join", room })); resolve(ws); };
    ws.onerror = (e) => reject(e);
  });
}

const RTC_CONFIG = {
  iceServers: [{ urls: "stun:stun.l.google.com:19302" }],
};

// ─── File selection ───────────────────────────────────────────────────────────
let selectedFile = null;

function applyFile(file) {
  selectedFile = file;
  ui.pillIcon.textContent = fileIcon(file.name);
  ui.pillName.textContent = file.name;
  ui.pillSize.textContent = fmtSize(file.size);
  ui.filePill.style.display = "flex";
  ui.dropZone.style.display = "none";
  ui.btnShare.disabled = false;
  setStep(1);
}

function clearFile() {
  selectedFile = null;
  ui.fileInput.value = "";
  ui.filePill.style.display = "none";
  ui.dropZone.style.display = "flex";
  ui.btnShare.disabled = true;
  ui.qrSection.style.display = "none";
  setStep(1);
  setStatus("");
}

// ─── Sender ───────────────────────────────────────────────────────────────────
let currentReceiverUrl = "";

async function startSender(file) {
  const room = crypto.randomUUID().slice(0, 8);
  currentReceiverUrl = `${location.origin}?room=${room}&role=receiver`;

  // Step 2 — show QR
  setStep(2);
  ui.qrSection.style.display = "flex";
  ui.qrContainer.innerHTML = "";
  new QRCode(ui.qrContainer, {
    text: currentReceiverUrl,
    width: 200,
    height: 200,
    colorDark: "#000000",
    colorLight: "#ffffff",
  });
  ui.qrUrl.textContent = currentReceiverUrl;
  setStatus("Escanea el QR con el otro dispositivo", true);

  const ws = await createSignaling(room);
  const pc = new RTCPeerConnection(RTC_CONFIG);
  const dc = pc.createDataChannel("file", { ordered: true });
  dc.binaryType = "arraybuffer";

  let startTime = 0;

  dc.onopen = async () => {
    setStep(3);
    showProgress("Enviando…");
    setStatus("Transferencia en curso…", true);

    // Send metadata
    dc.send(JSON.stringify({ name: file.name, size: file.size, type: file.type, xorKey: XOR_KEY }));

    let offset = 0;
    startTime = Date.now();

    while (offset < file.size) {
      // Respect backpressure
      if (dc.bufferedAmount > 4 * CHUNK_SIZE) {
        await new Promise((r) => setTimeout(r, 10));
        continue;
      }
      const slice  = file.slice(offset, offset + CHUNK_SIZE);
      const buffer = await slice.arrayBuffer();
      const encoded = wasmXor(new Uint8Array(buffer), XOR_KEY);
      dc.send(encoded);
      offset += encoded.byteLength;

      const pct     = Math.round((offset / file.size) * 100);
      const elapsed = (Date.now() - startTime) / 1000 || 0.001;
      const speed   = fmtSize(offset / elapsed) + "/s";
      updateProgress(pct, speed);
      await new Promise((r) => setTimeout(r, 0));
    }

    setStep(4);
    setStatus("✅ Archivo enviado con éxito");
    showToast("¡Enviado!");
  };

  dc.onerror = (e) => setStatus(`❌ Error en canal: ${e}`);

  pc.onicecandidate = ({ candidate }) => {
    if (candidate) ws.send(JSON.stringify({ type: "candidate", candidate }));
  };

  ws.onmessage = async ({ data }) => {
    const msg = JSON.parse(data);
    if (msg.type === "peer-joined") {
      const offer = await pc.createOffer();
      await pc.setLocalDescription(offer);
      ws.send(JSON.stringify({ type: "offer", sdp: pc.localDescription }));
    } else if (msg.type === "answer") {
      await pc.setRemoteDescription(new RTCSessionDescription(msg.sdp));
    } else if (msg.type === "candidate") {
      await pc.addIceCandidate(new RTCIceCandidate(msg.candidate));
    }
  };
}

// ─── Receiver ────────────────────────────────────────────────────────────────
async function startReceiver(room) {
  ui.senderUI.style.display = "none";
  ui.receiverWaiting.style.display = "flex";
  setStatus("Esperando conexión…", true);

  const ws = await createSignaling(room);
  const pc = new RTCPeerConnection(RTC_CONFIG);

  let meta = null;
  let receivedChunks = [];
  let receivedBytes  = 0;
  let startTime      = 0;

  pc.ondatachannel = ({ channel }) => {
    channel.binaryType = "arraybuffer";

    ui.waitingTitle.textContent = "Conectado — recibiendo…";
    ui.waitingSub.textContent   = "No cierres esta pestaña.";
    showProgress("Recibiendo…");

    channel.onmessage = ({ data }) => {
      if (typeof data === "string") {
        meta = JSON.parse(data);
        startTime = Date.now();
        setStatus(`Recibiendo "${meta.name}"…`, true);
        return;
      }
      const decoded = wasmXor(new Uint8Array(data), meta.xorKey);
      receivedChunks.push(decoded);
      receivedBytes += decoded.byteLength;

      const pct     = Math.round((receivedBytes / meta.size) * 100);
      const elapsed = (Date.now() - startTime) / 1000 || 0.001;
      const speed   = fmtSize(receivedBytes / elapsed) + "/s";
      updateProgress(pct, speed);

      if (receivedBytes >= meta.size) {
        const blob = new Blob(receivedChunks, { type: meta.type || "application/octet-stream" });
        const url  = URL.createObjectURL(blob);

        ui.receiverWaiting.style.display = "none";
        ui.downloadSection.style.display = "flex";
        ui.dlIcon.textContent            = fileIcon(meta.name);
        ui.dlFilename.textContent        = meta.name;
        ui.dlSize.textContent            = fmtSize(meta.size);
        ui.downloadLink.href             = url;
        ui.downloadLink.download         = meta.name;
        setStatus("✅ ¡Archivo recibido!");
        showToast("¡Descarga lista!");
      }
    };

    channel.onerror = (e) => setStatus(`❌ Error: ${e}`);
  };

  pc.onicecandidate = ({ candidate }) => {
    if (candidate) ws.send(JSON.stringify({ type: "candidate", candidate }));
  };

  ws.onmessage = async ({ data }) => {
    const msg = JSON.parse(data);
    if (msg.type === "offer") {
      await pc.setRemoteDescription(new RTCSessionDescription(msg.sdp));
      const answer = await pc.createAnswer();
      await pc.setLocalDescription(answer);
      ws.send(JSON.stringify({ type: "answer", sdp: pc.localDescription }));
    } else if (msg.type === "candidate") {
      await pc.addIceCandidate(new RTCIceCandidate(msg.candidate));
    }
  };
}

// ─── Init ─────────────────────────────────────────────────────────────────────
async function init() {
  await loadWasm();

  // ── Localhost QR (logo button) ──
  function showLocalhostQR() {
    const localhostUrl = location.origin;
    ui.localQrContainer.innerHTML = "";
    new QRCode(ui.localQrContainer, {
      text: localhostUrl,
      width: 200,
      height: 200,
      colorDark: "#000000",
      colorLight: "#ffffff",
    });
    ui.localQrUrl.textContent = localhostUrl;
    ui.qrModal.style.display = "flex";
  }

  ui.logoBtn.addEventListener("click", showLocalhostQR);
  ui.closeQrModal.addEventListener("click", () => {
    ui.qrModal.style.display = "none";
  });
  ui.qrModal.addEventListener("click", (e) => {
    if (e.target === ui.qrModal) ui.qrModal.style.display = "none";
  });

  if (!isSender && urlRoom) {
    startReceiver(urlRoom);
    return;
  }

  // ── Sender wiring ──
  setStep(1);

  // File input
  ui.fileInput.addEventListener("change", () => {
    if (ui.fileInput.files[0]) applyFile(ui.fileInput.files[0]);
  });

  // Drag & drop
  ui.dropZone.addEventListener("dragover", (e) => {
    e.preventDefault();
    ui.dropZone.classList.add("drag-over");
  });
  ui.dropZone.addEventListener("dragleave", () => ui.dropZone.classList.remove("drag-over"));
  ui.dropZone.addEventListener("drop", (e) => {
    e.preventDefault();
    ui.dropZone.classList.remove("drag-over");
    const file = e.dataTransfer.files[0];
    if (file) applyFile(file);
  });

  // Remove file
  ui.btnRemoveFile.addEventListener("click", clearFile);

  // Share button
  ui.btnShare.addEventListener("click", () => {
    if (!selectedFile) return;
    ui.btnShare.disabled = true;
    startSender(selectedFile);
  });

  // Copy link
  ui.btnCopyLink.addEventListener("click", () => {
    if (!currentReceiverUrl) return;
    navigator.clipboard.writeText(currentReceiverUrl).then(() => showToast("¡Enlace copiado!"));
  });

  // New file
  ui.btnNewFile.addEventListener("click", () => {
    clearFile();
    ui.progressWrap.style.display = "none";
    setStatus("");
  });
}

init().catch((err) => setStatus(`❌ Error de inicio: ${err.message}`));
