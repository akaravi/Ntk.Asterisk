export type CallJobType = 'ExtToExt' | 'MobileToExt' | 'MobileToMobile';

export type CallJobStatus =
  | 'queued'
  | 'dialing_leg1'
  | 'waiting_answer'
  | 'dialing_leg2'
  | 'bridged'
  | 'completed'
  | 'failed'
  | 'cancelled';

export interface CallJob {
  id: string;
  type: CallJobType;
  status: CallJobStatus;
  from?: string | null;
  to?: string | null;
  mobile1?: string | null;
  mobile2?: string | null;
  timeoutSec?: number | null;
  errorMessage?: string | null;
  createdAtUtc?: string | null;
  updatedAtUtc?: string | null;
}

export interface CallJobAddRequest {
  type: CallJobType;
  from?: string;
  to?: string;
  mobile1?: string;
  mobile2?: string;
  timeoutSec?: number;
}
