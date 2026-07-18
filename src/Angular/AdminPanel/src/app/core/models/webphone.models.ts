/** WebPhone API contracts — mirrors WebApi WebPhoneDtos. */
export interface WebPhoneOptions {
  enableTransfer: boolean;
  enableConference: boolean;
  enableRecordAll: boolean;
  enableVideo: boolean;
  enablePresence: boolean;
  enableMwi: boolean;
  requireApiKey: boolean;
}

export interface WebPhoneExtension {
  id: string;
  sipUsername: string;
  profileName: string;
  serverId?: string | null;
  isEnabled: boolean;
  secretConfigured: boolean;
}

export interface WebPhoneExtensionUpsert {
  id?: string | null;
  sipUsername: string;
  sipPassword?: string | null;
  profileName?: string | null;
  serverId?: string | null;
  isEnabled?: boolean | null;
}

export interface WebPhoneBuddy {
  id: string;
  type: string;
  displayName: string;
  extensionNumber?: string | null;
  description?: string | null;
  mobileNumber?: string | null;
  email?: string | null;
  contactNumber1?: string | null;
  contactNumber2?: string | null;
  subscribe: boolean;
  subscribeUser?: string | null;
  enableDuringDnd: boolean;
  updatedAtUtc: string;
}

export interface WebPhoneBuddyUpsert {
  id?: string | null;
  type?: string | null;
  displayName: string;
  extensionNumber?: string | null;
  description?: string | null;
  mobileNumber?: string | null;
  email?: string | null;
  contactNumber1?: string | null;
  contactNumber2?: string | null;
  subscribe?: boolean | null;
  subscribeUser?: string | null;
  enableDuringDnd?: boolean | null;
}

export interface WebPhoneCdr {
  id: string;
  buddyId?: string | null;
  direction?: string | null;
  withNumber?: string | null;
  displayName?: string | null;
  startedAtUtc: string;
  endedAtUtc?: string | null;
  durationSeconds?: number | null;
  disposition?: string | null;
  recordingId?: string | null;
  notes?: string | null;
}

export interface WebPhoneRecording {
  id: string;
  fileName: string;
  contentType?: string | null;
  sizeBytes: number;
  buddyId?: string | null;
  cdrId?: string | null;
  notes?: string | null;
  createdAtUtc: string;
}

export interface WebPhoneQos {
  id: string;
  callId?: string | null;
  buddyId?: string | null;
  mos?: number | null;
  packetLossPct?: number | null;
  jitterMs?: number | null;
  rttMs?: number | null;
  rawJson?: string | null;
  atUtc: string;
}
