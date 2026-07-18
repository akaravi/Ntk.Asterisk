export interface WebPhoneBuddy {
  id: string;
  type: string;
  displayName: string;
  extensionNumber?: string | null;
  description?: string | null;
  mobileNumber?: string | null;
  email?: string | null;
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
}

export interface WebPhoneCdr {
  id: string;
  direction?: string | null;
  withNumber?: string | null;
  displayName?: string | null;
  startedAtUtc: string;
  endedAtUtc?: string | null;
  durationSeconds?: number | null;
  disposition?: string | null;
}

export interface WebPhoneRecording {
  id: string;
  fileName: string;
  sizeBytes: number;
  createdAtUtc: string;
}
