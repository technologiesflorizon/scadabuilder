(function () {
  'use strict';

  /**
   * Animation controller for the SCADA Builder V2 runtime.
   * Sets and clears animation CSS classes (scada-anim-blink / pulse / halo / spin)
   * on DOM elements based on element state or command triggers.
   *
   * Recognised animation names: blink, pulse, halo, spin
   */

  /**
   * Removes all scada-anim-* classes from the given element.
   *
   * @param {Element} element  - DOM element to clear.
   */
  function clearAnimation(element) {
    if (!element) {
      return;
    }

    var classList = element.classList;
    var toRemove = [];

    for (var i = 0; i < classList.length; i++) {
      if (classList[i].indexOf('scada-anim-') === 0) {
        toRemove.push(classList[i]);
      }
    }

    for (var j = 0; j < toRemove.length; j++) {
      classList.remove(toRemove[j]);
    }
  }

  /**
   * Sets an animation CSS class on the given element.
   * Removes any previously-applied scada-anim-* class first.
   *
   * @param {Element} element        - DOM element to animate.
   * @param {string}  animationName  - Animation name (e.g. 'blink', 'pulse', 'halo', 'spin').
   */
  function setAnimation(element, animationName) {
    if (!element) {
      return;
    }

    clearAnimation(element);

    if (animationName) {
      element.classList.add('scada-anim-' + animationName);
    }
  }

  // ── public API ──────────────────────────────────────────────────────────

  window.ScadaRuntime = window.ScadaRuntime || {};

  window.ScadaRuntime.AnimationController = {
    setAnimation: setAnimation,
    clearAnimation: clearAnimation
  };
})();
