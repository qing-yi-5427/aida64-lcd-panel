package com.paneldeck.aida;

import android.app.AlarmManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Build;

import java.time.DayOfWeek;
import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneId;
import java.time.ZonedDateTime;
import java.time.format.DateTimeFormatter;
import java.util.HashSet;
import java.util.Locale;
import java.util.Set;

final class ScheduleManager {
    static final String PREFS = "panel_deck_preferences";
    static final String KEY_ENABLED = "schedule_enabled";
    static final String KEY_HOME_URL = "home_url";
    static final String KEY_WORK_START = "work_start";
    static final String KEY_WORK_END = "work_end";
    static final String KEY_REST_START = "rest_start";
    static final String KEY_REST_END = "rest_end";
    static final String KEY_HOLIDAYS = "holiday_dates";
    static final String KEY_WORKDAYS = "special_workdays";
    static final String KEY_BRIGHTNESS = "screen_brightness";
    static final String KEY_OVERRIDE_UNTIL = "override_until";
    static final String KEY_DESKTOP = "desktop_mode";
    static final String KEY_FULLSCREEN = "fullscreen_mode";
    static final String KEY_SCHEDULE_MODE = "schedule_mode";
    static final String DEFAULT_HOME = "http://192.168.1.100:8080/";

    private static final int REQUEST_ALARM = 7001;
    private static final int REQUEST_WAKE = 7002;
    private static final DateTimeFormatter DATE = DateTimeFormatter.ISO_LOCAL_DATE;

    private ScheduleManager() {}

    static SharedPreferences prefs(Context context) {
        return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    static void ensureDefaults(Context context) {
        SharedPreferences p = prefs(context);
        SharedPreferences.Editor edit = p.edit();
        if (!p.contains(KEY_ENABLED)) {
            edit.putBoolean(KEY_ENABLED, true)
                    .putString(KEY_HOME_URL, DEFAULT_HOME)
                    .putInt(KEY_WORK_START, 2 * 60)
                    .putInt(KEY_WORK_END, 20 * 60 + 30)
                    .putInt(KEY_REST_START, 2 * 60)
                    .putInt(KEY_REST_END, 8 * 60)
                    .putFloat(KEY_BRIGHTNESS, 0.85f);
        }
        // Version 2 changes the configured intervals from "screen off" to "screen on".
        if (p.getInt(KEY_SCHEDULE_MODE, 1) < 2) {
            edit.putInt(KEY_SCHEDULE_MODE, 2)
                    .putLong(KEY_OVERRIDE_UNTIL, System.currentTimeMillis() + 10 * 60_000L);
        }
        edit.apply();
    }

    static boolean isOff(Context context, long atMillis) {
        SharedPreferences p = prefs(context);
        if (!p.getBoolean(KEY_ENABLED, true)) return false;
        if (p.getLong(KEY_OVERRIDE_UNTIL, 0L) > atMillis) return false;
        return !isScheduledOn(p, atMillis);
    }

    private static boolean isScheduledOn(SharedPreferences p, long atMillis) {

        ZonedDateTime now = Instant.ofEpochMilli(atMillis).atZone(ZoneId.systemDefault());
        int minute = now.getHour() * 60 + now.getMinute();
        LocalDate date = now.toLocalDate();

        int todayStart = startFor(p, date);
        int todayEnd = endFor(p, date);
        if (todayStart <= todayEnd) {
            return minute >= todayStart && minute < todayEnd;
        }
        if (minute >= todayStart) return true;
        LocalDate yesterday = date.minusDays(1);
        int previousStart = startFor(p, yesterday);
        int previousEnd = endFor(p, yesterday);
        return previousStart > previousEnd && minute < previousEnd;
    }

    static long nextChange(Context context, long fromMillis) {
        SharedPreferences p = prefs(context);
        if (!p.getBoolean(KEY_ENABLED, true)) return 0L;
        long override = p.getLong(KEY_OVERRIDE_UNTIL, 0L);
        if (override > fromMillis) {
            if (!isScheduledOn(p, override)) return override;
            fromMillis = override;
        }

        boolean current = isOff(context, fromMillis);
        ZonedDateTime cursor = Instant.ofEpochMilli(fromMillis).atZone(ZoneId.systemDefault())
                .withSecond(0).withNano(0).plusMinutes(1);
        for (int i = 0; i < 11 * 24 * 60; i++) {
            long candidate = cursor.toInstant().toEpochMilli();
            if (isOff(context, candidate) != current) return candidate;
            cursor = cursor.plusMinutes(1);
        }
        return 0L;
    }

    static void scheduleNext(Context context) {
        ensureDefaults(context);
        AlarmManager alarms = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        Intent intent = new Intent(context, ScheduleReceiver.class).setAction(ScheduleReceiver.ACTION_TICK);
        PendingIntent pending = PendingIntent.getBroadcast(context, REQUEST_ALARM, intent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        alarms.cancel(pending);
        Intent wakeIntent = new Intent(context, MainActivity.class)
                .setAction(MainActivity.ACTION_WAKE)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_SINGLE_TOP | Intent.FLAG_ACTIVITY_REORDER_TO_FRONT);
        PendingIntent wakePending = PendingIntent.getActivity(context, REQUEST_WAKE, wakeIntent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        alarms.cancel(wakePending);
        long next = nextChange(context, System.currentTimeMillis());
        if (next == 0L) return;
        // A user-visible alarm clock is the most reliable non-root wake path through Doze and OEM power savers.
        if (!isOff(context, next + 1_000L)) {
            alarms.setAlarmClock(new AlarmManager.AlarmClockInfo(next, wakePending), wakePending);
            return;
        }
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.S || alarms.canScheduleExactAlarms()) {
            alarms.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, next, pending);
        } else {
            alarms.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, next, pending);
        }
    }

    static boolean canScheduleExactly(Context context) {
        AlarmManager alarms = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        return Build.VERSION.SDK_INT < Build.VERSION_CODES.S || alarms.canScheduleExactAlarms();
    }

    static boolean isRestDay(SharedPreferences p, LocalDate date) {
        String key = DATE.format(date);
        Set<String> forcedWorkdays = parseDates(p.getString(KEY_WORKDAYS, ""));
        if (forcedWorkdays.contains(key)) return false;
        Set<String> holidays = parseDates(p.getString(KEY_HOLIDAYS, ""));
        if (holidays.contains(key)) return true;
        DayOfWeek day = date.getDayOfWeek();
        return day == DayOfWeek.SATURDAY || day == DayOfWeek.SUNDAY;
    }

    static String formatTime(int minute) {
        return String.format(Locale.CHINA, "%02d:%02d", minute / 60, minute % 60);
    }

    static String formatNext(Context context, long millis) {
        ZonedDateTime z = Instant.ofEpochMilli(millis).atZone(ZoneId.systemDefault());
        ZonedDateTime now = ZonedDateTime.now();
        if (z.toLocalDate().equals(now.toLocalDate())) {
            return String.format(Locale.CHINA, "今天 %02d:%02d", z.getHour(), z.getMinute());
        }
        if (z.toLocalDate().equals(now.toLocalDate().plusDays(1))) {
            return String.format(Locale.CHINA, "明天 %02d:%02d", z.getHour(), z.getMinute());
        }
        return String.format(Locale.CHINA, "%d月%d日 %02d:%02d", z.getMonthValue(), z.getDayOfMonth(), z.getHour(), z.getMinute());
    }

    private static int startFor(SharedPreferences p, LocalDate date) {
        return p.getInt(isRestDay(p, date) ? KEY_REST_START : KEY_WORK_START, 2 * 60);
    }

    private static int endFor(SharedPreferences p, LocalDate date) {
        return p.getInt(isRestDay(p, date) ? KEY_REST_END : KEY_WORK_END,
                isRestDay(p, date) ? 8 * 60 : 20 * 60 + 30);
    }

    private static Set<String> parseDates(String raw) {
        Set<String> dates = new HashSet<>();
        if (raw == null) return dates;
        for (String value : raw.split("[,，;；\\s]+")) {
            String normalized = value.trim().replace('/', '-').replace('.', '-');
            if (normalized.matches("\\d{4}-\\d{1,2}-\\d{1,2}")) {
                try { dates.add(DATE.format(LocalDate.parse(normalized, flexibleDateFormatter()))); }
                catch (Exception ignored) { }
            }
        }
        return dates;
    }

    private static DateTimeFormatter flexibleDateFormatter() {
        return DateTimeFormatter.ofPattern("yyyy-M-d", Locale.CHINA);
    }
}
