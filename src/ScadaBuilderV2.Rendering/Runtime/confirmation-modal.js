(function () {
  'use strict';

  /**
   * Confirmation modal for the SCADA Builder V2 runtime.
   * Provides a custom modal dialog to replace the native window.confirm,
   * with Cancel and Confirm buttons. The Confirm button is auto-focused.
   *
   * Usage: window.ScadaRuntime.showConfirmation(message, onAccepted)
   *
   * @param {string}   message    - The confirmation message to display.
   * @param {function} onAccepted - Callback invoked when the user confirms.
   */
  function showConfirmation(message, onAccepted) {
    // Create full-viewport overlay
    var overlay = document.createElement('div');
    overlay.className = 'scada-confirm-overlay';
    overlay.style.cssText =
      'position:fixed;inset:0;background:rgba(0,0,0,0.4);z-index:99999;display:flex;align-items:center;justify-content:center';

    // Create dialog card
    var dialog = document.createElement('div');
    dialog.className = 'scada-confirm-dialog';
    dialog.style.cssText =
      'background:#fff;padding:24px;border-radius:8px;min-width:300px;max-width:480px;box-shadow:0 4px 24px rgba(0,0,0,0.15)';

    // Message text
    var messageEl = document.createElement('p');
    messageEl.style.cssText = 'margin:0 0 20px;font-size:14px;color:#333';
    messageEl.textContent = message;
    dialog.appendChild(messageEl);

    // Buttons container
    var buttonsEl = document.createElement('div');
    buttonsEl.style.cssText = 'display:flex;justify-content:flex-end;gap:8px';

    // Cancel button — removes overlay
    var cancelBtn = document.createElement('button');
    cancelBtn.textContent = 'Cancel';
    cancelBtn.style.cssText =
      'padding:8px 16px;border:1px solid #ccc;border-radius:4px;background:#fff;color:#333;cursor:pointer;font-size:14px';
    cancelBtn.addEventListener('click', function () {
      overlay.remove();
    });

    // Confirm button — removes overlay, calls onAccepted
    var confirmBtn = document.createElement('button');
    confirmBtn.textContent = 'Confirm';
    confirmBtn.style.cssText =
      'padding:8px 16px;border:none;border-radius:4px;background:#1565C0;color:#fff;cursor:pointer;font-size:14px';
    confirmBtn.addEventListener('click', function () {
      overlay.remove();
      if (typeof onAccepted === 'function') {
        onAccepted();
      }
    });

    buttonsEl.appendChild(cancelBtn);
    buttonsEl.appendChild(confirmBtn);
    dialog.appendChild(buttonsEl);
    overlay.appendChild(dialog);
    document.body.appendChild(overlay);

    // Auto-focus the Confirm button
    confirmBtn.focus();
  }

  // ── public API ──────────────────────────────────────────────────────────

  window.ScadaRuntime = window.ScadaRuntime || {};

  window.ScadaRuntime.showConfirmation = showConfirmation;
})();
