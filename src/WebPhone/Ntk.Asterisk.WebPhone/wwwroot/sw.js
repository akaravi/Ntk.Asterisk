// NTK: PWA cache — self-hosted lib only (no CloudFront/CDN). Bump cacheID on asset changes.
const cacheID = "ntk-webphone-v4";
const CacheItems = [
    "index.html",
    "offline.html",
    "phone.js",
    "ntk-webphone-bridge.js",
    "ntk-webphone-config.js",
    "phone.css",

    "favicon.ico",

    "avatars/default.0.webp",
    "avatars/default.1.webp",
    "avatars/default.2.webp",
    "avatars/default.3.webp",
    "avatars/default.4.webp",
    "avatars/default.5.webp",
    "avatars/default.6.webp",
    "avatars/default.7.webp",
    "avatars/default.8.webp",

    "wallpaper.dark.webp",
    "wallpaper.light.webp",

    "media/Alert.mp3",
    "media/Ringtone_1.mp3",
    "media/speech_orig.mp3",
    "media/Tone_Busy-UK.mp3",
    "media/Tone_Busy-US.mp3",
    "media/Tone_CallWaiting.mp3",
    "media/Tone_Congestion-UK.mp3",
    "media/Tone_Congestion-US.mp3",
    "media/Tone_EarlyMedia-Australia.mp3",
    "media/Tone_EarlyMedia-European.mp3",
    "media/Tone_EarlyMedia-Japan.mp3",
    "media/Tone_EarlyMedia-UK.mp3",
    "media/Tone_EarlyMedia-US.mp3",

    "lib/jquery/jquery-3.6.1.min.js",
    "lib/jquery/jquery-ui-1.13.2.min.js",
    "lib/jquery/jquery.md5-min.js",
    "lib/Chart/Chart.bundle-2.7.2.min.js",
    "lib/SipJS/sip-0.20.0.min.js",
    "lib/FabricJS/fabric-2.4.6.min.js",
    "lib/Moment/moment-with-locales-2.24.0.min.js",
    "lib/Croppie/croppie-2.6.4.min.js",
    "lib/XMPP/strophe-1.4.1.umd.min.js",

    "lib/Normalize/normalize-v8.0.1.css",
    "lib/fonts/font_roboto/roboto.css",
    "lib/fonts/font_awesome/css/font-awesome.min.css",
    "lib/Croppie/croppie.css",

    "phone.js",
    "phone.css",
    "phone.light.css",
    "phone.dark.css"
];

self.addEventListener('install', function(event){
    console.log("Service Worker: Install", cacheID);
    event.waitUntil(caches.open(cacheID).then(function(cache){
        console.log("Cache open, adding Items:", CacheItems.length);
        return cache.addAll(CacheItems);
    }).then(function(){
        console.log("Items Added to Cache, skipWaiting");
        self.skipWaiting();
    }).catch(function(error){
        console.warn("Error opening Cache:", error);
        self.skipWaiting();
    }));
});

self.addEventListener('activate', function(event){
    console.log("Service Worker: Activate", cacheID);
    event.waitUntil(
        caches.keys().then(function(keys){
            return Promise.all(keys.filter(function(k){ return k !== cacheID; }).map(function(k){
                console.log("Deleting old cache", k);
                return caches.delete(k);
            }));
        }).then(function(){ return clients.claim(); })
    );
});

function isApiOrProvisionRequest(url) {
    try {
        var u = new URL(url);
        return u.pathname.indexOf("/api/") === 0;
    } catch (e) {
        return false;
    }
}

self.addEventListener("fetch", function(event){
    var url = event.request.url;
    // Never cache REST / provision — always network
    if (isApiOrProvisionRequest(url)) {
        event.respondWith(fetch(event.request));
        return;
    }
    if (url.indexOf("index.html") !== -1 || (event.request.mode === "navigate")) {
        event.respondWith(loadHomePage(event.request));
    }
    else {
        event.respondWith(loadFromCacheFirst(event.request));
    }
});


const loadFromCacheFirst = async function(request) {
    const responseFromCache = await caches.match(request);
    if (responseFromCache) {
        return responseFromCache;
    }
    try {
        const responseFromNetwork = await fetch(request);
        if(responseFromNetwork.ok){
            addToCache(request, responseFromNetwork.clone());
        }
        return responseFromNetwork;
    }
    catch (error) {
        return new Response("Network Error", { status: 408, statusText : "Network Error", headers: { "Content-Type": "text/plain" },});
    }
}
const loadHomePage = async function(request) {
    try {
        const responseFromNetwork = await fetch(request);
        if(responseFromNetwork.ok){
            return responseFromNetwork;
        } else {
            throw new Error("Server Error");
        }
    }
    catch (error) {
        const responseFromCache = await caches.match("offline.html");
        if (responseFromCache) {
            return responseFromCache;
        } else {
            return new Response("Network Error", { status: 408, statusText : "Network Error", headers: { "Content-Type": "text/plain" },});
        }
    }
}
const addToCache = async function(request, response) {
    if (isApiOrProvisionRequest(request.url))
        return;
    const cache = await caches.open(cacheID);
    await cache.put(request, response);
}
