export interface ConnectionStatus {
  connected: boolean;
  lastError: string | null;
  uptimeSeconds: number | null;
  amiHostConfigured: boolean;
  amiPortConfigured: boolean;
  amiUserConfigured: boolean;
  amiSecretConfigured: boolean;
  channelTech: string | null;
  trunkPeerFilterConfigured: boolean;
}

export interface PeerItem {
  id: string;
  tech: string;
  status: string;
  ip: string | null;
  channel: string | null;
}

export interface ChannelItem {
  channel: string;
  uniqueId: string | null;
  callerId: string | null;
  state: string | null;
  application: string | null;
  durationSec: number | null;
}

export interface TrunkItem {
  id: string;
  tech: string;
  status: string;
  ip: string | null;
}

export interface HangupRequest {
  channel: string;
}

export type CallJobType = 'ExtToExt' | 'MobileToExt' | 'MobileToMobile' | 'CommandHangup';

export type CallJobState =
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
  type: CallJobType | string;
  state: CallJobState | string;
  from: string | null;
  to: string | null;
  mobile1: string | null;
  mobile2: string | null;
  timeoutSec: number | null;
  errorMessage: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface ListQuery {
  pageIndex: number;
  pageSize: number;
  sortBy: string;
  sortDir: 'asc' | 'desc';
  quickSearch: string;
  filter: Record<string, string>;
}
