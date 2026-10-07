package com.paneldeck.aida;

import android.content.Context;
import android.content.Intent;
import android.os.PowerManager;

final class ScreenWake {
    private static PowerManager.WakeLock wake;
    @SuppressWarnings("deprecation")
    static synchronized void wake(Context context) {
        release();
        PowerManager power = (PowerManager) context.getSystemService(Context.POWER_SERVICE);
        wake = power.newWakeLock(PowerManager.SCREEN_BRIGHT_WAKE_LOCK | PowerManager.ACQUIRE_CAUSES_WAKEUP, "PanelDeck:stateWake");
        try { wake.acquire(12000L); }
        catch (SecurityException error) { android.util.Log.w("PanelDeckWake", "Screen wake permission denied", error); }
        android.util.Log.i("PanelDeckWake", "Request panel; backgroundLaunchPermission=" + android.provider.Settings.canDrawOverlays(context));
        Intent show = new Intent(context, MainActivity.class).setAction(MainActivity.ACTION_WAKE)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_SINGLE_TOP | Intent.FLAG_ACTIVITY_REORDER_TO_FRONT);
        try { context.startActivity(show); }
        catch (Exception error) { android.util.Log.w("PanelDeckWake", "Panel launch failed", error); }
    }
    static synchronized void release() {
        if (wake != null && wake.isHeld()) wake.release();
        wake = null;
    }
}
