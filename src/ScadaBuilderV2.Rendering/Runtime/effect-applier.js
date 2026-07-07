(function () {
  'use strict';

  /**
   * Effect applier for the SCADA Builder V2 runtime.
   * Applies visual effects (state-driven style changes) to a DOM element.
   *
   * All effect properties are optional (null/undefined = skip).
   */
  function apply(element, effect) {
    if (!element || !effect) {
      return;
    }

    // ── background color ────────────────────────────────────────────────
    if (effect.backgroundColor != null) {
      element.style.backgroundColor = effect.backgroundColor;
    }

    // ── border color ─────────────────────────────────────────────────────
    if (effect.borderColor != null) {
      element.style.borderColor = effect.borderColor;
    }

    // ── border width ─────────────────────────────────────────────────────
    if (effect.borderWidth != null) {
      element.style.borderWidth = effect.borderWidth + 'px';
    }

    // ── text color ───────────────────────────────────────────────────────
    if (effect.textColor != null) {
      element.style.color = effect.textColor;
    }

    // ── text content ─────────────────────────────────────────────────────
    if (effect.textContent != null) {
      var textTarget = element.querySelector('[data-scada-text]');
      if (textTarget) {
        textTarget.textContent = effect.textContent;
      }
    }

    // ── text visible ─────────────────────────────────────────────────────
    if (effect.textVisible != null) {
      var textTarget2 = element.querySelector('[data-scada-text]');
      if (textTarget2) {
        textTarget2.hidden = !effect.textVisible;
      }
    }

    // ── element visible ──────────────────────────────────────────────────
    if (effect.elementVisible != null) {
      element.hidden = !effect.elementVisible;
    }

    // ── opacity ──────────────────────────────────────────────────────────
    if (effect.opacity != null) {
      element.style.opacity = effect.opacity;
    }

    // ── rotation ─────────────────────────────────────────────────────────
    if (effect.rotation != null) {
      element.style.transform = (element.style.transform || '') + ' rotate(' + effect.rotation + 'deg)';
    }

    // ── animation ────────────────────────────────────────────────────────
    if (effect.animation !== null && effect.animation !== undefined) {
      // Remove all scada-anim-* classes
      var classList = element.classList;
      var animClasses = [];
      for (var i = 0; i < classList.length; i++) {
        if (classList[i].indexOf('scada-anim-') === 0) {
          animClasses.push(classList[i]);
        }
      }
      for (var j = 0; j < animClasses.length; j++) {
        classList.remove(animClasses[j]);
      }
      // If a non-null/non-None animation name was provided, add the class
      if (effect.animation !== 'None' && effect.animation !== 'none') {
        classList.add('scada-anim-' + effect.animation);
      }
    }
  }

  // ── public API ──────────────────────────────────────────────────────────

  window.ScadaRuntime = window.ScadaRuntime || {};

  window.ScadaRuntime.EffectApplier = {
    apply: apply
  };
})();
