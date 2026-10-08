// Job timeline player: steps through snapshots of a status API response.
// Markup:
//   <div class="job-timeline" data-job-timeline data-endpoint="GET /file-jobs/1"
//        data-states='{"importStatus": [["Queued"], ["Imported", "final"], ["ImportFailed", "bad"]]}'>
//   (data-states is optional: draws a state path per field and highlights the current value)
//     <script type="application/json">
//       [ { "t": "0 วินาที", "caption": "…", "state": { "importStatus": "Queued", … } }, … ]
//     </script>
//   </div>
// Fields that changed since the previous frame are highlighted, so the learner
// sees which part of the status moves at each step.
(function () {
  function el(tag, cls, text) {
    const node = document.createElement(tag);
    if (cls) node.className = cls;
    if (text !== undefined) node.textContent = text;
    return node;
  }

  function fmt(value) {
    return value === null ? "null" : typeof value === "string" ? `"${value}"` : String(value);
  }

  function setup(root) {
    const data = root.querySelector('script[type="application/json"]');
    const frames = JSON.parse(data.textContent);
    const endpoint = root.dataset.endpoint || "GET /status";
    let index = 0;
    let timer = null;

    root.innerHTML = "";
    const head = el("div", "jt-head");
    const time = el("span", "jt-time");
    head.append(el("span", "jt-endpoint", endpoint), time);
    const caption = el("p", "jt-caption");
    const body = el("pre", "jt-body");
    const controls = el("div", "jt-controls");
    const prev = el("button", null, "◀ ก่อนหน้า");
    const next = el("button", "primary", "ถัดไป ▶");
    const play = el("button", null, "เล่นอัตโนมัติ");
    const range = el("input", "jt-range");
    range.type = "range";
    range.min = "0";
    range.max = String(frames.length - 1);
    range.setAttribute("aria-label", "เลือกช่วงเวลา");
    controls.append(prev, next, play, range);
    const stateRows = [];
    if (root.dataset.states) {
      const spec = JSON.parse(root.dataset.states);
      const wrap = el("div", "jt-states");
      Object.entries(spec).forEach(([field, states]) => {
        const row = el("div", "states");
        row.append(el("span", "jt-field", field));
        const pills = states.map(([name, kind], i) => {
          if (i > 0) row.append(el("span", "arrow", "·"));
          const pill = el("span", "state" + (kind ? " " + kind : ""), name);
          row.append(pill);
          return pill;
        });
        stateRows.push({ field, pills });
        wrap.append(row);
      });
      root.append(head, wrap, caption, body, controls);
    } else {
      root.append(head, caption, body, controls);
    }

    function render() {
      const frame = frames[index];
      const before = index > 0 ? frames[index - 1].state : {};
      time.textContent = `${frame.t} · ${index + 1}/${frames.length}`;
      caption.textContent = frame.caption;
      body.innerHTML = "";
      body.append("{\n");
      const keys = Object.keys(frame.state);
      keys.forEach((key, i) => {
        const line = el("span", "jt-line");
        if (index > 0 && fmt(before[key]) !== fmt(frame.state[key])) line.classList.add("changed");
        line.textContent = `  "${key}": ${fmt(frame.state[key])}${i < keys.length - 1 ? "," : ""}`;
        body.append(line, "\n");
      });
      body.append("}");
      stateRows.forEach(({ field, pills }) => {
        pills.forEach((pill) => pill.classList.toggle("current", pill.textContent === String(frame.state[field])));
      });
      range.value = String(index);
      prev.disabled = index === 0;
      next.disabled = index === frames.length - 1;
    }

    function go(i) {
      index = Math.max(0, Math.min(frames.length - 1, i));
      render();
    }

    function stop() {
      if (timer) clearInterval(timer);
      timer = null;
      play.textContent = "เล่นอัตโนมัติ";
    }

    prev.addEventListener("click", () => { stop(); go(index - 1); });
    next.addEventListener("click", () => { stop(); go(index + 1); });
    range.addEventListener("input", () => { stop(); go(Number(range.value)); });
    play.addEventListener("click", () => {
      if (timer) { stop(); return; }
      if (index === frames.length - 1) go(0);
      play.textContent = "หยุด";
      timer = setInterval(() => {
        if (index >= frames.length - 1) { stop(); return; }
        go(index + 1);
      }, 2200);
    });

    render();
  }

  document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("[data-job-timeline]").forEach(setup);
  });
})();
