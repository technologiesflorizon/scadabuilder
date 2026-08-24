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
   * Subscription bookkeeping keyed by owner. It creates no second poller and no second cache: the shared
   * bridge stays the only reader, and the map exists so an owner can drop exactly its own subscriptions
   * when it is disposed.
   */
  var _subscriptionsByOwner = {};

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
      return false;
    }

    // Delegate to host bridge if available
    if (window.tf100webScadaBuilder && typeof window.tf100webScadaBuilder.writeTag === 'function') {
      return window.tf100webScadaBuilder.writeTag(tagId, value, payload || {});
    }

    // Standalone preview fallback only. Deployed values remain confirmed by host snapshots.
    _localValues[tagId] = value;
    return true;
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

  /**
   * Records that an owner reads a tag through the shared bridge.
   *
   * @param {string} tagId  - Tag identifier.
   * @param {string} owner  - Owner id, typically a quick-window RuntimeInstanceId.
   */
  function subscribe(tagId, owner) {
    if (!tagId || !owner) {
      return false;
    }
    var owned = _subscriptionsByOwner[owner] || (_subscriptionsByOwner[owner] = []);
    if (owned.indexOf(tagId) < 0) {
      owned.push(tagId);
    }
    return true;
  }

  /**
   * Drops every subscription of one owner. Calling it twice is a no-op.
   *
   * @param {string} owner - Owner id.
   */
  function unsubscribeAll(owner) {
    if (!owner || !Object.prototype.hasOwnProperty.call(_subscriptionsByOwner, owner)) {
      return 0;
    }
    var count = _subscriptionsByOwner[owner].length;
    delete _subscriptionsByOwner[owner];
    return count;
  }

  /**
   * Returns the tags currently read by one owner, or every owner when none is given.
   *
   * @param {string} [owner] - Owner id.
   */
  function subscriptions(owner) {
    if (owner) {
      return (_subscriptionsByOwner[owner] || []).slice();
    }
    var all = {};
    for (var key in _subscriptionsByOwner) {
      if (Object.prototype.hasOwnProperty.call(_subscriptionsByOwner, key)) {
        all[key] = _subscriptionsByOwner[key].slice();
      }
    }
    return all;
  }

  window.ScadaRuntime.TagBridge = {
    getTagValue: getTagValue,
    writeTag: writeTag,
    setTagValue: setTagValue,
    setValues: setValues,
    subscribe: subscribe,
    unsubscribeAll: unsubscribeAll,
    subscriptions: subscriptions
  };
})();
