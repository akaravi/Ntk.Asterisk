/** AMI QueueStatus snapshot — Queue Panel parity (type aliases avoid `interface` keyword). */

export type QueueItem = {
  name: string;
  /** AMI queue name for pause/hangup (equals name when not renamed). */
  realName?: string;
  strategy?: string | null;
  max: number;
  callsWaiting: number;
  holdtimeSeconds: number;
  talkTimeSeconds: number;
  completed: number;
  abandoned: number;
  serviceLevelSeconds: number;
  serviceLevelPerf: number;
  abandonedPercent: number;
  weight: number;
  members: QueueMemberItem[];
  entries: QueueEntryItem[];
  snapshotUtc: string;
};

export type QueueMemberItem = {
  interface: string;
  stateInterface?: string | null;
  name?: string | null;
  membership?: string | null;
  penalty: number;
  callsTaken: number;
  lastCallEpoch: number;
  lastCallAgoSeconds?: number | null;
  status: number;
  statusLabel: string;
  paused: boolean;
  pausedReason?: string | null;
  inCall: boolean;
  lastPauseEpoch: number;
  lastPauseAgoSeconds?: number | null;
};

export type QueueEntryItem = {
  position: number;
  channel?: string | null;
  uniqueId?: string | null;
  callerId?: string | null;
  callerIdName?: string | null;
  waitSeconds: number;
  priority: number;
};

export type QueueMemberPauseRequest = {
  interface: string;
  queue?: string | null;
  reason?: string | null;
};
