(function () {
  'use strict';

  /**
   * Tag bridge for the SCADA Builder V2 runtime.
   * Provides a unified interface for reading and writing SCADA tag values.
   *
   * Prefers window.tf100webScadaBuilder.getTagValue / writeTag when available
   * (host-provided bridge), otherwise falls back to a local value cache.
   */

  /** Internal local cache. */
  var _localValues = {};

  /**
   * Returns the cached value for a tag, or undefined if not cached.
   *
   * @param {string} tagId  - Tag identifier.
   * @returns {*|undefined}
   */
  function getTagValue(tagId) {
    if (!tagId) {
      return undefined;
    }

    // Prefer host bridge
    if (window.tf100webScadaBuilder && typeof window.tf100webScadaBuilder.getTagValue === 'function') {
      return window.tf100webScadaBuilder.getTagValue(tagId);
    }

    // Fallback to local cache
    return _localValues[tagId];
  }

  /**
   * Writes a tag value. Delegates to the host writeTag if available,
   * otherwise updates the local cache directly.
   *
   * @param {string} tagId   - Tag identifier.
   * @param {*}      value   - Value to write.
   * @param {object} [payload] - Optional additional payload.
   */
  function writeTag(tagId, value, payload) {
    if (!tagId) {
      return;
    }

    // Update local cache
    _localValues[tagId] = value;

    // Delegate to host bridge if available
    if (window.tf100webScadaBuilder && typeof window.tf100webScadaBuilder.writeTag === 'function') {
      window.tf100webScadaBuilder.writeTag(tagId, value, payload || {});
    }
  }

  /**
   * Sets a single tag value in the local cache.
   *
   * @param {string} tagId  - Tag identifier.
   * @param {*}      value  - Value to cache.
   */
  function setTagValue(tagId, value) {
    if (!tagId) {
      return;
    }
    _localValues[tagId] = value;
  }

  /**
   * Sets multiple tag values in the local cache at once.
   *
   * @param {object} values - Map of tagId -> value.
   */
  function setValues(values) {
    if (!values || typeof values !== 'object') {
      return;
    }
    for (var key in values) {
      if (Object.prototype.hasOwnProperty.call(values, key)) {
        _localValues[key] = values[key];
      }
    }
  }

  // ── public API ──────────────────────────────────────────────────────────

  window.ScadaRuntime = window.ScadaRuntime || {};

  window.ScadaRuntime.TagBridge = {
    getTagValue: getTagValue,
    writeTag: writeTag,
    setTagValue: setTagValue,
    setValues: setValues
  };
})();
