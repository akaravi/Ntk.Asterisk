/**
 * NTK WebPhone bridge — syncs local softphone state to WebApi stores.
 * Loads after phone.js. Does not replace IndexedDB/localStorage; additive server sync.
 * AGPL UI remains under vendor-voiz NOTICE; this file is NTK-owned.
 */
(function (global) {
  "use strict";

  var presenceTimer = null;
  var mwiTimer = null;
  var options = {
    enablePresence: true,
    enableMwi: true,
    enableRecordAll: false
  };

  function resolveApiRoot() {
    var fromOpts =
      (typeof phoneOptions !== "undefined" && phoneOptions.webPhoneApiBase) || "";
    var fromQuery =
      (typeof URLSearchParams !== "undefined" &&
        new URLSearchParams(window.location.search).get("webPhoneApiBase")) ||
      "";
    var fromLs =
      (typeof localStorage !== "undefined" &&
        localStorage.getItem("webPhoneApiBase")) ||
      "";
    var fromCfg =
      (global.NTK_WEBPHONE_CONFIG && global.NTK_WEBPHONE_CONFIG.apiBaseUrl) || "";
    return String(fromOpts || fromQuery || fromLs || fromCfg || "").replace(
      /\/$/,
      ""
    );
  }

  function apiBase() {
    return resolveApiRoot() + "/api/v1/WebPhone";
  }

  function apiHeaders(extra) {
    var headers = Object.assign({ Accept: "application/json" }, extra || {});
    var key =
      (typeof phoneOptions !== "undefined" && phoneOptions.webPhoneApiKey) ||
      (typeof URLSearchParams !== "undefined" &&
        new URLSearchParams(window.location.search).get("webphoneApiKey")) ||
      (typeof localStorage !== "undefined" && localStorage.getItem("webPhoneApiKey")) ||
      "";
    if (key) {
      headers["X-WebPhone-Api-Key"] = key;
      headers["X-Api-Key"] = key;
    }
    return headers;
  }

  function envelopeData(json) {
    if (!json) return null;
    if (json.isSuccess === false) throw new Error(json.errorMessage || "API failed");
    if (Array.isArray(json.data)) return json.data;
    return json.data != null ? [json.data] : [];
  }

  function postJson(path, body) {
    return fetch(apiBase() + path, {
      method: "POST",
      credentials: "include",
      cache: "no-store",
      headers: apiHeaders({ "Content-Type": "application/json" }),
      body: JSON.stringify(body || {})
    }).then(function (r) {
      if (!r.ok) throw new Error("HTTP " + r.status);
      return r.json();
    });
  }

  function getJson(path) {
    return fetch(apiBase() + path, {
      method: "GET",
      credentials: "include",
      cache: "no-store",
      headers: apiHeaders()
    }).then(function (r) {
      if (!r.ok) throw new Error("HTTP " + r.status);
      return r.json();
    });
  }

  function applyFeatureFlags(features) {
    if (!features) return;
    options.enablePresence = features.enablePresence !== false;
    options.enableMwi = features.enableMwi !== false;
    options.enableRecordAll = !!features.enableRecordAll;

    try {
      if (typeof EnableTransfer !== "undefined" && features.enableTransfer === false)
        EnableTransfer = false;
      if (typeof EnableConference !== "undefined" && features.enableConference === false)
        EnableConference = false;
      if (typeof EnableVideoCalling !== "undefined" && features.enableVideo === false)
        EnableVideoCalling = false;
      if (features.enableRecordAll === true && typeof RecordAllCalls !== "undefined") {
        RecordAllCalls = true;
        if (typeof localDB !== "undefined") localDB.setItem("RecordAllCalls", "1");
      }
    } catch (e) {
      console.warn("NTK bridge: feature flags apply skipped", e);
    }
  }

  function loadOptions() {
    return getJson("/Config/GetWebPhoneOptions")
      .then(function (json) {
        var rows = envelopeData(json);
        if (rows && rows[0]) applyFeatureFlags(rows[0]);
      })
      .catch(function (err) {
        console.warn("NTK bridge: GetWebPhoneOptions failed", err);
      });
  }

  function syncCdrFromSession(buddy, session) {
    if (!session || !session.data) return;
    var dir = session.data.calldirection || "";
    var withNumber =
      dir === "outbound"
        ? session.data.dst || buddy
        : (session.remoteIdentity &&
            (session.remoteIdentity.uri && session.remoteIdentity.uri.user)) ||
          buddy;
    var started = session.data.callstart
      ? new Date(String(session.data.callstart).replace(" UTC", "Z"))
      : new Date();
    var ended = new Date();
    var durationSec = null;
    if (session.data.startTime) {
      durationSec = Math.max(
        0,
        Math.round((ended.getTime() - new Date(session.data.startTime).getTime()) / 1000)
      );
    }
    postJson("/Cdr/Add", {
      buddyId: buddy,
      direction: dir,
      withNumber: withNumber,
      displayName:
        (session.remoteIdentity && session.remoteIdentity.displayName) || withNumber,
      startedAtUtc: started.toISOString(),
      endedAtUtc: ended.toISOString(),
      durationSeconds: durationSec,
      disposition: session.data.reasonText || session.data.terminateby || null,
      notes: session.id || null
    }).catch(function (err) {
      console.warn("NTK bridge: CDR sync failed", err);
    });
  }

  function syncQos(QosData, sessionId, buddy) {
    var loss = null;
    var jitter = null;
    try {
      if (QosData && QosData.ReceivePacketLoss && QosData.ReceivePacketLoss.length)
        loss = QosData.ReceivePacketLoss[QosData.ReceivePacketLoss.length - 1];
      if (QosData && QosData.ReceiveJitter && QosData.ReceiveJitter.length)
        jitter = QosData.ReceiveJitter[QosData.ReceiveJitter.length - 1];
    } catch (_) {}
    postJson("/Qos/Add", {
      callId: sessionId,
      buddyId: buddy,
      packetLossPct: typeof loss === "number" ? loss : null,
      jitterMs: typeof jitter === "number" ? jitter : null,
      rawJson: JSON.stringify(QosData || {})
    }).catch(function (err) {
      console.warn("NTK bridge: QoS sync failed", err);
    });
  }

  function syncRecording(blob, id, buddy, sessionid) {
    if (!blob) return;
    var form = new FormData();
    form.append("file", blob, id + ".webm");
    if (buddy) form.append("buddyId", buddy);
    if (sessionid) form.append("cdrId", sessionid);
    form.append("notes", id);
    fetch(apiBase() + "/Recordings/Add", {
      method: "POST",
      credentials: "include",
      headers: apiHeaders(),
      body: form
    })
      .then(function (r) {
        if (!r.ok) throw new Error("HTTP " + r.status);
        return r.json();
      })
      .then(function (json) {
        envelopeData(json);
        console.log("NTK bridge: recording uploaded", id);
      })
      .catch(function (err) {
        console.warn("NTK bridge: recording upload failed", err);
      });
  }

  function syncBuddy(buddyObj) {
    if (!buddyObj) return;
    postJson("/Buddies/Add", {
      id: buddyObj.identity || buddyObj.Id,
      type: buddyObj.type || "extension",
      displayName: buddyObj.CallerIDName || buddyObj.displayName || buddyObj.identity,
      extensionNumber: buddyObj.ExtNo || buddyObj.extensionNumber || buddyObj.identity,
      description: buddyObj.Desc || buddyObj.description || null,
      mobileNumber: buddyObj.MobileNumber || null,
      email: buddyObj.Email || null,
      contactNumber1: buddyObj.Contact1 || null,
      contactNumber2: buddyObj.Contact2 || null,
      subscribe: !!buddyObj.Subscribe,
      subscribeUser: buddyObj.SubscribeUser || buddyObj.ExtNo || null,
      enableDuringDnd: !!buddyObj.AllowCallDuringDnd
    }).catch(function (err) {
      console.warn("NTK bridge: buddy sync failed", err);
    });
  }

  function mergeBuddyRows(rows) {
    if (!rows || !rows.length) return false;
    if (typeof AddBuddy !== "function" || typeof FindBuddyByIdentity !== "function") return false;
    rows.forEach(function (b) {
      var id = b.extensionNumber || b.ExtensionNumber || b.id || b.Id;
      if (!id || FindBuddyByIdentity(id)) return;
      try {
        var buddyObj = {
          identity: id,
          type: b.type || b.Type || "extension",
          CallerIDName: b.displayName || b.DisplayName || id,
          ExtNo: b.extensionNumber || b.ExtensionNumber || id,
          Desc: b.description || b.Description || "",
          MobileNumber: b.mobileNumber || b.MobileNumber || "",
          Email: b.email || b.Email || "",
          Contact1: b.contactNumber1 || b.ContactNumber1 || "",
          Contact2: b.contactNumber2 || b.ContactNumber2 || "",
          Subscribe: !!(b.subscribe || b.Subscribe),
          SubscribeUser:
            b.subscribeUser || b.SubscribeUser || b.extensionNumber || b.ExtensionNumber || id,
          AllowCallDuringDnd: !!(b.enableDuringDnd || b.EnableDuringDnd),
          AllowAutoDelete: false,
          lastActivity: new Date().toISOString()
        };
        AddBuddy(buddyObj, false, false, !!buddyObj.Subscribe, false);
      } catch (e) {
        console.warn("NTK bridge: merge buddy failed", id, e);
      }
    });
    if (typeof PopulateBuddyList === "function") PopulateBuddyList();
    return true;
  }

  function applyProvisionCache() {
    var hadBuddies = false;
    try {
      var optsRaw = localStorage.getItem("ntkProvisionOptions");
      if (optsRaw) applyFeatureFlags(JSON.parse(optsRaw));
    } catch (e) {
      console.warn("NTK bridge: provision options cache skipped", e);
    }
    try {
      var buddiesRaw = localStorage.getItem("ntkProvisionBuddies");
      if (buddiesRaw) hadBuddies = mergeBuddyRows(JSON.parse(buddiesRaw));
    } catch (e) {
      console.warn("NTK bridge: provision buddies cache skipped", e);
    }
    return hadBuddies;
  }

  function pullServerBuddies() {
    return getJson("/Buddies/GetList?pageSize=200")
      .then(function (json) {
        var rows = envelopeData(json);
        mergeBuddyRows(rows);
      })
      .catch(function (err) {
        console.warn("NTK bridge: Buddies/GetList failed", err);
      });
  }

  function pollPresence() {
    if (!options.enablePresence || typeof Buddies === "undefined") return;
    for (var i = 0; i < Buddies.length; i++) {
      var b = Buddies[i];
      var ext = b.ExtNo || b.SubscribeUser || b.identity;
      if (!ext) continue;
      (function (buddy, extension) {
        postJson(
          "/Presence/ActionQuery?extension=" + encodeURIComponent(extension),
          { extension: extension }
        )
          .then(function (json) {
            var rows = envelopeData(json);
            var p = rows && rows[0];
            if (!p) return;
            var state = (p.state || "").toLowerCase();
            if (buddy.presence !== state) {
              buddy.presence = state;
              if (typeof console !== "undefined")
                console.log("NTK bridge: presence", extension, p.state);
            }
          })
          .catch(function () {});
      })(b, ext);
    }
  }

  function pollMwi() {
    if (!options.enableMwi) return;
    var mailbox =
      (typeof SipUsername !== "undefined" && SipUsername) ||
      (typeof localDB !== "undefined" && localDB.getItem("SipUsername")) ||
      "";
    if (!mailbox) return;
    postJson(
      "/Mwi/ActionQuery?mailbox=" + encodeURIComponent(mailbox),
      { mailbox: mailbox }
    )
      .then(function (json) {
        var rows = envelopeData(json);
        var m = rows && rows[0];
        if (!m) return;
        var el = document.getElementById("TxtVoiceMessages");
        if (el) el.innerText = String(m.newMessages || 0);
      })
      .catch(function () {});
  }

  function startPolling() {
    if (presenceTimer) clearInterval(presenceTimer);
    if (mwiTimer) clearInterval(mwiTimer);
    if (options.enablePresence) presenceTimer = setInterval(pollPresence, 15000);
    if (options.enableMwi) mwiTimer = setInterval(pollMwi, 30000);
    pollPresence();
    pollMwi();
  }

  function wrapLocals() {
    if (typeof AddCallMessage === "function") {
      var origCdr = AddCallMessage;
      global.AddCallMessage = function (buddy, session) {
        var result = origCdr.apply(this, arguments);
        try {
          syncCdrFromSession(buddy, session);
        } catch (e) {
          console.warn("NTK bridge: CDR wrap error", e);
        }
        return result;
      };
    }

    if (typeof SaveQosData === "function") {
      var origQos = SaveQosData;
      global.SaveQosData = function (QosData, sessionId, buddy) {
        var result = origQos.apply(this, arguments);
        try {
          syncQos(QosData, sessionId, buddy);
        } catch (e) {
          console.warn("NTK bridge: QoS wrap error", e);
        }
        return result;
      };
    }

    if (typeof SaveCallRecording === "function") {
      var origRec = SaveCallRecording;
      global.SaveCallRecording = function (blob, id, buddy, sessionid) {
        var result = origRec.apply(this, arguments);
        try {
          syncRecording(blob, id, buddy, sessionid);
        } catch (e) {
          console.warn("NTK bridge: recording wrap error", e);
        }
        return result;
      };
    }

    if (typeof AddBuddy === "function") {
      var origBuddy = AddBuddy;
      global.AddBuddy = function (buddyObj, update, focus, subscribe, cleanup) {
        var result = origBuddy.apply(this, arguments);
        try {
          if (buddyObj && cleanup !== false) syncBuddy(buddyObj);
        } catch (e) {
          console.warn("NTK bridge: buddy wrap error", e);
        }
        return result;
      };
    }
  }

  function chainHook(name, fn) {
    var prev = global[name];
    global[name] = function () {
      try {
        if (typeof prev === "function") prev.apply(this, arguments);
      } catch (_) {}
      try {
        fn.apply(this, arguments);
      } catch (e) {
        console.warn("NTK bridge hook " + name, e);
      }
    };
  }

  function boot() {
    wrapLocals();
    var hadProvisionBuddies = applyProvisionCache();
    loadOptions()
      .then(function () {
        if (!hadProvisionBuddies) return pullServerBuddies();
      })
      .then(startPolling);

    chainHook("web_hook_on_register", function () {
      startPolling();
    });

    chainHook("web_hook_on_terminate", function (session) {
      try {
        if (session && session.data) {
          var buddy =
            session.data.calldirection === "outbound"
              ? session.data.dst
              : (session.remoteIdentity &&
                  session.remoteIdentity.uri &&
                  session.remoteIdentity.uri.user) ||
                null;
          if (buddy) syncCdrFromSession(buddy, session);
        }
      } catch (_) {}
    });

    console.log("NTK WebPhone bridge active");
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", function () {
      setTimeout(boot, 0);
    });
  } else {
    setTimeout(boot, 0);
  }

  global.NtkWebPhoneBridge = {
    reloadOptions: loadOptions,
    pullBuddies: pullServerBuddies,
    pollPresence: pollPresence,
    pollMwi: pollMwi
  };
})(typeof window !== "undefined" ? window : this);
