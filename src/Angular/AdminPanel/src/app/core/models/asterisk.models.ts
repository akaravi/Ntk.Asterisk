export interface ConnectionStatus {
  connected: boolean;
  configured?: boolean;
  host?: string | null;
  port?: number | null;
  lastError: string | null;
  uptimeSeconds: number | null;
  asteriskVersion?: string | null;
  amiHostConfigured: boolean;
  amiPortConfigured: boolean;
  amiUserConfigured: boolean;
  amiSecretConfigured: boolean;
  channelTech: string | null;
  defaultTrunk?: string | null;
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
  resultReason?: string | null;
  createdAtUtc: string;
  callTimeUtc?: string;
  updatedAtUtc: string;
  startedAtUtc?: string | null;
  endedAtUtc?: string | null;
  durationSeconds?: number | null;
}

export interface ListQuery {
  pageIndex: number;
  pageSize: number;
  sortBy: string;
  sortDir: 'asc' | 'desc';
  quickSearch: string;
  filter: Record<string, string>;
}

/** AMI connection settings managed via Admin (secret never returned). */
export interface SiteSettings {
  amiConfigured: boolean;
  host: string | null;
  port: number | null;
  username: string | null;
  usernameConfigured: string | null;
  secretConfigured: boolean;
  channelTech: string;
  originateVia: string;
  originateContext: string;
  defaultTrunk: string | null;
  trunkPeerFilter: string | null;
  defaultTimeoutMs: number;
  defaultCallerId: string | null;
  keepAlive: boolean;
  pingIntervalMs: number;
  autoConnectOnStartup: boolean;
  persisted: boolean;
  note: string | null;
}

export interface SiteSettingsUpdateRequest {
  host?: string | null;
  port?: number | null;
  username?: string | null;
  secret?: string | null;
  clearSecret?: boolean;
  channelTech?: string | null;
  originateVia?: string | null;
  originateContext?: string | null;
  defaultTrunk?: string | null;
  trunkPeerFilter?: string | null;
  defaultTimeoutMs?: number | null;
  defaultCallerId?: string | null;
  keepAlive?: boolean | null;
  pingIntervalMs?: number | null;
  autoConnectOnStartup?: boolean | null;
  reconnectAfterSave?: boolean;
}
