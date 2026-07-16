namespace ScadaBuilderV2.App.TableEditor;

/// <summary>Owns the table-specific WebView renderer and cell interaction bridge.</summary>
internal static class TableWebViewScript
{
    public const string Source = """
(() => {
  if (window.scadaModernTable) return;
  const css = document.createElement('style');
  css.textContent = `
    .scada-editor-table { display:grid; width:100%; height:100%; overflow:visible; }
    .scada-editor-table-cell { display:flex; box-sizing:border-box; min-width:0; min-height:0; overflow:hidden; position:relative; }
    .scada-editor-table-cell[data-selected="true"] { outline:2px solid #2090a0; outline-offset:-2px; z-index:2; }
    .scada-editor-table-cell input { width:100%; height:100%; min-width:0; border:0; padding:0; background:transparent; color:inherit; font:inherit; }
    .scada-editor-table-track { position:absolute; z-index:4; background:transparent; }
    .scada-editor-table-track.column { top:0; bottom:0; width:7px; cursor:col-resize; }
    .scada-editor-table-track.row { left:0; right:0; height:7px; cursor:row-resize; }
    .scada-editor-table-header { position:absolute; z-index:6; display:flex; align-items:center; justify-content:center; background:#dcecef; color:#17343b; border:1px solid #8aa0a6; box-sizing:border-box; font:10px Segoe UI; opacity:.92; }
    .scada-editor-table-header.column { top:-18px; height:18px; }
    .scada-editor-table-header.row { left:-24px; width:24px; }
    .scada-editor-table-corner { left:-24px; top:-18px; width:24px; height:18px; }
    .scada-editor-table[data-mode="object"] .scada-editor-table-track,
    .scada-editor-table[data-mode="object"] .scada-editor-table-header { display:none; }
    .scada-editor-table[data-editor-guides="hidden"] .scada-editor-table-header { display:none; }
  `;
  document.head.appendChild(css);
  let trackDrag = null;
  let interactionMode = 'object';
  let showEditorGuides = true;
  let activeTableId = null;
  let cellSelectionDrag = null;
  document.addEventListener('pointermove', event => {
    if (!trackDrag) return;
    const delta = trackDrag.orientation === 'column' ? event.clientX - trackDrag.start : event.clientY - trackDrag.start;
    trackDrag.size = Math.max(trackDrag.minimum, trackDrag.initial + delta);
    if (trackDrag.orientation === 'column') trackDrag.node.style.left = `${trackDrag.offset + delta - 3}px`;
    else trackDrag.node.style.top = `${trackDrag.offset + delta - 3}px`;
    event.preventDefault();
  }, true);
  document.addEventListener('pointerup', event => {
    if (cellSelectionDrag?.pointerId === event.pointerId) cellSelectionDrag = null;
    if (trackDrag) {
      window.chrome?.webview?.postMessage({ type:'tableTrackResize', id:trackDrag.id, orientation:trackDrag.orientation, trackIndex:trackDrag.index, trackSize:trackDrag.size });
      trackDrag = null; event.preventDefault();
    }
  }, true);
  document.addEventListener('pointercancel', event => { if (cellSelectionDrag?.pointerId === event.pointerId) cellSelectionDrag = null; }, true);

  const effective = (table, row, column, cell) => {
    const base = table?.Style?.Base || table?.style?.base || {};
    const columnStyle = table?.Columns?.[column]?.Style || {};
    const rowDef = table?.Rows?.[row] || {};
    const band = rowDef.IsHeader ? (table?.Style?.Header || {}) : (row % 2 === 1 ? (table?.Style?.AlternatingRows || {}) : {});
    return Object.assign({}, base, columnStyle, band, rowDef.Style || {}, cell?.Style || {});
  };

  const render = (element, wrapper) => {
    const table = element.Table || element.table;
    if (!table) return;
    wrapper.style.padding = '0'; wrapper.style.background = 'transparent'; wrapper.style.border = '0';
    const grid = document.createElement('div'); grid.className = 'scada-editor-table'; grid.dataset.tableId = element.Id; grid.dataset.mode = interactionMode; grid.dataset.editorGuides = interactionMode === 'cells' && showEditorGuides ? 'visible' : 'hidden'; grid.style.position = 'relative';
    grid.dataset.columnSizes = JSON.stringify((table.Columns || []).map(item => Number(item.Width || 24)));
    grid.dataset.rowSizes = JSON.stringify((table.Rows || []).map(item => Number(item.Height || 20)));
    grid.style.gridTemplateColumns = (table.Columns || []).map(item => `${Number(item.Width || 24)}px`).join(' ');
    grid.style.gridTemplateRows = (table.Rows || []).map(item => `${Number(item.Height || 20)}px`).join(' ');
    let anchor = null;
    const cellFragment = document.createDocumentFragment();
    (table.Cells || []).forEach(cell => {
      const node = document.createElement('div'); node.className = 'scada-editor-table-cell'; node.dataset.row = cell.Row; node.dataset.column = cell.Column; node.dataset.rowSpan = Math.max(1, Number(cell.RowSpan || 1)); node.dataset.columnSpan = Math.max(1, Number(cell.ColumnSpan || 1));
      node.style.gridRow = `${Number(cell.Row)+1} / span ${Math.max(1, Number(cell.RowSpan || 1))}`;
      node.style.gridColumn = `${Number(cell.Column)+1} / span ${Math.max(1, Number(cell.ColumnSpan || 1))}`;
      const format = effective(table, Number(cell.Row), Number(cell.Column), cell);
      node.style.background = format.Background || '#fff'; node.style.color = format.Foreground || '#0f2a30';
      const gridStyles = ['none','solid','dashed','dotted','double'];
      const gridStyle = typeof format.GridStyle === 'number' ? gridStyles[format.GridStyle] : String(format.GridStyle || 'solid').toLowerCase();
      node.style.border = `${Number(format.GridWidth ?? 1)}px ${gridStyle} ${format.GridColor || '#8aa0a6'}`;
      node.style.padding = `${Number(format.Padding ?? 4)}px`; const vertical = typeof format.VerticalAlignment === 'number' ? ['top','middle','bottom'][format.VerticalAlignment] : String(format.VerticalAlignment || 'middle').toLowerCase(); node.style.alignItems = vertical === 'top' ? 'flex-start' : (vertical === 'bottom' ? 'flex-end' : 'center');
      const horizontalValues = ['left','center','right','justify'];
      node.style.textAlign = typeof format.HorizontalAlignment === 'number' ? horizontalValues[format.HorizontalAlignment] : String(format.HorizontalAlignment || 'left').toLowerCase(); node.style.fontFamily = format.FontFamily || 'Segoe UI'; node.style.fontSize = `${Number(format.FontSize || 14)}px`; node.style.fontWeight = format.FontWeight || 'normal';
      node.style.fontStyle = format.FontStyle || 'normal'; node.style.whiteSpace = format.TextWrap === true ? 'normal' : 'nowrap'; node.style.overflowWrap = format.TextWrap === true ? 'anywhere' : 'normal'; if (format.LineHeight != null) node.style.lineHeight = `${Number(format.LineHeight)}px`;
      const content = cell.Content || {}; const kind = typeof content.Kind === 'number' ? ['Text','InputText','InputNumeric'][content.Kind] : String(content.Kind || 'Text');
      node.dataset.contentKind = kind;
      if (kind === 'InputText' || kind === 'InputNumeric') {
        const input = document.createElement('input'); input.type = kind === 'InputNumeric' ? 'number' : 'text'; input.value = kind === 'InputNumeric' ? (content.NumericValue ?? content.Text ?? '') : (content.Text || ''); input.placeholder = content.Placeholder || ''; input.readOnly = content.IsReadOnly === true;
        ['Minimum','Maximum','Step'].forEach((name, index) => { if (content[name] != null) input[['min','max','step'][index]] = content[name]; });
        node.appendChild(input);
      } else {
        const span = document.createElement('span'); span.textContent = content.Text || ''; node.appendChild(span);
      }
      node.tabIndex = 0;
      cellFragment.appendChild(node);
    });
    grid.appendChild(cellFragment);
    const cellFrom = event => event.target.closest?.('.scada-editor-table-cell');
    const selectRange = (start, end, scope='cells') => { const row=Math.min(start.row,end.row),column=Math.min(start.column,end.column),endRow=Math.max(start.row,end.row),endColumn=Math.max(start.column,end.column); grid.querySelectorAll('.scada-editor-table-cell').forEach(item=>{const r=Number(item.dataset.row),c=Number(item.dataset.column);item.dataset.selected=r>=row&&r<=endRow&&c>=column&&c<=endColumn?'true':'false';}); window.chrome?.webview?.postMessage({type:'tableSelection',id:element.Id,row,column,endRow,endColumn,scope}); };
    const selectTo = (node, extend) => { if(!node) return; const end={row:Number(node.dataset.row),column:Number(node.dataset.column)}; if(!extend||!anchor) anchor=end; selectRange(anchor,end); };
    grid.addEventListener('change', event => { const node=cellFrom(event); if(node && event.target.matches('input')) window.chrome?.webview?.postMessage({type:'tableCellEdit',id:element.Id,row:Number(node.dataset.row),column:Number(node.dataset.column),contentKind:node.dataset.contentKind,text:event.target.value}); });
    grid.addEventListener('dblclick', event => { const node=cellFrom(event); if(!node) return; setEditorState('cells',showEditorGuides,element.Id);window.chrome?.webview?.postMessage({type:'tableInteractionModeChanged',id:element.Id,mode:'cells'});event.preventDefault();event.stopPropagation();const row=Number(node.dataset.row),column=Number(node.dataset.column);if(node.dataset.contentKind==='InputNumeric'){anchor={row,column};selectRange(anchor,anchor);window.chrome?.webview?.postMessage({type:'tableOpenNumericProperties',id:element.Id,row,column});return;}if(node.dataset.contentKind!=='Text')return;const span=node.querySelector('span');if(!span)return;const input=document.createElement('input');input.type='text';input.value=span.textContent||'';node.replaceChild(input,span);input.focus();input.select();input.addEventListener('blur',()=>window.chrome?.webview?.postMessage({type:'tableCellEdit',id:element.Id,row,column,contentKind:'Text',text:input.value}),{once:true}); });
    grid.addEventListener('pointerdown', event => { activeTableId=element.Id;if(interactionMode!=='cells'||event.button!==0||event.isPrimary===false)return;const node=cellFrom(event);if(!node)return;event.stopPropagation();selectTo(node,event.shiftKey);cellSelectionDrag={grid,pointerId:event.pointerId}; });
    grid.addEventListener('pointerover', event => { if(interactionMode!=='cells'||cellSelectionDrag?.grid!==grid||cellSelectionDrag.pointerId!==event.pointerId||(event.buttons&1)!==1||!anchor)return;selectTo(cellFrom(event),true); });
    grid.addEventListener('contextmenu', event => { if(interactionMode!=='cells')return;const node=cellFrom(event);if(!node)return;event.preventDefault();event.stopPropagation();if(!anchor)anchor={row:Number(node.dataset.row),column:Number(node.dataset.column)};window.chrome?.webview?.postMessage({type:'contextMenuRequest',targetKind:'table',id:element.Id,row:anchor.row,column:anchor.column,endRow:Number(node.dataset.row),endColumn:Number(node.dataset.column),x:event.clientX,y:event.clientY}); });
    grid.addEventListener('keydown', event => { const node=cellFrom(event);if(!node)return;if((event.ctrlKey||event.metaKey)&&['c','v'].includes(event.key.toLowerCase())){window.chrome?.webview?.postMessage({type:'executeCommand',commandId:event.key.toLowerCase()==='c'?'table.copy':'table.paste',id:element.Id});event.preventDefault();return;}const delta={ArrowLeft:[0,-1],ArrowRight:[0,1],ArrowUp:[-1,0],ArrowDown:[1,0]}[event.key];if(!delta)return;const target=grid.querySelector(`.scada-editor-table-cell[data-row="${Number(node.dataset.row)+delta[0]}"][data-column="${Number(node.dataset.column)+delta[1]}"]`);if(target){target.focus();selectTo(target,event.shiftKey);event.preventDefault();}});
    let columnOffset = 0;
    (table.Columns || []).slice(0, -1).forEach((track, index) => {
      columnOffset += Number(track.Width || 24);
      const handle = document.createElement('div'); handle.className = 'scada-editor-table-track column'; handle.style.left = `${columnOffset - 3}px`;
      handle.addEventListener('pointerdown', event => { if (interactionMode !== 'cells') return; event.preventDefault(); event.stopPropagation(); trackDrag = { id:element.Id, orientation:'column', index, node:handle, start:event.clientX, initial:Number(track.Width || 24), size:Number(track.Width || 24), offset:columnOffset, minimum:24 }; });
      grid.appendChild(handle);
    });
    let rowOffset = 0;
    (table.Rows || []).slice(0, -1).forEach((track, index) => {
      rowOffset += Number(track.Height || 20);
      const handle = document.createElement('div'); handle.className = 'scada-editor-table-track row'; handle.style.top = `${rowOffset - 3}px`;
      handle.addEventListener('pointerdown', event => { if (interactionMode !== 'cells') return; event.preventDefault(); event.stopPropagation(); trackDrag = { id:element.Id, orientation:'row', index, node:handle, start:event.clientY, initial:Number(track.Height || 20), size:Number(track.Height || 20), offset:rowOffset, minimum:20 }; });
      grid.appendChild(handle);
    });
    const xOffsets=[0], yOffsets=[0]; (table.Columns||[]).forEach(t=>xOffsets.push(xOffsets.at(-1)+Number(t.Width||24))); (table.Rows||[]).forEach(t=>yOffsets.push(yOffsets.at(-1)+Number(t.Height||20)));
    (table.BorderOverrides||[]).forEach(item => { const border=item.Border; if(!border || Number(border.Width)<=0) return; const line=document.createElement('span'); line.className='scada-editor-table-border'; line.style.cssText='position:absolute;pointer-events:none;z-index:5;box-sizing:border-box;'; const style=typeof border.Style==='number'?['none','solid','dashed','dotted','double'][border.Style]:String(border.Style||'solid').toLowerCase(); const horizontal=Number(item.Orientation)===0 || String(item.Orientation).toLowerCase()==='horizontal'; if(horizontal){line.style.left=`${xOffsets[item.Segment]}px`;line.style.top=`${yOffsets[item.GridLine]}px`;line.style.width=`${Number(table.Columns[item.Segment].Width)}px`;line.style.borderTop=`${Number(border.Width)}px ${style} ${border.Color}`;}else{line.style.left=`${xOffsets[item.GridLine]}px`;line.style.top=`${yOffsets[item.Segment]}px`;line.style.height=`${Number(table.Rows[item.Segment].Height)}px`;line.style.borderLeft=`${Number(border.Width)}px ${style} ${border.Color}`;} grid.appendChild(line); });
    let x = 0;
    (table.Columns || []).forEach((track, index) => {
      const header = document.createElement('button'); header.type = 'button'; header.className = 'scada-editor-table-header column'; header.textContent = String.fromCharCode(65 + (index % 26)); header.style.left = `${x}px`; header.style.width = `${Number(track.Width || 24)}px`; x += Number(track.Width || 24);
      header.addEventListener('pointerdown', event => { event.preventDefault(); event.stopPropagation(); anchor = { row:0, column:index }; selectRange(anchor,{row:(table.Rows||[]).length-1,column:index},'column'); }); grid.appendChild(header);
    });
    let y = 0;
    (table.Rows || []).forEach((track, index) => {
      const header = document.createElement('button'); header.type = 'button'; header.className = 'scada-editor-table-header row'; header.textContent = String(index+1); header.style.top = `${y}px`; header.style.height = `${Number(track.Height || 20)}px`; y += Number(track.Height || 20);
      header.addEventListener('pointerdown', event => { event.preventDefault(); event.stopPropagation(); anchor = { row:index, column:0 }; selectRange(anchor,{row:index,column:(table.Columns||[]).length-1},'row'); }); grid.appendChild(header);
    });
    const corner = document.createElement('button'); corner.type='button'; corner.className='scada-editor-table-header scada-editor-table-corner'; corner.addEventListener('pointerdown', event => { event.preventDefault(); event.stopPropagation(); anchor={row:0,column:0};selectRange(anchor,{row:(table.Rows||[]).length-1,column:(table.Columns||[]).length-1},'table'); }); grid.appendChild(corner);
    wrapper.replaceChildren(grid);
  };
  const setEditorState = (mode, showGuides, tableId) => { interactionMode = mode === 'cells' ? 'cells' : 'object'; showEditorGuides = showGuides !== false; activeTableId = tableId || null; const guidesVisible = interactionMode === 'cells' && showEditorGuides; document.querySelectorAll('.scada-editor-table').forEach(grid => { grid.dataset.mode = interactionMode; grid.dataset.editorGuides = guidesVisible ? 'visible' : 'hidden'; }); };
  const setMode = mode => setEditorState(mode, showEditorGuides, activeTableId);
  const setGuides = visible => setEditorState(interactionMode, visible, activeTableId);
  const autoFit = id => {
    const grid = document.querySelector(`.scada-editor-table[data-table-id="${CSS.escape(id)}"]`); if (!grid) return;
    const cells = [...grid.querySelectorAll('.scada-editor-table-cell')];
    const canvas = document.createElement('canvas'); const context = canvas.getContext('2d');
    const desired = node => { const style=getComputedStyle(node); const input=node.querySelector('input'); let width=node.scrollWidth; if(input&&context){const inputStyle=getComputedStyle(input);context.font=inputStyle.font;const value=input.value||'';const placeholder=input.placeholder||'';width=Math.max(width,context.measureText(value).width,context.measureText(placeholder).width)+parseFloat(style.paddingLeft||0)+parseFloat(style.paddingRight||0)+4;} return {width:Math.ceil(width*2)/2,height:Math.ceil(node.scrollHeight*2)/2}; };
    const measures = cells.map(node => { const size=desired(node); return { node, row:Number(node.dataset.row), column:Number(node.dataset.column), rowSpan:Number(node.dataset.rowSpan), columnSpan:Number(node.dataset.columnSpan), width:size.width, height:size.height }; });
    const currentColumns=JSON.parse(grid.dataset.columnSizes||'[]'); const currentRows=JSON.parse(grid.dataset.rowSizes||'[]');
    const columnSizes = currentColumns.map(() => 24); const rowSizes = currentRows.map(() => 20);
    measures.filter(m=>m.columnSpan===1).forEach(m=>columnSizes[m.column]=Math.max(columnSizes[m.column],m.width));
    measures.filter(m=>m.rowSpan===1).forEach(m=>rowSizes[m.row]=Math.max(rowSizes[m.row],m.height));
    const distributeDeficit=(sizes,current,start,span,desiredSize,minimum)=>{const covered=sizes.slice(start,start+span);const deficit=desiredSize-covered.reduce((a,b)=>a+b,0);if(deficit<=0)return;const weights=current.slice(start,start+span);const total=weights.reduce((a,b)=>a+b,0)||span;for(let i=0;i<span;i++)sizes[start+i]=Math.max(minimum,sizes[start+i]+deficit*(weights[i]||1)/total);};
    measures.filter(m=>m.columnSpan>1).forEach(m=>distributeDeficit(columnSizes,currentColumns,m.column,m.columnSpan,m.width,24));
    measures.filter(m=>m.rowSpan>1).forEach(m=>distributeDeficit(rowSizes,currentRows,m.row,m.rowSpan,m.height,20));
    for(let i=0;i<columnSizes.length;i++)columnSizes[i]=Math.ceil(columnSizes[i]*2)/2; for(let i=0;i<rowSizes.length;i++)rowSizes[i]=Math.ceil(rowSizes[i]*2)/2;
    window.chrome?.webview?.postMessage({type:'tableAutoFitMeasured', id, columnSizes, rowSizes});
  };
  document.addEventListener('keydown', event => { if (event.key === 'Escape' && interactionMode === 'cells') { const id=event.target?.closest?.('.scada-editor-table')?.dataset?.tableId||activeTableId;setEditorState('object',showEditorGuides,id);if(id)window.chrome?.webview?.postMessage({type:'tableInteractionModeChanged',id,mode:'object'});event.preventDefault();event.stopPropagation(); } }, true);
  window.scadaModernTable = { render, setEditorState, setMode, setGuides, autoFit };
})();
""";
}
