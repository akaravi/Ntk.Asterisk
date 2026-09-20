export interface FastAgiPortStatus {
  port: number;
  isListening: boolean;
  status: string; // 'Listening' | 'Offline' | 'Error'
  errorMessage?: string | null;
  lastCheckedAtUtc: string;
}

export interface FastAgiStatus {
  ports: FastAgiPortStatus[];
  totalPacketsReceived: number;
  lastPacketAtUtc?: string | null;
  lastClientIp?: string | null;
  overallStatus: string;
}

export interface FastAgiPacket {
  id: string;
  port: number;
  scriptName: string;
  callerId: string;
  callerIdName?: string | null;
  channel?: string | null;
  uniqueId?: string | null;
  context?: string | null;
  extension?: string | null;
  priority?: string | null;
  remoteClientIp?: string | null;
  timestampUtc: string;
  headers: Record<string, string>;
  commands: string[];
  status: string; // 'Processing' | 'Completed' | 'Error'
  note?: string | null;
}
