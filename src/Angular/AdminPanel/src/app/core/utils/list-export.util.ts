export interface ExportColumn {
  key: string;
  header: string;
}

function escapeCsv(value: unknown): string {
  const raw = value == null ? '' : String(value);
  if (/[",\n\r]/.test(raw)) {
    return `"${raw.replace(/"/g, '""')}"`;
  }
  return raw;
}

function cellValue(row: Record<string, unknown>, key: string): unknown {
  return key.split('.').reduce<unknown>((acc, part) => {
    if (acc && typeof acc === 'object') {
      return (acc as Record<string, unknown>)[part];
    }
    return undefined;
  }, row);
}

/** Excel-compatible UTF-8 CSV download (BOM). */
export function downloadListAsExcel(
  fileBase: string,
  columns: ExportColumn[],
  rows: Record<string, unknown>[]
): void {
  const header = columns.map((c) => escapeCsv(c.header)).join(',');
  const body = rows
    .map((row) => columns.map((c) => escapeCsv(cellValue(row, c.key))).join(','))
    .join('\r\n');
  const csv = `\uFEFF${header}\r\n${body}`;
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
  triggerDownload(blob, `${fileBase}.csv`);
}

/** Printable HTML table — browser print dialog (Save as PDF). */
export function downloadListAsPdf(
  title: string,
  columns: ExportColumn[],
  rows: Record<string, unknown>[]
): void {
  const head = columns.map((c) => `<th>${escapeHtml(c.header)}</th>`).join('');
  const body = rows
    .map((row) => {
      const cells = columns
        .map((c) => `<td>${escapeHtml(String(cellValue(row, c.key) ?? ''))}</td>`)
        .join('');
      return `<tr>${cells}</tr>`;
    })
    .join('');
  const html = `<!doctype html><html lang="fa" dir="rtl"><head><meta charset="utf-8"/><title>${escapeHtml(
    title
  )}</title>
<style>
body{font-family:Segoe UI,Tahoma,sans-serif;padding:16px;color:#111}
h1{font-size:18px;margin:0 0 12px}
table{border-collapse:collapse;width:100%;font-size:12px}
th,td{border:1px solid #ccc;padding:6px 8px;text-align:start}
th{background:#f2f2f2}
@media print{body{padding:0}}
</style></head><body>
<h1>${escapeHtml(title)}</h1>
<table><thead><tr>${head}</tr></thead><tbody>${body || `<tr><td colspan="${columns.length}">—</td></tr>`}</tbody></table>
<script>window.onload=function(){window.print();}</script>
</body></html>`;
  const w = window.open('', '_blank', 'noopener,noreferrer,width=960,height=720');
  if (!w) return;
  w.document.open();
  w.document.write(html);
  w.document.close();
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function triggerDownload(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.rel = 'noopener';
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}
