/* NTK WebPhone — setup: token provision (admin) vs manual user settings */
(function (global) {
  const LS_MODE = 'webPhoneSetupMode'; // 'token' | 'manual'
  const LS_TOKEN = 'webPhoneProvisionToken';

  function resolveApiBase() {
    const fromOpts = (typeof phoneOptions !== 'undefined' && phoneOptions.webPhoneApiBase) || '';
    const fromQuery =
      (typeof URLSearchParams !== 'undefined' &&
        new URLSearchParams(global.location.search).get('webPhoneApiBase')) ||
      '';
    const fromLs =
      (typeof localStorage !== 'undefined' && localStorage.getItem('webPhoneApiBase')) || '';
    const fromCfg =
      (typeof global.NTK_WEBPHONE_CONFIG !== 'undefined' && global.NTK_WEBPHONE_CONFIG.apiBaseUrl) ||
      '';
    return String(fromOpts || fromQuery || fromLs || fromCfg || '').replace(/\/$/, '');
  }

  function t(key) {
    const fa = {
      title: 'راه‌اندازی تلفن وب',
      subtitle: 'یکی از روش‌های زیر را انتخاب کنید.',
      tokenTab: 'توکن پنل مدیریت',
      manualTab: 'تنظیم دستی',
      tokenHint:
        'توکن ایجادشده در پنل مدیریت را وارد کنید. همه تنظیمات SIP و مخاطبین بارگذاری می‌شود.',
      tokenPh: 'توکن provision',
      tokenSubmit: 'فعال‌سازی و تماس',
      manualHint:
        'تنظیمات SIP را خودتان در منوی Settings وارد کنید. بارگذاری خودکار از سرور انجام نمی‌شود.',
      manualSubmit: 'ادامه با تنظیم دستی',
      tokenRequired: 'توکن الزامی است.',
      redeemFail: 'توکن نامعتبر یا منقضی است.',
      loading: 'در حال دریافت تنظیمات…',
      changeMode: 'تغییر روش راه‌اندازی',
    };
    const en = {
      title: 'WebPhone setup',
      subtitle: 'Choose how to configure this softphone.',
      tokenTab: 'Admin provision token',
      manualTab: 'Manual settings',
      tokenHint:
        'Enter the token created in Admin Panel. SIP settings and buddies load automatically.',
      tokenPh: 'Provision token',
      tokenSubmit: 'Activate and call',
      manualHint: 'Enter SIP settings yourself in the Settings menu. No auto-load from server.',
      manualSubmit: 'Continue with manual setup',
      tokenRequired: 'Token is required.',
      redeemFail: 'Invalid or expired token.',
      loading: 'Loading settings…',
      changeMode: 'Change setup method',
    };
    const lang =
      (document.documentElement.lang || '').toLowerCase().startsWith('fa') ? fa : en;
    return lang[key] || key;
  }

  function ensureOverlay() {
    if (document.getElementById('ntk-setup-overlay')) return;

    const style = document.createElement('link');
    style.rel = 'stylesheet';
    style.href = 'ntk-webphone-setup.css';
    document.head.appendChild(style);

    const el = document.createElement('div');
    el.id = 'ntk-setup-overlay';
    el.innerHTML =
      '<div class="ntk-setup" role="dialog" aria-modal="true" aria-labelledby="ntk-setup-title">' +
      '<h1 id="ntk-setup-title"></h1>' +
      '<p class="ntk-setup__sub"></p>' +
      '<div class="ntk-setup__tabs">' +
      '<button type="button" data-mode="token" class="ntk-setup__tab is-active"></button>' +
      '<button type="button" data-mode="manual" class="ntk-setup__tab"></button>' +
      '</div>' +
      '<section class="ntk-setup__panel" data-panel="token">' +
      '<p class="ntk-setup__hint"></p>' +
      '<label class="ntk-setup__field"><span></span>' +
      '<input type="text" id="ntk-setup-token" class="ltr" dir="ltr" autocomplete="off" /></label>' +
      '<p class="ntk-setup__error" hidden role="alert"></p>' +
      '<button type="button" id="ntk-setup-token-go" class="ntk-setup__primary"></button>' +
      '</section>' +
      '<section class="ntk-setup__panel" data-panel="manual" hidden>' +
      '<p class="ntk-setup__hint-manual"></p>' +
      '<button type="button" id="ntk-setup-manual-go" class="ntk-setup__ghost"></button>' +
      '</section>' +
      '</div>';
    document.body.appendChild(el);

    el.querySelector('#ntk-setup-title').textContent = t('title');
    el.querySelector('.ntk-setup__sub').textContent = t('subtitle');
    const tabs = el.querySelectorAll('.ntk-setup__tab');
    tabs[0].textContent = t('tokenTab');
    tabs[1].textContent = t('manualTab');
    el.querySelector('[data-panel="token"] .ntk-setup__hint').textContent = t('tokenHint');
    el.querySelector('#ntk-setup-token').placeholder = t('tokenPh');
    el.querySelector('#ntk-setup-token-go').textContent = t('tokenSubmit');
    el.querySelector('[data-panel="manual"] .ntk-setup__hint-manual').textContent = t('manualHint');
    el.querySelector('#ntk-setup-manual-go').textContent = t('manualSubmit');

    tabs.forEach(function (btn) {
      btn.addEventListener('click', function () {
        const mode = btn.getAttribute('data-mode');
        tabs.forEach(function (b) {
          b.classList.toggle('is-active', b === btn);
        });
        el.querySelector('[data-panel="token"]').hidden = mode !== 'token';
        el.querySelector('[data-panel="manual"]').hidden = mode !== 'manual';
      });
    });
  }

  function hideOverlay() {
    const el = document.getElementById('ntk-setup-overlay');
    if (el) el.remove();
  }

  function showError(msg) {
    const err = document.querySelector('#ntk-setup-overlay .ntk-setup__error');
    if (!err) return;
    err.textContent = msg;
    err.hidden = !msg;
  }

  async function redeemToken(token) {
    const apiRoot = resolveApiBase();
    if (!apiRoot) throw new Error('API base URL is not configured.');
    const response = await fetch(apiRoot + '/api/v1/WebPhone/GetProvisionByToken', {
      method: 'POST',
      credentials: 'include',
      cache: 'no-store',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: JSON.stringify({ token: token }),
    });
    if (!response.ok) throw new Error('HTTP ' + response.status);
    const envelope = await response.json();
    if (envelope && envelope.isSuccess === false)
      throw new Error(envelope.errorMessage || t('redeemFail'));
    const bundle =
      envelope && Array.isArray(envelope.data) && envelope.data[0] ? envelope.data[0] : null;
    if (!bundle || !bundle.sipConfig) throw new Error(t('redeemFail'));
    return bundle;
  }

  function applyBundle(bundle) {
    const sip = bundle.sipConfig || bundle.SipConfig;
    if (typeof global.applySipConfigDto === 'function') {
      global.applySipConfigDto(sip);
    }
    if (bundle.buddies || bundle.Buddies) {
      try {
        localStorage.setItem(
          'ntkProvisionBuddies',
          JSON.stringify(bundle.buddies || bundle.Buddies),
        );
      } catch (e) {
        console.warn('NTK setup: could not cache buddies', e);
      }
    }
    if (bundle.options || bundle.Options) {
      try {
        localStorage.setItem(
          'ntkProvisionOptions',
          JSON.stringify(bundle.options || bundle.Options),
        );
      } catch (e) {
        /* ignore */
      }
    }
  }

  function waitForUserChoice() {
    ensureOverlay();
    return new Promise(function (resolve) {
      const tokenInput = document.getElementById('ntk-setup-token');
      const qToken =
        typeof URLSearchParams !== 'undefined'
          ? new URLSearchParams(global.location.search).get('token')
          : '';
      if (qToken) tokenInput.value = qToken;

      document.getElementById('ntk-setup-manual-go').onclick = function () {
        localStorage.setItem(LS_MODE, 'manual');
        localStorage.removeItem(LS_TOKEN);
        hideOverlay();
        resolve({ mode: 'manual', provisioned: false, skipClear: true });
      };

      document.getElementById('ntk-setup-token-go').onclick = async function () {
        const raw = (tokenInput.value || '').trim();
        if (!raw) {
          showError(t('tokenRequired'));
          return;
        }
        showError('');
        const btn = document.getElementById('ntk-setup-token-go');
        btn.disabled = true;
        btn.textContent = t('loading');
        try {
          const bundle = await redeemToken(raw);
          applyBundle(bundle);
          localStorage.setItem(LS_MODE, 'token');
          localStorage.setItem(LS_TOKEN, raw);
          hideOverlay();
          resolve({ mode: 'token', provisioned: true, skipClear: false });
        } catch (err) {
          btn.disabled = false;
          btn.textContent = t('tokenSubmit');
          showError(err && err.message ? err.message : t('redeemFail'));
        }
      };
    });
  }

  async function prepare() {
    const qToken =
      typeof URLSearchParams !== 'undefined'
        ? new URLSearchParams(global.location.search).get('token')
        : '';
    const savedMode = localStorage.getItem(LS_MODE);
    const savedToken = localStorage.getItem(LS_TOKEN);

    if (qToken) {
      try {
        const bundle = await redeemToken(qToken);
        applyBundle(bundle);
        localStorage.setItem(LS_MODE, 'token');
        localStorage.setItem(LS_TOKEN, qToken);
        return { mode: 'token', provisioned: true, skipClear: false };
      } catch (err) {
        console.warn('NTK setup: URL token failed', err);
      }
    }

    if (savedMode === 'token' && savedToken) {
      try {
        const bundle = await redeemToken(savedToken);
        applyBundle(bundle);
        return { mode: 'token', provisioned: true, skipClear: false };
      } catch (err) {
        console.warn('NTK setup: saved token failed', err);
        localStorage.removeItem(LS_TOKEN);
      }
    }

    if (savedMode === 'manual') {
      return { mode: 'manual', provisioned: false, skipClear: true };
    }

    return waitForUserChoice();
  }

  global.ntkWebPhoneSetupPrepare = prepare;
  global.ntkWebPhoneResetSetup = function () {
    localStorage.removeItem(LS_MODE);
    localStorage.removeItem(LS_TOKEN);
    global.location.reload();
  };
})(window);
