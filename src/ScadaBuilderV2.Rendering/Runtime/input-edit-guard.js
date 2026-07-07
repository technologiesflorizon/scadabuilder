(function () {
  'use strict';

  /**
   * Input edit guard for the SCADA Builder V2 runtime.
   * Watches input/textarea/select elements, locks them during editing,
   * and auto-releases after 30 seconds of inactivity.
   *
   * Dependencies: window.ScadaRuntime.StateEngine (pauseElement / resumeElement)
   *               window.ScadaRuntime.TagBridge (getTagValue)
   */

  var EDIT_TIMEOUT = 30000;

  /** Map of element id -> { timerId, overlay, inputEl } for locked elements. */
  var _locks = {};

  /**
   * Creates the overlay div that visually indicates an element is locked.
   * @returns {HTMLDivElement}
   */
  function _createOverlay() {
    var div = document.createElement('div');
    div.className = 'scada-input-edit-overlay';
    div.style.cssText =
      'position:absolute;inset:0;background:rgba(15,42,48,0.06);border:2px solid rgba(15,42,48,0.32);border-radius:4px;pointer-events:none;z-index:10';
    div.setAttribute('data-scada-input-overlay', '');
    return div;
  }

  /**
   * Watches an element for focus on input/textarea/select and locks it for editing.
   *
   * @param {Element} element - DOM element to watch.
   */
  function watch(element) {
    if (!element) {
      return;
    }

    var elementId = element.id;
    if (!elementId) {
      return;
    }

    var inputEl = element.querySelector('input, textarea, select');
    if (!inputEl) {
      return;
    }

    // Prevent duplicate handler registration
    if (element.getAttribute('data-scada-edit-guard') === 'attached') {
      return;
    }
    element.setAttribute('data-scada-edit-guard', 'attached');

    inputEl.addEventListener('focus', function () {
      lock(elementId, inputEl);
    });

    inputEl.addEventListener('blur', function () {
      release(elementId);
    });

    inputEl.addEventListener('change', function () {
      if (isLocked(elementId)) {
        release(elementId);
      }
    });

    inputEl.addEventListener('keydown', function (e) {
      if (e.key === 'Enter' || e.key === 'Escape') {
        release(elementId);
      }
    });
  }

  /**
   * Locks an element for editing: pauses the state engine, shows the overlay,
   * and starts a 30-second timeout.
   *
   * @param {string}  elementId - Element id.
   * @param {Element} inputEl   - The input/textarea/select element.
   */
  function lock(elementId, inputEl) {
    if (!elementId) {
      return;
    }

    // Release any existing lock on the same element
    if (_locks[elementId]) {
      release(elementId);
    }

    // Pause state engine for this element
    if (window.ScadaRuntime && window.ScadaRuntime.StateEngine) {
      window.ScadaRuntime.StateEngine.pauseElement(elementId);
    }

    var overlay = _createOverlay();

    var parentEl = inputEl.parentElement;
    if (parentEl) {
      // Ensure parent is positioned so the overlay can use position:absolute
      var computedStyle = window.getComputedStyle(parentEl);
      if (computedStyle.position === 'static') {
        parentEl.style.position = 'relative';
      }
      parentEl.appendChild(overlay);
    }

    var timerId = setTimeout(function () {
      // Timeout reached — release lock
      release(elementId);
      // Refresh input value from TagBridge
      if (window.ScadaRuntime && window.ScadaRuntime.TagBridge) {
        var value = window.ScadaRuntime.TagBridge.getTagValue(elementId);
        if (value !== undefined && value !== null) {
          inputEl.value = value;
        }
      }
      inputEl.blur();
    }, EDIT_TIMEOUT);

    _locks[elementId] = {
      timerId: timerId,
      overlay: overlay,
      inputEl: inputEl
    };
  }

  /**
   * Releases a locked element: clears the timer, removes the overlay,
   * and resumes the state engine.
   *
   * @param {string} elementId - Element id.
   */
  function release(elementId) {
    if (!elementId) {
      return;
    }

    var lock = _locks[elementId];
    if (!lock) {
      return;
    }

    // Clear timeout
    if (lock.timerId) {
      clearTimeout(lock.timerId);
    }

    // Remove overlay
    if (lock.overlay && lock.overlay.parentNode) {
      lock.overlay.parentNode.removeChild(lock.overlay);
    }

    // Resume state engine
    if (window.ScadaRuntime && window.ScadaRuntime.StateEngine) {
      window.ScadaRuntime.StateEngine.resumeElement(elementId);
    }

    delete _locks[elementId];
  }

  /**
   * Checks whether an element is currently locked for editing.
   *
   * @param {string} elementId - Element id.
   * @returns {boolean}
   */
  function isLocked(elementId) {
    return !!_locks[elementId];
  }

  // ── public API ──────────────────────────────────────────────────────────

  window.ScadaRuntime = window.ScadaRuntime || {};

  window.ScadaRuntime.InputEditGuard = {
    watch: watch,
    lock: lock,
    release: release,
    isLocked: isLocked
  };
})();
