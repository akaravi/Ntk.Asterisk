import { ListQuery } from '../models/asterisk.models';

const PAGE_SIZE_OPTIONS = [10, 25, 50, 100] as const;

export function defaultListQuery(sortBy = 'id'): ListQuery {
  return {
    pageIndex: 0,
    pageSize: 25,
    sortBy,
    sortDir: 'asc',
    quickSearch: '',
    filter: {},
  };
}

export function pageSizeOptions(): readonly number[] {
  return PAGE_SIZE_OPTIONS;
}

export function applyClientList<T extends Record<string, unknown>>(
  items: T[],
  query: ListQuery,
  searchFields: (keyof T)[]
): { rows: T[]; totalCount: number } {
  let filtered = [...items];

  const q = (query.quickSearch || '').trim().toLowerCase();
  if (q) {
    filtered = filtered.filter((row) =>
      searchFields.some((field) => String(row[field] ?? '').toLowerCase().includes(q))
    );
  }

  for (const [key, value] of Object.entries(query.filter || {})) {
    const v = (value || '').trim().toLowerCase();
    if (!v) continue;
    filtered = filtered.filter((row) => String(row[key] ?? '').toLowerCase().includes(v));
  }

  const sortBy = query.sortBy || 'id';
  const dir = query.sortDir === 'desc' ? -1 : 1;
  filtered.sort((a, b) => {
    const av = a[sortBy];
    const bv = b[sortBy];
    if (av == null && bv == null) return 0;
    if (av == null) return -1 * dir;
    if (bv == null) return 1 * dir;
    if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
    return String(av).localeCompare(String(bv), undefined, { numeric: true }) * dir;
  });

  const totalCount = filtered.length;
  const start = query.pageIndex * query.pageSize;
  const rows = filtered.slice(start, start + query.pageSize);
  return { rows, totalCount };
}
