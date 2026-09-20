export interface CallRoute {
  id: string;
  callerNumber: string;
  contactName?: string | null;
  description?: string | null;
  targetExtension?: string | null;
  targetExternalNumber?: string | null;
  extensionTimeout: number;
  externalTimeout: number;
  outboundTrunk?: string | null;
  isActive: boolean;
  priority: number;
  createdAt: string;
  updatedAt: string;
}

export interface CallRouteUpsertRequest {
  id?: string | null;
  callerNumber: string;
  contactName?: string | null;
  description?: string | null;
  targetExtension?: string | null;
  targetExternalNumber?: string | null;
  extensionTimeout?: number;
  externalTimeout?: number;
  outboundTrunk?: string | null;
  isActive?: boolean;
  priority?: number;
}

export interface CallRouteOrderItem {
  id: string;
  priority: number;
}

export interface CallRouteReorderRequest {
  items: CallRouteOrderItem[];
}

export interface CallRouteLookupRequest {
  callerNumber: string;
  channel?: string | null;
  uniqueId?: string | null;
  source?: string | null;
  dnis?: string | null;
}

export interface SmartRouteStep {
  stepOrder: number;
  ruleId: string;
  contactName?: string | null;
  destinationExtension?: string | null;
  destinationExternalNumber?: string | null;
  extensionTimeoutSeconds: number;
  externalTimeoutSeconds: number;
  outboundTrunk?: string | null;
  priority: number;
}

export interface SmartRouteDecision {
  id: string;
  callerNumber: string;
  normalizedCallerNumber: string;
  contactName?: string | null;
  channel?: string | null;
  uniqueId?: string | null;
  source: string;
  matched: boolean;
  action: string;
  destinationExtension?: string | null;
  destinationExternalNumber?: string | null;
  outboundTrunk?: string | null;
  timeoutSeconds: number;
  extensionTimeoutSeconds: number;
  externalTimeoutSeconds: number;
  reason: string;
  matchedRuleId?: string | null;
  timestampUtc: string;
  status: string;
  note?: string | null;
}

export interface CallRouteLookupResult {
  matched: boolean;
  route?: CallRoute | null;
  steps?: SmartRouteStep[];
  action: string;
  destinationExtension?: string | null;
  destinationExternalNumber?: string | null;
  timeoutSeconds: number;
  extensionTimeoutSeconds: number;
  externalTimeoutSeconds: number;
  outboundTrunk?: string | null;
  normalizedCallerNumber: string;
  message: string;
  decision?: SmartRouteDecision | null;
}
