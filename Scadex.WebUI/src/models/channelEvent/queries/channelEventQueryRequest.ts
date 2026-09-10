/**
 * Ayna: Scadex.Model/Dtos/ChannelEvent/Queries/ChannelEventQueryRequest.cs
 * Sözleşme: docs/api-contract/12-channel-events.md
 *
 * Serbest filtre YOKTUR ve olmayacaktır: uç, yalnızca iki indeksin
 * (`CabinetId+OccurredAtUtc`, `IoChannelId+OccurredAtUtc`) cevaplayabildiği
 * soruları kabul eder.
 */
export interface ChannelEventQueryRequest {
  cabinetId: string;
  /** Tek bir kanala daraltmak için. */
  ioChannelId?: string | null;
  /** `occurredAtUtc >=` (dahil) — ISO 8601. */
  fromUtc?: string | null;
  /** `occurredAtUtc <=` (dahil) — ISO 8601. */
  toUtc?: string | null;
  page?: number;
  /**
   * Sunucu tavanı **1000** (`QueryablePaginationExtension.MaxPageSize`) ve aşan
   * değer 400 DÖNDÜRMEZ, sessizce kırpılır — gelen yanıttaki `pageSize`
   * gönderdiğinizden küçük olabilir.
   */
  pageSize?: number;
}

/**
 * Ayna: Scadex.Core/Utils/Pagination/PaginationResponse.cs
 *
 * Bu, "yanıt zarfı yoktur" kuralının istisnası DEĞİL: o kural hata/başarı
 * zarfına dair; sayfalama meta verisi verinin kendisidir.
 */
export interface PaginationResponse<T> {
  page: number;
  pageSize: number;
  dataCount: number;
  pageCount: number;
  data: T[];
  hasPrevious: boolean;
  hasNext: boolean;
}
