// Queue simulator: one producer, one Kafka-style topic (append-only log), one worker.
// Markup:
//   <div class="sim" data-queue-sim
//        data-topic="mapping.file-import"
//        data-produce-label="วางไฟล์ CSV ลง input/"
//        data-job-prefix="file job"
//        data-work-ms="2500"
//        data-pos-label="ลำดับ"></div>   (optional; default "offset")
// Models what mapping-demo does: the producer returns immediately, messages stay
// in the log after being read, and the worker only moves its position forward
// (commits) after it finishes a message. Stopping mid-message means re-reading it.
(function () {
  function el(tag, cls, text) {
    const node = document.createElement(tag);
    if (cls) node.className = cls;
    if (text !== undefined) node.textContent = text;
    return node;
  }

  function setup(root) {
    const topic = root.dataset.topic || "topic";
    const produceLabel = root.dataset.produceLabel || "ส่ง message";
    const jobPrefix = root.dataset.jobPrefix || "job";
    const workMs = Number(root.dataset.workMs || 2500);
    const pos = root.dataset.posLabel || "offset";

    const state = {
      log: [],          // [{ offset, jobId }]
      committed: 0,     // next offset the worker will read
      busyOffset: null, // offset being processed, or null
      workerOn: true,
      nextJobId: 1,
      done: 0,
      timer: null,
    };

    root.innerHTML = "";
    const head = el("div", "sim-head");
    head.append(el("span", null, "จำลอง topic "), el("span", "sim-topic", topic));
    const controls = el("div", "sim-controls");
    const produceBtn = el("button", "primary", produceLabel);
    const workerBtn = el("button", "sim-worker on");
    const resetBtn = el("button", null, "เริ่มใหม่");
    controls.append(produceBtn, workerBtn, resetBtn);
    const logView = el("div", "sim-log");
    const stats = el("div", "sim-stats");
    const events = el("ul", "sim-events");
    root.append(head, controls, logView, stats, events);

    function say(text, kind) {
      const li = el("li", kind, text);
      events.prepend(li);
      while (events.children.length > 6) events.lastChild.remove();
    }

    function render() {
      workerBtn.textContent = state.workerOn ? "Worker: กำลังทำงาน (กดเพื่อปิด)" : "Worker: ปิดอยู่ (กดเพื่อเปิด)";
      workerBtn.className = "sim-worker " + (state.workerOn ? "on" : "off");
      logView.innerHTML = "";
      state.log.forEach((m) => {
        const cell = el("div", "sim-cell");
        if (m.offset < state.committed) cell.classList.add("done");
        if (m.offset === state.busyOffset) cell.classList.add("busy");
        if (m.offset === state.committed) cell.classList.add("next");
        cell.append(el("span", "off", pos + " " + m.offset), el("span", "val", "#" + m.jobId));
        logView.append(cell);
      });
      logView.scrollLeft = logView.scrollWidth;
      const lag = state.log.length - state.committed;
      stats.innerHTML = "";
      [["message ใน topic", state.log.length], ["ค้างรอ worker", lag], ["ทำเสร็จแล้ว", state.done]].forEach(([k, v]) => {
        const s = el("span", null, k + ": ");
        s.append(el("b", null, String(v)));
        stats.append(s);
      });
    }

    function pump() {
      if (!state.workerOn || state.busyOffset !== null) return;
      const msg = state.log[state.committed];
      if (!msg) return;
      state.busyOffset = msg.offset;
      say(`Worker: หยิบ ${pos} ${msg.offset} → เริ่มทำ ${jobPrefix} #${msg.jobId}`, "worker");
      render();
      state.timer = setTimeout(() => {
        state.timer = null;
        state.busyOffset = null;
        state.committed = msg.offset + 1;
        state.done++;
        say(`Worker: ${jobPrefix} #${msg.jobId} เสร็จ → แจ้ง queue ว่า ${pos} ${msg.offset} เสร็จแล้ว`, "worker");
        render();
        pump();
      }, workMs);
    }

    produceBtn.addEventListener("click", () => {
      const jobId = state.nextJobId++;
      const offset = state.log.length;
      state.log.push({ offset, jobId });
      say(`API: สร้าง ${jobPrefix} #${jobId} และส่ง message เข้า topic (${pos} ${offset}) จากนั้นทำงานต่อได้เลย ไม่ต้องรอ worker`, "api");
      render();
      pump();
    });

    workerBtn.addEventListener("click", () => {
      state.workerOn = !state.workerOn;
      if (!state.workerOn && state.timer) {
        clearTimeout(state.timer);
        state.timer = null;
        say(`Worker หยุดก่อนแจ้งว่า ${pos} ${state.busyOffset} เสร็จ → พอกลับมาจะทำงานนี้ซ้ำ`, "warn");
        state.busyOffset = null;
      } else if (!state.workerOn) {
        say("Worker หยุดแล้ว message ใหม่จะรออยู่ใน topic", "warn");
      } else {
        say(`Worker กลับมาแล้ว ทำต่อจาก${pos}ที่ ${state.committed}`, "worker");
      }
      render();
      pump();
    });

    resetBtn.addEventListener("click", () => {
      if (state.timer) clearTimeout(state.timer);
      Object.assign(state, { log: [], committed: 0, busyOffset: null, workerOn: true, nextJobId: 1, done: 0, timer: null });
      events.innerHTML = "";
      render();
    });

    render();
  }

  document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("[data-queue-sim]").forEach(setup);
  });
})();
