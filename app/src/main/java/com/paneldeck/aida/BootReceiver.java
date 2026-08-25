package com.paneldeck.aida;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

public final class BootReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(Context context, Intent intent) {
        ScheduleManager.ensureDefaults(context);
        HolidaySync.ensureOfflineData(context);
        ScheduleManager.scheduleNext(context);
        PendingResult pending = goAsync();
        HolidaySync.syncAsync(context, false, updated -> {
            if (updated) ScheduleManager.scheduleNext(context);
            pending.finish();
        });
    }
}
