import { Injectable, computed, signal } from '@angular/core';

export type AppLocale = 'fa' | 'en';

const MESSAGES: Record<AppLocale, Record<string, string>> = {
  fa: {
    'APP.TITLE': 'پنل کاربر — تماس Asterisk',
    'APP.SUBTITLE': 'صدور و پیگیری دستورات تماس (بدون احراز هویت)',
    'NAV.CALLS': 'فرم‌های تماس',
    'NAV.JOBS': 'کارهای تماس',
    'LOCALE.FA': 'فارسی',
    'LOCALE.EN': 'English',
    'HUB.CONNECTED': 'اتصال زنده',
    'HUB.DISCONNECTED': 'قطع از هاب',
    'HUB.RECONNECTING': 'در حال اتصال مجدد…',
    'FORM.EXT_TO_EXT': 'داخلی به داخلی',
    'FORM.MOBILE_TO_EXT': 'موبایل به داخلی',
    'FORM.MOBILE_TO_MOBILE': 'موبایل به موبایل',
    'FORM.FROM_EXT': 'داخلی مبدأ',
    'FORM.TO_EXT': 'داخلی مقصد',
    'FORM.MOBILE': 'شماره موبایل',
    'FORM.MOBILE1': 'موبایل اول',
    'FORM.MOBILE2': 'موبایل دوم',
    'FORM.TIMEOUT': 'مهلت (ثانیه)',
    'FORM.SUBMIT': 'ثبت تماس',
    'FORM.SUBMITTING': 'در حال ثبت…',
    'FORM.SUCCESS': 'کار تماس ثبت شد',
    'FORM.PLACEHOLDER_EXT': 'مثلاً ۱۰۰۱',
    'FORM.PLACEHOLDER_MOBILE': 'مثلاً ۰۹۱۲۱۲۳۴۵۶۷',
    'FORM.PLACEHOLDER_TIMEOUT': '۶۰',
    'FORM.VALIDATION_REQUIRED': 'این فیلد الزامی است',
    'JOBS.TITLE': 'فهرست کارهای تماس',
    'JOBS.REFRESH': 'بازخوانی',
    'JOBS.CANCEL': 'لغو',
    'JOBS.CANCELLING': 'در حال لغو…',
    'JOBS.EMPTY': 'کاری ثبت نشده است',
    'JOBS.QUICK_SEARCH': 'جستجوی سریع',
    'JOBS.QUICK_SEARCH_PH': 'شناسه، نوع، شماره…',
    'JOBS.PAGE_SIZE': 'تعداد در صفحه',
    'JOBS.COL_ID': 'شناسه',
    'JOBS.COL_TYPE': 'نوع',
    'JOBS.COL_STATUS': 'وضعیت',
    'JOBS.COL_FROM': 'مبدأ',
    'JOBS.COL_TO': 'مقصد',
    'JOBS.COL_UPDATED': 'به‌روزرسانی',
    'JOBS.COL_ACTIONS': 'عملیات',
    'JOBS.SORT_ASC': 'صعودی',
    'JOBS.SORT_DESC': 'نزولی',
    'STATUS.queued': 'در صف',
    'STATUS.dialing_leg1': 'شروع پایه ۱',
    'STATUS.waiting_answer': 'منتظر پاسخ',
    'STATUS.dialing_leg2': 'شروع پایه ۲',
    'STATUS.bridged': 'متصل',
    'STATUS.completed': 'پایان‌یافته',
    'STATUS.failed': 'ناموفق',
    'STATUS.cancelled': 'لغو شده',
    'TYPE.ExtToExt': 'داخلی→داخلی',
    'TYPE.MobileToExt': 'موبایل→داخلی',
    'TYPE.MobileToMobile': 'موبایل→موبایل',
    'ERROR.GENERIC': 'خطا در ارتباط با سرور',
  },
  en: {
    'APP.TITLE': 'User Panel — Asterisk Calls',
    'APP.SUBTITLE': 'Create and track call jobs (no auth)',
    'NAV.CALLS': 'Call forms',
    'NAV.JOBS': 'Call jobs',
    'LOCALE.FA': 'فارسی',
    'LOCALE.EN': 'English',
    'HUB.CONNECTED': 'Live connected',
    'HUB.DISCONNECTED': 'Hub disconnected',
    'HUB.RECONNECTING': 'Reconnecting…',
    'FORM.EXT_TO_EXT': 'Ext to Ext',
    'FORM.MOBILE_TO_EXT': 'Mobile to Ext',
    'FORM.MOBILE_TO_MOBILE': 'Mobile to Mobile',
    'FORM.FROM_EXT': 'From extension',
    'FORM.TO_EXT': 'To extension',
    'FORM.MOBILE': 'Mobile number',
    'FORM.MOBILE1': 'First mobile',
    'FORM.MOBILE2': 'Second mobile',
    'FORM.TIMEOUT': 'Timeout (sec)',
    'FORM.SUBMIT': 'Submit call',
    'FORM.SUBMITTING': 'Submitting…',
    'FORM.SUCCESS': 'Call job created',
    'FORM.PLACEHOLDER_EXT': 'e.g. 1001',
    'FORM.PLACEHOLDER_MOBILE': 'e.g. 09121234567',
    'FORM.PLACEHOLDER_TIMEOUT': '60',
    'FORM.VALIDATION_REQUIRED': 'This field is required',
    'JOBS.TITLE': 'Call jobs',
    'JOBS.REFRESH': 'Refresh',
    'JOBS.CANCEL': 'Cancel',
    'JOBS.CANCELLING': 'Cancelling…',
    'JOBS.EMPTY': 'No jobs yet',
    'JOBS.QUICK_SEARCH': 'Quick search',
    'JOBS.QUICK_SEARCH_PH': 'id, type, number…',
    'JOBS.PAGE_SIZE': 'Page size',
    'JOBS.COL_ID': 'Id',
    'JOBS.COL_TYPE': 'Type',
    'JOBS.COL_STATUS': 'Status',
    'JOBS.COL_FROM': 'From',
    'JOBS.COL_TO': 'To',
    'JOBS.COL_UPDATED': 'Updated',
    'JOBS.COL_ACTIONS': 'Actions',
    'JOBS.SORT_ASC': 'Asc',
    'JOBS.SORT_DESC': 'Desc',
    'STATUS.queued': 'Queued',
    'STATUS.dialing_leg1': 'Dialing leg 1',
    'STATUS.waiting_answer': 'Waiting answer',
    'STATUS.dialing_leg2': 'Dialing leg 2',
    'STATUS.bridged': 'Bridged',
    'STATUS.completed': 'Completed',
    'STATUS.failed': 'Failed',
    'STATUS.cancelled': 'Cancelled',
    'TYPE.ExtToExt': 'Ext→Ext',
    'TYPE.MobileToExt': 'Mobile→Ext',
    'TYPE.MobileToMobile': 'Mobile→Mobile',
    'ERROR.GENERIC': 'Server communication error',
  },
};

@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly localeSignal = signal<AppLocale>('fa');

  readonly locale = this.localeSignal.asReadonly();
  readonly dir = computed(() => (this.localeSignal() === 'fa' ? 'rtl' : 'ltr'));
  readonly lang = computed(() => (this.localeSignal() === 'fa' ? 'fa' : 'en'));

  t(key: string): string {
    const locale = this.localeSignal();
    return MESSAGES[locale][key] ?? MESSAGES.en[key] ?? key;
  }

  setLocale(locale: AppLocale): void {
    this.localeSignal.set(locale);
    document.documentElement.lang = locale === 'fa' ? 'fa' : 'en';
    document.documentElement.dir = locale === 'fa' ? 'rtl' : 'ltr';
  }
}
