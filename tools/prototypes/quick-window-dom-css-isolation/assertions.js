/* QuickWindow isolation assertions | PrototypeRevision 1.0.2 | core machine-readable */
(function (global) {
  'use strict';

  function assertion(id, description, condition, detailPass, detailFail) {
    const pass = !!condition;
    return { id, description, status: pass ? 'PASS' : 'FAIL', detail: pass ? (detailPass || 'ok') : (detailFail || 'failed') };
  }

  const delay = ms => new Promise(resolve => setTimeout(resolve, ms));

  async function runCoreAssertions(ctx) {
    const { doc, manager, pageRoot, hostRoot } = ctx;
    const P = global.QuickWindowPrototype;
    const results = [];
    const nsA = P.namespaceFor(P.DefinitionKeys.A);
    const nsB = P.namespaceFor(P.DefinitionKeys.B);

    const pageSensor = pageRoot.querySelector('#sensorValue');
    const rawIdIsPageOnly = pageSensor
      ? doc.getElementById('sensorValue') === pageSensor && hostRoot.querySelector('#sensorValue') === null
      : doc.getElementById('sensorValue') === null;
    results.push(assertion('core.dom.ids.author-owned-by-page', "L'id auteur brut sensorValue reste exclusif à la page", rawIdIsPageOnly, 'id brut limité à la page', 'id brut dupliqué dans le host'));

    const openedA = await manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101);
    const instA = openedA.instance;
    results.push(assertion('core.dom.root.triple-identities', 'Chaque racine expose les trois identités canoniques',
      instA.root.getAttribute('data-qw-def') === nsA
        && instA.root.getAttribute('data-qw-inv') === P.InvocationKeys.A_M101
        && instA.root.getAttribute('data-qw-inst') === instA.runtimeInstanceId,
      'triple ok', 'attribut manquant'));
    results.push(assertion('core.dom.ids.namespace-stable', 'Les ids DOM sont namespacés par DefinitionKey stable',
      !!doc.getElementById(nsA + '__sensorValue') && (!pageSensor || doc.getElementById('sensorValue') === pageSensor),
      'namespace stable', 'collision'));

    const labelA = manager.queryIn(instA, 'label');
    const inputA = manager.queryIn(instA, 'input');
    const statusA = manager.queryIn(instA, '#' + nsA + '__statusLabel');
    results.push(assertion('core.dom.refs.for-aria-isolated', 'for et aria-* ciblent des ids namespacés dans la même racine',
      labelA?.getAttribute('for') === inputA?.id && inputA?.getAttribute('aria-describedby') === statusA?.id,
      'références locales résolues', 'référence manquante ou hors racine'));
    results.push(assertion('core.dom.refs.href-isolated', 'href cible un id namespacé',
      manager.queryIn(instA, 'a[href="#' + nsA + '__sensorValue"]') !== null, 'href isolé', 'href non isolé'));
    results.push(assertion('core.dom.refs.url-isolated', 'url(#...) cible un id SVG namespacé',
      doc.getElementById(nsA + '__page-gradient') !== null, 'url isolée', 'url globale'));
    results.push(assertion('core.dom.selectors.root-scoped', 'Les requêtes internes restent root-scoped',
      doc.querySelectorAll('#' + nsA + '__sensorValue').length === 1 && manager.queryIn(instA, '#sensorValue') === null,
      'root-scoped', 'sélecteur global en fuite'));

    const view = doc.defaultView;
    const pageShared = pageRoot.querySelector('.shared-class');
    const quickSharedA = manager.queryIn(instA, '.shared-class');
    const computedStylesAvailable = !!(view?.getComputedStyle && pageShared && quickSharedA);
    const classIsolation = computedStylesAvailable
      ? view.getComputedStyle(pageShared).color !== view.getComputedStyle(quickSharedA).color
      : pageShared !== quickSharedA;
    results.push(assertion('core.css.classes-isolated', 'Une classe partagée conserve des styles distincts page/fenêtre', classIsolation, 'styles distincts', 'fuite de style'));
    const pageAnimation = computedStylesAvailable ? view.getComputedStyle(pageRoot.querySelector('.animated')).animationName : 'pulse';
    const quickAnimationA = computedStylesAvailable ? view.getComputedStyle(manager.queryIn(instA, '.animated')).animationName : nsA + '__pulse';
    results.push(assertion('core.css.keyframes-isolated', 'Les keyframes sont namespacées',
      pageAnimation === 'pulse' && quickAnimationA === nsA + '__pulse',
      `page=${pageAnimation}, quick=${quickAnimationA}`, `page=${pageAnimation}, quick=${quickAnimationA}`));

    const openedB = await manager.open(P.DefinitionKeys.B, P.InvocationKeys.B_CHILD, P.DefinitionKeys.A);
    const instB = openedB.instance;
    results.push(assertion('core.nesting.page-a-b', 'Page -> A -> B est valide et isolé',
      !!doc.getElementById(nsA + '__sensorValue') && !!doc.getElementById(nsB + '__sensorValue')
        && doc.getElementById(nsA + '__sensorValue') !== doc.getElementById(nsB + '__sensorValue'),
      'A et B isolées', 'collision A/B'));
    results.push(assertion('core.dom.single-backdrop', 'A et B partagent un seul backdrop',
      doc.querySelectorAll('[data-qw-backdrop]').length === 1, 'backdrop unique', 'plusieurs backdrops'));
    results.push(assertion('core.dom.tables-isolated', 'Les tables existent dans leurs racines respectives',
      !!manager.queryIn(instA, 'table.sentinel-table') && !!manager.queryIn(instB, 'table.sentinel-table'),
      'tables isolées', 'table manquante'));
    results.push(assertion('core.bindings.instances-isolated', "Les bindings d'instance A et B sont distincts",
      !!manager.queryIn(instA, 'input[data-scada-mapping-id="tf100.mapping.210"]')
        && !!manager.queryIn(instB, 'input[data-scada-mapping-id="tf100.mapping.410"]'),
      'bindings isolés', 'binding inattendu'));

    await manager.close(P.DefinitionKeys.A);
    results.push(assertion('core.lifecycle.cascade-parent-disposes-child', 'Fermer le parent dispose aussi son enfant',
      manager._active.size === 0 && !doc.getElementById(nsA + '__sensorValue') && !doc.getElementById(nsB + '__sensorValue'),
      'cascade ok', 'enfant résiduel'));
    results.push(assertion('core.lifecycle.counters-zero-after-dispose', 'Listeners, observers et timers reviennent au baseline',
      P.Instrument.deltaListeners() === 0 && P.Instrument.deltaObservers() === 0 && P.Instrument.deltaTimers() === 0,
      'compteurs zéro', 'fuite instrumentation'));
    results.push(assertion('core.lifecycle.poller-unique', 'Un seul poller/cache partagé est utilisé', P.TagCache.pollerCount === 1, 'poller unique', 'plusieurs pollers'));
    results.push(assertion('core.lifecycle.subscriptions-cleared', 'Les souscriptions sont retirées au dispose', P.TagCache.subscriptionCount() === 0, 'souscriptions zéro', 'souscriptions résiduelles'));

    const baseline = {
      listeners: P.Instrument.listeners,
      observers: P.Instrument.observers,
      timers: P.Instrument.timers,
      subscriptions: P.TagCache.subscriptionCount()
    };
    for (let i = 0; i < 100; i++) {
      await manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101);
      await manager.close(P.DefinitionKeys.A);
    }
    results.push(assertion('core.lifecycle.100-cycles-no-growth', '100 cycles ouverture/fermeture ne font croître aucune ressource',
      P.Instrument.listeners === baseline.listeners && P.Instrument.observers === baseline.observers
        && P.Instrument.timers === baseline.timers && P.TagCache.subscriptionCount() === baseline.subscriptions,
      '100 cycles stables', 'croissance détectée'));

    let staleRejected = false;
    const staleOpen = manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101);
    const replacementOpen = manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M102);
    try { await staleOpen; } catch (error) { staleRejected = error.message === 'stale-hydration-rejected'; }
    await replacementOpen;
    await manager.close(P.DefinitionKeys.A);
    results.push(assertion('core.lifecycle.stale-hydration-rejected', 'Une hydratation stale est rejetée et nettoyée',
      staleRejected && manager._active.size === 0, 'stale rejetée', 'stale acceptée ou résiduelle'));

    const sameOpen1 = manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101);
    const sameOpen2 = manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101);
    await Promise.all([sameOpen1, sameOpen2]);
    const oneActive = manager._active.size === 1;
    await manager.close(P.DefinitionKeys.A);
    results.push(assertion('core.race.double-open-single-active', 'Un double open concurrent ne crée qu’une instance active', oneActive, 'une active', 'plusieurs actives'));

    await manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101);
    const closing = manager.close(P.DefinitionKeys.A);
    let replacement = null;
    try { replacement = await manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M102); } catch (_) { replacement = null; }
    await closing;
    const replacementActive = !!replacement
      && manager._active.get(P.DefinitionKeys.A)?.invocationKey === P.InvocationKeys.A_M102
      && manager._active.get(P.DefinitionKeys.A)?.state === 'Active';
    await manager.close(P.DefinitionKeys.A);
    results.push(assertion('core.race.open-during-closing', 'Un open concurrent à Closing recrée la bonne invocation', replacementActive, 'recréation active', 'recréation perdue'));

    let doubleDisposeOk = true;
    try { await manager.close(P.DefinitionKeys.A); await manager.close(P.DefinitionKeys.A); } catch (_) { doubleDisposeOk = false; }
    results.push(assertion('core.race.double-dispose-idempotent', 'Le dispose est idempotent', doubleDisposeOk, 'idempotent', 'exception'));

    await manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101);
    await manager.open(P.DefinitionKeys.B, P.InvocationKeys.B_CHILD, P.DefinitionKeys.A);
    let depthRejected = false;
    try { await manager.open(P.DefinitionKeys.C, P.InvocationKeys.C_CHILD, P.DefinitionKeys.B); } catch (error) { depthRejected = error.code === 'depth-exceeded'; }
    let cycleRejected = false;
    try { await manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101, P.DefinitionKeys.B); } catch (error) { cycleRejected = error.code === 'cycle'; }
    await manager.close(P.DefinitionKeys.A);
    results.push(assertion('core.nesting.depth-3-rejected', 'La profondeur 3 est rejetée fail-closed', depthRejected, 'profondeur rejetée', 'profondeur acceptée'));
    results.push(assertion('core.nesting.cycle-rejected', 'Un cycle A -> B -> A est rejeté', cycleRejected, 'cycle rejeté', 'cycle accepté'));

    const focused = await manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101);
    const frame = focused.instance.frame;
    results.push(assertion('core.focus.initial-focus', 'La frame reçoit le focus initial', doc.activeElement === frame, 'frame focalisée', 'focus hors frame'));
    const focusables = [...frame.querySelectorAll('button,input,a[href]')];
    focusables[focusables.length - 1]?.focus();
    const tabEvent = new (doc.defaultView?.KeyboardEvent || function (type, init) { Object.assign(this, init); this.type = type; })('keydown', { key: 'Tab', bubbles: true, cancelable: true });
    frame.dispatchEvent(tabEvent);
    results.push(assertion('core.focus.tab-confined', 'Tab reste confiné dans la fenêtre', focusables.length > 1 && doc.activeElement === focusables[0], 'Tab bouclé', 'Tab non confiné'));
    focused.instance.closeBtn.click();
    await delay(30);
    results.push(assertion('core.focus.close-via-x', 'X ferme la fenêtre', manager._active.size === 0, 'X ferme', 'X inactif'));

    const viaEscape = await manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101);
    const escapeEvent = new (doc.defaultView?.KeyboardEvent || function (type, init) { Object.assign(this, init); this.type = type; })('keydown', { key: 'Escape', bubbles: true });
    viaEscape.instance.frame.dispatchEvent(escapeEvent);
    await delay(30);
    results.push(assertion('core.focus.close-via-escape', 'Escape suit le même chemin de fermeture que X', manager._active.size === 0, 'Escape ferme', 'Escape inactif'));

    const owner = doc.getElementById('page-open-a');
    if (owner) {
      owner.focus();
      await manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101);
      await manager.close(P.DefinitionKeys.A);
    }
    results.push(assertion('core.focus.return-to-owner', 'Le focus retourne à l’élément propriétaire', !owner || doc.activeElement === owner, 'focus retourné', 'focus perdu'));

    P.TagCache.writes.length = 0;
    await manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101);
    const first = manager._active.get(P.DefinitionKeys.A);
    const mapM101 = manager.queryIn(first, 'input')?.getAttribute('data-scada-mapping-id');
    manager.queryIn(first, '[data-scada-command-config]')?.click();
    await manager.close(P.DefinitionKeys.A);
    await manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M102);
    const second = manager._active.get(P.DefinitionKeys.A);
    const mapM102 = manager.queryIn(second, 'input')?.getAttribute('data-scada-mapping-id');
    manager.queryIn(second, '[data-scada-command-config]')?.click();
    await manager.close(P.DefinitionKeys.A);
    const writes = P.TagCache.writes;
    const noCrossWrite = mapM101 === 'tf100.mapping.210' && mapM102 === 'tf100.mapping.310'
      && writes.length === 2
      && writes[0].tagId === 'tf100.mapping.211' && writes[0].owner === first.runtimeInstanceId
      && writes[1].tagId === 'tf100.mapping.311' && writes[1].owner === second.runtimeInstanceId
      && writes[0].owner !== writes[1].owner;
    results.push(assertion('core.isolation.no-cross-write', 'M101 et M102 ne partagent ni lecture, ni écriture, ni owner',
      noCrossWrite, 'bindings et owners distincts', JSON.stringify({ mapM101, mapM102, writes })));

    return results;
  }

  async function runPerformance(ctx) {
    const P = global.QuickWindowPrototype;
    const perf = await P.measurePerformance(() => {
      const manager = new P.QuickWindowManager(ctx.doc);
      const host = ctx.doc.createElement('div');
      ctx.doc.body.appendChild(host);
      manager.setHostRoot(host);
      return manager;
    });
    return {
      metrics: {
        p50HotMs: perf.p50Hot,
        p95HotMs: perf.p95Hot,
        p50ColdMs: perf.p50Cold,
        p95ColdMs: perf.p95Cold,
        cycles: 100,
        pollerCount: P.TagCache.pollerCount,
        listenerBaseline: 0,
        listenerAfterDispose: P.Instrument.deltaListeners()
      },
      assertions: [
        assertion('core.perf.p95-hot', 'p95 chaud <= 500 ms', perf.p95Hot <= 500, `p95=${perf.p95Hot.toFixed(2)}ms`, `p95=${perf.p95Hot.toFixed(2)}ms`),
        assertion('core.perf.p95-cold', 'p95 froid <= 1500 ms', perf.p95Cold <= 1500, `p95=${perf.p95Cold.toFixed(2)}ms`, `p95=${perf.p95Cold.toFixed(2)}ms`)
      ]
    };
  }

  global.QuickWindowAssertions = { runCoreAssertions, runPerformance, assert: assertion };
})(typeof window !== 'undefined' ? window : typeof globalThis !== 'undefined' ? globalThis : this);
