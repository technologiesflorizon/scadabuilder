const fs = require('fs');
const scene = JSON.parse(fs.readFileSync('projects/AMR_REF_SCADA_V2/scenes/win00008.scene.json', 'utf8'));
scene.Elements.forEach((el, i) => {
    const sc = el.StateConfig || {};
    const hasStates = sc.States && sc.States.length > 0;
    const defaultCFC = sc.DefaultEffect ? sc.DefaultEffect.ColorFilterColor : 'N/A';
    const qfCFC = sc.QualityFallback ? sc.QualityFallback.ColorFilterColor : 'N/A';
    const firstStateEffect = (sc.States && sc.States.length > 0) ? sc.States[0].Effect : null;
    const fsCFC = firstStateEffect ? firstStateEffect.ColorFilterColor : 'N/A';
    console.log('[' + i + '] id=' + el.Id.substring(0,8) + ' kind=' + el.Kind + ' name=' + (el.DisplayName||'').substring(0,50) + ' hasStates=' + hasStates + ' states=' + (sc.States||[]).length + ' qf.CFC=' + qfCFC + ' def.CFC=' + defaultCFC + ' s0.CFC=' + fsCFC);
});
