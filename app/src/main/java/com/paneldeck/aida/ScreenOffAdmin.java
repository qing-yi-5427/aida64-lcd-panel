package com.paneldeck.aida;

import android.app.admin.DeviceAdminReceiver;
import android.app.admin.DevicePolicyManager;
import android.content.ComponentName;
import android.content.Context;

public final class ScreenOffAdmin extends DeviceAdminReceiver {
    static final String KEY_IMMEDIATE = "desktop_immediate_lock";
    static boolean available(Context c) {
        return ((DevicePolicyManager)c.getSystemService(Context.DEVICE_POLICY_SERVICE)).isAdminActive(new ComponentName(c, ScreenOffAdmin.class));
    }
    static void lockIfEnabled(Context c) {
        if (!ScheduleManager.prefs(c).getBoolean(KEY_IMMEDIATE, false) || !available(c)) return;
        try { ((DevicePolicyManager)c.getSystemService(Context.DEVICE_POLICY_SERVICE)).lockNow(); } catch (SecurityException ignored) { }
    }
}
