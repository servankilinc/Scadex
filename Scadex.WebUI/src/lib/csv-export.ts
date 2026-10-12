import type { PaginationResponse } from '@/models/channelEvent';

/** CSV'nin tek kolonu. Değer `null`/`undefined` ise hücre boş kalır. */
export interface CsvColumn<T> {
  header: string;
  value: (row: T) => string | number | null | undefined;
}

/**
 * Ayraç `;`: Türkçe bölgesel ayarlı Excel liste ayracı olarak `;` bekler (ondalık ayracı virgül olduğu için); `,` ile
 * yazılan dosya tek kolona yığılırdı. Başa UTF-8 BOM konur, yoksa Excel ç/ş/ğ/ı'yı bozuk açar.
 */
const DELIMITER = ';';
const BOM = '﻿';

/**
 * Excel `=`, `+`, `-`, `@` ile başlayan metni formül sayar. Hücreye kullanıcı kaynaklı metin de girer (dosya adı, PC adı):
 * metnin başına `'` konur, formül çalışmaz. Sayılar (`number`) dokunulmadan yazılır — eksi sayı formül değildir.
 */
const FORMULA_PREFIX = /^[=+\-@\t\r]/;

function toCell(value: string | number | null | undefined): string {
  if (value == null) return '';
  if (typeof value === 'number') return Number.isFinite(value) ? String(value) : '';

  const text = FORMULA_PREFIX.test(value) ? `'${value}` : value;
  return /[";\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

/** Satırları CSV metnine çevirip tarayıcıya dosya olarak indirtir. Sunucuya hiçbir şey gitmez. */
export function downloadCsv<T>(fileName: string, columns: CsvColumn<T>[], rows: T[]) {
  const lines = [columns.map(column => toCell(column.header)), ...rows.map(row => columns.map(column => toCell(column.value(row))))];
  const content = BOM + lines.map(cells => cells.join(DELIMITER)).join('\r\n');

  const url = URL.createObjectURL(new Blob([content], { type: 'text/csv;charset=utf-8' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName.endsWith('.csv') ? fileName : `${fileName}.csv`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

/**
 * Sunucu sayfalı bir listenin filtreye uyan TÜM satırlarını sırayla çeker — dışa aktarma ekrandaki sayfayla sınırlı kalmasın.
 * `maxRows` tavanı tarayıcıyı ve sunucuyu korur (denetim tabloları sınırsız büyür, saklama işi yok); `truncated` tavana
 * takılındığını söyler, ekran kullanıcıyı uyarır.
 */
export async function fetchAllPages<T>(
  fetchPage: (page: number) => Promise<PaginationResponse<T>>,
  maxRows: number
): Promise<{ rows: T[]; truncated: boolean }> {
  const rows: T[] = [];
  for (let page = 1; ; page++) {
    const response = await fetchPage(page);
    rows.push(...response.data);
    if (rows.length >= maxRows) return { rows: rows.slice(0, maxRows), truncated: response.dataCount > maxRows };
    if (!response.hasNext || response.data.length === 0) return { rows, truncated: false };
  }
}

/** Dosya adına eklenen yerel gün: `2026-10-12`. */
export function exportDateStamp(date = new Date()): string {
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}
