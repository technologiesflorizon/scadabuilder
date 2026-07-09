const fs = require('fs');
const html = fs.readFileSync('tools/temp/win00008_exported.html', 'utf8');

// Extract all data-scada-state-config attributes
const re = /data-scada-state-config="([^"]*)"/g;
let match;
let configs = [];
while ((match = re.exec(html)) !== null) {
    const decoded = match[1].replace(/&quot;/g, '"');
    configs.push(JSON.parse(decoded));
}

// Also extract element IDs near these configs
// Find the surrounding context for each config
const idRe = /data-scada-element-id="([^"]+)"|id="([^"]+)".*?data-scada-state-config/gs;
// Actually let's find the element DIV that contains each config
const elemRe = /<div[^>]*data-scada-element-id="([^"]*)"[^>]*data-scada-state-config="([^"]*)"/g;
while ((match = elemRe.exec(html)) !== null) {
    const elementId = match[1];
    const rawConfig = match[2].replace(/&quot;/g, '"');
    const config = JSON.parse(rawConfig);
    console.log('=== Element: ' + elementId + ' ===');
    console.log('  States count: ' + config.states.length);
    config.states.forEach((s, i) => {
        console.log('  State[' + i + ']: ' + s.name + ' | expr: ' + s.expression.source + ' | CFC: ' + s.effect.colorFilterColor);
        console.log('    AST op: ' + (s.expression.ast ? s.expression.ast.op : 'N/A') + ' type: ' + (s.expression.ast ? s.expression.ast.type : 'N/A'));
        if (s.expression.ast && s.expression.ast.left) {
            console.log('    left.tagName: ' + s.expression.ast.left.tagName + ' left.type: ' + s.expression.ast.left.type);
        }
        if (s.expression.ast && s.expression.ast.right) {
            console.log('    right.value: ' + s.expression.ast.right.value + ' right.type: ' + s.expression.ast.right.type);
        }
    });
    console.log('  qualityFallback.CFC: ' + config.qualityFallback.colorFilterColor);
    console.log('  defaultEffect.CFC: ' + config.defaultEffect.colorFilterColor);
    console.log('');
}

console.log('Total elements with state config: ' + configs.length);
