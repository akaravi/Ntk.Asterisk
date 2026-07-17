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
  /** Canonical from API (`state`); some payloads also send `status`. */
  status: CallJobStatus;
  state?: CallJobStatus;
  from?: string | null;
  to?: string | null;
  mobile1?: string | null;
  mobile2?: string | null;
  timeoutSec?: number | null;
  errorMessage?: string | null;
  /** Success or failure reason from API. */
  resultReason?: string | null;
  createdAtUtc?: string | null;
  /** Alias when API sends callTimeUtc. */
  callTimeUtc?: string | null;
  updatedAtUtc?: string | null;
  startedAtUtc?: string | null;
  endedAtUtc?: string | null;
  durationSeconds?: number | null;
  hasRecording?: boolean;
  recordingFileName?: string | null;
  recordingAvailable?: boolean;
}

export function resolveJobStatus(job: Pick<CallJob, 'status' | 'state'>): CallJobStatus | undefined {
  return job.status ?? job.state;
}

export interface CallJobAddRequest {
  type: CallJobType;
  from?: string;
  to?: string;
  mobile1?: string;
  mobile2?: string;
  timeoutSec?: number;
}
