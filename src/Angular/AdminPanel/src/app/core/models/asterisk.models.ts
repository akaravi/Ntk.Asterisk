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
  serverId?: string | null;
  serverName?: string | null;
  username?: string | null;
  isEnabled?: boolean;
  isDefault?: boolean;
  isLiveSession?: boolean;
}

export interface PeerItem {
  id: string;
  tech: string;
  status: string;
  ip: string | null;
  channel: string | null;
  isTrunk?: boolean;
  lastActivityUtc?: string | null;
  callState?: string | null;
  callDurationSeconds?: number | null;
  callerId?: string | null;
  inCall?: boolean;
}

export interface ChannelItem {
  channel: string;
  uniqueId: string | null;
  callerId: string | null;
  state: string | null;
  application: string | null;
  durationSec?: number | null;
  durationSeconds?: number | null;
  lastActivityUtc?: string | null;
}

export interface TrunkItem {
  id: string;
  tech: string;
  status: string;
  ip: string | null;
  lastActivityUtc?: string | null;
  callState?: string | null;
  callDurationSeconds?: number | null;
  callerId?: string | null;
  inCall?: boolean;
  channel?: string | null;
}

/** Unified monitor tile (All tab). */
export interface MonitorUnifiedItem {
  key: string;
  kind: 'extension' | 'trunk' | 'channel';
  title: string;
  status: string;
  tone: 'ok' | 'fail' | 'active' | 'warn' | 'neutral';
  tech?: string | null;
  ip?: string | null;
  detail?: string | null;
  lastActivityUtc?: string | null;
  durationSec?: number | null;
  channel?: string | null;
}

export interface HangupRequest {
  channel: string;
}

/** Live AMI / app / system event for Admin console. */
export interface LiveEventItem {
  id: string;
  atUtc: string;
  source: 'ami' | 'app' | 'system' | string;
  category: string;
  level: string;
  message: string;
  channel?: string | null;
  uniqueId?: string | null;
  privilege?: string | null;
  attributes?: Record<string, string> | null;
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
  hasRecording?: boolean;
  recordingFileName?: string | null;
  recordingAvailable?: boolean;
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
  musicOnHoldClass: string;
  defaultTrunk: string | null;
  trunkPeerFilter: string | null;
  defaultTimeoutMs: number;
  defaultCallerId: string | null;
  keepAlive: boolean;
  pingIntervalMs: number;
  autoConnectOnStartup: boolean;
  recordingEnabled?: boolean;
  recordingLocalDirectory?: string | null;
  recordingHttpBaseUrl?: string | null;
  recordingAsteriskDirectory?: string | null;
  recordingFormat?: string;
  persisted: boolean;
  note: string | null;
  serverId?: string | null;
  serverName?: string | null;
  isEnabled?: boolean;
  isDefault?: boolean;
}

/** One Asterisk AMI server in the multi-server registry. */
export interface AsteriskServer {
  id: string;
  name: string;
  isEnabled: boolean;
  isDefault: boolean;
  amiConfigured: boolean;
  host: string | null;
  port: number | null;
  username: string | null;
  secretConfigured: boolean;
  channelTech: string;
  originateVia: string;
  originateContext: string;
  musicOnHoldClass: string;
  defaultTrunk: string | null;
  trunkPeerFilter: string | null;
  defaultTimeoutMs: number;
  defaultCallerId: string | null;
  keepAlive: boolean;
  pingIntervalMs: number;
  autoConnectOnStartup: boolean;
  recordingEnabled?: boolean;
  recordingLocalDirectory?: string | null;
  recordingHttpBaseUrl?: string | null;
  recordingAsteriskDirectory?: string | null;
  recordingFormat?: string;
}

export interface SiteSettingsUpdateRequest {
  serverId?: string | null;
  host?: string | null;
  port?: number | null;
  username?: string | null;
  secret?: string | null;
  clearSecret?: boolean;
  channelTech?: string | null;
  originateVia?: string | null;
  originateContext?: string | null;
  musicOnHoldClass?: string | null;
  defaultTrunk?: string | null;
  trunkPeerFilter?: string | null;
  defaultTimeoutMs?: number | null;
  defaultCallerId?: string | null;
  keepAlive?: boolean | null;
  pingIntervalMs?: number | null;
  autoConnectOnStartup?: boolean | null;
  recordingEnabled?: boolean | null;
  recordingLocalDirectory?: string | null;
  recordingHttpBaseUrl?: string | null;
  recordingAsteriskDirectory?: string | null;
  recordingFormat?: string | null;
  reconnectAfterSave?: boolean;
}

export interface AsteriskServerUpdateRequest extends SiteSettingsUpdateRequest {
  id: string;
  name?: string | null;
  isEnabled?: boolean | null;
  isDefault?: boolean | null;
}

export interface AsteriskServerAddRequest {
  name?: string | null;
  isEnabled?: boolean;
  isDefault?: boolean;
  host?: string | null;
  port?: number | null;
  username?: string | null;
  secret?: string | null;
  channelTech?: string | null;
  originateVia?: string | null;
  originateContext?: string | null;
  musicOnHoldClass?: string | null;
  defaultTrunk?: string | null;
  trunkPeerFilter?: string | null;
  defaultTimeoutMs?: number | null;
  defaultCallerId?: string | null;
  keepAlive?: boolean | null;
  pingIntervalMs?: number | null;
  autoConnectOnStartup?: boolean | null;
  recordingEnabled?: boolean | null;
  recordingLocalDirectory?: string | null;
  recordingHttpBaseUrl?: string | null;
  recordingAsteriskDirectory?: string | null;
  recordingFormat?: string | null;
  reconnectAfterSave?: boolean;
}
