// Step diagram: a learner-paced animated flow (no autoplay).
// Markup:
//   <div class="step-diagram" data-step-diagram>
//     <script type="application/json">{ …config… }</script>
//   </div>
// Config:
//   width, height            design size in px (the stage scales down to fit)
//   nodes: [{ id, label, sub, x, y, w, h, kind }]
//       kind: producer | queue | consumer | store | plain
//   edges: [{ from, to, label?, dashed? }]
//   steps: [{
//       caption,                                   one or two short sentences
//       nodes: { id: { state, sub, label } },      cumulative; state: active | down | dim | ok | bad | null
//       tokens: [{ id, label, at, slot?, x?, y?, kind? }]   full list for this step
//   }]
//       token kind: msg | row | ok | bad | ghost ; at: node id, or give x/y
// Tokens keep their id between steps, so a token that changes `at` glides there.
(function () {
  const TOKEN_W = 40, TOKEN_H = 20, GAP = 4, PAD = 7;
  const reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  function el(tag, cls, text) {
    const node = document.createElement(tag);
    if (cls) node.className = cls;
    if (text !== undefined) node.textContent = text;
    return node;
  }

  function border(node, tx, ty) {
    // Point where the line from node centre towards (tx, ty) leaves the node box.
    const cx = node.x + node.w / 2, cy = node.y + node.h / 2;
    const dx = tx - cx, dy = ty - cy;
    if (dx === 0 && dy === 0) return [cx, cy];
    const sx = dx !== 0 ? (node.w / 2) / Math.abs(dx) : Infinity;
    const sy = dy !== 0 ? (node.h / 2) / Math.abs(dy) : Infinity;
    const s = Math.min(sx, sy);
    return [cx + dx * s, cy + dy * s];
  }

  function setup(root) {
    const cfg = JSON.parse(root.querySelector('script[type="application/json"]').textContent);
    const W = cfg.width || 640, H = cfg.height || 240;
    const byId = Object.fromEntries(cfg.nodes.map((n) => [n.id, n]));
    let index = 0;

    root.innerHTML = "";
    const frame = el("div", "sd-frame");
    const stage = el("div", "sd-stage");
    stage.style.width = W + "px";
    stage.style.height = H + "px";
    frame.append(stage);

    // edges
    const svgNS = "http://www.w3.org/2000/svg";
    const svg = document.createElementNS(svgNS, "svg");
    svg.setAttribute("class", "sd-edges");
    svg.setAttribute("width", W);
    svg.setAttribute("height", H);
    svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
    const defs = document.createElementNS(svgNS, "defs");
    const marker = document.createElementNS(svgNS, "marker");
    const mid = "sd-arrow-" + Math.random().toString(36).slice(2);
    marker.setAttribute("id", mid);
    marker.setAttribute("viewBox", "0 0 10 10");
    marker.setAttribute("refX", "9");
    marker.setAttribute("refY", "5");
    marker.setAttribute("markerWidth", "7");
    marker.setAttribute("markerHeight", "7");
    marker.setAttribute("orient", "auto-start-reverse");
    const tip = document.createElementNS(svgNS, "path");
    tip.setAttribute("d", "M0,0 L10,5 L0,10 z");
    tip.setAttribute("class", "sd-arrowhead");
    marker.append(tip);
    defs.append(marker);
    svg.append(defs);
    (cfg.edges || []).forEach((e) => {
      const a = byId[e.from], b = byId[e.to];
      const [x1, y1] = border(a, b.x + b.w / 2, b.y + b.h / 2);
      const [x2, y2] = border(b, a.x + a.w / 2, a.y + a.h / 2);
      const line = document.createElementNS(svgNS, "line");
      line.setAttribute("x1", x1); line.setAttribute("y1", y1);
      line.setAttribute("x2", x2); line.setAttribute("y2", y2);
      line.setAttribute("class", "sd-edge" + (e.dashed ? " dashed" : ""));
      line.setAttribute("marker-end", `url(#${mid})`);
      svg.append(line);
      if (e.label) {
        const t = document.createElementNS(svgNS, "text");
        t.setAttribute("x", (x1 + x2) / 2);
        t.setAttribute("y", (y1 + y2) / 2 - 5);
        t.setAttribute("class", "sd-edge-label");
        t.textContent = e.label;
        svg.append(t);
      }
    });
    stage.append(svg);

    // nodes
    const nodeEls = {};
    cfg.nodes.forEach((n) => {
      const box = el("div", "sd-node " + (n.kind || "plain"));
      Object.assign(box.style, { left: n.x + "px", top: n.y + "px", width: n.w + "px", height: n.h + "px" });
      const label = el("b", null, n.label);
      const sub = el("small", null, n.sub || "");
      box.append(label, sub);
      stage.append(box);
      nodeEls[n.id] = { box, label, sub };
    });

    // tokens live here, keyed by id
    const tokenEls = {};

    const caption = el("p", "sd-caption");
    caption.setAttribute("aria-live", "polite");
    const controls = el("div", "sd-controls");
    const prev = el("button", null, "◀ ก่อนหน้า");
    const next = el("button", "primary", "ถัดไป ▶");
    const reset = el("button", null, "เริ่มใหม่");
    const count = el("span", "sd-count");
    controls.append(prev, next, reset, count);
    root.append(frame, caption, controls);
    root.tabIndex = 0;

    function fit() {
      const s = Math.min(1, frame.clientWidth / W);
      stage.style.transform = `scale(${s})`;
      frame.style.height = H * s + "px";
    }
    if (window.ResizeObserver) new ResizeObserver(fit).observe(frame);
    window.addEventListener("resize", fit);

    function nodeStateAt(i) {
      const acc = {};
      for (let k = 0; k <= i; k++) {
        const ov = cfg.steps[k].nodes || {};
        Object.entries(ov).forEach(([id, v]) => { acc[id] = Object.assign({}, acc[id], v); });
      }
      return acc;
    }

    function tokenPos(t, slotCounters) {
      if (t.x !== undefined) return [t.x, t.y];
      const n = byId[t.at];
      const slot = t.slot !== undefined ? t.slot : (slotCounters[t.at] = (slotCounters[t.at] || 0) + 1) - 1;
      const perRow = Math.max(1, Math.floor((n.w - PAD * 2 + GAP) / (TOKEN_W + GAP)));
      const row = Math.floor(slot / perRow), col = slot % perRow;
      return [n.x + PAD + col * (TOKEN_W + GAP), n.y + n.h - PAD - TOKEN_H - row * (TOKEN_H + GAP)];
    }

    function render() {
      const step = cfg.steps[index];
      // nodes
      const states = nodeStateAt(index);
      cfg.nodes.forEach((n) => {
        const st = states[n.id] || {};
        const ne = nodeEls[n.id];
        ne.box.className = "sd-node " + (n.kind || "plain") + (st.state ? " " + st.state : "");
        ne.label.textContent = st.label !== undefined && st.label !== null ? st.label : n.label;
        ne.sub.textContent = st.sub !== undefined && st.sub !== null ? st.sub : (n.sub || "");
      });
      // tokens
      const seen = new Set();
      const slots = {};
      (step.tokens || []).forEach((t) => {
        seen.add(t.id);
        let te = tokenEls[t.id];
        const [x, y] = tokenPos(t, slots);
        if (!te) {
          te = el("span", "sd-token");
          te.style.left = x + "px";
          te.style.top = y + "px";
          te.classList.add("entering");
          stage.append(te);
          tokenEls[t.id] = te;
          requestAnimationFrame(() => requestAnimationFrame(() => te.classList.remove("entering")));
        }
        te.className = "sd-token " + (t.kind || "msg") + (te.classList.contains("entering") ? " entering" : "");
        te.textContent = t.label;
        te.style.left = x + "px";
        te.style.top = y + "px";
      });
      Object.keys(tokenEls).forEach((id) => {
        if (seen.has(id)) return;
        const te = tokenEls[id];
        delete tokenEls[id];
        te.classList.add("leaving");
        setTimeout(() => te.remove(), reduceMotion ? 0 : 450);
      });
      caption.textContent = step.caption;
      count.textContent = `ขั้นที่ ${index + 1}/${cfg.steps.length}`;
      prev.disabled = index === 0;
      next.disabled = index === cfg.steps.length - 1;
    }

    function go(i) { index = Math.max(0, Math.min(cfg.steps.length - 1, i)); render(); }
    prev.addEventListener("click", () => go(index - 1));
    next.addEventListener("click", () => go(index + 1));
    reset.addEventListener("click", () => {
      Object.values(tokenEls).forEach((te) => te.remove());
      Object.keys(tokenEls).forEach((k) => delete tokenEls[k]);
      go(0);
    });
    root.addEventListener("keydown", (e) => {
      if (e.key === "ArrowRight") { go(index + 1); e.preventDefault(); }
      if (e.key === "ArrowLeft") { go(index - 1); e.preventDefault(); }
    });

    fit();
    render();
  }

  document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("[data-step-diagram]").forEach(setup);
  });
})();
