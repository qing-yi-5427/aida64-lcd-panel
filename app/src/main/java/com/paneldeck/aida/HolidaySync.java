package com.paneldeck.aida;

import android.content.Context;
import android.content.SharedPreferences;
import android.os.Handler;
import android.os.Looper;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneId;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;

final class HolidaySync {
    interface Callback { void onFinished(boolean updated); }

    private static final String KEY_LAST_SYNC = "cn_holiday_last_sync";
    private static final String KEY_DATA_YEARS = "cn_holiday_data_years";
    private static final String KEY_DATA_VERSION = "cn_holiday_data_version";
    private static final long REFRESH_INTERVAL = 7L * 24 * 60 * 60 * 1000;
    private static final ExecutorService EXECUTOR = Executors.newSingleThreadExecutor();
    private static final AtomicBoolean RUNNING = new AtomicBoolean(false);

    // Offline baseline: State Council General Office notice for the 2026 holiday schedule.
    private static final String HOLIDAYS_2026 =
            "2026-01-01,2026-01-02,2026-01-03,"
            + "2026-02-15,2026-02-16,2026-02-17,2026-02-18,2026-02-19,2026-02-20,2026-02-21,2026-02-22,2026-02-23,"
            + "2026-04-04,2026-04-05,2026-04-06,"
            + "2026-05-01,2026-05-02,2026-05-03,2026-05-04,2026-05-05,"
            + "2026-06-19,2026-06-20,2026-06-21,"
            + "2026-09-25,2026-09-26,2026-09-27,"
            + "2026-10-01,2026-10-02,2026-10-03,2026-10-04,2026-10-05,2026-10-06,2026-10-07";
    private static final String WORKDAYS_2026 =
            "2026-01-04,2026-02-14,2026-02-28,2026-05-09,2026-09-20,2026-10-10";

    private HolidaySync() {}

    static void ensureOfflineData(Context context) {
        SharedPreferences p = ScheduleManager.prefs(context);
        if (p.getInt(KEY_DATA_VERSION, 0) >= 202600) return;
        p.edit()
                .putString(ScheduleManager.KEY_HOLIDAYS, HOLIDAYS_2026)
                .putString(ScheduleManager.KEY_WORKDAYS, WORKDAYS_2026)
                .putString(KEY_DATA_YEARS, "2026")
                .putInt(KEY_DATA_VERSION, 202600)
                .apply();
    }

    static void syncAsync(Context context, boolean force, Callback callback) {
        Context app = context.getApplicationContext();
        ensureOfflineData(app);
        SharedPreferences p = ScheduleManager.prefs(app);
        int year = LocalDate.now().getYear();
        long lastSync = p.getLong(KEY_LAST_SYNC, 0L);
        boolean fresh = System.currentTimeMillis() - lastSync < REFRESH_INTERVAL
                && p.getString(KEY_DATA_YEARS, "").contains(Integer.toString(year));
        if (!force && fresh) {
            deliver(callback, false);
            return;
        }
        if (!RUNNING.compareAndSet(false, true)) {
            deliver(callback, false);
            return;
        }

        EXECUTOR.execute(() -> {
            boolean updated = false;
            try {
                CalendarData current = fetchYear(year);
                CalendarData next = null;
                try { next = fetchYear(year + 1); } catch (Exception ignored) { }
                List<String> holidays = new ArrayList<>(current.holidays);
                List<String> workdays = new ArrayList<>(current.workdays);
                String years = Integer.toString(year);
                if (next != null) {
                    holidays.addAll(next.holidays);
                    workdays.addAll(next.workdays);
                    years += "," + (year + 1);
                }
                Collections.sort(holidays);
                Collections.sort(workdays);
                p.edit()
                        .putString(ScheduleManager.KEY_HOLIDAYS, String.join(",", holidays))
                        .putString(ScheduleManager.KEY_WORKDAYS, String.join(",", workdays))
                        .putString(KEY_DATA_YEARS, years)
                        .putLong(KEY_LAST_SYNC, System.currentTimeMillis())
                        .apply();
                ScheduleManager.scheduleNext(app);
                updated = true;
            } catch (Exception ignored) {
                // Keep the verified offline baseline or the last successful cache.
            } finally {
                RUNNING.set(false);
                deliver(callback, updated);
            }
        });
    }

    static String status(Context context) {
        SharedPreferences p = ScheduleManager.prefs(context);
        long last = p.getLong(KEY_LAST_SYNC, 0L);
        String years = p.getString(KEY_DATA_YEARS, "2026");
        if (RUNNING.get()) return "正在同步中国大陆节假日…";
        if (last == 0L) return "已载入 2026 官方离线数据 · 等待联网同步";
        java.time.ZonedDateTime time = Instant.ofEpochMilli(last).atZone(ZoneId.systemDefault());
        return String.format(Locale.CHINA, "自动同步 %s 年数据 · 更新于 %d月%d日",
                years.replace(',', '、'), time.getMonthValue(), time.getDayOfMonth());
    }

    private static CalendarData fetchYear(int year) throws Exception {
        String[] urls = new String[] {
                "https://gcore.jsdelivr.net/gh/cg-zhou/holiday-calendar@main/data/CN/" + year + ".json",
                "https://raw.githubusercontent.com/cg-zhou/holiday-calendar/main/data/CN/" + year + ".json"
        };
        Exception last = null;
        for (String source : urls) {
            try { return parse(download(source), year); }
            catch (Exception error) { last = error; }
        }
        throw last == null ? new IllegalStateException("holiday source unavailable") : last;
    }

    private static String download(String source) throws Exception {
        HttpURLConnection connection = (HttpURLConnection) new URL(source).openConnection();
        connection.setConnectTimeout(8_000);
        connection.setReadTimeout(8_000);
        connection.setRequestProperty("Accept", "application/json");
        connection.setRequestProperty("User-Agent", "PanelDeck/1.0 Android");
        try {
            if (connection.getResponseCode() != HttpURLConnection.HTTP_OK) {
                throw new IllegalStateException("HTTP " + connection.getResponseCode());
            }
            StringBuilder body = new StringBuilder();
            try (BufferedReader reader = new BufferedReader(new InputStreamReader(
                    connection.getInputStream(), StandardCharsets.UTF_8))) {
                String line;
                while ((line = reader.readLine()) != null) body.append(line);
            }
            return body.toString();
        } finally {
            connection.disconnect();
        }
    }

    private static CalendarData parse(String json, int expectedYear) throws Exception {
        JSONObject root = new JSONObject(json);
        if (root.getInt("year") != expectedYear || !"CN".equals(root.getString("region"))) {
            throw new IllegalArgumentException("unexpected holiday calendar");
        }
        List<String> holidays = new ArrayList<>();
        List<String> workdays = new ArrayList<>();
        JSONArray dates = root.getJSONArray("dates");
        for (int i = 0; i < dates.length(); i++) {
            JSONObject item = dates.getJSONObject(i);
            String date = item.getString("date");
            LocalDate parsed = LocalDate.parse(date);
            if (parsed.getYear() != expectedYear) continue;
            String type = item.getString("type");
            if ("public_holiday".equals(type)) holidays.add(date);
            else if ("transfer_workday".equals(type)) workdays.add(date);
        }
        if (holidays.size() < 7) throw new IllegalArgumentException("incomplete holiday calendar");
        return new CalendarData(holidays, workdays);
    }

    private static void deliver(Callback callback, boolean updated) {
        if (callback == null) return;
        new Handler(Looper.getMainLooper()).post(() -> callback.onFinished(updated));
    }

    private static final class CalendarData {
        final List<String> holidays;
        final List<String> workdays;
        CalendarData(List<String> holidays, List<String> workdays) {
            this.holidays = holidays;
            this.workdays = workdays;
        }
    }
}
