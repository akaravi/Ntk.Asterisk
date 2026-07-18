export type QueueAclRole = 'viewer' | 'admin';

export interface QueueAclUser {
  id: string;
  username: string;
  isEnabled: boolean;
  role: QueueAclRole | string;
  allowedQueues: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface QueueAclUserUpsertRequest {
  id?: string | null;
  username: string;
  password?: string | null;
  isEnabled: boolean;
  role: string;
  allowedQueues: string[];
}

export interface QueueAclSession {
  username: string;
  role: string;
  token: string;
  expiresAtUtc: string;
  allowedQueues: string[];
  isAdmin: boolean;
}

export interface QueueAclStatus {
  requireLogin: boolean;
  gateActive: boolean;
  isAuthenticated: boolean;
  username?: string | null;
  role?: string | null;
  allowedQueues: string[];
  isAdmin: boolean;
  userCount: number;
}

export interface QueueStatsSample {
  snapshotUtc: string;
  realName: string;
  name: string;
  callsWaiting: number;
  completed: number;
  abandoned: number;
  abandonedPercent: number;
  holdtimeSeconds: number;
  talkTimeSeconds: number;
  serviceLevelPerf: number;
  memberCount: number;
  pausedMemberCount: number;
  inCallMemberCount: number;
}
