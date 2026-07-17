export interface ApiResult<T> {
  isSuccess: boolean;
  data: T[];
  errorMessage: string | null;
  totalCount?: number;
  pageIndex?: number;
  pageSize?: number;
}
