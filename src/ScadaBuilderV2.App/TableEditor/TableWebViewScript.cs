namespace ScadaBuilderV2.App.TableEditor;

/// <summary>Owns the table-specific WebView renderer and cell interaction bridge.</summary>
internal static class TableWebViewScript
{
    public const string Source = """
(() => {
  if (window.scadaModernTable) return;
  const css = document.createElement('style');
  css.textContent = `
    .scada-editor-table { display:grid; width:100%; height:100%; overflow:hidden; }
    .scada-editor-table-cell { display:flex; box-sizing:border-box; min-width:0; min-height:0; overflow:hidden; position:relative; }
    .scada-editor-table-cell[data-selected="true"] { outline:2px solid #2090a0; outline-offset:-2px; z-index:2; }
    .scada-editor-table-cell input { width:100%; height:100%; min-width:0; border:0; padding:0; background:transparent; color:inherit; font:inherit; }
    .scada-editor-table-track { position:absolute; z-index:4; background:transparent; }
    .scada-editor-table-track.column { top:0; bottom:0; width:7px; cursor:col-resize; }
    .scada-editor-table-track.row { left:0; right:0; height:7px; cursor:row-resize; }
  `;
  document.head.appendChild(css);
  let trackDrag = null;
  document.addEventListener('pointermove', event => {
    if (!trackDrag) return;
    const delta = trackDrag.orientation === 'column' ? event.clientX - trackDrag.start : event.clientY - trackDrag.start;
    trackDrag.size = Math.max(trackDrag.minimum, trackDrag.initial + delta);
    if (trackDrag.orientation === 'column') trackDrag.node.style.left = `${trackDrag.offset + delta - 3}px`;
    else trackDrag.node.style.top = `${trackDrag.offset + delta - 3}px`;
    event.preventDefault();
  }, true);
  document.addEventListener('pointerup', event => {
    if (!trackDrag) return;
    window.chrome?.webview?.postMessage({ type:'tableTrackResize', id:trackDrag.id, orientation:trackDrag.orientation, trackIndex:trackDrag.index, trackSize:trackDrag.size });
    trackDrag = null; event.preventDefault();
  }, true);

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
    const grid = document.createElement('div'); grid.className = 'scada-editor-table'; grid.dataset.tableId = element.Id; grid.style.position = 'relative';
    grid.style.gridTemplateColumns = (table.Columns || []).map(item => `${Number(item.Width || 24)}px`).join(' ');
    grid.style.gridTemplateRows = (table.Rows || []).map(item => `${Number(item.Height || 20)}px`).join(' ');
    let anchor = null;
    (table.Cells || []).forEach(cell => {
      const node = document.createElement('div'); node.className = 'scada-editor-table-cell'; node.dataset.row = cell.Row; node.dataset.column = cell.Column;
      node.style.gridRow = `${Number(cell.Row)+1} / span ${Math.max(1, Number(cell.RowSpan || 1))}`;
      node.style.gridColumn = `${Number(cell.Column)+1} / span ${Math.max(1, Number(cell.ColumnSpan || 1))}`;
      const format = effective(table, Number(cell.Row), Number(cell.Column), cell);
      node.style.background = format.Background || '#fff'; node.style.color = format.Foreground || '#0f2a30';
      node.style.border = `${Number(format.GridWidth ?? 1)}px ${String(format.GridStyle || 'solid').toLowerCase()} ${format.GridColor || '#8aa0a6'}`;
      node.style.padding = `${Number(format.Padding ?? 4)}px`; node.style.alignItems = String(format.VerticalAlignment || 'middle').toLowerCase() === 'top' ? 'flex-start' : (String(format.VerticalAlignment || '').toLowerCase() === 'bottom' ? 'flex-end' : 'center');
      node.style.textAlign = String(format.HorizontalAlignment || 'left').toLowerCase(); node.style.fontFamily = format.FontFamily || 'Segoe UI'; node.style.fontSize = `${Number(format.FontSize || 14)}px`; node.style.fontWeight = format.FontWeight || 'normal';
      const content = cell.Content || {}; const kind = String(content.Kind || 'Text');
      if (kind === 'InputText' || kind === 'InputNumeric') {
        const input = document.createElement('input'); input.type = kind === 'InputNumeric' ? 'number' : 'text'; input.value = kind === 'InputNumeric' ? (content.NumericValue ?? content.Text ?? '') : (content.Text || ''); input.placeholder = content.Placeholder || ''; input.readOnly = content.IsReadOnly === true;
        ['Minimum','Maximum','Step'].forEach((name, index) => { if (content[name] != null) input[['min','max','step'][index]] = content[name]; });
        input.addEventListener('change', () => window.chrome?.webview?.postMessage({ type:'tableCellEdit', id:element.Id, row:Number(cell.Row), column:Number(cell.Column), contentKind:kind, text:input.value }));
        node.appendChild(input);
      } else { const span = document.createElement('span'); span.textContent = content.Text || ''; node.appendChild(span); }
      node.addEventListener('pointerdown', event => {
        event.stopPropagation();
        if (!event.shiftKey || !anchor) anchor = { row:Number(cell.Row), column:Number(cell.Column) };
        const end = { row:Number(cell.Row), column:Number(cell.Column) };
        grid.querySelectorAll('.scada-editor-table-cell').forEach(item => { const r=Number(item.dataset.row), c=Number(item.dataset.column); item.dataset.selected = r>=Math.min(anchor.row,end.row)&&r<=Math.max(anchor.row,end.row)&&c>=Math.min(anchor.column,end.column) ? 'true':'false'; });
        window.chrome?.webview?.postMessage({ type:'tableSelection', id:element.Id, row:anchor.row, column:anchor.column, endRow:end.row, endColumn:end.column });
      });
      node.addEventListener('contextmenu', event => {
        event.preventDefault(); event.stopPropagation();
        if (!anchor) anchor = { row:Number(cell.Row), column:Number(cell.Column) };
        window.chrome?.webview?.postMessage({ type:'contextMenuRequest', targetKind:'table', id:element.Id, row:anchor.row, column:anchor.column, endRow:Number(cell.Row), endColumn:Number(cell.Column), x:event.clientX, y:event.clientY });
      });
      grid.appendChild(node);
    });
    let columnOffset = 0;
    (table.Columns || []).slice(0, -1).forEach((track, index) => {
      columnOffset += Number(track.Width || 24);
      const handle = document.createElement('div'); handle.className = 'scada-editor-table-track column'; handle.style.left = `${columnOffset - 3}px`;
      handle.addEventListener('pointerdown', event => { event.preventDefault(); event.stopPropagation(); trackDrag = { id:element.Id, orientation:'column', index, node:handle, start:event.clientX, initial:Number(track.Width || 24), size:Number(track.Width || 24), offset:columnOffset, minimum:24 }; });
      grid.appendChild(handle);
    });
    let rowOffset = 0;
    (table.Rows || []).slice(0, -1).forEach((track, index) => {
      rowOffset += Number(track.Height || 20);
      const handle = document.createElement('div'); handle.className = 'scada-editor-table-track row'; handle.style.top = `${rowOffset - 3}px`;
      handle.addEventListener('pointerdown', event => { event.preventDefault(); event.stopPropagation(); trackDrag = { id:element.Id, orientation:'row', index, node:handle, start:event.clientY, initial:Number(track.Height || 20), size:Number(track.Height || 20), offset:rowOffset, minimum:20 }; });
      grid.appendChild(handle);
    });
    wrapper.appendChild(grid);
  };
  window.scadaModernTable = { render };
})();
""";
}
