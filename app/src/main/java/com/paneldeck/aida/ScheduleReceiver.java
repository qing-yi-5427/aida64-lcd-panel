package com.paneldeck.aida;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.os.PowerManager;

public final class ScheduleReceiver extends BroadcastReceiver {
    static final String ACTION_TICK = "com.paneldeck.aida.SCHEDULE_TICK";
    static final String ACTION_APPLY = "com.paneldeck.aida.APPLY_SCREEN_MODE";

    @Override
    public void onReceive(Context context, Intent intent) {
        boolean off = ScheduleManager.isOff(context, System.currentTimeMillis());
        if (!off) {
            PowerManager power = (PowerManager) context.getSystemService(Context.POWER_SERVICE);
            @SuppressWarnings("deprecation")
            PowerManager.WakeLock wake = power.newWakeLock(
                    PowerManager.SCREEN_BRIGHT_WAKE_LOCK | PowerManager.ACQUIRE_CAUSES_WAKEUP,
                    "PanelDeck:scheduleWake");
            wake.acquire(12_000L);
        }

        Intent apply = new Intent(ACTION_APPLY).setPackage(context.getPackageName());
        context.sendBroadcast(apply);

        if (!off) {
            Intent show = new Intent(context, MainActivity.class)
                    .setAction(ACTION_APPLY)
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_SINGLE_TOP | Intent.FLAG_ACTIVITY_REORDER_TO_FRONT);
            try { context.startActivity(show); } catch (Exception ignored) { }
        }
        ScheduleManager.scheduleNext(context);
        HolidaySync.syncAsync(context, false, updated -> {
            if (updated) ScheduleManager.scheduleNext(context);
        });
    }
}
