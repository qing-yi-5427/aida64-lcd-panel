package com.paneldeck.aida;

/** PC power state only; keyboard, mouse, gamepad and lock screen are irrelevant. */
final class DesktopScreenPolicy {
    static boolean needsWake(boolean connected, String mode, boolean interactive, boolean locked, boolean panelResumed) {
        return connected && "on".equals(mode) && (!interactive || (locked && !panelResumed));
    }
    static String decide(boolean suspended, boolean manualOff) {
        return suspended || manualOff ? "off" : "on";
    }
    static String connectionMode(long now, long lastSeen, long startedAt, long holdUntil, String lastMode) {
        boolean connected = lastSeen > 0 && now - lastSeen < 12000L;
        if (!connected) {
            if (now < holdUntil) return "on";
            if (startedAt > 0 && now - Math.max(lastSeen, startedAt) > 45000L) return "off";
        }
        return lastMode;
    }
}
