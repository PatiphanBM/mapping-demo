// Quiz component.
// Markup:
//   <div class="quiz">
//     <p class="q">Question</p>
//     <ol class="options"><li data-correct>Right</li><li>Wrong</li>…</ol>
//     <div class="why">Explanation shown after answering</div>
//   </div>
//   <p class="quiz-score" data-quiz-score></p>   (optional, one per page)
// Options are shuffled on every load so position never hints at the answer.
(function () {
  function shuffle(items) {
    for (let i = items.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [items[i], items[j]] = [items[j], items[i]];
    }
    return items;
  }

  const quizzes = [];

  function setup(quiz) {
    const list = quiz.querySelector(".options");
    const originals = Array.from(list.children).map((li) => ({
      html: li.innerHTML,
      correct: li.hasAttribute("data-correct"),
    }));
    const state = { quiz, list, originals, answered: false, right: false };

    state.render = function () {
      state.answered = false;
      state.right = false;
      quiz.classList.remove("answered");
      list.innerHTML = "";
      shuffle(originals.slice()).forEach((opt) => {
        const li = document.createElement("li");
        const btn = document.createElement("button");
        btn.type = "button";
        btn.innerHTML = opt.html;
        btn.dataset.correct = opt.correct ? "1" : "";
        btn.addEventListener("click", () => answer(state, btn));
        li.appendChild(btn);
        list.appendChild(li);
      });
    };

    state.render();
    quizzes.push(state);
  }

  function answer(state, chosen) {
    if (state.answered) return;
    state.answered = true;
    state.right = chosen.dataset.correct === "1";
    state.quiz.classList.add("answered");
    state.list.querySelectorAll("button").forEach((btn) => {
      btn.disabled = true;
      if (btn.dataset.correct === "1") btn.classList.add("correct");
    });
    if (!state.right) chosen.classList.add("wrong");
    updateScore();
  }

  function updateScore() {
    const out = document.querySelector("[data-quiz-score]");
    if (!out) return;
    const done = quizzes.filter((q) => q.answered).length;
    const right = quizzes.filter((q) => q.right).length;
    out.textContent =
      done < quizzes.length
        ? `ตอบแล้ว ${done}/${quizzes.length}`
        : `ตอบถูก ${right}/${quizzes.length}` +
          (right === quizzes.length ? " ครบทุกข้อ" : " ลองอ่านคำอธิบายข้อที่ผิด แล้วกดเริ่มใหม่");
  }

  document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll(".quiz").forEach(setup);
    const reset = document.querySelector("[data-quiz-reset]");
    if (reset) {
      reset.addEventListener("click", () => {
        quizzes.forEach((q) => q.render());
        updateScore();
      });
    }
    updateScore();
  });
})();
