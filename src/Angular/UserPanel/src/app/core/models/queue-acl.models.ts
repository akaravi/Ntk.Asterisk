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
